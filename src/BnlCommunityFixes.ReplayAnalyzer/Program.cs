using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: BnlCommunityFixes.ReplayAnalyzer <zone-capture.jsonl | replay-directory> [output-directory]");
    return args.Length == 0 ? 1 : 0;
}

var inputPath = ResolveInputPath(args[0]);
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Capture file not found: {inputPath}");
    return 1;
}

var outputDir = args.Length >= 2
    ? Path.GetFullPath(args[1])
    : Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileNameWithoutExtension(inputPath) + "-analysis");

Directory.CreateDirectory(outputDir);

var analyzer = new ReplayAnalyzer(inputPath);
var result = analyzer.Analyze();
ReplayReportWriter.Write(outputDir, result);

Console.WriteLine($"Capture: {inputPath}");
Console.WriteLine($"Packets: {result.Packets.Count}");
Console.WriteLine($"Duration: {result.DurationSeconds:0.000}s");
Console.WriteLine($"InitZone: remaining={result.InitZoneRemainingBytes}, payload={result.InitZonePayloadBytes}, full={result.InitZoneFullyCaptured}");
Console.WriteLine($"Units created: {result.UnitCreates.Count}");
Console.WriteLine($"Moves: {result.UnitMoves.Count}");
Console.WriteLine($"Damage events: {result.Damages.Count}");
Console.WriteLine($"Channel events: {result.ChannelEvents.Count}  dash charges: {result.DashChargeEvents.Count}  pickups: {result.PickupTakenEvents.Count}  recalls: {result.RecallEvents.Count}  portal teleports: {result.PortalTeleports.Count}  kicks: {result.KickPlayerEvents.Count}");
Console.WriteLine($"Block updates: {result.BlockUpdates.Sum(static item => item.Count)} across {result.BlockUpdates.Count} packets");
Console.WriteLine($"Output: {outputDir}");
return 0;

static string ResolveInputPath(string input)
{
    var path = Path.GetFullPath(input);
    if (!Directory.Exists(path))
    {
        return path;
    }

    var newestCapture = Directory
        .EnumerateFiles(path, "zone-capture-*.jsonl.gz", SearchOption.TopDirectoryOnly)
        .Concat(Directory.EnumerateFiles(path, "zone-capture-*.jsonl", SearchOption.TopDirectoryOnly))
        .Select(static file => new FileInfo(file))
        .Where(static file => file.Length > 0)
        .OrderByDescending(static file => file.LastWriteTimeUtc)
        .FirstOrDefault();

    if (newestCapture is null)
    {
        return Path.Combine(path, "zone-capture-*.jsonl");
    }

    return newestCapture.FullName;
}

internal sealed class ReplayAnalyzer
{
    private readonly string inputPath;

    public ReplayAnalyzer(string inputPath)
    {
        this.inputPath = inputPath;
    }

    private static IEnumerable<string> ReadAllLines(string filePath)
    {
        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                yield return line;
            }
        }
        else
        {
            foreach (var line in File.ReadLines(filePath))
            {
                yield return line;
            }
        }
    }

    public ReplayAnalysis Analyze()
    {
        var analysis = new ReplayAnalysis
        {
            SourcePath = inputPath
        };

        foreach (var line in ReadAllLines(inputPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var kind = root.GetStringOrDefault("kind");

            if (string.Equals(kind, "session_start", StringComparison.Ordinal))
            {
                analysis.Schema = root.GetStringOrDefault("schema") ?? "";
                analysis.SessionUtc = root.GetStringOrDefault("utc") ?? "";
                analysis.MaxPayloadBytes = root.GetIntOrDefault("maxPayloadBytes");
                continue;
            }

            if (!string.Equals(kind, "zone_packet", StringComparison.Ordinal))
            {
                continue;
            }

            var packet = new ReplayPacket
            {
                Time = root.GetDoubleOrDefault("t"),
                Event = root.GetStringOrDefault("event") ?? "",
                Remaining = root.GetIntOrDefault("remaining"),
                PayloadBytes = root.GetIntOrDefault("payloadBytes")
            };
            analysis.Packets.Add(packet);

            var payload = root.TryGetProperty("payloadBase64", out var payloadElement)
                ? Convert.FromBase64String(payloadElement.GetString() ?? "")
                : [];

            DecodePacket(packet, payload, analysis);
        }

        if (analysis.Packets.Count > 0)
        {
            analysis.StartTime = analysis.Packets.Min(static packet => packet.Time);
            analysis.EndTime = analysis.Packets.Max(static packet => packet.Time);
        }

        analysis.KeyNames = KeyNameResolver.Load(inputPath);
        analysis.BlockNames = BlockNameResolver.Load(inputPath);

        return analysis;
    }

    private static void DecodePacket(ReplayPacket packet, byte[] payload, ReplayAnalysis analysis)
    {
        try
        {
            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream);

            switch (packet.Event)
            {
                case "Recv_InitZone":
                    analysis.InitZoneRemainingBytes = packet.Remaining;
                    analysis.InitZonePayloadBytes = packet.PayloadBytes;
                    analysis.InitZonePayload = payload;
                    TryReadInitZone(reader, packet, analysis);
                    break;
                case "Recv_UnitCreate":
                    analysis.UnitCreates.Add(ReadUnitCreate(reader, packet));
                    break;
                case "Recv_UnitMove":
                    analysis.UnitMoves.Add(ReadUnitMove(reader, packet));
                    break;
                case "Recv_UnitUpdate":
                    analysis.UnitUpdates.Add(ReadUnitUpdate(reader, packet));
                    break;
                case "Recv_UnitDrop":
                    analysis.UnitDrops.Add(new UnitDropEvent(packet.Time, reader.ReadUInt32()));
                    break;
                case "Recv_UnitManeuver":
                    analysis.UnitManeuvers.Add(ReadUnitManeuver(reader, packet));
                    break;
                case "Recv_Damage":
                    analysis.Damages.Add(ReadDamage(reader, packet));
                    break;
                case "Recv_Kill":
                    analysis.Kills.Add(ReadKill(reader, packet));
                    break;
                case "Recv_Impact":
                    analysis.Impacts.Add(ReadImpact(reader, packet));
                    break;
                case "Recv_BroadcastZoneEvent":
                    analysis.ZoneEvents.Add(ReadZoneEvent(reader, packet));
                    break;
                case "Recv_CreateProjectile":
                    analysis.ProjectileCreates.Add(ReadCreateProjectile(reader, packet));
                    break;
                case "Recv_MoveProjectile":
                    analysis.ProjectileMoves.Add(ReadMoveProjectile(reader, packet));
                    break;
                case "Recv_DropProjectile":
                    analysis.ProjectileDrops.Add(ReadDropProjectile(reader, packet));
                    break;
                case "Recv_Cast":
                    analysis.Casts.Add(ReadCast(reader, packet));
                    break;
                case "Recv_DoCastAbility":
                    analysis.AbilityCasts.Add(ReadAbilityCast(reader, packet));
                    break;
                case "Recv_DoStartBuild":
                    analysis.BuildStarts.Add(ReadBuildStart(reader, packet));
                    break;
                case "Recv_DoCancelBuild":
                    analysis.BuildCancels.Add(new BuildCancelEvent(packet.Time, reader.ReadUInt32()));
                    break;
                case "Recv_StartBuild":
                    analysis.RpcResults.Add(ReadBoolRpcResult(reader, packet, "StartBuild"));
                    break;
                case "Recv_DeviceBuilt":
                    analysis.DevicesBuilt.Add(ReadDeviceBuilt(reader, packet));
                    break;
                case "Recv_BlockMined":
                    analysis.BlockMined.Add(ReadBlockMined(reader, packet));
                    break;
                case "Recv_BlockUpdates":
                    analysis.BlockUpdates.Add(ReadBlockUpdates(reader, packet));
                    break;
                case "Recv_UpdateBarriers":
                    analysis.BarrierUpdates.Add(ReadBarrierUpdate(reader, packet));
                    break;
                case "Recv_UpdateZone":
                    analysis.ZoneUpdates.Add(ReadZoneUpdate(reader, packet));
                    break;
                case "Recv_SwitchGear":
                    analysis.RpcResults.Add(ReadBoolRpcResult(reader, packet, "SwitchGear"));
                    break;
                case "Recv_Reload":
                    analysis.RpcResults.Add(ReadBoolRpcResult(reader, packet, "Reload"));
                    break;
                case "Recv_StartReload":
                    analysis.RpcResults.Add(ReadBoolRpcResult(reader, packet, "StartReload"));
                    break;
                case "Recv_DoStartReload":
                    analysis.ReloadEvents.Add(new ReloadEvent(packet.Time, "Start", reader.ReadUInt32()));
                    break;
                case "Recv_DoEndReload":
                    analysis.ReloadEvents.Add(new ReloadEvent(packet.Time, "End", reader.ReadUInt32()));
                    break;
                case "Recv_DoStartChannel":
                    analysis.ChannelEvents.Add(ReadChannelStart(reader, packet));
                    break;
                case "Recv_DoEndChannel":
                    analysis.ChannelEvents.Add(new ChannelEvent(packet.Time, "End", reader.ReadUInt32(), reader.ReadByte(), null, null, null));
                    break;
                case "Recv_DoDashStartCharge":
                    analysis.DashChargeEvents.Add(new DashChargeEvent(packet.Time, "Start", reader.ReadUInt32(), reader.ReadByte()));
                    break;
                case "Recv_DoDashEndCharge":
                    analysis.DashChargeEvents.Add(new DashChargeEvent(packet.Time, "End", reader.ReadUInt32(), reader.ReadByte()));
                    break;
                case "Recv_DashEndCharge":
                    analysis.RpcResults.Add(ReadBoolRpcResult(reader, packet, "DashEndCharge"));
                    break;
                case "Recv_PickupTaken":
                    analysis.PickupTakenEvents.Add(new PickupTakenEvent(packet.Time, reader.ReadUInt32(), reader.ReadUInt32()));
                    break;
                case "Recv_DoStartRecall":
                    analysis.RecallEvents.Add(new RecallEvent(packet.Time, "Start", reader.ReadUInt32(), reader.ReadSingle(), reader.ReadUInt64()));
                    break;
                case "Recv_DoCancelRecall":
                    analysis.RecallEvents.Add(new RecallEvent(packet.Time, "Cancel", reader.ReadUInt32(), null, null));
                    break;
                case "Recv_DoRecall":
                    analysis.RecallEvents.Add(new RecallEvent(packet.Time, "Recall", reader.ReadUInt32(), null, null));
                    break;
                case "Recv_PortalTeleport":
                    analysis.PortalTeleports.Add(new PortalTeleportEvent(packet.Time, reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32()));
                    break;
                case "Recv_KickPlayer":
                    analysis.KickPlayerEvents.Add(ReadKickPlayer(reader, packet));
                    break;
                case "Recv_CastAbility":
                    analysis.RpcResults.Add(ReadBoolRpcResult(reader, packet, "CastAbility"));
                    break;
                case "Recv_EndMatch":
                    analysis.EndMatch = new MatchEnd(packet.Time, ReadTeamType(reader.ReadByte()));
                    break;
                case "Recv_EndMatchResult":
                    analysis.EndMatchResultPayloadBytes = payload.Length;
                    analysis.EndMatchResult = ReadEndMatchResult(reader, packet);
                    break;
                case "Recv_SurrenderBegin":
                    analysis.SurrenderEvents.Add(new SurrenderEvent(packet.Time, "Begin", null, reader.ReadUInt64(), null, null));
                    break;
                case "Recv_SurrenderStart":
                    analysis.RpcResults.Add(ReadSurrenderStartRpcResult(reader, packet));
                    break;
                case "Recv_SurrenderProgress":
                    analysis.SurrenderProgress.Add(ReadSurrenderProgress(reader, packet));
                    break;
                case "Recv_SurrenderEnd":
                    analysis.SurrenderEvents.Add(new SurrenderEvent(packet.Time, "End", ReadTeamType(reader.ReadByte()), null, reader.ReadBoolean(), null));
                    break;
            }
        }
        catch (Exception exception)
        {
            if (packet.Event == "Recv_InitZone" && packet.PayloadBytes < packet.Remaining && exception is EndOfStreamException)
            {
                analysis.InitZoneUnreadBytes = -1;
                return;
            }

            analysis.DecodeErrors.Add(new DecodeError(packet.Time, packet.Event, exception.Message));
        }
    }

    private static void TryReadInitZone(BinaryReader reader, ReplayPacket packet, ReplayAnalysis analysis)
    {
        var flags = ReadBitField(reader, 7);
        MapDataRecord? map = null;
        byte[]? mapData = null;
        byte[]? colorData = null;
        IReadOnlyList<InitialBlockUpdate> updates = [];
        bool? canSwitchHero = null;
        bool? isCustomGame = null;

        if (flags[0])
        {
            analysis.MapKeyHash = reader.ReadUInt32();
        }

        if (flags[1])
        {
            map = ReadMapData(reader);
        }

        if (flags[2])
        {
            mapData = ReadBinary(reader);
        }

        if (flags[3])
        {
            colorData = ReadBinary(reader);
        }

        if (flags[4])
        {
            updates = ReadInitialBlockUpdates(reader);
        }

        if (flags[5])
        {
            canSwitchHero = reader.ReadBoolean();
        }

        if (flags[6])
        {
            isCustomGame = reader.ReadBoolean();
        }

        analysis.InitZoneFlags = string.Join("", flags.Select(static flag => flag ? "1" : "0"));
        analysis.InitZoneUnreadBytes = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
        analysis.InitZone = new ZoneInitDataRecord(
            analysis.MapKeyHash,
            map,
            mapData,
            colorData,
            updates,
            canSwitchHero,
            isCustomGame);
        analysis.DecodedMap = mapData is { Length: > 0 }
            ? DecodeMapBinary(mapData, colorData)
            : null;
    }

    private static DecodedMapData DecodeMapBinary(byte[] mapData, byte[]? colorData)
    {
        using var mapInput = new MemoryStream(mapData);
        using var mapZlib = new ZLibStream(mapInput, CompressionMode.Decompress);
        using var mapMemory = new MemoryStream();
        mapZlib.CopyTo(mapMemory);
        var bytes = mapMemory.ToArray();

        using var reader = new BinaryReader(new MemoryStream(bytes));
        var size = new Vector3s((short)reader.ReadUInt16(), (short)reader.ReadUInt16(), (short)reader.ReadUInt16());
        var blockCount = size.X * size.Y * size.Z;
        var expectedBytes = 6 + blockCount * 6;
        if (bytes.Length != expectedBytes)
        {
            throw new InvalidDataException($"Decoded map byte count mismatch. Expected {expectedBytes}, got {bytes.Length}.");
        }

        var colors = DecodeMapColors(colorData, blockCount);
        var nonEmptyBlocks = new List<DecodedMapBlock>();
        var counts = new Dictionary<ushort, int>();
        var index = 0;
        for (var x = 0; x < size.X; x++)
        {
            for (var y = 0; y < size.Y; y++)
            {
                for (var z = 0; z < size.Z; z++)
                {
                    var id = reader.ReadUInt16();
                    var damage = reader.ReadByte();
                    var vdata = reader.ReadUInt16();
                    var ldata = reader.ReadByte();
                    var color = colors is null ? (byte?)null : colors[index];
                    index++;

                    if (id == 0 && damage == 0 && vdata == 0 && ldata == 0 && (color is null or 0))
                    {
                        continue;
                    }

                    counts[id] = counts.TryGetValue(id, out var count) ? count + 1 : 1;
                    nonEmptyBlocks.Add(new DecodedMapBlock(new Vector3s((short)x, (short)y, (short)z), id, damage, vdata, ldata, color));
                }
            }
        }

        return new DecodedMapData(size, blockCount, bytes.Length, colors?.Length ?? 0, nonEmptyBlocks, counts.OrderByDescending(static item => item.Value).Select(static item => new DecodedMapBlockCount(item.Key, item.Value)).ToArray());
    }

    private static byte[]? DecodeMapColors(byte[]? colorData, int blockCount)
    {
        if (colorData is not { Length: > 0 })
        {
            return null;
        }

        using var colorInput = new MemoryStream(colorData);
        using var colorZlib = new ZLibStream(colorInput, CompressionMode.Decompress);
        using var colorMemory = new MemoryStream();
        colorZlib.CopyTo(colorMemory);
        var colors = colorMemory.ToArray();
        if (colors.Length != blockCount)
        {
            throw new InvalidDataException($"Decoded color byte count mismatch. Expected {blockCount}, got {colors.Length}.");
        }

        return colors;
    }

    private static MapDataRecord ReadMapData(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 12);
        int? version = flags[0] ? reader.ReadInt32() : null;
        int? schema = flags[1] ? reader.ReadInt32() : null;
        string? match = flags[2] ? ReadMatchType(reader.ReadByte()) : null;
        var colorPalette = flags[3] ? ReadColorPalette(reader) : [];
        var spawnPoints = flags[4] ? ReadMapSpawnPoints(reader) : [];
        var units = flags[5] ? ReadMapUnits(reader) : [];
        var cameras = flags[6] ? ReadMapCameras(reader) : [];
        var triggers = flags[7] ? ReadMapTriggers(reader) : [];
        var properties = flags[8] ? ReadMapDataProps(reader) : null;
        Vector3s? size = flags[9] ? ReadVector3s(reader) : null;
        var blocksData = flags[10] ? ReadBinary(reader) : null;
        var colorsData = flags[11] ? ReadBinary(reader) : null;

        return new MapDataRecord(
            string.Join("", flags.Select(static flag => flag ? "1" : "0")),
            version,
            schema,
            match,
            colorPalette,
            spawnPoints,
            units,
            cameras,
            triggers,
            properties,
            size,
            blocksData,
            colorsData);
    }

    private static IReadOnlyList<Color32Data> ReadColorPalette(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<Color32Data>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new Color32Data(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
        }

        return items;
    }

    private static IReadOnlyList<MapSpawnPointData> ReadMapSpawnPoints(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<MapSpawnPointData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 4);
            string? team = flags[0] ? ReadTeamType(reader.ReadByte()) : null;
            Vector3f? position = flags[1] ? ReadVector3f(reader) : null;
            string? direction = flags[2] ? ReadDirection2D(reader.ReadByte()) : null;
            string? label = flags[3] ? ReadSpawnPointLabel(reader.ReadByte()) : null;
            items.Add(new MapSpawnPointData(team, position, direction, label));
        }

        return items;
    }

    private static IReadOnlyList<MapUnitData> ReadMapUnits(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<MapUnitData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 4);
            Vector3f? position = flags[0] ? ReadVector3f(reader) : null;
            Vector3s? rotation = flags[1] ? ReadVector3s(reader) : null;
            uint? unitKeyHash = flags[2] ? reader.ReadUInt32() : null;
            string? team = flags[3] ? ReadTeamType(reader.ReadByte()) : null;
            items.Add(new MapUnitData(position, rotation, unitKeyHash, team));
        }

        return items;
    }

    private static IReadOnlyList<MapCameraData> ReadMapCameras(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<MapCameraData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 4);
            Vector3f? direction = flags[0] ? ReadVector3f(reader) : null;
            Vector3f? position = flags[1] ? ReadVector3f(reader) : null;
            string? team = flags[2] ? ReadTeamType(reader.ReadByte()) : null;
            var labels = flags[3] ? ReadByteEnumList(reader, ReadMapCameraLabel) : [];
            items.Add(new MapCameraData(direction, position, team, labels));
        }

        return items;
    }

    private static IReadOnlyList<MapTriggerData> ReadMapTriggers(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<MapTriggerData>(count);
        for (var i = 0; i < count; i++)
        {
            var type = reader.ReadByte();
            var flags = ReadBitField(reader, 4);
            string? tag = flags[0] ? ReadString(reader) : null;
            var labels = flags[1] ? ReadByteEnumList(reader, ReadMapTriggerLabel) : [];
            Vector3f? position = flags[2] ? ReadVector3f(reader) : null;
            Vector3f? size = null;
            float? radius = null;
            if (flags[3])
            {
                if (type == 1)
                {
                    size = ReadVector3f(reader);
                }
                else if (type == 2)
                {
                    radius = reader.ReadSingle();
                }
            }

            items.Add(new MapTriggerData(ReadMapTriggerType(type), tag, labels, position, size, radius));
        }

        return items;
    }

    private static MapDataPropsData ReadMapDataProps(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 13);
        string? audioAmbience = flags[0] ? ReadString(reader) : null;
        string? render = flags[1] ? ReadString(reader) : null;
        string? plane = flags[2] ? ReadString(reader) : null;
        float? planePosition = flags[3] ? reader.ReadSingle() : null;
        float? killPosition = flags[4] ? reader.ReadSingle() : null;
        float? barrier1Team1 = flags[5] ? reader.ReadSingle() : null;
        float? barrier1Team2 = flags[6] ? reader.ReadSingle() : null;
        float? barrier2Team1 = flags[7] ? reader.ReadSingle() : null;
        float? barrier2Team2 = flags[8] ? reader.ReadSingle() : null;
        float? minFallHeight = flags[9] ? reader.ReadSingle() : null;
        float? maxFallHeight = flags[10] ? reader.ReadSingle() : null;
        float? buildTime = flags[11] ? reader.ReadSingle() : null;
        float? startingResources = flags[12] ? reader.ReadSingle() : null;
        return new MapDataPropsData(audioAmbience, render, plane, planePosition, killPosition, barrier1Team1, barrier1Team2, barrier2Team1, barrier2Team2, minFallHeight, maxFallHeight, buildTime, startingResources);
    }

    private static IReadOnlyList<InitialBlockUpdate> ReadInitialBlockUpdates(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<InitialBlockUpdate>(count);
        for (var i = 0; i < count; i++)
        {
            var position = ReadVector3s(reader);
            var block = ReadBlockUpdate(reader);
            items.Add(new InitialBlockUpdate(position, block.Id, block.Damage, block.Vdata, block.Ldata));
        }

        return items;
    }

    private static UnitCreateEvent ReadUnitCreate(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var init = ReadUnitInit(reader);
        return new UnitCreateEvent(packet.Time, unitId, init.KeyHash, init.Team, init.PlayerId, init.OwnerId, init.Controlled, init.Transform, init.SkinKeyHash, init.GearKeyHashes);
    }

    private static UnitMoveEvent ReadUnitMove(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var serverTime = reader.ReadUInt64();
        var transform = ReadZoneTransform(reader);
        return new UnitMoveEvent(packet.Time, unitId, serverTime, transform);
    }

    private static UnitUpdateEvent ReadUnitUpdate(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var flags = ReadBitField(reader, 22);
        string? team = null;
        float? health = null;
        float? forcefield = null;
        float? shield = null;
        float? capturePoints = null;
        float? resource = null;
        bool? movementActive = null;
        IReadOnlyDictionary<uint, IReadOnlyList<AmmoData>> ammo = new Dictionary<uint, IReadOnlyList<AmmoData>>();
        uint? currentGearKeyHash = null;
        uint? abilityKeyHash = null;
        int? abilityCharges = null;
        ulong? abilityChargeCooldownEnd = null;
        IReadOnlyDictionary<uint, ulong?> effects = new Dictionary<uint, ulong?>();
        IReadOnlyDictionary<string, float> buffs = new Dictionary<string, float>();
        IReadOnlyDictionary<int, DeviceDataRecord> devices = new Dictionary<int, DeviceDataRecord>();
        uint? turretTargetId = null;
        IReadOnlyList<Vector3s> cloudAffectedBlocks = [];
        float? projectileInitSpeed = null;
        ulong? bombTimeoutEnd = null;
        IReadOnlyList<uint> damageCapturers = [];
        PortalLinkData? portalLink = null;
        string? teslaCharge = null;

        if (flags[0]) team = ReadTeamType(reader.ReadByte());
        if (flags[1]) health = reader.ReadSingle();
        if (flags[2]) forcefield = reader.ReadSingle();
        if (flags[3]) shield = reader.ReadSingle();
        if (flags[4]) capturePoints = reader.ReadSingle();
        if (flags[5]) movementActive = reader.ReadBoolean();
        if (flags[6]) ammo = ReadAmmoDictionary(reader);
        if (flags[7]) currentGearKeyHash = reader.ReadUInt32();
        if (flags[8]) abilityKeyHash = reader.ReadUInt32();
        if (flags[9]) abilityCharges = reader.ReadInt32();
        if (flags[10]) abilityChargeCooldownEnd = reader.ReadUInt64();
        if (flags[11]) resource = reader.ReadSingle();
        if (flags[12]) effects = ReadOptionalUlongDictionary(reader);
        if (flags[13]) buffs = ReadFloatDictionary(reader, ReadBuffType);
        if (flags[14]) devices = ReadDeviceDictionary(reader);
        if (flags[15]) turretTargetId = reader.ReadUInt32();
        if (flags[16]) cloudAffectedBlocks = ReadVector3sList(reader);
        if (flags[17]) projectileInitSpeed = reader.ReadSingle();
        if (flags[18]) bombTimeoutEnd = reader.ReadUInt64();
        if (flags[19]) damageCapturers = ReadUIntList(reader);
        if (flags[20]) portalLink = ReadPortalLink(reader);
        if (flags[21]) teslaCharge = ReadTeslaChargeType(reader.ReadByte());

        return new UnitUpdateEvent(packet.Time, unitId, team, health, forcefield, shield, capturePoints, resource, movementActive, ammo, currentGearKeyHash, abilityKeyHash, abilityCharges, abilityChargeCooldownEnd, effects, buffs, devices, turretTargetId, cloudAffectedBlocks, projectileInitSpeed, bombTimeoutEnd, damageCapturers, portalLink, teslaCharge, FlagsToIndexes(flags));
    }

    private static UnitManeuverEvent ReadUnitManeuver(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var maneuver = ReadManeuver(reader);
        return new UnitManeuverEvent(packet.Time, unitId, maneuver);
    }

    private static ManeuverData ReadManeuver(BinaryReader reader)
    {
        var type = reader.ReadByte();
        return type switch
        {
            1 => ReadTeleportManeuver(reader, type),
            2 => ReadKnockbackManeuver(reader, type),
            3 => ReadPullManeuver(reader, type),
            4 => ReadSlipManeuver(reader, type),
            _ => new ManeuverData(type, $"Unknown:{type}", null, null, null, null, null, null, null, null, null, null)
        };
    }

    private static ManeuverData ReadTeleportManeuver(BinaryReader reader, byte type)
    {
        var flags = ReadBitField(reader, 1);
        var position = flags[0] ? ReadVector3f(reader) : (Vector3f?)null;
        return new ManeuverData(type, "Teleport", position, null, null, null, null, null, null, null, null, null);
    }

    private static ManeuverData ReadKnockbackManeuver(BinaryReader reader, byte type)
    {
        var flags = ReadBitField(reader, 3);
        var origin = flags[0] ? ReadVector3f(reader) : (Vector3f?)null;
        float? force = flags[1] ? reader.ReadSingle() : null;
        float? midairForce = flags[2] ? reader.ReadSingle() : null;
        return new ManeuverData(type, "Knockback", null, origin, null, force, midairForce, null, null, null, null, null);
    }

    private static ManeuverData ReadPullManeuver(BinaryReader reader, byte type)
    {
        var flags = ReadBitField(reader, 4);
        var originPosition = flags[0] ? ReadVector3f(reader) : (Vector3f?)null;
        uint? originUnitId = flags[1] ? reader.ReadUInt32() : null;
        float? force = flags[2] ? reader.ReadSingle() : null;
        bool? enabled = flags[3] ? reader.ReadBoolean() : null;
        return new ManeuverData(type, "Pull", null, originPosition, originUnitId, force, null, null, null, null, null, enabled);
    }

    private static ManeuverData ReadSlipManeuver(BinaryReader reader, byte type)
    {
        var flags = ReadBitField(reader, 4);
        float? directionAngle = flags[0] ? reader.ReadSingle() : null;
        float? distance = flags[1] ? reader.ReadSingle() : null;
        float? time = flags[2] ? reader.ReadSingle() : null;
        float? rotationTime = flags[3] ? reader.ReadSingle() : null;
        return new ManeuverData(type, "Slip", null, null, null, null, null, directionAngle, distance, time, rotationTime, null);
    }

    private static DamageEvent ReadDamage(BinaryReader reader, ReplayPacket packet)
    {
        var flags = ReadBitField(reader, 7);
        uint? targetUnitId = flags[0] ? reader.ReadUInt32() : null;
        uint? sourceUnitId = flags[1] ? reader.ReadUInt32() : null;
        Vector3f? sourcePosition = flags[2] ? ReadVector3f(reader) : null;
        uint? impactKeyHash = flags[3] ? reader.ReadUInt32() : null;
        float? damage = flags[4] ? reader.ReadSingle() : null;
        float? initialDamage = flags[5] ? reader.ReadSingle() : null;
        bool crit = flags[6] && reader.ReadBoolean();

        return new DamageEvent(packet.Time, targetUnitId, sourceUnitId, sourcePosition, impactKeyHash, damage, initialDamage, crit);
    }

    private static KillEvent ReadKill(BinaryReader reader, ReplayPacket packet)
    {
        var flags = ReadBitField(reader, 7);
        uint? deadUnitId = flags[0] ? reader.ReadUInt32() : null;
        uint? deadPlayerId = flags[1] ? reader.ReadUInt32() : null;
        uint? killerPlayerId = flags[2] ? reader.ReadUInt32() : null;
        var assistants = flags[3] ? ReadUIntList(reader) : [];
        uint? damageSourceKeyHash = flags[4] ? reader.ReadUInt32() : null;
        Vector3f? sourcePosition = flags[5] ? ReadVector3f(reader) : null;
        var crit = flags[6] && reader.ReadBoolean();
        return new KillEvent(packet.Time, deadUnitId, deadPlayerId, killerPlayerId, assistants, damageSourceKeyHash, sourcePosition, crit);
    }

    private static ImpactEvent ReadImpact(BinaryReader reader, ReplayPacket packet)
    {
        var flags = ReadBitField(reader, 9);
        Vector3f? insidePoint = flags[0] ? ReadVector3f(reader) : null;
        Vector3s? normal = flags[1] ? ReadVector3s(reader) : null;
        uint? casterUnitId = flags[2] ? reader.ReadUInt32() : null;
        uint? casterPlayerId = flags[3] ? reader.ReadUInt32() : null;
        uint? impactKeyHash = flags[4] ? reader.ReadUInt32() : null;
        uint? sourceKeyHash = flags[5] ? reader.ReadUInt32() : null;
        IReadOnlyList<uint> hitUnits = flags[6] ? ReadUIntList(reader) : [];
        Vector3f? shotPosition = flags[7] ? ReadVector3f(reader) : null;
        bool crit = flags[8] && reader.ReadBoolean();

        return new ImpactEvent(packet.Time, insidePoint, normal, casterUnitId, casterPlayerId, impactKeyHash, sourceKeyHash, hitUnits, shotPosition, crit);
    }

    private static ZoneEventEvent ReadZoneEvent(BinaryReader reader, ReplayPacket packet)
    {
        var eventType = reader.ReadByte();
        return eventType switch
        {
            1 => ReadUnitOnlyZoneEvent(reader, packet, eventType, "UnitCommonLand"),
            2 => ReadPadZoneEvent(reader, packet, eventType, "SpeedPadUsed"),
            3 => ReadPadZoneEvent(reader, packet, eventType, "JumpPadUsed"),
            4 => ReadUnitOnlyZoneEvent(reader, packet, eventType, "DoubleJump"),
            5 => ReadUnitOnlyZoneEvent(reader, packet, eventType, "ForceFall"),
            6 => ReadToolZoneEvent(reader, packet, eventType, "ToolFire", hasActive: false),
            7 => ReadToolZoneEvent(reader, packet, eventType, "ToolFireLoop", hasActive: true),
            8 => ReadToolZoneEvent(reader, packet, eventType, "ToolHold", hasActive: true),
            9 => ReadTurretZoneEvent(reader, packet, eventType),
            _ => new ZoneEventEvent(packet.Time, eventType, $"Unknown:{eventType}", null, null, null, null, null)
        };
    }

    private static ZoneEventEvent ReadUnitOnlyZoneEvent(BinaryReader reader, ReplayPacket packet, byte eventType, string name)
    {
        var flags = ReadBitField(reader, 1);
        uint? unitId = flags[0] ? reader.ReadUInt32() : null;
        return new ZoneEventEvent(packet.Time, eventType, name, unitId, null, null, null, null);
    }

    private static ZoneEventEvent ReadPadZoneEvent(BinaryReader reader, ReplayPacket packet, byte eventType, string name)
    {
        var flags = ReadBitField(reader, 2);
        uint? unitId = flags[0] ? reader.ReadUInt32() : null;
        Vector3f? position = flags[1] ? ReadVector3f(reader) : null;
        return new ZoneEventEvent(packet.Time, eventType, name, unitId, null, null, null, position);
    }

    private static ZoneEventEvent ReadToolZoneEvent(BinaryReader reader, ReplayPacket packet, byte eventType, string name, bool hasActive)
    {
        var flags = ReadBitField(reader, hasActive ? 3 : 2);
        uint? unitId = flags[0] ? reader.ReadUInt32() : null;
        byte? toolIndex = flags[1] ? reader.ReadByte() : null;
        bool? active = hasActive && flags[2] ? reader.ReadBoolean() : null;
        return new ZoneEventEvent(packet.Time, eventType, name, unitId, null, toolIndex, active, null);
    }

    private static ZoneEventEvent ReadTurretZoneEvent(BinaryReader reader, ReplayPacket packet, byte eventType)
    {
        var flags = ReadBitField(reader, 1);
        uint? turretId = flags[0] ? reader.ReadUInt32() : null;
        return new ZoneEventEvent(packet.Time, eventType, "TurretFire", null, turretId, null, null, null);
    }

    private static ProjectileCreateEvent ReadCreateProjectile(BinaryReader reader, ReplayPacket packet)
    {
        var projectileId = reader.ReadUInt64();
        var info = ReadProjectileInfo(reader);
        return new ProjectileCreateEvent(packet.Time, projectileId, info);
    }

    private static ProjectileMoveEvent ReadMoveProjectile(BinaryReader reader, ReplayPacket packet)
    {
        var projectileId = reader.ReadUInt64();
        var serverTime = reader.ReadUInt64();
        var transform = ReadZoneTransform(reader);
        return new ProjectileMoveEvent(packet.Time, projectileId, serverTime, transform);
    }

    private static ProjectileDropEvent ReadDropProjectile(BinaryReader reader, ReplayPacket packet)
    {
        var projectileId = reader.ReadUInt64();
        return new ProjectileDropEvent(packet.Time, projectileId);
    }

    private static ProjectileInfoData ReadProjectileInfo(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 5);
        uint? projectileKeyHash = flags[0] ? reader.ReadUInt32() : null;
        ZoneTransformData? transform = flags[1] ? ReadZoneTransform(reader) : null;
        float? speed = flags[2] ? reader.ReadSingle() : null;
        uint? ownerUnitId = flags[3] ? reader.ReadUInt32() : null;
        string? ownerTeam = flags[4] ? ReadTeamType(reader.ReadByte()) : null;
        return new ProjectileInfoData(projectileKeyHash, transform, speed, ownerUnitId, ownerTeam);
    }

    private static CastEvent ReadCast(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var data = ReadCastData(reader);
        return new CastEvent(packet.Time, unitId, data);
    }

    private static AbilityCastEvent ReadAbilityCast(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var flags = ReadBitField(reader, 3);
        uint? abilityKeyHash = flags[0] ? reader.ReadUInt32() : null;
        Vector3f? shotPosition = flags[1] ? ReadVector3f(reader) : null;
        IReadOnlyList<ShotData> shots = flags[2] ? ReadShots(reader) : [];
        return new AbilityCastEvent(packet.Time, unitId, abilityKeyHash, shotPosition, shots);
    }

    private static ChannelEvent ReadChannelStart(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var flags = ReadBitField(reader, 4);
        byte? toolIndex = flags[0] ? reader.ReadByte() : null;
        Vector3f? hitPosition = flags[1] ? ReadVector3f(reader) : null;
        Vector3s? targetBlock = flags[2] ? ReadVector3s(reader) : null;
        uint? targetUnit = flags[3] ? reader.ReadUInt32() : null;
        return new ChannelEvent(packet.Time, "Start", unitId, toolIndex, hitPosition, targetBlock, targetUnit);
    }

    private static KickPlayerEvent ReadKickPlayer(BinaryReader reader, ReplayPacket packet)
    {
        var remaining = reader.BaseStream.CanSeek ? reader.BaseStream.Length - reader.BaseStream.Position : 0;
        if (remaining >= 9)
        {
            return new KickPlayerEvent(packet.Time, reader.ReadUInt64(), ReadString(reader));
        }

        if (remaining >= 5)
        {
            return new KickPlayerEvent(packet.Time, reader.ReadUInt32(), reader.ReadByte().ToString(CultureInfo.InvariantCulture));
        }

        if (remaining >= 4)
        {
            return new KickPlayerEvent(packet.Time, reader.ReadUInt32(), "");
        }

        return new KickPlayerEvent(packet.Time, 0, "");
    }

    private static CastData ReadCastData(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 4);
        byte? toolIndex = flags[0] ? reader.ReadByte() : null;
        Vector3f? shotPosition = flags[1] ? ReadVector3f(reader) : null;
        IReadOnlyList<ShotData> shots = flags[2] ? ReadShots(reader) : [];
        float? unitProjectileSpeed = flags[3] ? reader.ReadSingle() : null;
        return new CastData(toolIndex, shotPosition, shots, unitProjectileSpeed);
    }

    private static IReadOnlyList<ShotData> ReadShots(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var shots = new List<ShotData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 2);
            Vector3f? targetPosition = flags[0] ? ReadVector3f(reader) : null;
            ulong? shotId = flags[1] ? reader.ReadUInt64() : null;
            shots.Add(new ShotData(targetPosition, shotId));
        }

        return shots;
    }

    private static BuildStartEvent ReadBuildStart(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var flags = ReadBitField(reader, 6);
        byte? toolIndex = flags[0] ? reader.ReadByte() : null;
        uint? deviceKeyHash = flags[1] ? reader.ReadUInt32() : null;
        Vector3s? insidePosition = flags[2] ? ReadVector3s(reader) : null;
        Vector3s? outsidePosition = flags[3] ? ReadVector3s(reader) : null;
        string? direction = flags[4] ? ReadDirection2D(reader.ReadByte()) : null;
        bool? showGhost = flags[5] ? reader.ReadBoolean() : null;
        return new BuildStartEvent(packet.Time, unitId, toolIndex, deviceKeyHash, insidePosition, outsidePosition, direction, showGhost);
    }

    private static DeviceBuiltEvent ReadDeviceBuilt(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var deviceKeyHash = reader.ReadUInt32();
        var position = ReadVector3f(reader);
        return new DeviceBuiltEvent(packet.Time, unitId, deviceKeyHash, position);
    }

    private static BlockMinedEvent ReadBlockMined(BinaryReader reader, ReplayPacket packet)
    {
        var unitId = reader.ReadUInt32();
        var blockKeyHash = reader.ReadUInt32();
        return new BlockMinedEvent(packet.Time, unitId, blockKeyHash);
    }

    private static BlockUpdatesEvent ReadBlockUpdates(BinaryReader reader, ReplayPacket packet)
    {
        var count = ReadSize(reader);
        var updates = new List<BlockUpdateSample>(count);
        var samples = new List<BlockUpdateSample>();
        for (var i = 0; i < count; i++)
        {
            var pos = ReadVector3s(reader);
            var block = ReadBlockUpdate(reader);
            var update = new BlockUpdateSample(pos, block.Id, block.Damage, block.Vdata, block.Ldata);
            updates.Add(update);
            if (samples.Count < 20)
            {
                samples.Add(update);
            }
        }

        return new BlockUpdatesEvent(packet.Time, count, samples, updates);
    }

    private static BarrierUpdateEvent ReadBarrierUpdate(BinaryReader reader, ReplayPacket packet)
    {
        var count = ReadSize(reader);
        var labels = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            labels.Add(ReadBarrierLabel(reader.ReadByte()));
        }

        return new BarrierUpdateEvent(packet.Time, labels);
    }

    private static RpcResultEvent ReadBoolRpcResult(BinaryReader reader, ReplayPacket packet, string name)
    {
        var rpcId = reader.ReadUInt16();
        var status = reader.ReadByte();
        return status switch
        {
            0 => new RpcResultEvent(packet.Time, name, rpcId, "Success", reader.ReadBoolean().ToString(CultureInfo.InvariantCulture)),
            255 => new RpcResultEvent(packet.Time, name, rpcId, "Fail", reader.ReadString()),
            _ => new RpcResultEvent(packet.Time, name, rpcId, $"Unknown:{status}", "")
        };
    }

    private static RpcResultEvent ReadSurrenderStartRpcResult(BinaryReader reader, ReplayPacket packet)
    {
        var rpcId = reader.ReadUInt16();
        var status = reader.ReadByte();
        return status switch
        {
            0 => new RpcResultEvent(packet.Time, "SurrenderStart", rpcId, "Success", ReadSurrenderStartResult(reader.ReadByte())),
            255 => new RpcResultEvent(packet.Time, "SurrenderStart", rpcId, "Fail", reader.ReadString()),
            _ => new RpcResultEvent(packet.Time, "SurrenderStart", rpcId, $"Unknown:{status}", "")
        };
    }

    private static SurrenderProgressEvent ReadSurrenderProgress(BinaryReader reader, ReplayPacket packet)
    {
        var count = ReadSize(reader);
        var votes = new List<SurrenderVoteData>(count);
        for (var i = 0; i < count; i++)
        {
            var playerId = reader.ReadUInt32();
            var voted = ReadOptionalBool(reader);
            votes.Add(new SurrenderVoteData(playerId, voted));
        }

        return new SurrenderProgressEvent(packet.Time, votes);
    }

    private static EndMatchResultData ReadEndMatchResult(BinaryReader reader, ReplayPacket packet)
    {
        var flags = ReadBitField(reader, 21);
        float? matchSeconds = flags[0] ? reader.ReadSingle() : null;
        var players = flags[1] ? ReadEndMatchPlayers(reader) : [];
        bool? isWinner = flags[2] ? reader.ReadBoolean() : null;
        bool? isBackfiller = flags[3] ? reader.ReadBoolean() : null;
        bool? isAfk = flags[4] ? reader.ReadBoolean() : null;
        uint? heroKeyHash = flags[5] ? reader.ReadUInt32() : null;
        uint? skinKeyHash = flags[6] ? reader.ReadUInt32() : null;
        XpInfoData? oldHeroXp = flags[7] ? ReadXpInfo(reader) : null;
        XpInfoData? oldPlayerXp = flags[8] ? ReadXpInfo(reader) : null;
        XpInfoData? newHeroXp = flags[9] ? ReadXpInfo(reader) : null;
        float? rewardXp = flags[10] ? reader.ReadSingle() : null;
        var oldCurrency = flags[11] ? ReadFloatDictionary(reader, ReadCurrencyType) : new Dictionary<string, float>();
        var rewardCurrency = flags[12] ? ReadFloatDictionary(reader, ReadCurrencyType) : new Dictionary<string, float>();
        var rewardBonuses = flags[13] ? ReadFloatDictionary(reader, ReadMatchRewardBonusType) : new Dictionary<string, float>();
        float? xpBoost = flags[14] ? reader.ReadSingle() : null;
        float? goldBoost = flags[15] ? reader.ReadSingle() : null;
        string? rankedStatus = flags[16] ? ReadRankedMatchStatus(reader.ReadByte()) : null;
        RankedMatchResultData? rankedData = flags[17] ? ReadRankedMatchResult(reader) : null;
        var challenges = flags[18] ? ReadChallengeDiffs(reader) : [];
        TimeTrialResultData? timeTrialData = flags[19] ? ReadTimeTrialResult(reader) : null;
        uint? lootCrateKeyHash = flags[20] ? reader.ReadUInt32() : null;
        return new EndMatchResultData(packet.Time, FlagsToIndexes(flags), matchSeconds, players, isWinner, isBackfiller, isAfk, heroKeyHash, skinKeyHash, oldHeroXp, oldPlayerXp, newHeroXp, rewardXp, oldCurrency, rewardCurrency, rewardBonuses, xpBoost, goldBoost, rankedStatus, rankedData, challenges, timeTrialData, lootCrateKeyHash);
    }

    private static IReadOnlyList<EndMatchPlayerData> ReadEndMatchPlayers(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<EndMatchPlayerData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 7);
            uint? playerId = flags[0] ? reader.ReadUInt32() : null;
            ulong? squadId = flags[1] ? reader.ReadUInt64() : null;
            bool? backfiller = flags[2] ? reader.ReadBoolean() : null;
            bool? noob = flags[3] ? reader.ReadBoolean() : null;
            EndMatchPlayerStatsData? stats = flags[4] ? ReadEndMatchPlayerStats(reader) : null;
            uint? medalPositiveKeyHash = flags[5] ? reader.ReadUInt32() : null;
            uint? medalNegativeKeyHash = flags[6] ? reader.ReadUInt32() : null;
            items.Add(new EndMatchPlayerData(playerId, squadId, backfiller, noob, stats, medalPositiveKeyHash, medalNegativeKeyHash));
        }

        return items;
    }

    private static EndMatchPlayerStatsData ReadEndMatchPlayerStats(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 2);
        var stats = flags[0] ? ReadIntDictionary(reader, ReadPlayerMatchStatType) : new Dictionary<string, int>();
        int? total = flags[1] ? reader.ReadInt32() : null;
        return new EndMatchPlayerStatsData(stats, total);
    }

    private static XpInfoData ReadXpInfo(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 3);
        int? level = flags[0] ? reader.ReadInt32() : null;
        float? levelXp = flags[1] ? reader.ReadSingle() : null;
        float? xpForNextLevel = flags[2] ? reader.ReadSingle() : null;
        return new XpInfoData(level, levelXp, xpForNextLevel);
    }

    private static RankedMatchResultData ReadRankedMatchResult(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 6);
        int? leagueTierOld = flags[0] ? reader.ReadInt32() : null;
        int? leagueTierNew = flags[1] ? reader.ReadInt32() : null;
        int? leagueDivisionOld = flags[2] ? reader.ReadInt32() : null;
        int? leagueDivisionNew = flags[3] ? reader.ReadInt32() : null;
        int? leaguePointsOld = flags[4] ? reader.ReadInt32() : null;
        int? leaguePointsNew = flags[5] ? reader.ReadInt32() : null;
        return new RankedMatchResultData(leagueTierOld, leagueTierNew, leagueDivisionOld, leagueDivisionNew, leaguePointsOld, leaguePointsNew);
    }

    private static IReadOnlyList<ChallengeDiffData> ReadChallengeDiffs(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<ChallengeDiffData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 6);
            uint? keyHash = flags[0] ? reader.ReadUInt32() : null;
            bool? completed = flags[1] ? reader.ReadBoolean() : null;
            ChallengeResultData? oldResult = flags[2] ? ReadChallengeResult(reader) : null;
            ChallengeResultData? newResult = flags[3] ? ReadChallengeResult(reader) : null;
            ChallengeFriendInfoData? friendInfo = flags[4] ? ReadChallengeFriendInfo(reader) : null;
            bool? betterThanFriend = flags[5] ? reader.ReadBoolean() : null;
            items.Add(new ChallengeDiffData(keyHash, completed, oldResult, newResult, friendInfo, betterThanFriend));
        }

        return items;
    }

    private static ChallengeResultData ReadChallengeResult(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 3);
        float? totalValue = flags[0] ? reader.ReadSingle() : null;
        int? matchesSpent = flags[1] ? reader.ReadInt32() : null;
        float? matchSecondsSpent = flags[2] ? reader.ReadSingle() : null;
        return new ChallengeResultData(totalValue, matchesSpent, matchSecondsSpent);
    }

    private static ChallengeFriendInfoData ReadChallengeFriendInfo(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 3);
        uint? id = flags[0] ? reader.ReadUInt32() : null;
        string? name = flags[1] ? ReadString(reader) : null;
        ChallengeResultData? result = flags[2] ? ReadChallengeResult(reader) : null;
        return new ChallengeFriendInfoData(id, name, result);
    }

    private static TimeTrialResultData ReadTimeTrialResult(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 6);
        var oldGoals = flags[0] ? ReadIntList(reader) : [];
        var newGoals = flags[1] ? ReadIntList(reader) : [];
        float? xpReward = flags[2] ? reader.ReadSingle() : null;
        var currencyReward = flags[3] ? ReadFloatDictionary(reader, ReadCurrencyType) : new Dictionary<string, float>();
        float? resultTime = flags[4] ? reader.ReadSingle() : null;
        float? bestResultTime = flags[5] ? reader.ReadSingle() : null;
        return new TimeTrialResultData(oldGoals, newGoals, xpReward, currencyReward, resultTime, bestResultTime);
    }

    private static ZoneUpdateEvent ReadZoneUpdate(BinaryReader reader, ReplayPacket packet)
    {
        var flags = ReadBitField(reader, 9);
        ZonePhaseData? phase = flags[0] ? ReadZonePhase(reader) : null;
        MatchStatsData? stats = flags[1] ? ReadMatchStats(reader) : null;
        IReadOnlyList<SpawnPointData> spawnPoints = flags[2] ? ReadSpawnPoints(reader) : [];
        IReadOnlyList<PlayerSpawnPointData> playerSpawnPoints = flags[3] ? ReadPlayerSpawnPoints(reader) : [];
        IReadOnlyList<RespawnInfoData> respawnInfo = flags[4] ? ReadRespawnInfo(reader) : [];
        IReadOnlyList<ZonePlayerInfoData> playerInfo = flags[5] ? ReadPlayerInfo(reader) : [];
        SupplyInfoData? supplyInfo = flags[6] ? ReadSupplyInfo(reader) : null;
        IReadOnlyList<ZoneObjectiveData> objectives = flags[7] ? ReadObjectives(reader) : [];
        float? resourceCap = flags[8] ? reader.ReadSingle() : null;

        return new ZoneUpdateEvent(packet.Time, FlagsToIndexes(flags), phase, stats, spawnPoints, playerSpawnPoints, respawnInfo, playerInfo, supplyInfo, objectives, resourceCap);
    }

    private static ZonePhaseData ReadZonePhase(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 3);
        string? phaseType = flags[0] ? ReadZonePhaseType(reader.ReadByte()) : null;
        long? startTime = flags[1] ? reader.ReadInt64() : null;
        long? endTime = flags[2] ? reader.ReadInt64() : null;
        return new ZonePhaseData(phaseType, startTime, endTime);
    }

    private static MatchStatsData ReadMatchStats(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 3);
        var playerStats = flags[0] ? ReadMatchPlayerStatsDictionary(reader) : [];
        var team1Stats = flags[1] ? ReadMatchTeamStats(reader) : null;
        var team2Stats = flags[2] ? ReadMatchTeamStats(reader) : null;
        return new MatchStatsData(playerStats, team1Stats, team2Stats);
    }

    private static IReadOnlyList<MatchPlayerStatsData> ReadMatchPlayerStatsDictionary(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<MatchPlayerStatsData>(count);
        for (var i = 0; i < count; i++)
        {
            var playerId = reader.ReadUInt32();
            var flags = ReadBitField(reader, 4);
            string? team = flags[0] ? ReadTeamType(reader.ReadByte()) : null;
            int? kills = flags[1] ? reader.ReadInt32() : null;
            int? deaths = flags[2] ? reader.ReadInt32() : null;
            int? assists = flags[3] ? reader.ReadInt32() : null;
            items.Add(new MatchPlayerStatsData(playerId, team, kills, deaths, assists));
        }

        return items;
    }

    private static MatchTeamStatsData ReadMatchTeamStats(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 4);
        int? warfare = flags[0] ? reader.ReadInt32() : null;
        int? construction = flags[1] ? reader.ReadInt32() : null;
        int? tactics = flags[2] ? reader.ReadInt32() : null;
        int? healing = flags[3] ? reader.ReadInt32() : null;
        return new MatchTeamStatsData(warfare, construction, tactics, healing);
    }

    private static IReadOnlyList<SpawnPointData> ReadSpawnPoints(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<SpawnPointData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 5);
            uint? id = flags[0] ? reader.ReadUInt32() : null;
            string? team = flags[1] ? ReadTeamType(reader.ReadByte()) : null;
            Vector3f? position = flags[2] ? ReadVector3f(reader) : null;
            string? lockType = flags[3] ? ReadSpawnPointLockType(reader.ReadByte()) : null;
            uint? owner = flags[4] ? reader.ReadUInt32() : null;
            items.Add(new SpawnPointData(id, team, position, lockType, owner));
        }

        return items;
    }

    private static IReadOnlyList<PlayerSpawnPointData> ReadPlayerSpawnPoints(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<PlayerSpawnPointData>(count);
        for (var i = 0; i < count; i++)
        {
            var playerId = reader.ReadUInt32();
            var spawnPointId = ReadOptionalUInt(reader);
            items.Add(new PlayerSpawnPointData(playerId, spawnPointId));
        }

        return items;
    }

    private static IReadOnlyList<RespawnInfoData> ReadRespawnInfo(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<RespawnInfoData>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new RespawnInfoData(reader.ReadUInt32(), reader.ReadUInt64()));
        }

        return items;
    }

    private static IReadOnlyList<ZonePlayerInfoData> ReadPlayerInfo(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<ZonePlayerInfoData>(count);
        for (var i = 0; i < count; i++)
        {
            var playerId = reader.ReadUInt32();
            var flags = ReadBitField(reader, 4);
            string? nickname = flags[0] ? ReadString(reader) : null;
            ulong? steamId = flags[1] ? reader.ReadUInt64() : null;
            ulong? squadId = flags[2] ? reader.ReadUInt64() : null;
            bool? lookingForFriends = flags[3] ? reader.ReadBoolean() : null;
            items.Add(new ZonePlayerInfoData(playerId, nickname, steamId, squadId, lookingForFriends));
        }

        return items;
    }

    private static SupplyInfoData ReadSupplyInfo(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 3);
        uint? nextSupplyDropKeyHash = flags[0] ? reader.ReadUInt32() : null;
        ulong? nextSupplyDropTime = flags[1] ? reader.ReadUInt64() : null;
        Vector3f? position = flags[2] ? ReadVector3f(reader) : null;
        return new SupplyInfoData(nextSupplyDropKeyHash, nextSupplyDropTime, position);
    }

    private static IReadOnlyList<ZoneObjectiveData> ReadObjectives(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var items = new List<ZoneObjectiveData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 4);
            string? team = flags[0] ? ReadTeamType(reader.ReadByte()) : null;
            int? id = flags[1] ? reader.ReadInt32() : null;
            int? counter = flags[2] ? reader.ReadInt32() : null;
            int? requiredCounter = flags[3] ? reader.ReadInt32() : null;
            items.Add(new ZoneObjectiveData(team, id, counter, requiredCounter));
        }

        return items;
    }

    private static UnitInitData ReadUnitInit(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 8);
        uint? keyHash = flags[0] ? reader.ReadUInt32() : null;
        ZoneTransformData? transform = flags[1] ? ReadZoneTransform(reader) : null;
        var controlled = flags[2] && reader.ReadBoolean();
        uint? ownerId = flags[3] ? reader.ReadUInt32() : null;
        var team = flags[4] ? ReadTeamType(reader.ReadByte()) : "";
        uint? playerId = flags[5] ? reader.ReadUInt32() : null;
        uint? skinKeyHash = flags[6] ? reader.ReadUInt32() : null;
        var gearKeyHashes = flags[7] ? ReadUIntList(reader) : [];

        return new UnitInitData(keyHash, transform, controlled, ownerId, team, playerId, skinKeyHash, gearKeyHashes);
    }

    private static ZoneTransformData ReadZoneTransform(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 10);
        Vector3f? position = flags[0] ? ReadVector3f(reader) : null;
        Vector3s? rotation = flags[1] ? ReadVector3s(reader) : null;
        Vector3s? velocity = flags[2] ? ReadVector3s(reader) : null;
        bool? crouch = flags[3] ? reader.ReadBoolean() : null;
        bool? jump = flags[4] ? reader.ReadBoolean() : null;
        bool? sprint = flags[5] ? reader.ReadBoolean() : null;
        bool? wallClimb = flags[6] ? reader.ReadBoolean() : null;
        bool? dash = flags[7] ? reader.ReadBoolean() : null;
        bool? groundSlam = flags[8] ? reader.ReadBoolean() : null;
        bool? noInterpolation = flags[9] ? reader.ReadBoolean() : null;

        return new ZoneTransformData(position, rotation, velocity, crouch, jump, sprint, wallClimb, dash, groundSlam, noInterpolation);
    }

    private static BlockUpdateData ReadBlockUpdate(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 4);
        ushort? id = flags[0] ? reader.ReadUInt16() : null;
        byte? damage = flags[1] ? reader.ReadByte() : null;
        ushort? vdata = flags[2] ? reader.ReadUInt16() : null;
        byte? ldata = flags[3] ? reader.ReadByte() : null;
        return new BlockUpdateData(id, damage, vdata, ldata);
    }

    private static bool[] ReadBitField(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes((count + 7) / 8);
        var flags = new bool[count];
        for (var i = 0; i < count; i++)
        {
            var byteIndex = i >> 3;
            var mask = 0x80 >> (i & 7);
            flags[i] = (bytes[byteIndex] & mask) != 0;
        }

        return flags;
    }

    private static int ReadSize(BinaryReader reader)
    {
        var result = 0;
        var shift = 0;
        while (true)
        {
            var value = reader.ReadByte();
            if ((value & 0x80) == 0)
            {
                result |= value << (shift & 31);
                return result;
            }

            result |= (value & 0x7F) << (shift & 31);
            shift += 7;
        }
    }

    private static IReadOnlyList<uint> ReadUIntList(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var values = new List<uint>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(reader.ReadUInt32());
        }

        return values;
    }

    private static IReadOnlyList<int> ReadIntList(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var values = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(reader.ReadInt32());
        }

        return values;
    }

    private static Dictionary<string, int> ReadIntDictionary(BinaryReader reader, Func<byte, string> keyReader)
    {
        var count = ReadSize(reader);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            values[keyReader(reader.ReadByte())] = reader.ReadInt32();
        }

        return values;
    }

    private static Dictionary<string, float> ReadFloatDictionary(BinaryReader reader, Func<byte, string> keyReader)
    {
        var count = ReadSize(reader);
        var values = new Dictionary<string, float>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            values[keyReader(reader.ReadByte())] = reader.ReadSingle();
        }

        return values;
    }

    private static IReadOnlyDictionary<uint, IReadOnlyList<AmmoData>> ReadAmmoDictionary(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var values = new Dictionary<uint, IReadOnlyList<AmmoData>>();
        for (var i = 0; i < count; i++)
        {
            var keyHash = reader.ReadUInt32();
            values[keyHash] = ReadAmmoList(reader);
        }

        return values;
    }

    private static IReadOnlyList<AmmoData> ReadAmmoList(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var values = new List<AmmoData>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadBitField(reader, 3);
            int? index = flags[0] ? reader.ReadInt32() : null;
            float? mag = flags[1] ? reader.ReadSingle() : null;
            float? pool = flags[2] ? reader.ReadSingle() : null;
            values.Add(new AmmoData(index, mag, pool));
        }

        return values;
    }

    private static IReadOnlyDictionary<uint, ulong?> ReadOptionalUlongDictionary(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var values = new Dictionary<uint, ulong?>();
        for (var i = 0; i < count; i++)
        {
            var keyHash = reader.ReadUInt32();
            values[keyHash] = reader.ReadBoolean() ? reader.ReadUInt64() : null;
        }

        return values;
    }

    private static IReadOnlyDictionary<int, DeviceDataRecord> ReadDeviceDictionary(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var values = new Dictionary<int, DeviceDataRecord>();
        for (var i = 0; i < count; i++)
        {
            var key = reader.ReadInt32();
            values[key] = ReadDeviceData(reader);
        }

        return values;
    }

    private static DeviceDataRecord ReadDeviceData(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 3);
        uint? deviceKeyHash = flags[0] ? reader.ReadUInt32() : null;
        float? totalCost = flags[1] ? reader.ReadSingle() : null;
        float? costInc = flags[2] ? reader.ReadSingle() : null;
        return new DeviceDataRecord(deviceKeyHash, totalCost, costInc);
    }

    private static IReadOnlyList<Vector3s> ReadVector3sList(BinaryReader reader)
    {
        var count = ReadSize(reader);
        var values = new List<Vector3s>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(ReadVector3s(reader));
        }

        return values;
    }

    private static PortalLinkData ReadPortalLink(BinaryReader reader)
    {
        var flags = ReadBitField(reader, 1);
        uint? linkedPortalUnitId = flags[0] ? reader.ReadUInt32() : null;
        return new PortalLinkData(linkedPortalUnitId);
    }

    private static IReadOnlyList<string> ReadByteEnumList(BinaryReader reader, Func<byte, string> nameFactory)
    {
        var count = ReadSize(reader);
        var values = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(nameFactory(reader.ReadByte()));
        }

        return values;
    }

    private static byte[] ReadBinary(BinaryReader reader)
    {
        var count = ReadSize(reader);
        return count <= 0 ? [] : reader.ReadBytes(count);
    }

    private static uint? ReadOptionalUInt(BinaryReader reader) =>
        reader.ReadBoolean() ? reader.ReadUInt32() : null;

    private static bool? ReadOptionalBool(BinaryReader reader) =>
        reader.ReadBoolean() ? reader.ReadBoolean() : null;

    private static string ReadString(BinaryReader reader)
    {
        var count = ReadSize(reader);
        return count <= 0 ? "" : Encoding.UTF8.GetString(reader.ReadBytes(count));
    }

    private static Vector3f ReadVector3f(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static Vector3s ReadVector3s(BinaryReader reader) => new(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());

    private static string ReadTeamType(byte value) => value switch
    {
        0 => "Neutral",
        1 => "Team1",
        2 => "Team2",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadZonePhaseType(byte value) => value switch
    {
        1 => "Waiting",
        2 => "TutorialInit",
        3 => "Build",
        4 => "Assault",
        5 => "Build2",
        6 => "Assault2",
        7 => "SuddenDeath",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadPlayerMatchStatType(byte value) => value switch
    {
        1 => "Earned",
        2 => "Built",
        3 => "Destroyed",
        4 => "Objective",
        5 => "BlockAssist",
        6 => "Kill",
        7 => "Death",
        8 => "Assist",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadCurrencyType(byte value) => value switch
    {
        1 => "Virtual",
        2 => "Real",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadMatchRewardBonusType(byte value) => value switch
    {
        1 => "Victory",
        2 => "Noob",
        3 => "Backfilling",
        4 => "Shorthand",
        5 => "DailyWin",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadRankedMatchStatus(byte value) => value switch
    {
        1 => "None",
        2 => "League",
        3 => "Noob",
        4 => "Backfiller",
        5 => "Undeveloped",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadBuffType(byte value) => value switch
    {
        1 => "ResourceProduction",
        2 => "ResourceBonus",
        3 => "KillResourceBonus",
        4 => "KillByTeamResourceBonus",
        5 => "SupplyResourceBonus",
        6 => "ObjectiveResourceBonus",
        7 => "MiningBonus",
        8 => "HealthCap",
        9 => "HealthRegen",
        10 => "ForcefieldCap",
        11 => "ForcefieldRegen",
        12 => "AmmoRegen",
        13 => "AmmoDrain",
        14 => "Bleeding",
        15 => "Burning",
        16 => "Poisoned",
        17 => "Decay",
        18 => "Shield",
        19 => "VisionMark",
        20 => "WallClimb",
        21 => "Invulnerability",
        22 => "PlayerDamage",
        23 => "WorldDamage",
        24 => "ObjectiveDamage",
        25 => "BuildSpeed",
        26 => "BuildCostReduction",
        27 => "WeaponMagazine",
        28 => "WeaponPool",
        29 => "WeaponReload",
        30 => "WeaponSwitch",
        31 => "RunSpeed",
        32 => "SprintSpeed",
        33 => "JumpHeight",
        34 => "FallDamageReduction",
        35 => "MiningAmmoRefill",
        36 => "MiningHealthRefill",
        37 => "SupplyForcefield",
        38 => "SupplyAmmo",
        39 => "SupplyHealth",
        40 => "Sway",
        41 => "CofBonus",
        42 => "Root",
        43 => "Disarm",
        44 => "AbilityCooldownReduction",
        45 => "Confusion",
        46 => "Disabled",
        47 => "HealthGain",
        48 => "AmmoGain",
        49 => "SplashDamageReduction",
        50 => "InfiniteAmmo",
        51 => "SlipperyImmunity",
        52 => "DashTime",
        53 => "DashDistance",
        54 => "KnockbackIgnore",
        55 => "SwimSpeed",
        56 => "ToolWorldDamage",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadTeslaChargeType(byte value) => value switch
    {
        1 => "NoCharge",
        2 => "RemoteCharge",
        3 => "SelfCharge",
        4 => "FullSelfCharge",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadMatchType(byte value) => value switch
    {
        1 => "ShieldRush2",
        2 => "ShieldCapture",
        3 => "Tutorial",
        4 => "TimeTrial",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadSpawnPointLockType(byte value) => value switch
    {
        1 => "Free",
        2 => "PlayerBlocked",
        3 => "WorldBlocked",
        4 => "ServerBlocked",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadSpawnPointLabel(byte value) => value switch
    {
        1 => "Base",
        2 => "Objective1",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadDirection2D(byte value) => value switch
    {
        0 => "Left",
        1 => "Right",
        2 => "Back",
        3 => "Front",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadMapCameraLabel(byte value) => value switch
    {
        1 => "Base",
        2 => "Line1",
        3 => "Line2",
        4 => "Line3",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadMapTriggerLabel(byte value) => value switch
    {
        1 => "TutorialZone",
        2 => "Audio",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadMapTriggerType(byte value) => value switch
    {
        1 => "Box",
        2 => "Sphere",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadBarrierLabel(byte value) => value switch
    {
        1 => "Build1Team1",
        2 => "Build1Team2",
        3 => "Build2Team1",
        4 => "Build2Team2",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadSurrenderStartResult(byte value) => value switch
    {
        1 => "Accepted",
        2 => "Disabled",
        3 => "TooEarly",
        4 => "TooFrequent",
        5 => "InProgress",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string FlagsToIndexes(bool[] flags) =>
        string.Join("|", flags.Select((flag, index) => (flag, index)).Where(static item => item.flag).Select(static item => item.index.ToString(CultureInfo.InvariantCulture)));
}

internal static class ReplayReportWriter
{
    private static readonly string[] SlopeCornerNames = ["C000", "C100", "C010", "C001", "C110", "C011", "C101", "C111"];

    public static void Write(string outputDir, ReplayAnalysis analysis)
    {
        var buildPlacements = BuildBuildPlacements(analysis);
        var mapTimeline = BuildMapStateTimeline(analysis, buildPlacements);
        var mapVerification = VerifyMapStateTimeline(analysis, mapTimeline);
        WriteMapBinaryAssets(outputDir, analysis);
        WriteSummary(outputDir, analysis, mapVerification);
        WriteValidation(outputDir, analysis);
        WriteCsv(Path.Combine(outputDir, "packets.csv"), ["time", "event", "remaining", "payload_bytes"], analysis.Packets, static p => [p.TimeText, p.Event, p.Remaining.ToString(CultureInfo.InvariantCulture), p.PayloadBytes.ToString(CultureInfo.InvariantCulture)]);
        WriteCsv(Path.Combine(outputDir, "map_spawn_points.csv"), ["team", "label", "direction", "x", "y", "z"], analysis.InitZone?.Map?.SpawnPoints ?? [], static item => [item.Team ?? "", item.Label ?? "", item.Direction ?? "", item.Position?.XText ?? "", item.Position?.YText ?? "", item.Position?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "map_units.csv"), ["unit_key_hash", "unit_name", "team", "x", "y", "z", "rot_x", "rot_y", "rot_z"], analysis.InitZone?.Map?.Units ?? [], item => [item.UnitKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.UnitKeyHash), item.Team ?? "", item.Position?.XText ?? "", item.Position?.YText ?? "", item.Position?.ZText ?? "", item.Rotation?.XText ?? "", item.Rotation?.YText ?? "", item.Rotation?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "map_cameras.csv"), ["team", "labels", "x", "y", "z", "dir_x", "dir_y", "dir_z"], analysis.InitZone?.Map?.Cameras ?? [], static item => [item.Team ?? "", string.Join("|", item.Labels), item.Position?.XText ?? "", item.Position?.YText ?? "", item.Position?.ZText ?? "", item.Direction?.XText ?? "", item.Direction?.YText ?? "", item.Direction?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "map_triggers.csv"), ["type", "tag", "labels", "x", "y", "z", "size_x", "size_y", "size_z", "radius"], analysis.InitZone?.Map?.Triggers ?? [], static item => [item.Type, item.Tag ?? "", string.Join("|", item.Labels), item.Position?.XText ?? "", item.Position?.YText ?? "", item.Position?.ZText ?? "", item.Size?.XText ?? "", item.Size?.YText ?? "", item.Size?.ZText ?? "", item.Radius?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""]);
        WriteCsv(Path.Combine(outputDir, "init_block_updates.csv"), ["x", "y", "z", "id", "damage", "vdata", "vdata_low_byte", "vdata_high_byte", "slope_existing_corner_count", "slope_existing_corners", "slope_missing_corners", "ldata", "team_bits", "team", "ldata_flags"], analysis.InitZone?.Updates ?? [], static item => [item.Position.XText, item.Position.YText, item.Position.ZText, item.Id?.ToString(CultureInfo.InvariantCulture) ?? "", item.Damage?.ToString(CultureInfo.InvariantCulture) ?? "", item.Vdata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatVdataLowByte(item.Vdata), FormatVdataHighByte(item.Vdata), FormatSlopeExistingCornerCount(item.Vdata), FormatSlopeExistingCorners(item.Vdata), FormatSlopeMissingCorners(item.Vdata), item.Ldata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatTeamBits(item.Ldata), FormatBlockTeam(item.Ldata), FormatLdataFlags(item.Ldata)]);
        WriteCsv(Path.Combine(outputDir, "map_blocks.csv"), ["x", "y", "z", "id", "name", "damage", "vdata", "vdata_low_byte", "vdata_high_byte", "slope_existing_corner_count", "slope_existing_corners", "slope_missing_corners", "ldata", "team_bits", "team", "ldata_flags", "color"], analysis.DecodedMap?.NonEmptyBlocks ?? [], item => [item.Position.XText, item.Position.YText, item.Position.ZText, item.Id.ToString(CultureInfo.InvariantCulture), ResolveBlockName(analysis, item.Id), item.Damage.ToString(CultureInfo.InvariantCulture), item.Vdata.ToString(CultureInfo.InvariantCulture), FormatVdataLowByte(item.Vdata), FormatVdataHighByte(item.Vdata), FormatSlopeExistingCornerCount(item.Vdata), FormatSlopeExistingCorners(item.Vdata), FormatSlopeMissingCorners(item.Vdata), item.Ldata.ToString(CultureInfo.InvariantCulture), FormatTeamBits(item.Ldata), FormatBlockTeam(item.Ldata), FormatLdataFlags(item.Ldata), item.Color?.ToString(CultureInfo.InvariantCulture) ?? ""]);
        WriteCsv(Path.Combine(outputDir, "map_block_counts.csv"), ["id", "name", "count"], analysis.DecodedMap?.BlockCounts ?? [], item => [item.Id.ToString(CultureInfo.InvariantCulture), ResolveBlockName(analysis, item.Id), item.Count.ToString(CultureInfo.InvariantCulture)]);
        WriteCsv(Path.Combine(outputDir, "unit_moves.csv"), ["time", "unit_id", "server_time", "x", "y", "z", "rot_x", "rot_y", "rot_z"], analysis.UnitMoves, static item => [item.TimeText, item.UnitIdText, item.ServerTime.ToString(CultureInfo.InvariantCulture), item.Transform.Position?.XText ?? "", item.Transform.Position?.YText ?? "", item.Transform.Position?.ZText ?? "", item.Transform.Rotation?.XText ?? "", item.Transform.Rotation?.YText ?? "", item.Transform.Rotation?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "unit_creates.csv"), ["time", "unit_id", "key_hash", "key_name", "team", "player_id", "owner_id", "controlled", "skin_key_hash", "skin_name", "gear_key_hashes", "gear_names", "x", "y", "z"], analysis.UnitCreates, item => [item.TimeText, item.UnitIdText, item.KeyHashText, ResolveKeyName(analysis, item.KeyHash), item.Team, item.PlayerId?.ToString(CultureInfo.InvariantCulture) ?? "", item.OwnerId?.ToString(CultureInfo.InvariantCulture) ?? "", item.Controlled ? "true" : "false", item.SkinKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.SkinKeyHash), FormatKeyHashes(item.GearKeyHashes), FormatKeyNames(analysis, item.GearKeyHashes), item.Transform?.Position?.XText ?? "", item.Transform?.Position?.YText ?? "", item.Transform?.Position?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "unit_updates.csv"), ["time", "unit_id", "team", "health", "forcefield", "shield", "capture_points", "resource", "movement_active", "current_gear_hash", "current_gear_name", "ability_hash", "ability_name", "ability_charges", "ability_cooldown_end", "ammo", "effects", "buffs", "devices", "turret_target_id", "cloud_blocks", "projectile_init_speed", "bomb_timeout_end", "damage_capturers", "portal_link_unit_id", "tesla_charge", "flags"], analysis.UnitUpdates, item => [item.TimeText, item.UnitIdText, item.Team ?? "", item.Health?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Forcefield?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Shield?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.CapturePoints?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Resource?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.MovementActive?.ToString() ?? "", item.CurrentGearKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.CurrentGearKeyHash), item.AbilityKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.AbilityKeyHash), item.AbilityCharges?.ToString(CultureInfo.InvariantCulture) ?? "", item.AbilityChargeCooldownEnd?.ToString(CultureInfo.InvariantCulture) ?? "", FormatAmmo(analysis, item.Ammo), FormatEffects(analysis, item.Effects), FormatFloatDictionary(item.Buffs), FormatDevices(analysis, item.Devices), item.TurretTargetId?.ToString(CultureInfo.InvariantCulture) ?? "", item.CloudAffectedBlocks.Count.ToString(CultureInfo.InvariantCulture), item.ProjectileInitSpeed?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.BombTimeoutEnd?.ToString(CultureInfo.InvariantCulture) ?? "", string.Join("|", item.DamageCapturers), item.PortalLink?.LinkedPortalUnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.TeslaCharge ?? "", item.Flags]);
        WriteCsv(Path.Combine(outputDir, "unit_drops.csv"), ["time", "unit_id"], analysis.UnitDrops, static item => [item.TimeText, item.UnitIdText]);
        WriteCsv(Path.Combine(outputDir, "unit_maneuvers.csv"), ["time", "unit_id", "type", "x", "y", "z", "origin_x", "origin_y", "origin_z", "origin_unit_id", "force", "midair_force", "direction_angle", "distance", "maneuver_time", "rotation_time", "enabled"], analysis.UnitManeuvers, static item => [item.TimeText, item.UnitIdText, item.Maneuver.Name, item.Maneuver.Position?.XText ?? "", item.Maneuver.Position?.YText ?? "", item.Maneuver.Position?.ZText ?? "", item.Maneuver.OriginPosition?.XText ?? "", item.Maneuver.OriginPosition?.YText ?? "", item.Maneuver.OriginPosition?.ZText ?? "", item.Maneuver.OriginUnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.Maneuver.Force?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Maneuver.MidairForce?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Maneuver.DirectionAngle?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Maneuver.Distance?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Maneuver.Time?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Maneuver.RotationTime?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Maneuver.Enabled?.ToString() ?? ""]);
        WriteCsv(Path.Combine(outputDir, "damage.csv"), ["time", "target_unit_id", "source_unit_id", "damage", "initial_damage", "crit", "impact_key_hash", "impact_name"], analysis.Damages, item => [item.TimeText, item.TargetUnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.SourceUnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.Damage?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.InitialDamage?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Crit ? "true" : "false", item.ImpactKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.ImpactKeyHash)]);
        WriteCsv(Path.Combine(outputDir, "kills.csv"), ["time", "dead_unit_id", "dead_player_id", "killer_player_id", "assistants", "damage_source_key_hash", "damage_source_name", "crit", "source_x", "source_y", "source_z"], analysis.Kills, item => [item.TimeText, item.DeadUnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.DeadPlayerId?.ToString(CultureInfo.InvariantCulture) ?? "", item.KillerPlayerId?.ToString(CultureInfo.InvariantCulture) ?? "", string.Join("|", item.Assistants), item.DamageSourceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.DamageSourceKeyHash), item.Crit ? "true" : "false", item.SourcePosition?.XText ?? "", item.SourcePosition?.YText ?? "", item.SourcePosition?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "impacts.csv"), ["time", "caster_unit_id", "caster_player_id", "impact_key_hash", "impact_name", "source_key_hash", "source_name", "hit_units", "crit", "inside_x", "inside_y", "inside_z", "shot_x", "shot_y", "shot_z", "normal_x", "normal_y", "normal_z"], analysis.Impacts, item => [item.TimeText, item.CasterUnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.CasterPlayerId?.ToString(CultureInfo.InvariantCulture) ?? "", item.ImpactKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.ImpactKeyHash), item.SourceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.SourceKeyHash), string.Join("|", item.HitUnits), item.Crit ? "true" : "false", item.InsidePoint?.XText ?? "", item.InsidePoint?.YText ?? "", item.InsidePoint?.ZText ?? "", item.ShotPosition?.XText ?? "", item.ShotPosition?.YText ?? "", item.ShotPosition?.ZText ?? "", item.Normal?.XText ?? "", item.Normal?.YText ?? "", item.Normal?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "zone_events.csv"), ["time", "type", "event", "unit_id", "turret_id", "tool_index", "active", "x", "y", "z"], analysis.ZoneEvents, static item => [item.TimeText, item.EventType.ToString(CultureInfo.InvariantCulture), item.Name, item.UnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.TurretId?.ToString(CultureInfo.InvariantCulture) ?? "", item.ToolIndex?.ToString(CultureInfo.InvariantCulture) ?? "", item.Active?.ToString() ?? "", item.Position?.XText ?? "", item.Position?.YText ?? "", item.Position?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "projectile_creates.csv"), ["time", "projectile_id", "projectile_key_hash", "projectile_name", "owner_unit_id", "owner_team", "speed", "x", "y", "z"], analysis.ProjectileCreates, item => [item.TimeText, item.ProjectileIdText, item.Info.ProjectileKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.Info.ProjectileKeyHash), item.Info.OwnerUnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.Info.OwnerTeam ?? "", item.Info.Speed?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.Info.Transform?.Position?.XText ?? "", item.Info.Transform?.Position?.YText ?? "", item.Info.Transform?.Position?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "projectile_moves.csv"), ["time", "projectile_id", "server_time", "x", "y", "z", "rot_x", "rot_y", "rot_z"], analysis.ProjectileMoves, static item => [item.TimeText, item.ProjectileIdText, item.ServerTime.ToString(CultureInfo.InvariantCulture), item.Transform.Position?.XText ?? "", item.Transform.Position?.YText ?? "", item.Transform.Position?.ZText ?? "", item.Transform.Rotation?.XText ?? "", item.Transform.Rotation?.YText ?? "", item.Transform.Rotation?.ZText ?? ""]);
        WriteCsv(Path.Combine(outputDir, "projectile_drops.csv"), ["time", "projectile_id"], analysis.ProjectileDrops, static item => [item.TimeText, item.ProjectileIdText]);
        WriteCsv(Path.Combine(outputDir, "casts.csv"), ["time", "unit_id", "tool_index", "shot_x", "shot_y", "shot_z", "shots", "projectile_speed"], analysis.Casts, static item => [item.TimeText, item.UnitIdText, item.Data.ToolIndex?.ToString(CultureInfo.InvariantCulture) ?? "", item.Data.ShotPosition?.XText ?? "", item.Data.ShotPosition?.YText ?? "", item.Data.ShotPosition?.ZText ?? "", FormatShots(item.Data.Shots), item.Data.UnitProjectileSpeed?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""]);
        WriteCsv(Path.Combine(outputDir, "ability_casts.csv"), ["time", "unit_id", "ability_key_hash", "ability_name", "shot_x", "shot_y", "shot_z", "shots"], analysis.AbilityCasts, item => [item.TimeText, item.UnitIdText, item.AbilityKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.AbilityKeyHash), item.ShotPosition?.XText ?? "", item.ShotPosition?.YText ?? "", item.ShotPosition?.ZText ?? "", FormatShots(item.Shots)]);
        WriteCsv(Path.Combine(outputDir, "build_starts.csv"), ["time", "unit_id", "tool_index", "device_key_hash", "device_name", "inside_x", "inside_y", "inside_z", "outside_x", "outside_y", "outside_z", "direction", "show_ghost"], analysis.BuildStarts, item => [item.TimeText, item.UnitIdText, item.ToolIndex?.ToString(CultureInfo.InvariantCulture) ?? "", item.DeviceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.DeviceKeyHash), item.InsidePosition?.XText ?? "", item.InsidePosition?.YText ?? "", item.InsidePosition?.ZText ?? "", item.OutsidePosition?.XText ?? "", item.OutsidePosition?.YText ?? "", item.OutsidePosition?.ZText ?? "", item.Direction ?? "", item.ShowGhost?.ToString() ?? ""]);
        WriteCsv(Path.Combine(outputDir, "build_cancels.csv"), ["time", "unit_id"], analysis.BuildCancels, static item => [item.TimeText, item.UnitIdText]);
        WriteCsv(Path.Combine(outputDir, "devices_built.csv"), ["time", "unit_id", "device_key_hash", "device_name", "x", "y", "z"], analysis.DevicesBuilt, item => [item.TimeText, item.UnitIdText, item.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture), ResolveKeyName(analysis, item.DeviceKeyHash), item.Position.XText, item.Position.YText, item.Position.ZText]);
        WriteCsv(Path.Combine(outputDir, "build_placements.csv"), ["device_time", "build_start_time", "build_to_device_seconds", "builder_unit_id", "built_unit_id", "device_key_hash", "device_name", "cell_x", "cell_y", "cell_z", "x", "y", "z", "inside_x", "inside_y", "inside_z", "outside_x", "outside_y", "outside_z", "direction", "show_ghost", "block_time", "block_id", "block_name", "damage", "vdata", "vdata_low_byte", "vdata_high_byte", "slope_existing_corner_count", "slope_existing_corners", "slope_missing_corners", "ldata", "team_bits", "team", "ldata_flags", "footprint_updates"], buildPlacements, item => [item.DeviceBuilt.TimeText, item.BuildStart?.TimeText ?? "", item.BuildStart is null ? "" : (item.DeviceBuilt.Time - item.BuildStart.Time).ToString("0.000", CultureInfo.InvariantCulture), item.BuildStart?.UnitId.ToString(CultureInfo.InvariantCulture) ?? "", item.DeviceBuilt.UnitIdText, item.DeviceBuilt.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture), ResolveKeyName(analysis, item.DeviceBuilt.DeviceKeyHash), item.Cell.XText, item.Cell.YText, item.Cell.ZText, item.DeviceBuilt.Position.XText, item.DeviceBuilt.Position.YText, item.DeviceBuilt.Position.ZText, item.BuildStart?.InsidePosition?.XText ?? "", item.BuildStart?.InsidePosition?.YText ?? "", item.BuildStart?.InsidePosition?.ZText ?? "", item.BuildStart?.OutsidePosition?.XText ?? "", item.BuildStart?.OutsidePosition?.YText ?? "", item.BuildStart?.OutsidePosition?.ZText ?? "", item.BuildStart?.Direction ?? "", item.BuildStart?.ShowGhost?.ToString() ?? "", item.BlockUpdateTime?.ToString("0.000", CultureInfo.InvariantCulture) ?? "", item.BlockUpdate?.Id?.ToString(CultureInfo.InvariantCulture) ?? "", item.BlockUpdate?.Id is ushort id ? ResolveBlockName(analysis, id) : "", item.BlockUpdate?.Damage?.ToString(CultureInfo.InvariantCulture) ?? "", item.BlockUpdate?.Vdata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatVdataLowByte(item.BlockUpdate?.Vdata), FormatVdataHighByte(item.BlockUpdate?.Vdata), FormatSlopeExistingCornerCount(item.BlockUpdate?.Vdata), FormatSlopeExistingCorners(item.BlockUpdate?.Vdata), FormatSlopeMissingCorners(item.BlockUpdate?.Vdata), item.BlockUpdate?.Ldata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatTeamBits(item.BlockUpdate?.Ldata), FormatBlockTeam(item.BlockUpdate?.Ldata), FormatLdataFlags(item.BlockUpdate?.Ldata), item.FootprintUpdates.Count.ToString(CultureInfo.InvariantCulture)]);
        WriteCsv(Path.Combine(outputDir, "build_placement_footprints.csv"), ["device_time", "footprint_index", "block_time", "dt", "distance", "dx", "dy", "dz", "device_key_hash", "device_name", "cell_x", "cell_y", "cell_z", "block_x", "block_y", "block_z", "block_id", "block_name", "damage", "vdata", "vdata_low_byte", "vdata_high_byte", "slope_existing_corner_count", "slope_existing_corners", "slope_missing_corners", "ldata", "team_bits", "team", "ldata_flags"], buildPlacements.SelectMany(static placement => placement.FootprintUpdates.Select((update, index) => (placement, update, index))), item => [item.placement.DeviceBuilt.TimeText, item.index.ToString(CultureInfo.InvariantCulture), item.update.Time.ToString("0.000", CultureInfo.InvariantCulture), (item.update.Time - item.placement.DeviceBuilt.Time).ToString("0.000", CultureInfo.InvariantCulture), item.update.Distance.ToString(CultureInfo.InvariantCulture), item.update.Dx.ToString(CultureInfo.InvariantCulture), item.update.Dy.ToString(CultureInfo.InvariantCulture), item.update.Dz.ToString(CultureInfo.InvariantCulture), item.placement.DeviceBuilt.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture), ResolveKeyName(analysis, item.placement.DeviceBuilt.DeviceKeyHash), item.placement.Cell.XText, item.placement.Cell.YText, item.placement.Cell.ZText, item.update.Sample.Position.XText, item.update.Sample.Position.YText, item.update.Sample.Position.ZText, item.update.Sample.Id?.ToString(CultureInfo.InvariantCulture) ?? "", item.update.Sample.Id is ushort id ? ResolveBlockName(analysis, id) : "", item.update.Sample.Damage?.ToString(CultureInfo.InvariantCulture) ?? "", item.update.Sample.Vdata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatVdataLowByte(item.update.Sample.Vdata), FormatVdataHighByte(item.update.Sample.Vdata), FormatSlopeExistingCornerCount(item.update.Sample.Vdata), FormatSlopeExistingCorners(item.update.Sample.Vdata), FormatSlopeMissingCorners(item.update.Sample.Vdata), item.update.Sample.Ldata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatTeamBits(item.update.Sample.Ldata), FormatBlockTeam(item.update.Sample.Ldata), FormatLdataFlags(item.update.Sample.Ldata)]);
        WriteCsv(Path.Combine(outputDir, "block_mined.csv"), ["time", "unit_id", "block_key_hash", "block_name"], analysis.BlockMined, item => [item.TimeText, item.UnitIdText, item.BlockKeyHash.ToString("X8", CultureInfo.InvariantCulture), ResolveKeyName(analysis, item.BlockKeyHash)]);
        WriteCsv(Path.Combine(outputDir, "barrier_updates.csv"), ["time", "labels"], analysis.BarrierUpdates, static item => [item.TimeText, string.Join("|", item.Labels)]);
        WriteCsv(Path.Combine(outputDir, "reloads.csv"), ["time", "phase", "unit_id"], analysis.ReloadEvents, static item => [item.TimeText, item.Phase, item.UnitIdText]);
        WriteCsv(Path.Combine(outputDir, "channels.csv"), ["time", "phase", "unit_id", "tool_index", "hit_x", "hit_y", "hit_z", "target_block_x", "target_block_y", "target_block_z", "target_unit_id"], analysis.ChannelEvents, static item => [item.TimeText, item.Phase, item.UnitId.ToString(CultureInfo.InvariantCulture), item.ToolIndex?.ToString(CultureInfo.InvariantCulture) ?? "", item.HitPosition?.XText ?? "", item.HitPosition?.YText ?? "", item.HitPosition?.ZText ?? "", item.TargetBlock?.XText ?? "", item.TargetBlock?.YText ?? "", item.TargetBlock?.ZText ?? "", item.TargetUnitId?.ToString(CultureInfo.InvariantCulture) ?? ""]);
        WriteCsv(Path.Combine(outputDir, "dash_charges.csv"), ["time", "phase", "unit_id", "tool_index"], analysis.DashChargeEvents, static item => [item.TimeText, item.Phase, item.UnitId.ToString(CultureInfo.InvariantCulture), item.ToolIndex.ToString(CultureInfo.InvariantCulture)]);
        WriteCsv(Path.Combine(outputDir, "pickup_taken.csv"), ["time", "player_id", "pickup_key_hash", "pickup_name"], analysis.PickupTakenEvents, item => [item.TimeText, item.PlayerId.ToString(CultureInfo.InvariantCulture), item.PickupKeyHash.ToString("X8", CultureInfo.InvariantCulture), ResolveKeyName(analysis, item.PickupKeyHash)]);
        WriteCsv(Path.Combine(outputDir, "recalls.csv"), ["time", "phase", "unit_id", "duration", "end_time"], analysis.RecallEvents, static item => [item.TimeText, item.Phase, item.UnitId.ToString(CultureInfo.InvariantCulture), item.Duration?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", item.EndTime?.ToString(CultureInfo.InvariantCulture) ?? ""]);
        WriteCsv(Path.Combine(outputDir, "portal_teleports.csv"), ["time", "unit_id", "portal_from_id", "portal_to_id"], analysis.PortalTeleports, static item => [item.TimeText, item.UnitId.ToString(CultureInfo.InvariantCulture), item.PortalFromId.ToString(CultureInfo.InvariantCulture), item.PortalToId.ToString(CultureInfo.InvariantCulture)]);
        WriteCsv(Path.Combine(outputDir, "kick_players.csv"), ["time", "player_id", "reason"], analysis.KickPlayerEvents, static item => [item.TimeText, item.PlayerId.ToString(CultureInfo.InvariantCulture), item.Reason]);
        WriteCsv(Path.Combine(outputDir, "rpc_results.csv"), ["time", "name", "rpc_id", "status", "value"], analysis.RpcResults, static item => [item.TimeText, item.Name, item.RpcId.ToString(CultureInfo.InvariantCulture), item.Status, item.Value]);
        WriteCsv(Path.Combine(outputDir, "surrender_events.csv"), ["time", "phase", "team", "deadline", "accepted", "detail"], analysis.SurrenderEvents, static item => [item.TimeText, item.Phase, item.Team ?? "", item.Deadline?.ToString(CultureInfo.InvariantCulture) ?? "", item.Accepted?.ToString() ?? "", item.Detail ?? ""]);
        WriteCsv(Path.Combine(outputDir, "surrender_progress.csv"), ["time", "votes"], analysis.SurrenderProgress, static item => [item.TimeText, string.Join("|", item.Votes.Select(static vote => $"{vote.PlayerId}:{vote.Voted?.ToString() ?? "null"}"))]);
        WriteCsv(Path.Combine(outputDir, "end_match_players.csv"), ["player_id", "nickname", "squad_id", "backfiller", "noob", "total", "earned", "built", "destroyed", "objective", "block_assist", "kills", "deaths", "assists", "positive_medal_hash", "positive_medal_name", "negative_medal_hash", "negative_medal_name"], analysis.EndMatchResult?.Players ?? [], item => [item.PlayerId?.ToString(CultureInfo.InvariantCulture) ?? "", ResolvePlayerNickname(analysis, item.PlayerId), item.SquadId?.ToString(CultureInfo.InvariantCulture) ?? "", item.Backfiller?.ToString() ?? "", item.Noob?.ToString() ?? "", item.Stats?.Total?.ToString(CultureInfo.InvariantCulture) ?? "", FormatStat(item.Stats, "Earned"), FormatStat(item.Stats, "Built"), FormatStat(item.Stats, "Destroyed"), FormatStat(item.Stats, "Objective"), FormatStat(item.Stats, "BlockAssist"), FormatStat(item.Stats, "Kill"), FormatStat(item.Stats, "Death"), FormatStat(item.Stats, "Assist"), item.MedalPositiveKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.MedalPositiveKeyHash), item.MedalNegativeKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", ResolveKeyName(analysis, item.MedalNegativeKeyHash)]);
        WriteCsv(Path.Combine(outputDir, "zone_updates.csv"), ["time", "flags", "phase", "phase_start", "phase_end", "players", "objectives", "spawn_points", "respawns", "resource_cap", "supply", "team1_stats", "team2_stats"], analysis.ZoneUpdates, item => [item.TimeText, item.Flags, item.Phase?.PhaseType ?? "", item.Phase?.StartTime?.ToString(CultureInfo.InvariantCulture) ?? "", item.Phase?.EndTime?.ToString(CultureInfo.InvariantCulture) ?? "", item.PlayerInfo.Count.ToString(CultureInfo.InvariantCulture), item.Objectives.Count.ToString(CultureInfo.InvariantCulture), item.SpawnPoints.Count.ToString(CultureInfo.InvariantCulture), item.RespawnInfo.Count.ToString(CultureInfo.InvariantCulture), item.ResourceCap?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", FormatSupply(analysis, item.SupplyInfo), FormatTeamStats(item.Stats?.Team1Stats), FormatTeamStats(item.Stats?.Team2Stats)]);
        WriteCsv(Path.Combine(outputDir, "match_player_stats.csv"), ["time", "player_id", "team", "kills", "deaths", "assists"], analysis.ZoneUpdates.SelectMany(static update => update.Stats?.PlayerStats.Select(player => (update, player)) ?? []), static item => [item.update.TimeText, item.player.PlayerId.ToString(CultureInfo.InvariantCulture), item.player.Team ?? "", item.player.Kills?.ToString(CultureInfo.InvariantCulture) ?? "", item.player.Deaths?.ToString(CultureInfo.InvariantCulture) ?? "", item.player.Assists?.ToString(CultureInfo.InvariantCulture) ?? ""]);
        WriteCsv(Path.Combine(outputDir, "respawns.csv"), ["time", "player_id", "respawn_time"], analysis.ZoneUpdates.SelectMany(static update => update.RespawnInfo.Select(respawn => (update, respawn))), static item => [item.update.TimeText, item.respawn.PlayerId.ToString(CultureInfo.InvariantCulture), item.respawn.RespawnTime.ToString(CultureInfo.InvariantCulture)]);
        WriteCsv(Path.Combine(outputDir, "players.csv"), ["time", "player_id", "nickname", "steam_id", "squad_id", "looking_for_friends"], analysis.ZoneUpdates.SelectMany(static update => update.PlayerInfo.Select(player => (update, player))), static item => [item.update.TimeText, item.player.PlayerId.ToString(CultureInfo.InvariantCulture), item.player.Nickname ?? "", item.player.SteamId?.ToString(CultureInfo.InvariantCulture) ?? "", item.player.SquadId?.ToString(CultureInfo.InvariantCulture) ?? "", item.player.LookingForFriends?.ToString() ?? ""]);
        WriteCsv(Path.Combine(outputDir, "player_units.csv"), ["player_id", "nickname", "steam_id", "team", "unit_id", "unit_key_hash", "unit_name", "skin_key_hash", "skin_name", "gear_key_hashes", "gear_names", "controlled"], ReplayIdentityBuilder.BuildPlayerUnitIdentities(analysis), static item => [item.PlayerId.ToString(CultureInfo.InvariantCulture), item.Nickname ?? "", item.SteamId?.ToString(CultureInfo.InvariantCulture) ?? "", item.Team ?? "", item.UnitId?.ToString(CultureInfo.InvariantCulture) ?? "", item.UnitKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", item.UnitName ?? "", item.SkinKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "", item.SkinName ?? "", string.Join("|", item.GearKeyHashes.Select(static key => key.ToString("X8", CultureInfo.InvariantCulture))), string.Join("|", item.GearNames), item.Controlled?.ToString() ?? ""]);
        WriteCsv(Path.Combine(outputDir, "objectives.csv"), ["time", "team", "id", "counter", "required_counter"], analysis.ZoneUpdates.SelectMany(static update => update.Objectives.Select(objective => (update, objective))), static item => [item.update.TimeText, item.objective.Team ?? "", item.objective.Id?.ToString(CultureInfo.InvariantCulture) ?? "", item.objective.Counter?.ToString(CultureInfo.InvariantCulture) ?? "", item.objective.RequiredCounter?.ToString(CultureInfo.InvariantCulture) ?? ""]);
        WriteCsv(Path.Combine(outputDir, "block_updates.csv"), ["time", "count", "samples"], analysis.BlockUpdates, static item => [item.TimeText, item.Count.ToString(CultureInfo.InvariantCulture), string.Join(";", item.Samples.Select(static sample => $"{sample.Position.X},{sample.Position.Y},{sample.Position.Z}:{sample.Id}/{sample.Damage}/{sample.Vdata}/{sample.Ldata}"))]);
        WriteCsv(Path.Combine(outputDir, "block_update_items.csv"), ["time", "index", "x", "y", "z", "id", "name", "damage", "vdata", "vdata_low_byte", "vdata_high_byte", "slope_existing_corner_count", "slope_existing_corners", "slope_missing_corners", "ldata", "team_bits", "team", "ldata_flags"], analysis.BlockUpdates.SelectMany(static update => update.Updates.Select((sample, index) => (update, sample, index))), item => [item.update.TimeText, item.index.ToString(CultureInfo.InvariantCulture), item.sample.Position.XText, item.sample.Position.YText, item.sample.Position.ZText, item.sample.Id?.ToString(CultureInfo.InvariantCulture) ?? "", item.sample.Id is ushort id ? ResolveBlockName(analysis, id) : "", item.sample.Damage?.ToString(CultureInfo.InvariantCulture) ?? "", item.sample.Vdata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatVdataLowByte(item.sample.Vdata), FormatVdataHighByte(item.sample.Vdata), FormatSlopeExistingCornerCount(item.sample.Vdata), FormatSlopeExistingCorners(item.sample.Vdata), FormatSlopeMissingCorners(item.sample.Vdata), item.sample.Ldata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatTeamBits(item.sample.Ldata), FormatBlockTeam(item.sample.Ldata), FormatLdataFlags(item.sample.Ldata)]);
        WriteCsv(Path.Combine(outputDir, "map_state_timeline.csv"), ["sequence", "time", "source", "placement_device_time", "device_key_hash", "device_name", "builder_unit_id", "built_unit_id", "direction", "x", "y", "z", "id", "name", "damage", "vdata", "vdata_low_byte", "vdata_high_byte", "slope_existing_corner_count", "slope_existing_corners", "slope_missing_corners", "ldata", "team_bits", "team", "ldata_flags"], mapTimeline, item => [item.Sequence.ToString(CultureInfo.InvariantCulture), item.TimeText, item.Source, item.Placement?.DeviceBuilt.TimeText ?? "", item.Placement?.DeviceBuilt.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture) ?? "", item.Placement is null ? "" : ResolveKeyName(analysis, item.Placement.DeviceBuilt.DeviceKeyHash), item.Placement?.BuildStart?.UnitId.ToString(CultureInfo.InvariantCulture) ?? "", item.Placement?.DeviceBuilt.UnitIdText ?? "", item.Placement?.BuildStart?.Direction ?? "", item.Update.Position.XText, item.Update.Position.YText, item.Update.Position.ZText, item.Update.Id?.ToString(CultureInfo.InvariantCulture) ?? "", item.Update.Id is ushort id ? ResolveBlockName(analysis, id) : "", item.Update.Damage?.ToString(CultureInfo.InvariantCulture) ?? "", item.Update.Vdata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatVdataLowByte(item.Update.Vdata), FormatVdataHighByte(item.Update.Vdata), FormatSlopeExistingCornerCount(item.Update.Vdata), FormatSlopeExistingCorners(item.Update.Vdata), FormatSlopeMissingCorners(item.Update.Vdata), item.Update.Ldata?.ToString(CultureInfo.InvariantCulture) ?? "", FormatTeamBits(item.Update.Ldata), FormatBlockTeam(item.Update.Ldata), FormatLdataFlags(item.Update.Ldata)]);
        WriteMapStateVerification(Path.Combine(outputDir, "map_state_verification.txt"), analysis, mapVerification);
        WriteCsv(Path.Combine(outputDir, "map_state_final_counts.csv"), ["id", "name", "count"], mapVerification.FinalBlockCounts, item => [item.Id.ToString(CultureInfo.InvariantCulture), ResolveBlockName(analysis, item.Id), item.Count.ToString(CultureInfo.InvariantCulture)]);
        WriteCsv(Path.Combine(outputDir, "map_state_changed_cells.csv"), ["x", "y", "z", "updates", "initial_id", "initial_name", "final_id", "final_name", "final_damage", "final_vdata", "final_ldata", "final_team"], mapVerification.ChangedCells, item => [item.Position.XText, item.Position.YText, item.Position.ZText, item.UpdateCount.ToString(CultureInfo.InvariantCulture), item.Initial.Id.ToString(CultureInfo.InvariantCulture), ResolveBlockName(analysis, item.Initial.Id), item.Final.Id.ToString(CultureInfo.InvariantCulture), ResolveBlockName(analysis, item.Final.Id), item.Final.Damage.ToString(CultureInfo.InvariantCulture), item.Final.Vdata.ToString(CultureInfo.InvariantCulture), item.Final.Ldata.ToString(CultureInfo.InvariantCulture), FormatBlockTeam(item.Final.Ldata)]);
        WriteNormalizedJson(Path.Combine(outputDir, "replay.normalized.json"), analysis);
        WriteViewer(Path.Combine(outputDir, "viewer.html"), analysis);
    }

    private static void WriteSummary(string outputDir, ReplayAnalysis analysis, MapStateVerificationData? mapVerification = null)
    {
        var validation = ReplayValidation.Evaluate(analysis);
        var buildPlacements = BuildBuildPlacements(analysis);
        var mapTimeline = BuildMapStateTimeline(analysis, buildPlacements);
        mapVerification ??= VerifyMapStateTimeline(analysis, mapTimeline);
        var lines = new List<string>
        {
            $"Source: {analysis.SourcePath}",
            $"Usable for replay: {validation.UsableForReplay}",
            $"Schema: {analysis.Schema}",
            $"Session UTC: {analysis.SessionUtc}",
            $"Packets: {analysis.Packets.Count}",
            $"Duration: {analysis.DurationSeconds:0.000}s",
            $"Payload cap: {analysis.MaxPayloadBytes}",
            $"InitZone full: {analysis.InitZoneFullyCaptured} ({analysis.InitZonePayloadBytes}/{analysis.InitZoneRemainingBytes})",
            $"InitZone map key hash: {analysis.MapKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "unknown"}",
            $"InitZone map key name: {ResolveKeyName(analysis, analysis.MapKeyHash, "unknown")}",
            $"InitZone unread bytes: {analysis.InitZoneUnreadBytes}",
            $"Map size: {FormatVector(analysis.InitZone?.Map?.Size)}",
            $"Map version/schema: {analysis.InitZone?.Map?.Version?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}/{analysis.InitZone?.Map?.Schema?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}",
            $"Map match type: {analysis.InitZone?.Map?.Match ?? "unknown"}",
            $"Map blocks data bytes: {analysis.InitZone?.Map?.BlocksData?.Length ?? 0}",
            $"Map colors data bytes: {analysis.InitZone?.Map?.ColorsData?.Length ?? 0}",
            $"Init map data bytes: {analysis.InitZone?.MapData?.Length ?? 0}",
            $"Init color data bytes: {analysis.InitZone?.ColorData?.Length ?? 0}",
            $"Decoded map block count: {analysis.DecodedMap?.BlockCount ?? 0}",
            $"Decoded non-empty map blocks: {analysis.DecodedMap?.NonEmptyBlocks.Count ?? 0}",
            $"Decoded map color bytes: {analysis.DecodedMap?.ColorBytes ?? 0}",
            $"Decoded block IDs: {analysis.DecodedMap?.BlockCounts.Count ?? 0}",
            $"Resolved block names: {analysis.BlockNames.Count}",
            $"Map spawn points: {analysis.InitZone?.Map?.SpawnPoints.Count ?? 0}",
            $"Map static units: {analysis.InitZone?.Map?.Units.Count ?? 0}",
            $"Map cameras: {analysis.InitZone?.Map?.Cameras.Count ?? 0}",
            $"Map triggers: {analysis.InitZone?.Map?.Triggers.Count ?? 0}",
            $"Init block updates: {analysis.InitZone?.Updates.Count ?? 0}",
            $"Can switch hero: {analysis.InitZone?.CanSwitchHero?.ToString() ?? "unknown"}",
            $"Custom game: {analysis.InitZone?.IsCustomGame?.ToString() ?? "unknown"}",
            $"Resolved key names: {analysis.KeyNames.Count}",
            $"Units created: {analysis.UnitCreates.Count}",
            $"Player/unit links: {ReplayIdentityBuilder.BuildPlayerUnitIdentities(analysis).Count}",
            $"Unit skin keys: {analysis.UnitCreates.Count(static item => item.SkinKeyHash is not null)}",
            $"Unit gear keys: {analysis.UnitCreates.Sum(static item => item.GearKeyHashes.Count)}",
            $"Units dropped: {analysis.UnitDrops.Count}",
            $"Moves: {analysis.UnitMoves.Count}",
            $"Unit maneuvers: {analysis.UnitManeuvers.Count}",
            $"Unit updates: {analysis.UnitUpdates.Count}",
            $"Unit update ammo entries: {analysis.UnitUpdates.Sum(static item => item.Ammo.Sum(static ammo => ammo.Value.Count))}",
            $"Unit update gear changes: {analysis.UnitUpdates.Count(static item => item.CurrentGearKeyHash is not null)}",
            $"Unit update ability states: {analysis.UnitUpdates.Count(static item => item.AbilityKeyHash is not null || item.AbilityCharges is not null)}",
            $"Unit update buff entries: {analysis.UnitUpdates.Sum(static item => item.Buffs.Count)}",
            $"Unit update effect entries: {analysis.UnitUpdates.Sum(static item => item.Effects.Count)}",
            $"Unit update device entries: {analysis.UnitUpdates.Sum(static item => item.Devices.Count)}",
            $"Damage events: {analysis.Damages.Count}",
            $"Kills: {analysis.Kills.Count}",
            $"Impacts: {analysis.Impacts.Count}",
            $"Zone events: {analysis.ZoneEvents.Count}",
            $"Projectiles created: {analysis.ProjectileCreates.Count}",
            $"Projectile moves: {analysis.ProjectileMoves.Count}",
            $"Projectiles dropped: {analysis.ProjectileDrops.Count}",
            $"Casts: {analysis.Casts.Count}",
            $"Ability casts: {analysis.AbilityCasts.Count}",
            $"Build starts: {analysis.BuildStarts.Count}",
            $"Build cancels: {analysis.BuildCancels.Count}",
            $"Devices built: {analysis.DevicesBuilt.Count}",
            $"Build placements: {buildPlacements.Count}",
            $"Build placements with build start: {buildPlacements.Count(static item => item.BuildStart is not null)}",
            $"Build placements with block update: {buildPlacements.Count(static item => item.BlockUpdate is not null)}",
            $"Build placement footprint updates: {buildPlacements.Sum(static item => item.FootprintUpdates.Count)}",
            $"Build placements with footprint updates: {buildPlacements.Count(static item => item.FootprintUpdates.Count > 0)}",
            $"Zone updates: {analysis.ZoneUpdates.Count}",
            $"Player info records: {analysis.ZoneUpdates.Sum(static item => item.PlayerInfo.Count)}",
            $"Objective records: {analysis.ZoneUpdates.Sum(static item => item.Objectives.Count)}",
            $"Block update packets: {analysis.BlockUpdates.Count}",
            $"Block updates total: {analysis.BlockUpdates.Sum(static item => item.Count)}",
            $"Map state timeline updates: {mapTimeline.Count}",
            $"Map state timeline placement updates: {mapTimeline.Count(static item => item.Placement is not null)}",
            $"Map state final non-empty blocks: {mapVerification.FinalNonEmptyBlockCount}",
            $"Map state changed cells: {mapVerification.ChangedCells.Count}",
            $"Map state repeated cells: {mapVerification.RepeatedCellCount}",
            $"Map state duplicate no-op updates: {mapVerification.DuplicateNoOpUpdates}",
            $"Map state out-of-order updates: {mapVerification.OutOfOrderUpdates}",
            $"Blocks mined: {analysis.BlockMined.Count}",
            $"Barrier updates: {analysis.BarrierUpdates.Count}",
            $"Reload events: {analysis.ReloadEvents.Count}",
            $"Channel events: {analysis.ChannelEvents.Count}",
            $"Dash charge events: {analysis.DashChargeEvents.Count}",
            $"Pickup taken events: {analysis.PickupTakenEvents.Count}",
            $"Recall events: {analysis.RecallEvents.Count}",
            $"Portal teleport events: {analysis.PortalTeleports.Count}",
            $"Kick player events: {analysis.KickPlayerEvents.Count}",
            $"RPC results: {analysis.RpcResults.Count}",
            $"Surrender events: {analysis.SurrenderEvents.Count}",
            $"Surrender progress packets: {analysis.SurrenderProgress.Count}",
            $"End match winner: {analysis.EndMatch?.WinnerTeam ?? "unknown"}",
            $"End result payload bytes: {analysis.EndMatchResultPayloadBytes}",
            $"End result decoded: {analysis.EndMatchResult is not null}",
            $"End result match seconds: {analysis.EndMatchResult?.MatchSeconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unknown"}",
            $"End result players: {analysis.EndMatchResult?.Players.Count ?? 0}",
            $"End result reward XP: {analysis.EndMatchResult?.RewardXp?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unknown"}",
            $"Decode errors: {analysis.DecodeErrors.Count}"
        };

        if (analysis.DecodeErrors.Count > 0)
        {
            lines.Add("");
            lines.Add("Decode errors:");
            lines.AddRange(analysis.DecodeErrors.Select(static error => $"{error.Time:0.000} {error.Event}: {error.Message}"));
        }

        File.WriteAllLines(Path.Combine(outputDir, "summary.txt"), lines, Encoding.UTF8);
    }

    private static void WriteValidation(string outputDir, ReplayAnalysis analysis)
    {
        var validation = ReplayValidation.Evaluate(analysis);
        var lines = new List<string>
        {
            $"Usable for replay: {validation.UsableForReplay}",
            $"Quality: {validation.Quality}",
            $"Required checks passed: {validation.RequiredPassed}/{validation.RequiredTotal}",
            $"Warnings: {validation.Warnings.Count}",
            "",
            "Required checks:"
        };

        lines.AddRange(validation.RequiredChecks.Select(static check => $"{FormatCheckStatus(check.Passed)} {check.Name}: {check.Detail}"));

        if (validation.Warnings.Count > 0)
        {
            lines.Add("");
            lines.Add("Warnings:");
            lines.AddRange(validation.Warnings.Select(static warning => $"- {warning.Name}: {warning.Detail}"));
        }

        lines.Add("");
        lines.Add("Coverage:");
        lines.AddRange(validation.Coverage.Select(static item => $"{item.Name}: {item.Value}"));

        File.WriteAllLines(Path.Combine(outputDir, "validation.txt"), lines, Encoding.UTF8);
    }

    private static string FormatCheckStatus(bool passed) => passed ? "PASS" : "FAIL";

    private static void WriteMapBinaryAssets(string outputDir, ReplayAnalysis analysis)
    {
        WriteBytesIfPresent(Path.Combine(outputDir, "init_zone_payload.bin"), analysis.InitZonePayload);
        WriteBytesIfPresent(Path.Combine(outputDir, "init_map_data.bin"), analysis.InitZone?.MapData);
        WriteBytesIfPresent(Path.Combine(outputDir, "init_color_data.bin"), analysis.InitZone?.ColorData);
        WriteBytesIfPresent(Path.Combine(outputDir, "map_blocks_data.bin"), analysis.InitZone?.Map?.BlocksData);
        WriteBytesIfPresent(Path.Combine(outputDir, "map_colors_data.bin"), analysis.InitZone?.Map?.ColorsData);
    }

    private static void WriteBytesIfPresent(string path, byte[]? value)
    {
        if (value is { Length: > 0 })
        {
            File.WriteAllBytes(path, value);
        }
    }

    private static void WriteCsv<T>(string path, string[] header, IEnumerable<T> items, Func<T, string[]> rowFactory)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine(string.Join(",", header.Select(EscapeCsv)));
        foreach (var item in items)
        {
            writer.WriteLine(string.Join(",", rowFactory(item).Select(EscapeCsv)));
        }
    }

    private static void WriteViewer(string path, ReplayAnalysis analysis)
    {
        var units = analysis.UnitCreates
            .Select(unit => new
            {
                id = unit.UnitId,
                team = string.IsNullOrWhiteSpace(unit.Team) ? "Unknown" : unit.Team,
                key = unit.KeyHashText,
                name = ResolveKeyName(analysis, unit.KeyHash, unit.KeyHashText),
                playerId = unit.PlayerId,
                ownerId = unit.OwnerId,
                skin = ResolveKeyName(analysis, unit.SkinKeyHash),
                gears = unit.GearKeyHashes.Select(key => ResolveKeyName(analysis, key, key.ToString("X8", CultureInfo.InvariantCulture))).ToArray(),
                x = unit.Transform?.Position?.X,
                y = unit.Transform?.Position?.Y,
                z = unit.Transform?.Position?.Z
            })
            .ToArray();

        var moves = analysis.UnitMoves
            .Where(static move => move.Transform.Position is not null)
            .Select(move => new
            {
                t = Math.Round(move.Time, 3),
                id = move.UnitId,
                x = move.Transform.Position!.Value.X,
                y = move.Transform.Position.Value.Y,
                z = move.Transform.Position.Value.Z
            })
            .ToArray();

        var damage = analysis.Damages
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                source = item.SourceUnitId,
                target = item.TargetUnitId,
                damage = item.Damage,
                crit = item.Crit,
                impact = ResolveKeyName(analysis, item.ImpactKeyHash)
            })
            .ToArray();

        var impacts = analysis.Impacts
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                casterUnitId = item.CasterUnitId,
                casterPlayerId = item.CasterPlayerId,
                impact = ResolveKeyName(analysis, item.ImpactKeyHash, item.ImpactKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? ""),
                source = ResolveKeyName(analysis, item.SourceKeyHash, item.SourceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? ""),
                hitUnits = item.HitUnits,
                crit = item.Crit,
                insidePoint = item.InsidePoint is null ? null : new
                {
                    x = item.InsidePoint.Value.X,
                    y = item.InsidePoint.Value.Y,
                    z = item.InsidePoint.Value.Z
                },
                shotPosition = item.ShotPosition is null ? null : new
                {
                    x = item.ShotPosition.Value.X,
                    y = item.ShotPosition.Value.Y,
                    z = item.ShotPosition.Value.Z
                }
            })
            .ToArray();

        var zoneEvents = analysis.ZoneEvents
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                type = item.Name,
                unitId = item.UnitId,
                turretId = item.TurretId,
                toolIndex = item.ToolIndex,
                active = item.Active,
                position = item.Position
            })
            .ToArray();

        var projectiles = analysis.ProjectileCreates
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                id = item.ProjectileId,
                key = item.Info.ProjectileKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                name = ResolveKeyName(analysis, item.Info.ProjectileKeyHash, item.Info.ProjectileKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? ""),
                ownerUnitId = item.Info.OwnerUnitId,
                ownerTeam = item.Info.OwnerTeam,
                speed = item.Info.Speed,
                x = item.Info.Transform?.Position?.X,
                y = item.Info.Transform?.Position?.Y,
                z = item.Info.Transform?.Position?.Z
            })
            .ToArray();

        var projectileMoves = analysis.ProjectileMoves
            .Where(static item => item.Transform.Position is not null)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                id = item.ProjectileId,
                serverTime = item.ServerTime,
                x = item.Transform.Position!.Value.X,
                y = item.Transform.Position.Value.Y,
                z = item.Transform.Position.Value.Z
            })
            .ToArray();

        var projectileDrops = analysis.ProjectileDrops
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                id = item.ProjectileId
            })
            .ToArray();

        var abilityCasts = analysis.AbilityCasts
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                ability = ResolveKeyName(analysis, item.AbilityKeyHash, item.AbilityKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? ""),
                shotPosition = item.ShotPosition is null ? null : new { x = item.ShotPosition.Value.X, y = item.ShotPosition.Value.Y, z = item.ShotPosition.Value.Z },
                shots = item.Shots.Select(static shot => new { target = shot.TargetPosition is null ? null : new { x = shot.TargetPosition.Value.X, y = shot.TargetPosition.Value.Y, z = shot.TargetPosition.Value.Z }, shotId = shot.ShotId })
            })
            .ToArray();

        var builds = analysis.BuildStarts
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                device = ResolveKeyName(analysis, item.DeviceKeyHash, item.DeviceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? ""),
                inside = item.InsidePosition is null ? null : new
                {
                    x = item.InsidePosition.Value.X,
                    y = item.InsidePosition.Value.Y,
                    z = item.InsidePosition.Value.Z
                },
                outside = item.OutsidePosition is null ? null : new
                {
                    x = item.OutsidePosition.Value.X,
                    y = item.OutsidePosition.Value.Y,
                    z = item.OutsidePosition.Value.Z
                },
                direction = item.Direction,
                showGhost = item.ShowGhost
            })
            .ToArray();

        var devicesBuilt = analysis.DevicesBuilt
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                device = ResolveKeyName(analysis, item.DeviceKeyHash, item.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture)),
                x = item.Position.X,
                y = item.Position.Y,
                z = item.Position.Z
            })
            .ToArray();

        var updates = analysis.UnitUpdates
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                id = item.UnitId,
                health = item.Health,
                forcefield = item.Forcefield,
                shield = item.Shield,
                resource = item.Resource,
                gear = ResolveKeyName(analysis, item.CurrentGearKeyHash),
                ability = ResolveKeyName(analysis, item.AbilityKeyHash),
                charges = item.AbilityCharges,
                moving = item.MovementActive
            })
            .ToArray();

        var events = BuildTimelineEvents(analysis)
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                kind = item.Kind,
                text = item.Text
            })
            .ToArray();

        var packetCounts = analysis.Packets
            .GroupBy(static packet => packet.Event)
            .OrderByDescending(static group => group.Count())
            .Select(static group => new
            {
                eventName = group.Key.Replace("Recv_", "", StringComparison.Ordinal),
                count = group.Count()
            })
            .ToArray();

        var data = JsonSerializer.Serialize(new
        {
            source = Path.GetFileName(analysis.SourcePath),
            duration = analysis.DurationSeconds,
            start = analysis.StartTime,
            end = analysis.EndTime,
            mapKey = analysis.MapKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
            mapName = ResolveKeyName(analysis, analysis.MapKeyHash),
            winner = analysis.EndMatch?.WinnerTeam,
            units,
            moves,
            updates,
            damage,
            impacts,
            zoneEvents,
            projectiles,
            projectileMoves,
            projectileDrops,
            abilityCasts,
            builds,
            devicesBuilt,
            events,
            packetCounts
        });

        var html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>BNL Replay Viewer</title>
<style>
*{box-sizing:border-box}
body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:#111;color:#eee;overflow:hidden}
header{height:58px;display:flex;gap:14px;align-items:center;padding:10px 14px;background:#1b1b1b;border-bottom:1px solid #333}
main{display:grid;grid-template-columns:minmax(0,1fr) 360px;height:calc(100vh - 58px)}
canvas{display:block;width:100%;height:100%;background:#0b1012}
aside{border-left:1px solid #333;background:#151515;overflow:auto}
section{padding:12px 14px;border-bottom:1px solid #2a2a2a}
h2{font-size:13px;margin:0 0 9px 0;color:#ddd;font-weight:600}
button{background:#2e6f95;color:white;border:0;border-radius:4px;padding:8px 12px;min-width:70px}
select{background:#222;color:#eee;border:1px solid #444;border-radius:4px;padding:7px}
input[type=range]{width:min(440px,34vw)}
.stat{color:#bbb;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.metric-grid{display:grid;grid-template-columns:1fr 1fr;gap:8px}
.metric{background:#202020;border:1px solid #303030;border-radius:6px;padding:8px}
.metric strong{display:block;font-size:18px}
.metric span{font-size:12px;color:#aaa}
.row{display:grid;grid-template-columns:72px 1fr 64px;gap:8px;align-items:center;font-size:12px;padding:5px 0;border-bottom:1px solid #242424}
.row:last-child{border-bottom:0}
.pill{display:inline-block;min-width:56px;text-align:center;border-radius:999px;padding:2px 7px;font-size:11px;color:#111}
.team-Team1{background:#4aa3ff}.team-Team2{background:#ff5a5a}.team-Neutral{background:#d8d8d8}.team-Unknown{background:#ffd166}
.events{max-height:260px;overflow:auto}
.event{display:grid;grid-template-columns:56px 1fr 52px;gap:7px;font-size:12px;padding:5px 0;border-bottom:1px solid #242424}
.bar{height:6px;background:#303030;border-radius:4px;overflow:hidden}.bar>span{display:block;height:100%;background:#7fcf7f}
</style>
</head>
<body>
<header>
<button id="play">Play</button>
<input id="scrub" type="range" min="0" max="1000" value="0">
<strong id="time">0.000s</strong>
<select id="speed"><option value="0.25">0.25x</option><option value="0.5">0.5x</option><option value="1" selected>1x</option><option value="2">2x</option><option value="4">4x</option></select>
<span class="stat" id="meta"></span>
</header>
<main>
<canvas id="view"></canvas>
<aside>
<section><h2>Summary</h2><div class="metric-grid" id="metrics"></div></section>
<section><h2>Units</h2><div id="units"></div></section>
<section><h2>Events</h2><div class="events" id="events"></div></section>
<section><h2>Packet Counts</h2><div id="packets"></div></section>
</aside>
</main>
<script>
const replay = __DATA__;
const canvas = document.getElementById('view');
const ctx = canvas.getContext('2d');
const scrub = document.getElementById('scrub');
const play = document.getElementById('play');
const timeLabel = document.getElementById('time');
const speed = document.getElementById('speed');
document.getElementById('meta').textContent = `${replay.source} | map ${replay.mapName || replay.mapKey || 'unknown'} | winner ${replay.winner ?? 'unknown'} | ${replay.units.length} units | ${replay.moves.length} moves`;
let current = replay.start;
let playing = false;
let lastFrame = performance.now();
const colors = { Team1:'#4aa3ff', Team2:'#ff5a5a', Neutral:'#d8d8d8', Unknown:'#ffd166' };
const impactPositions = replay.impacts.flatMap(i=>[i.insidePoint,i.shotPosition].filter(p=>p&&p.x!=null));
const projectilePositions = replay.projectiles.filter(p=>p.x!=null).concat(replay.projectileMoves);
const abilityPositions = replay.abilityCasts.flatMap(a=>[a.shotPosition,...a.shots.map(s=>s.target)].filter(p=>p&&p.x!=null));
const buildPositions = replay.builds.flatMap(b=>[b.inside,b.outside].filter(p=>p&&p.x!=null)).concat(replay.devicesBuilt.filter(d=>d.x!=null));
const allPositions = replay.moves.concat(replay.units.filter(u=>u.x!=null), impactPositions, projectilePositions, abilityPositions, buildPositions);
const bounds = allPositions.reduce((b,m)=>({minX:Math.min(b.minX,m.x),maxX:Math.max(b.maxX,m.x),minZ:Math.min(b.minZ,m.z),maxZ:Math.max(b.maxZ,m.z)}),{minX:Infinity,maxX:-Infinity,minZ:Infinity,maxZ:-Infinity});
const createById = new Map(replay.units.map(u=>[u.id,u]));
const updatesByUnit = new Map();
for (const update of replay.updates) { if(!updatesByUnit.has(update.id)) updatesByUnit.set(update.id, []); updatesByUnit.get(update.id).push(update); }
const movesByUnit = new Map();
for (const move of replay.moves) { if(!movesByUnit.has(move.id)) movesByUnit.set(move.id, []); movesByUnit.get(move.id).push(move); }
document.getElementById('metrics').innerHTML=[
  ['Duration', `${replay.duration.toFixed(2)}s`],
  ['Units', replay.units.length],
  ['Moves', replay.moves.length],
  ['Impacts', replay.impacts.length],
  ['Projectiles', replay.projectiles.length],
  ['Abilities', replay.abilityCasts.length],
  ['Events', replay.zoneEvents.length]
].map(([k,v])=>`<div class="metric"><strong>${v}</strong><span>${k}</span></div>`).join('');
document.getElementById('packets').innerHTML=replay.packetCounts.slice(0,12).map(p=>`<div class="row"><span>${p.eventName}</span><div class="bar"><span style="width:${Math.min(100,p.count/replay.packetCounts[0].count*100)}%"></span></div><span>${p.count}</span></div>`).join('');
function resize(){canvas.width=canvas.clientWidth*devicePixelRatio;canvas.height=canvas.clientHeight*devicePixelRatio}
function pos(m){const pad=46*devicePixelRatio; const w=canvas.width-pad*2; const h=canvas.height-pad*2; const x=(m.x-bounds.minX)/Math.max(1,bounds.maxX-bounds.minX); const z=(m.z-bounds.minZ)/Math.max(1,bounds.maxZ-bounds.minZ); return {x:pad+x*w,y:pad+z*h}}
function latestBefore(list,t){let found=null; for(const item of list||[]){ if(item.t>t) break; found=item } return found}
function stateAt(t){const state=new Map(); for(const u of replay.units){if(u.x!=null) state.set(u.id,{id:u.id,x:u.x,y:u.y,z:u.z,team:u.team,key:u.key,name:u.name,health:null,shield:null})} for(const m of replay.moves){if(m.t>t) break; const created=createById.get(m.id); const prev=state.get(m.id)||{id:m.id,team:created?.team||'Unknown',name:created?.name,key:created?.key}; state.set(m.id,{...prev,x:m.x,y:m.y,z:m.z})} for(const [id,list] of updatesByUnit){const u=latestBefore(list,t); if(!u) continue; const created=createById.get(id); const prev=state.get(id)||{id,team:created?.team||'Unknown',name:created?.name,key:created?.key}; state.set(id,{...prev,health:u.health??prev.health,shield:u.shield??prev.shield})} return state}
function drawTrail(unitId){const list=movesByUnit.get(unitId)||[]; ctx.strokeStyle='rgba(255,255,255,.18)'; ctx.lineWidth=1.5*devicePixelRatio; ctx.beginPath(); let started=false; for(const m of list){ if(m.t>current) break; const p=pos(m); if(!started){ctx.moveTo(p.x,p.y); started=true}else ctx.lineTo(p.x,p.y)} if(started) ctx.stroke()}
function projectileStateAt(t){const state=new Map(); for(const p of replay.projectiles){if(p.t<=t&&p.x!=null) state.set(p.id,{...p})} for(const m of replay.projectileMoves){if(m.t>t) break; const prev=state.get(m.id)||{id:m.id}; state.set(m.id,{...prev,x:m.x,y:m.y,z:m.z})} for(const d of replay.projectileDrops){if(d.t<=t) state.delete(d.id)} return state}
function drawProjectileTrails(){const grouped=new Map(); for(const p of replay.projectiles){if(!grouped.has(p.id)) grouped.set(p.id,[]); if(p.x!=null) grouped.get(p.id).push(p)} for(const m of replay.projectileMoves){if(!grouped.has(m.id)) grouped.set(m.id,[]); grouped.get(m.id).push(m)} ctx.strokeStyle='rgba(93,214,255,.24)'; ctx.lineWidth=1.5*devicePixelRatio; for(const list of grouped.values()){ctx.beginPath(); let started=false; for(const m of list.sort((a,b)=>a.t-b.t)){if(m.t>current) break; if(m.x==null) continue; const p=pos(m); if(!started){ctx.moveTo(p.x,p.y); started=true}else ctx.lineTo(p.x,p.y)} if(started) ctx.stroke()}}
function drawImpacts(){for(const impact of replay.impacts.filter(x=>Math.abs(x.t-current)<0.6)){if(impact.shotPosition&&impact.insidePoint){const a=pos(impact.shotPosition); const b=pos(impact.insidePoint); ctx.strokeStyle=impact.crit?'rgba(255,234,0,.75)':'rgba(255,135,64,.55)'; ctx.lineWidth=2*devicePixelRatio; ctx.beginPath(); ctx.moveTo(a.x,a.y); ctx.lineTo(b.x,b.y); ctx.stroke()} if(impact.insidePoint){const p=pos(impact.insidePoint); ctx.strokeStyle=impact.crit?'#ffea00':'#ff8840'; ctx.fillStyle=impact.crit?'rgba(255,234,0,.25)':'rgba(255,136,64,.22)'; ctx.lineWidth=2*devicePixelRatio; ctx.beginPath(); ctx.arc(p.x,p.y,10*devicePixelRatio,0,Math.PI*2); ctx.fill(); ctx.stroke(); ctx.beginPath(); ctx.moveTo(p.x-7*devicePixelRatio,p.y); ctx.lineTo(p.x+7*devicePixelRatio,p.y); ctx.moveTo(p.x,p.y-7*devicePixelRatio); ctx.lineTo(p.x,p.y+7*devicePixelRatio); ctx.stroke()}}}
function drawProjectiles(){const state=projectileStateAt(current); for(const projectile of state.values()){if(projectile.x==null) continue; const p=pos(projectile); ctx.fillStyle='#5dd6ff'; ctx.strokeStyle='#06222b'; ctx.lineWidth=2*devicePixelRatio; ctx.beginPath(); ctx.arc(p.x,p.y,5*devicePixelRatio,0,Math.PI*2); ctx.fill(); ctx.stroke()}}
function drawAbilityShots(){for(const ability of replay.abilityCasts.filter(x=>Math.abs(x.t-current)<1.2)){if(!ability.shotPosition) continue; const a=pos(ability.shotPosition); for(const shot of ability.shots||[]){if(!shot.target) continue; const b=pos(shot.target); ctx.strokeStyle='rgba(190,132,255,.65)'; ctx.lineWidth=2*devicePixelRatio; ctx.setLineDash([7*devicePixelRatio,5*devicePixelRatio]); ctx.beginPath(); ctx.moveTo(a.x,a.y); ctx.lineTo(b.x,b.y); ctx.stroke(); ctx.setLineDash([]); ctx.fillStyle='rgba(190,132,255,.22)'; ctx.strokeStyle='#be84ff'; ctx.beginPath(); ctx.arc(b.x,b.y,8*devicePixelRatio,0,Math.PI*2); ctx.fill(); ctx.stroke()} ctx.fillStyle='#be84ff'; ctx.beginPath(); ctx.arc(a.x,a.y,4*devicePixelRatio,0,Math.PI*2); ctx.fill()}}
function drawBuilds(){for(const b of replay.builds.filter(x=>Math.abs(x.t-current)<2.0)){const p=b.outside||b.inside; if(!p) continue; const q=pos(p); const s=8*devicePixelRatio; ctx.strokeStyle=b.showGhost===false?'#c7a76c':'#7bd88f'; ctx.fillStyle=b.showGhost===false?'rgba(199,167,108,.22)':'rgba(123,216,143,.18)'; ctx.lineWidth=2*devicePixelRatio; ctx.strokeRect(q.x-s,q.y-s,s*2,s*2); ctx.fillRect(q.x-s,q.y-s,s*2,s*2)} for(const d of replay.devicesBuilt.filter(x=>x.t<=current)){const q=pos(d); const age=current-d.t; if(age>20) continue; const s=7*devicePixelRatio; ctx.strokeStyle='#7bd88f'; ctx.fillStyle='rgba(123,216,143,.32)'; ctx.lineWidth=2*devicePixelRatio; ctx.beginPath(); ctx.rect(q.x-s,q.y-s,s*2,s*2); ctx.fill(); ctx.stroke()}}
function draw(){ctx.clearRect(0,0,canvas.width,canvas.height); ctx.strokeStyle='#263238'; ctx.lineWidth=1; for(let i=0;i<12;i++){let x=i*canvas.width/11;ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x,canvas.height);ctx.stroke();let y=i*canvas.height/11;ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(canvas.width,y);ctx.stroke()} const state=stateAt(current); for(const id of state.keys()) drawTrail(id); drawProjectileTrails(); drawBuilds(); drawAbilityShots(); drawImpacts(); drawProjectiles(); for(const unit of state.values()){if(unit.x==null) continue; const p=pos(unit); ctx.fillStyle=colors[unit.team]||colors.Unknown; ctx.beginPath(); ctx.arc(p.x,p.y,8*devicePixelRatio,0,Math.PI*2); ctx.fill(); ctx.strokeStyle='#111'; ctx.lineWidth=2*devicePixelRatio; ctx.stroke(); if(unit.health!=null){ctx.fillStyle='#111'; ctx.fillRect(p.x-13*devicePixelRatio,p.y-18*devicePixelRatio,26*devicePixelRatio,4*devicePixelRatio); ctx.fillStyle='#79d279'; ctx.fillRect(p.x-13*devicePixelRatio,p.y-18*devicePixelRatio,Math.max(0,Math.min(26,unit.health/100*26))*devicePixelRatio,4*devicePixelRatio)} ctx.fillStyle='#eee'; ctx.font=`${11*devicePixelRatio}px Segoe UI`; ctx.fillText(String(unit.id),p.x+10*devicePixelRatio,p.y-8*devicePixelRatio)} for(const d of replay.damage.filter(x=>Math.abs(x.t-current)<0.35)){const target=state.get(d.target); if(!target) continue; const p=pos(target); ctx.fillStyle=d.crit?'#ffea00':'#ff7b7b'; ctx.font=`${16*devicePixelRatio}px Segoe UI`; ctx.fillText(String(d.damage ?? ''),p.x+13*devicePixelRatio,p.y+17*devicePixelRatio)} const sec=current-replay.start; timeLabel.textContent=`${sec.toFixed(3)}s`; renderSide(state)}
function renderSide(state){document.getElementById('units').innerHTML=[...state.values()].sort((a,b)=>String(a.team).localeCompare(String(b.team))||a.id-b.id).map(u=>`<div class="row"><span class="pill team-${u.team}">${u.team}</span><span>${u.name||u.id}<br><small>${u.id} ${u.key||''}</small></span><span>${u.health==null?'':Math.round(u.health)}</span></div>`).join(''); document.getElementById('events').innerHTML=replay.events.filter(e=>e.t<=current).slice(-40).reverse().map(e=>`<div class="event"><span>${(e.t-replay.start).toFixed(2)}</span><span><strong>${e.kind}</strong><br><small>${e.text}</small></span><span></span></div>`).join('')}
function tick(now){if(playing){current += ((now-lastFrame)/1000)*Number(speed.value); if(current>replay.end){current=replay.end;playing=false;play.textContent='Play'} scrub.value=String(Math.round((current-replay.start)/Math.max(0.001,replay.duration)*1000))} lastFrame=now; draw(); requestAnimationFrame(tick)}
scrub.addEventListener('input',()=>{current=replay.start+(Number(scrub.value)/1000)*replay.duration});
play.addEventListener('click',()=>{playing=!playing;play.textContent=playing?'Pause':'Play';lastFrame=performance.now()});
addEventListener('resize',resize); resize(); requestAnimationFrame(tick);
</script>
</body>
</html>
""".Replace("__DATA__", data, StringComparison.Ordinal);

        File.WriteAllText(path, html, new UTF8Encoding(false));
    }

    private static void WriteNormalizedJson(string path, ReplayAnalysis analysis)
    {
        var validation = ReplayValidation.Evaluate(analysis);
        var units = analysis.UnitCreates
            .GroupBy(static unit => unit.UnitId)
            .Select(group => group.OrderBy(static unit => unit.Time).First())
            .OrderBy(static unit => unit.UnitId)
            .Select(unit => new
            {
                id = unit.UnitId,
                createdAt = Math.Round(unit.Time, 3),
                keyHash = unit.KeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                name = ResolveKeyName(analysis, unit.KeyHash),
                team = string.IsNullOrWhiteSpace(unit.Team) ? "Unknown" : unit.Team,
                playerId = unit.PlayerId,
                ownerId = unit.OwnerId,
                controlled = unit.Controlled,
                skinKeyHash = unit.SkinKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                skinName = ResolveKeyName(analysis, unit.SkinKeyHash),
                gearKeyHashes = unit.GearKeyHashes.Select(static key => key.ToString("X8", CultureInfo.InvariantCulture)),
                gearNames = unit.GearKeyHashes.Select(key => ResolveKeyName(analysis, key, key.ToString("X8", CultureInfo.InvariantCulture))),
                spawn = ToNormalizedTransform(unit.Transform)
            })
            .ToArray();

        var tracks = analysis.UnitMoves
            .Where(static move => move.Transform.Position is not null)
            .GroupBy(static move => move.UnitId)
            .OrderBy(static group => group.Key)
            .Select(group => new
            {
                unitId = group.Key,
                points = group
                    .OrderBy(static move => move.Time)
                    .Select(static move => new
                    {
                        t = Math.Round(move.Time, 3),
                        serverTime = move.ServerTime,
                        position = ToNormalizedVector(move.Transform.Position!.Value),
                        rotation = ToNormalizedVector(move.Transform.Rotation),
                        localVelocity = ToNormalizedVector(move.Transform.LocalVelocity),
                        state = new
                        {
                            crouch = move.Transform.IsCrouch,
                            jump = move.Transform.IsJump,
                            sprint = move.Transform.IsSprint,
                            wallClimb = move.Transform.IsWallClimb,
                            dash = move.Transform.IsDash,
                            groundSlam = move.Transform.IsGroundSlam,
                            noInterpolation = move.Transform.NoInterpolation
                        }
                    })
                    .ToArray()
            })
            .ToArray();

        var unitUpdates = analysis.UnitUpdates
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                team = item.Team,
                health = item.Health,
                forcefield = item.Forcefield,
                shield = item.Shield,
                capturePoints = item.CapturePoints,
                resource = item.Resource,
                movementActive = item.MovementActive,
                currentGearKeyHash = item.CurrentGearKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                currentGearName = ResolveKeyName(analysis, item.CurrentGearKeyHash),
                abilityKeyHash = item.AbilityKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                abilityName = ResolveKeyName(analysis, item.AbilityKeyHash),
                item.AbilityCharges,
                item.AbilityChargeCooldownEnd,
                ammo = item.Ammo.Select(ammo => new
                {
                    keyHash = ammo.Key.ToString("X8", CultureInfo.InvariantCulture),
                    name = ResolveKeyName(analysis, ammo.Key, ammo.Key.ToString("X8", CultureInfo.InvariantCulture)),
                    values = ammo.Value
                }),
                effects = item.Effects.Select(effect => new
                {
                    keyHash = effect.Key.ToString("X8", CultureInfo.InvariantCulture),
                    name = ResolveKeyName(analysis, effect.Key, effect.Key.ToString("X8", CultureInfo.InvariantCulture)),
                    endTime = effect.Value
                }),
                item.Buffs,
                devices = item.Devices.Select(device => new
                {
                    slot = device.Key,
                    deviceKeyHash = device.Value.DeviceKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                    deviceName = ResolveKeyName(analysis, device.Value.DeviceKeyHash),
                    device.Value.TotalCost,
                    device.Value.CostInc
                }),
                item.TurretTargetId,
                cloudAffectedBlocks = item.CloudAffectedBlocks.Count,
                item.ProjectileInitSpeed,
                item.BombTimeoutEnd,
                item.DamageCapturers,
                item.PortalLink,
                item.TeslaCharge,
                flags = item.Flags
            })
            .ToArray();

        var unitDrops = analysis.UnitDrops
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId
            })
            .ToArray();

        var unitManeuvers = analysis.UnitManeuvers
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                type = item.Maneuver.Name,
                position = ToNormalizedVector(item.Maneuver.Position),
                originPosition = ToNormalizedVector(item.Maneuver.OriginPosition),
                item.Maneuver.OriginUnitId,
                item.Maneuver.Force,
                item.Maneuver.MidairForce,
                item.Maneuver.DirectionAngle,
                item.Maneuver.Distance,
                maneuverTime = item.Maneuver.Time,
                item.Maneuver.RotationTime,
                item.Maneuver.Enabled
            })
            .ToArray();

        var damage = analysis.Damages
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                targetUnitId = item.TargetUnitId,
                sourceUnitId = item.SourceUnitId,
                sourcePosition = ToNormalizedVector(item.SourcePosition),
                impactKeyHash = item.ImpactKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                impactName = ResolveKeyName(analysis, item.ImpactKeyHash),
                damage = item.Damage,
                initialDamage = item.InitialDamage,
                crit = item.Crit
            })
            .ToArray();

        var kills = analysis.Kills
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                item.DeadUnitId,
                item.DeadPlayerId,
                item.KillerPlayerId,
                item.Assistants,
                damageSourceKeyHash = item.DamageSourceKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                damageSourceName = ResolveKeyName(analysis, item.DamageSourceKeyHash),
                sourcePosition = ToNormalizedVector(item.SourcePosition),
                item.Crit
            })
            .ToArray();

        var impacts = analysis.Impacts
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                insidePoint = ToNormalizedVector(item.InsidePoint),
                normal = ToNormalizedVector(item.Normal),
                casterUnitId = item.CasterUnitId,
                casterPlayerId = item.CasterPlayerId,
                impactKeyHash = item.ImpactKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                impactName = ResolveKeyName(analysis, item.ImpactKeyHash),
                sourceKeyHash = item.SourceKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                sourceName = ResolveKeyName(analysis, item.SourceKeyHash),
                hitUnits = item.HitUnits,
                shotPosition = ToNormalizedVector(item.ShotPosition),
                crit = item.Crit
            })
            .ToArray();

        var zoneEvents = analysis.ZoneEvents
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                eventType = item.EventType,
                name = item.Name,
                unitId = item.UnitId,
                turretId = item.TurretId,
                toolIndex = item.ToolIndex,
                active = item.Active,
                position = ToNormalizedVector(item.Position)
            })
            .ToArray();

        var projectileCreates = analysis.ProjectileCreates
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                projectileId = item.ProjectileId,
                projectileKeyHash = item.Info.ProjectileKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                projectileName = ResolveKeyName(analysis, item.Info.ProjectileKeyHash),
                transform = ToNormalizedTransform(item.Info.Transform),
                speed = item.Info.Speed,
                ownerUnitId = item.Info.OwnerUnitId,
                ownerTeam = item.Info.OwnerTeam
            })
            .ToArray();

        var projectileMoves = analysis.ProjectileMoves
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                projectileId = item.ProjectileId,
                serverTime = item.ServerTime,
                transform = ToNormalizedTransform(item.Transform)
            })
            .ToArray();

        var projectileDrops = analysis.ProjectileDrops
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                projectileId = item.ProjectileId
            })
            .ToArray();

        var casts = analysis.Casts
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                toolIndex = item.Data.ToolIndex,
                shotPosition = ToNormalizedVector(item.Data.ShotPosition),
                shots = item.Data.Shots.Select(static shot => new { target = ToNormalizedVector(shot.TargetPosition), shotId = shot.ShotId }),
                unitProjectileSpeed = item.Data.UnitProjectileSpeed
            })
            .ToArray();

        var abilityCasts = analysis.AbilityCasts
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                abilityKeyHash = item.AbilityKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                abilityName = ResolveKeyName(analysis, item.AbilityKeyHash),
                shotPosition = ToNormalizedVector(item.ShotPosition),
                shots = item.Shots.Select(static shot => new { target = ToNormalizedVector(shot.TargetPosition), shotId = shot.ShotId })
            })
            .ToArray();

        var buildStarts = analysis.BuildStarts
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                toolIndex = item.ToolIndex,
                deviceKeyHash = item.DeviceKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                deviceName = ResolveKeyName(analysis, item.DeviceKeyHash),
                insidePosition = ToNormalizedVector(item.InsidePosition),
                outsidePosition = ToNormalizedVector(item.OutsidePosition),
                direction = item.Direction,
                showGhost = item.ShowGhost
            })
            .ToArray();

        var buildCancels = analysis.BuildCancels
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId
            })
            .ToArray();

        var devicesBuilt = analysis.DevicesBuilt
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                unitId = item.UnitId,
                deviceKeyHash = item.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture),
                deviceName = ResolveKeyName(analysis, item.DeviceKeyHash),
                position = ToNormalizedVector(item.Position)
            })
            .ToArray();

        var buildPlacements = BuildBuildPlacements(analysis)
            .OrderBy(static item => item.DeviceBuilt.Time)
            .Select(item => new
            {
                t = Math.Round(item.DeviceBuilt.Time, 3),
                buildStartTime = item.BuildStart is null ? (double?)null : Math.Round(item.BuildStart.Time, 3),
                buildToDeviceSeconds = item.BuildStart is null ? (double?)null : Math.Round(item.DeviceBuilt.Time - item.BuildStart.Time, 3),
                builderUnitId = item.BuildStart?.UnitId,
                builtUnitId = item.DeviceBuilt.UnitId,
                deviceKeyHash = item.DeviceBuilt.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture),
                deviceName = ResolveKeyName(analysis, item.DeviceBuilt.DeviceKeyHash),
                cell = ToNormalizedVector(item.Cell),
                position = ToNormalizedVector(item.DeviceBuilt.Position),
                insidePosition = ToNormalizedVector(item.BuildStart?.InsidePosition),
                outsidePosition = ToNormalizedVector(item.BuildStart?.OutsidePosition),
                direction = item.BuildStart?.Direction,
                showGhost = item.BuildStart?.ShowGhost,
                blockUpdateTime = item.BlockUpdateTime is null ? (double?)null : Math.Round(item.BlockUpdateTime.Value, 3),
                footprintUpdates = item.FootprintUpdates.Select(update => new
                {
                    t = Math.Round(update.Time, 3),
                    dt = Math.Round(update.Time - item.DeviceBuilt.Time, 3),
                    update.Distance,
                    update.Dx,
                    update.Dy,
                    update.Dz,
                    position = ToNormalizedVector(update.Sample.Position),
                    id = update.Sample.Id,
                    name = update.Sample.Id is ushort id ? ResolveBlockName(analysis, id) : "",
                    damage = update.Sample.Damage,
                    vdata = update.Sample.Vdata,
                    vdataLowByte = update.Sample.Vdata.HasValue ? (byte)(update.Sample.Vdata.Value & 0xFF) : (byte?)null,
                    vdataHighByte = update.Sample.Vdata.HasValue ? (byte)(update.Sample.Vdata.Value >> 8) : (byte?)null,
                    slopeExistingCornerCount = CountSlopeExistingCorners(update.Sample.Vdata),
                    slopeExistingCorners = GetSlopeExistingCorners(update.Sample.Vdata),
                    slopeMissingCorners = GetSlopeMissingCorners(update.Sample.Vdata),
                    ldata = update.Sample.Ldata,
                    teamBits = update.Sample.Ldata.HasValue ? update.Sample.Ldata.Value & 0x03 : (int?)null,
                    team = FormatBlockTeam(update.Sample.Ldata),
                    ldataFlags = update.Sample.Ldata.HasValue ? update.Sample.Ldata.Value & ~0x03 : (int?)null
                }),
                block = item.BlockUpdate is null ? null : new
                {
                    position = ToNormalizedVector(item.BlockUpdate.Position),
                    id = item.BlockUpdate.Id,
                    name = item.BlockUpdate.Id is ushort id ? ResolveBlockName(analysis, id) : "",
                    damage = item.BlockUpdate.Damage,
                    vdata = item.BlockUpdate.Vdata,
                    vdataLowByte = item.BlockUpdate.Vdata.HasValue ? (byte)(item.BlockUpdate.Vdata.Value & 0xFF) : (byte?)null,
                    vdataHighByte = item.BlockUpdate.Vdata.HasValue ? (byte)(item.BlockUpdate.Vdata.Value >> 8) : (byte?)null,
                    slopeExistingCornerCount = CountSlopeExistingCorners(item.BlockUpdate.Vdata),
                    slopeExistingCorners = GetSlopeExistingCorners(item.BlockUpdate.Vdata),
                    slopeMissingCorners = GetSlopeMissingCorners(item.BlockUpdate.Vdata),
                    ldata = item.BlockUpdate.Ldata,
                    teamBits = item.BlockUpdate.Ldata.HasValue ? item.BlockUpdate.Ldata.Value & 0x03 : (int?)null,
                    team = FormatBlockTeam(item.BlockUpdate.Ldata),
                    ldataFlags = item.BlockUpdate.Ldata.HasValue ? item.BlockUpdate.Ldata.Value & ~0x03 : (int?)null
                }
            })
            .ToArray();

        var blockMined = analysis.BlockMined
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                item.UnitId,
                blockKeyHash = item.BlockKeyHash.ToString("X8", CultureInfo.InvariantCulture),
                blockName = ResolveKeyName(analysis, item.BlockKeyHash)
            })
            .ToArray();

        var blockUpdates = analysis.BlockUpdates
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                item.Count,
                samples = item.Samples.Select(static sample => new
                {
                    position = ToNormalizedVector(sample.Position),
                    id = sample.Id,
                    damage = sample.Damage,
                    vdata = sample.Vdata,
                    ldata = sample.Ldata
                }),
                updates = item.Updates.Select(sample => new
                {
                    position = ToNormalizedVector(sample.Position),
                    id = sample.Id,
                    damage = sample.Damage,
                    vdata = sample.Vdata,
                    vdataLowByte = sample.Vdata.HasValue ? (byte)(sample.Vdata.Value & 0xFF) : (byte?)null,
                    vdataHighByte = sample.Vdata.HasValue ? (byte)(sample.Vdata.Value >> 8) : (byte?)null,
                    slopeExistingCornerCount = CountSlopeExistingCorners(sample.Vdata),
                    slopeExistingCorners = GetSlopeExistingCorners(sample.Vdata),
                    slopeMissingCorners = GetSlopeMissingCorners(sample.Vdata),
                    ldata = sample.Ldata,
                    teamBits = sample.Ldata.HasValue ? sample.Ldata.Value & 0x03 : (int?)null,
                    team = FormatBlockTeam(sample.Ldata),
                    ldataFlags = sample.Ldata.HasValue ? sample.Ldata.Value & ~0x03 : (int?)null
                })
            })
            .ToArray();

        var mapStateTimeline = BuildMapStateTimeline(analysis, BuildBuildPlacements(analysis));
        var mapStateVerification = VerifyMapStateTimeline(analysis, mapStateTimeline);
        var mapState = new
        {
            initialState = new
            {
                blocksCsv = "map_blocks.csv",
                nonEmptyBlockCount = analysis.DecodedMap?.NonEmptyBlocks.Count ?? 0,
                size = ToNormalizedVector(analysis.DecodedMap?.Size)
            },
            timelineCsv = "map_state_timeline.csv",
            timelineUpdateCount = mapStateTimeline.Count,
            placementLinkedUpdateCount = mapStateTimeline.Count(static item => item.Placement is not null),
            verification = new
            {
                report = "map_state_verification.txt",
                finalCountsCsv = "map_state_final_counts.csv",
                changedCellsCsv = "map_state_changed_cells.csv",
                mapStateVerification.InitialNonEmptyBlockCount,
                mapStateVerification.FinalNonEmptyBlockCount,
                changedCellCount = mapStateVerification.ChangedCells.Count,
                mapStateVerification.RepeatedCellCount,
                mapStateVerification.DuplicateNoOpUpdates,
                mapStateVerification.OutOfOrderUpdates,
                finalBlockCounts = mapStateVerification.FinalBlockCounts.Take(64).Select(item => new
                {
                    item.Id,
                    name = ResolveBlockName(analysis, item.Id),
                    item.Count
                })
            },
            samples = mapStateTimeline.Take(200).Select(item => new
            {
                item.Sequence,
                t = Math.Round(item.Time, 3),
                item.Source,
                placementDeviceTime = item.Placement is null ? (double?)null : Math.Round(item.Placement.DeviceBuilt.Time, 3),
                deviceKeyHash = item.Placement?.DeviceBuilt.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture),
                deviceName = item.Placement is null ? "" : ResolveKeyName(analysis, item.Placement.DeviceBuilt.DeviceKeyHash),
                direction = item.Placement?.BuildStart?.Direction,
                position = ToNormalizedVector(item.Update.Position),
                id = item.Update.Id,
                name = item.Update.Id is ushort id ? ResolveBlockName(analysis, id) : "",
                damage = item.Update.Damage,
                vdata = item.Update.Vdata,
                ldata = item.Update.Ldata,
                team = FormatBlockTeam(item.Update.Ldata)
            })
        };

        var barrierUpdates = analysis.BarrierUpdates
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Labels
            })
            .ToArray();

        var reloads = analysis.ReloadEvents
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Phase,
                item.UnitId
            })
            .ToArray();

        var channels = analysis.ChannelEvents
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Phase,
                item.UnitId,
                item.ToolIndex,
                hitPosition = ToNormalizedVector(item.HitPosition),
                targetBlock = item.TargetBlock is null ? null : new { x = item.TargetBlock.Value.X, y = item.TargetBlock.Value.Y, z = item.TargetBlock.Value.Z },
                item.TargetUnitId
            })
            .ToArray();

        var dashCharges = analysis.DashChargeEvents
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Phase,
                item.UnitId,
                item.ToolIndex
            })
            .ToArray();

        var pickups = analysis.PickupTakenEvents
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                item.PlayerId,
                pickupKeyHash = item.PickupKeyHash.ToString("X8", CultureInfo.InvariantCulture),
                pickupName = ResolveKeyName(analysis, item.PickupKeyHash)
            })
            .ToArray();

        var recalls = analysis.RecallEvents
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Phase,
                item.UnitId,
                item.Duration,
                item.EndTime
            })
            .ToArray();

        var portalTeleports = analysis.PortalTeleports
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.UnitId,
                item.PortalFromId,
                item.PortalToId
            })
            .ToArray();

        var kickPlayers = analysis.KickPlayerEvents
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.PlayerId,
                item.Reason
            })
            .ToArray();

        var rpcResults = analysis.RpcResults
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Name,
                item.RpcId,
                item.Status,
                item.Value
            })
            .ToArray();

        var surrenderEvents = analysis.SurrenderEvents
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Phase,
                item.Team,
                item.Deadline,
                item.Accepted,
                item.Detail
            })
            .ToArray();

        var surrenderProgress = analysis.SurrenderProgress
            .OrderBy(static item => item.Time)
            .Select(static item => new
            {
                t = Math.Round(item.Time, 3),
                item.Votes
            })
            .ToArray();

        var zoneUpdates = analysis.ZoneUpdates
            .OrderBy(static item => item.Time)
            .Select(item => new
            {
                t = Math.Round(item.Time, 3),
                flags = item.Flags,
                phase = item.Phase,
                stats = item.Stats,
                spawnPoints = item.SpawnPoints.Select(static spawn => new
                {
                    spawn.Id,
                    spawn.Team,
                    position = ToNormalizedVector(spawn.Position),
                    spawn.LockType,
                    spawn.Owner
                }),
                playerSpawnPoints = item.PlayerSpawnPoints,
                respawnInfo = item.RespawnInfo,
                playerInfo = item.PlayerInfo,
                supplyInfo = item.SupplyInfo is null ? null : new
                {
                    nextSupplyDropKeyHash = item.SupplyInfo.NextSupplyDropKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                    nextSupplyDropName = ResolveKeyName(analysis, item.SupplyInfo.NextSupplyDropKeyHash),
                    item.SupplyInfo.NextSupplyDropTime,
                    position = ToNormalizedVector(item.SupplyInfo.Position)
                },
                objectives = item.Objectives,
                resourceCap = item.ResourceCap
            })
            .ToArray();

        var mapInit = analysis.InitZone is null ? null : new
        {
            flags = analysis.InitZoneFlags,
            unreadBytes = analysis.InitZoneUnreadBytes,
            canSwitchHero = analysis.InitZone.CanSwitchHero,
            isCustomGame = analysis.InitZone.IsCustomGame,
            initMapDataBytes = analysis.InitZone.MapData?.Length ?? 0,
            initColorDataBytes = analysis.InitZone.ColorData?.Length ?? 0,
            initMapDataAsset = analysis.InitZone.MapData is { Length: > 0 } ? "init_map_data.bin" : null,
            initColorDataAsset = analysis.InitZone.ColorData is { Length: > 0 } ? "init_color_data.bin" : null,
            initialBlockUpdateCount = analysis.InitZone.Updates.Count,
                initialBlockUpdateSamples = analysis.InitZone.Updates.Take(200).Select(static item => new
                {
                    position = ToNormalizedVector(item.Position),
                    item.Id,
                    item.Damage,
                    item.Vdata,
                    vdataLowByte = item.Vdata.HasValue ? (byte)(item.Vdata.Value & 0xFF) : (byte?)null,
                    vdataHighByte = item.Vdata.HasValue ? (byte)(item.Vdata.Value >> 8) : (byte?)null,
                    slopeExistingCornerCount = CountSlopeExistingCorners(item.Vdata),
                    slopeExistingCorners = GetSlopeExistingCorners(item.Vdata),
                    slopeMissingCorners = GetSlopeMissingCorners(item.Vdata),
                    item.Ldata,
                    teamBits = item.Ldata.HasValue ? item.Ldata.Value & 0x03 : (int?)null,
                    team = FormatBlockTeam(item.Ldata),
                    ldataFlags = item.Ldata.HasValue ? item.Ldata.Value & ~0x03 : (int?)null
                }),
            map = analysis.InitZone.Map is null ? null : new
            {
                flags = analysis.InitZone.Map.Flags,
                analysis.InitZone.Map.Version,
                analysis.InitZone.Map.Schema,
                analysis.InitZone.Map.Match,
                size = ToNormalizedVector(analysis.InitZone.Map.Size),
                blocksDataBytes = analysis.InitZone.Map.BlocksData?.Length ?? 0,
                colorsDataBytes = analysis.InitZone.Map.ColorsData?.Length ?? 0,
                blocksDataAsset = analysis.InitZone.Map.BlocksData is { Length: > 0 } ? "map_blocks_data.bin" : null,
                colorsDataAsset = analysis.InitZone.Map.ColorsData is { Length: > 0 } ? "map_colors_data.bin" : null,
                colorPalette = analysis.InitZone.Map.ColorPalette,
                spawnPoints = analysis.InitZone.Map.SpawnPoints.Select(static item => new
                {
                    item.Team,
                    item.Label,
                    item.Direction,
                    position = ToNormalizedVector(item.Position)
                }),
                units = analysis.InitZone.Map.Units.Select(item => new
                {
                    unitKeyHash = item.UnitKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                    unitName = ResolveKeyName(analysis, item.UnitKeyHash),
                    item.Team,
                    position = ToNormalizedVector(item.Position),
                    rotation = ToNormalizedVector(item.Rotation)
                }),
                cameras = analysis.InitZone.Map.Cameras.Select(static item => new
                {
                    item.Team,
                    item.Labels,
                    position = ToNormalizedVector(item.Position),
                    direction = ToNormalizedVector(item.Direction)
                }),
                triggers = analysis.InitZone.Map.Triggers.Select(static item => new
                {
                    item.Type,
                    item.Tag,
                    item.Labels,
                    position = ToNormalizedVector(item.Position),
                    size = ToNormalizedVector(item.Size),
                    item.Radius
                }),
                properties = analysis.InitZone.Map.Properties
            },
            decodedBlocks = analysis.DecodedMap is null ? null : new
            {
                size = ToNormalizedVector(analysis.DecodedMap.Size),
                analysis.DecodedMap.BlockCount,
                nonEmptyBlockCount = analysis.DecodedMap.NonEmptyBlocks.Count,
                analysis.DecodedMap.DecodedBytes,
                analysis.DecodedMap.ColorBytes,
                blocksCsv = "map_blocks.csv",
                blockCountsCsv = "map_block_counts.csv",
                blockCounts = analysis.DecodedMap.BlockCounts.Take(64).Select(item => new
                {
                    item.Id,
                    name = ResolveBlockName(analysis, item.Id),
                    item.Count
                }),
                samples = analysis.DecodedMap.NonEmptyBlocks.Take(200).Select(item => new
                {
                    position = ToNormalizedVector(item.Position),
                    item.Id,
                    name = ResolveBlockName(analysis, item.Id),
                    item.Damage,
                    item.Vdata,
                    vdataLowByte = (byte)(item.Vdata & 0xFF),
                    vdataHighByte = (byte)(item.Vdata >> 8),
                    slopeExistingCornerCount = CountSlopeExistingCorners(item.Vdata),
                    slopeExistingCorners = GetSlopeExistingCorners(item.Vdata),
                    slopeMissingCorners = GetSlopeMissingCorners(item.Vdata),
                    item.Ldata,
                    teamBits = item.Ldata & 0x03,
                    team = FormatBlockTeam(item.Ldata),
                    ldataFlags = item.Ldata & ~0x03,
                    item.Color
                })
            }
        };

        var packetCounts = analysis.Packets
            .GroupBy(static packet => packet.Event)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new
            {
                eventName = group.Key,
                count = group.Count()
            })
            .ToArray();
        var playerUnits = ReplayIdentityBuilder.BuildPlayerUnitIdentities(analysis);

        var replay = new
        {
            format = "bnl-community-replay.normalized",
            version = 1,
            source = new
            {
                path = analysis.SourcePath,
                file = Path.GetFileName(analysis.SourcePath),
                schema = analysis.Schema,
                sessionUtc = analysis.SessionUtc
            },
            validation,
            mapInit,
            match = new
            {
                start = Math.Round(analysis.StartTime, 3),
                end = Math.Round(analysis.EndTime, 3),
                duration = Math.Round(analysis.DurationSeconds, 3),
                mapKeyHash = analysis.MapKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                mapName = ResolveKeyName(analysis, analysis.MapKeyHash),
                winner = analysis.EndMatch?.WinnerTeam,
                endMatchTime = analysis.EndMatch is null ? (double?)null : Math.Round(analysis.EndMatch.Time, 3)
            },
            endMatchResult = analysis.EndMatchResult is null ? null : new
            {
                t = Math.Round(analysis.EndMatchResult.Time, 3),
                analysis.EndMatchResult.Flags,
                analysis.EndMatchResult.MatchSeconds,
                analysis.EndMatchResult.IsWinner,
                analysis.EndMatchResult.IsBackfiller,
                analysis.EndMatchResult.IsAfk,
                heroKeyHash = analysis.EndMatchResult.HeroKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                heroName = ResolveKeyName(analysis, analysis.EndMatchResult.HeroKeyHash),
                skinKeyHash = analysis.EndMatchResult.SkinKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                skinName = ResolveKeyName(analysis, analysis.EndMatchResult.SkinKeyHash),
                analysis.EndMatchResult.OldHeroXp,
                analysis.EndMatchResult.OldPlayerXp,
                analysis.EndMatchResult.NewHeroXp,
                analysis.EndMatchResult.RewardXp,
                analysis.EndMatchResult.OldCurrency,
                analysis.EndMatchResult.RewardCurrency,
                analysis.EndMatchResult.RewardBonuses,
                analysis.EndMatchResult.XpBoost,
                analysis.EndMatchResult.GoldBoost,
                analysis.EndMatchResult.RankedStatus,
                analysis.EndMatchResult.RankedData,
                analysis.EndMatchResult.Challenges,
                analysis.EndMatchResult.TimeTrialData,
                lootCrateKeyHash = analysis.EndMatchResult.LootCrateKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                lootCrateName = ResolveKeyName(analysis, analysis.EndMatchResult.LootCrateKeyHash),
                players = analysis.EndMatchResult.Players.Select(player => new
                {
                    player.PlayerId,
                    nickname = ResolvePlayerNickname(analysis, player.PlayerId),
                    player.SquadId,
                    player.Backfiller,
                    player.Noob,
                    player.Stats,
                    medalPositiveKeyHash = player.MedalPositiveKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                    medalPositiveName = ResolveKeyName(analysis, player.MedalPositiveKeyHash),
                    medalNegativeKeyHash = player.MedalNegativeKeyHash?.ToString("X8", CultureInfo.InvariantCulture),
                    medalNegativeName = ResolveKeyName(analysis, player.MedalNegativeKeyHash)
                })
            },
            stats = new
            {
                packets = analysis.Packets.Count,
                units = units.Length,
                movementPoints = analysis.UnitMoves.Count,
                unitUpdates = analysis.UnitUpdates.Count,
                unitDrops = analysis.UnitDrops.Count,
                unitManeuvers = analysis.UnitManeuvers.Count,
                damageEvents = analysis.Damages.Count,
                kills = analysis.Kills.Count,
                impacts = analysis.Impacts.Count,
                zoneEvents = analysis.ZoneEvents.Count,
                projectileCreates = analysis.ProjectileCreates.Count,
                projectileMoves = analysis.ProjectileMoves.Count,
                projectileDrops = analysis.ProjectileDrops.Count,
                casts = analysis.Casts.Count,
                abilityCasts = analysis.AbilityCasts.Count,
                buildStarts = analysis.BuildStarts.Count,
                buildCancels = analysis.BuildCancels.Count,
                devicesBuilt = analysis.DevicesBuilt.Count,
                buildPlacements = buildPlacements.Length,
                buildPlacementFootprintUpdates = buildPlacements.Sum(static item => item.footprintUpdates.Count()),
                mapStateTimelineUpdates = mapState.timelineUpdateCount,
                mapStateTimelinePlacementUpdates = mapState.placementLinkedUpdateCount,
                blockMined = analysis.BlockMined.Count,
                zoneUpdates = analysis.ZoneUpdates.Count,
                playerInfoRecords = analysis.ZoneUpdates.Sum(static item => item.PlayerInfo.Count),
                playerUnitLinks = playerUnits.Count,
                objectiveRecords = analysis.ZoneUpdates.Sum(static item => item.Objectives.Count),
                blockUpdatePackets = analysis.BlockUpdates.Count,
                blockUpdates = analysis.BlockUpdates.Sum(static item => item.Count),
                barrierUpdates = analysis.BarrierUpdates.Count,
                reloadEvents = analysis.ReloadEvents.Count,
                channelEvents = analysis.ChannelEvents.Count,
                dashChargeEvents = analysis.DashChargeEvents.Count,
                pickupTakenEvents = analysis.PickupTakenEvents.Count,
                recallEvents = analysis.RecallEvents.Count,
                portalTeleportEvents = analysis.PortalTeleports.Count,
                kickPlayerEvents = analysis.KickPlayerEvents.Count,
                rpcResults = analysis.RpcResults.Count,
                surrenderEvents = analysis.SurrenderEvents.Count,
                surrenderProgress = analysis.SurrenderProgress.Count,
                endMatchResultPlayers = analysis.EndMatchResult?.Players.Count ?? 0,
                decodeErrors = analysis.DecodeErrors.Count,
                resolvedKeyNames = analysis.KeyNames.Count
            },
            playerUnits,
            units,
            tracks,
            unitUpdates,
            unitDrops,
            unitManeuvers,
            damage,
            kills,
            impacts,
            zoneEvents,
            projectileCreates,
            projectileMoves,
            projectileDrops,
            casts,
            abilityCasts,
            buildStarts,
            buildCancels,
            devicesBuilt,
            buildPlacements,
            mapState,
            blockMined,
            zoneUpdates,
            blockUpdates,
            barrierUpdates,
            reloads,
            channels,
            dashCharges,
            pickups,
            recalls,
            portalTeleports,
            kickPlayers,
            rpcResults,
            surrenderEvents,
            surrenderProgress,
            packetCounts
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(path, JsonSerializer.Serialize(replay, options), new UTF8Encoding(false));
    }

    private static object? ToNormalizedTransform(ZoneTransformData? transform)
    {
        if (transform is null)
        {
            return null;
        }

        return new
        {
            position = ToNormalizedVector(transform.Position),
            rotation = ToNormalizedVector(transform.Rotation),
            localVelocity = ToNormalizedVector(transform.LocalVelocity),
            state = new
            {
                crouch = transform.IsCrouch,
                jump = transform.IsJump,
                sprint = transform.IsSprint,
                wallClimb = transform.IsWallClimb,
                dash = transform.IsDash,
                groundSlam = transform.IsGroundSlam,
                noInterpolation = transform.NoInterpolation
            }
        };
    }

    private static object? ToNormalizedVector(Vector3f? value) =>
        value is null ? null : ToNormalizedVector(value.Value);

    private static object ToNormalizedVector(Vector3f value) => new
    {
        x = Math.Round(value.X, 4),
        y = Math.Round(value.Y, 4),
        z = Math.Round(value.Z, 4)
    };

    private static object? ToNormalizedVector(Vector3s? value) =>
        value is null ? null : ToNormalizedVector(value.Value);

    private static object ToNormalizedVector(Vector3s value) => new
    {
        x = value.X,
        y = value.Y,
        z = value.Z
    };

    private static IEnumerable<TimelineEvent> BuildTimelineEvents(ReplayAnalysis analysis)
    {
        foreach (var item in analysis.UnitCreates)
        {
            var key = ResolveKeyName(analysis, item.KeyHash, string.IsNullOrWhiteSpace(item.KeyHashText) ? "unknown" : item.KeyHashText);
            yield return new TimelineEvent(item.Time, "Unit", $"{item.UnitId} {item.Team} {key}");
        }

        foreach (var item in analysis.Damages)
        {
            var damage = item.Damage?.ToString("0.###", CultureInfo.InvariantCulture) ?? "?";
            var source = item.SourceUnitId?.ToString(CultureInfo.InvariantCulture) ?? "world";
            var target = item.TargetUnitId?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
            var impact = ResolveKeyName(analysis, item.ImpactKeyHash);
            var suffix = string.IsNullOrWhiteSpace(impact) ? "" : $" ({impact})";
            yield return new TimelineEvent(item.Time, "Damage", $"{source} -> {target} for {damage}{suffix}");
        }

        foreach (var item in analysis.Kills)
        {
            var killer = item.KillerPlayerId?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
            var dead = item.DeadPlayerId?.ToString(CultureInfo.InvariantCulture) ?? item.DeadUnitId?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
            yield return new TimelineEvent(item.Time, "Kill", $"{killer} killed {dead}");
        }

        foreach (var item in analysis.Impacts)
        {
            var source = ResolveKeyName(analysis, item.SourceKeyHash, item.SourceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "unknown");
            var impact = ResolveKeyName(analysis, item.ImpactKeyHash, item.ImpactKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "impact");
            var caster = item.CasterUnitId?.ToString(CultureInfo.InvariantCulture) ?? "world";
            var hits = item.HitUnits.Count == 0 ? "no units" : string.Join("|", item.HitUnits);
            var crit = item.Crit ? " crit" : "";
            yield return new TimelineEvent(item.Time, "Impact", $"{caster} {source} {impact} hit {hits}{crit}");
        }

        foreach (var item in analysis.ZoneEvents)
        {
            var subject = item.UnitId?.ToString(CultureInfo.InvariantCulture)
                ?? item.TurretId?.ToString(CultureInfo.InvariantCulture)
                ?? "unknown";
            var detail = item.ToolIndex is null ? "" : $" tool {item.ToolIndex}";
            if (item.Active is not null)
            {
                detail += $" active={item.Active}";
            }

            yield return new TimelineEvent(item.Time, "Zone", $"{item.Name} {subject}{detail}");
        }

        foreach (var item in analysis.ProjectileCreates)
        {
            var projectile = ResolveKeyName(analysis, item.Info.ProjectileKeyHash, item.Info.ProjectileKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "unknown");
            var owner = item.Info.OwnerUnitId?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
            yield return new TimelineEvent(item.Time, "Projectile", $"{item.ProjectileId} {projectile} from {owner}");
        }

        foreach (var item in analysis.ProjectileDrops)
        {
            yield return new TimelineEvent(item.Time, "Projectile", $"{item.ProjectileId} dropped");
        }

        foreach (var item in analysis.AbilityCasts)
        {
            var ability = ResolveKeyName(analysis, item.AbilityKeyHash, item.AbilityKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "unknown");
            yield return new TimelineEvent(item.Time, "Ability", $"{item.UnitId} {ability} shots={item.Shots.Count}");
        }

        foreach (var item in analysis.BuildStarts)
        {
            var device = ResolveKeyName(analysis, item.DeviceKeyHash, item.DeviceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "unknown");
            yield return new TimelineEvent(item.Time, "Build", $"{item.UnitId} started {device}");
        }

        foreach (var item in analysis.BuildCancels)
        {
            yield return new TimelineEvent(item.Time, "Build", $"{item.UnitId} cancelled build");
        }

        foreach (var item in analysis.DevicesBuilt)
        {
            var device = ResolveKeyName(analysis, item.DeviceKeyHash, item.DeviceKeyHash.ToString("X8", CultureInfo.InvariantCulture));
            yield return new TimelineEvent(item.Time, "Device", $"{item.UnitId} built {device}");
        }

        foreach (var item in analysis.UnitDrops)
        {
            yield return new TimelineEvent(item.Time, "Drop", $"unit {item.UnitId}");
        }

        foreach (var item in analysis.UnitManeuvers)
        {
            yield return new TimelineEvent(item.Time, "Maneuver", $"{item.UnitId} {item.Maneuver.Name}");
        }

        foreach (var item in analysis.ReloadEvents)
        {
            yield return new TimelineEvent(item.Time, "Reload", $"{item.UnitId} {item.Phase}");
        }

        foreach (var item in analysis.BlockMined)
        {
            yield return new TimelineEvent(item.Time, "Mine", $"{item.UnitId} mined {ResolveKeyName(analysis, item.BlockKeyHash, item.BlockKeyHash.ToString("X8", CultureInfo.InvariantCulture))}");
        }

        foreach (var item in analysis.BarrierUpdates)
        {
            yield return new TimelineEvent(item.Time, "Barriers", string.Join("|", item.Labels));
        }

        foreach (var item in analysis.SurrenderEvents)
        {
            yield return new TimelineEvent(item.Time, "Surrender", string.Join(" ", new[] { item.Phase, item.Team ?? "", item.Accepted?.ToString() ?? "" }.Where(static part => !string.IsNullOrWhiteSpace(part))));
        }

        foreach (var item in analysis.ZoneUpdates)
        {
            if (item.Phase?.PhaseType is not null)
            {
                yield return new TimelineEvent(item.Time, "Phase", item.Phase.PhaseType);
            }

            foreach (var player in item.PlayerInfo)
            {
                var name = string.IsNullOrWhiteSpace(player.Nickname) ? player.PlayerId.ToString(CultureInfo.InvariantCulture) : player.Nickname;
                yield return new TimelineEvent(item.Time, "Player", $"{player.PlayerId} {name}");
            }

            foreach (var objective in item.Objectives)
            {
                yield return new TimelineEvent(item.Time, "Objective", $"{objective.Team ?? "?"} {objective.Id?.ToString(CultureInfo.InvariantCulture) ?? "?"} {objective.Counter?.ToString(CultureInfo.InvariantCulture) ?? "?"}/{objective.RequiredCounter?.ToString(CultureInfo.InvariantCulture) ?? "?"}");
            }
        }

        foreach (var item in analysis.BlockUpdates)
        {
            yield return new TimelineEvent(item.Time, "Blocks", $"{item.Count} update(s)");
        }

        if (analysis.EndMatch is not null)
        {
            yield return new TimelineEvent(analysis.EndMatch.Time, "End", $"{analysis.EndMatch.WinnerTeam} wins");
        }

        if (analysis.EndMatchResultPayloadBytes > 0)
        {
            var resultPacket = analysis.Packets.FirstOrDefault(static packet => packet.Event == "Recv_EndMatchResult");
            if (resultPacket is not null)
            {
                yield return new TimelineEvent(resultPacket.Time, "Result", $"{analysis.EndMatchResultPayloadBytes} bytes");
            }
        }
    }

    public static string ResolveKeyName(ReplayAnalysis analysis, uint? keyHash, string fallback = "")
    {
        if (keyHash is null)
        {
            return fallback;
        }

        return analysis.KeyNames.TryGetValue(keyHash.Value, out var name) ? name : fallback;
    }

    private static string ResolveBlockName(ReplayAnalysis analysis, ushort blockId, string fallback = "") =>
        analysis.BlockNames.TryGetValue(blockId, out var name) ? name : fallback;

    private static IReadOnlyList<BuildPlacementData> BuildBuildPlacements(ReplayAnalysis analysis)
    {
        var starts = analysis.BuildStarts.OrderBy(static item => item.Time).ToList();
        var usedStarts = new HashSet<int>();
        var blockUpdates = analysis.BlockUpdates
            .SelectMany(static update => update.Updates.Select(sample => (time: update.Time, sample)))
            .ToList();
        var placements = new List<BuildPlacementData>(analysis.DevicesBuilt.Count);

        foreach (var device in analysis.DevicesBuilt.OrderBy(static item => item.Time))
        {
            var cell = ToBlockCell(device.Position);
            var matchedStartIndex = FindMatchingBuildStart(starts, usedStarts, device, cell);
            BuildStartEvent? start = null;
            if (matchedStartIndex.HasValue)
            {
                usedStarts.Add(matchedStartIndex.Value);
                start = starts[matchedStartIndex.Value];
            }

            var blockUpdate = FindMatchingBlockUpdate(blockUpdates, device.Time, cell);
            var footprintUpdates = FindFootprintBlockUpdates(blockUpdates, device.Time, cell);
            placements.Add(new BuildPlacementData(device, start, cell, blockUpdate?.time, blockUpdate?.sample, footprintUpdates));
        }

        return placements;
    }

    private static IReadOnlyList<MapStateTimelineItem> BuildMapStateTimeline(ReplayAnalysis analysis, IReadOnlyList<BuildPlacementData> buildPlacements)
    {
        var placementByUpdate = new Dictionary<BlockUpdateKey, (string source, BuildPlacementData placement)>();
        foreach (var placement in buildPlacements)
        {
            if (placement.BlockUpdateTime.HasValue && placement.BlockUpdate is not null)
            {
                placementByUpdate[new BlockUpdateKey(placement.BlockUpdateTime.Value, placement.BlockUpdate.Position)] = ("placement_center", placement);
            }

            foreach (var footprint in placement.FootprintUpdates)
            {
                var key = new BlockUpdateKey(footprint.Time, footprint.Sample.Position);
                if (!placementByUpdate.ContainsKey(key))
                {
                    placementByUpdate[key] = ("placement_footprint", placement);
                }
            }
        }

        var items = new List<MapStateTimelineItem>();
        var sequence = 0;
        foreach (var update in analysis.BlockUpdates.OrderBy(static item => item.Time))
        {
            foreach (var sample in update.Updates)
            {
                var key = new BlockUpdateKey(update.Time, sample.Position);
                if (placementByUpdate.TryGetValue(key, out var placement))
                {
                    items.Add(new MapStateTimelineItem(sequence++, update.Time, placement.source, sample, placement.placement));
                }
                else
                {
                    items.Add(new MapStateTimelineItem(sequence++, update.Time, "block_update", sample, null));
                }
            }
        }

        return items;
    }

    private static MapStateVerificationData VerifyMapStateTimeline(ReplayAnalysis analysis, IReadOnlyList<MapStateTimelineItem> timeline)
    {
        var initial = new Dictionary<Vector3s, MapBlockState>();
        if (analysis.DecodedMap is not null)
        {
            foreach (var block in analysis.DecodedMap.NonEmptyBlocks)
            {
                initial[block.Position] = new MapBlockState(block.Id, block.Damage, block.Vdata, block.Ldata);
            }
        }

        var current = new Dictionary<Vector3s, MapBlockState>(initial);
        var updateCounts = new Dictionary<Vector3s, int>();
        var duplicateNoOps = 0;
        var outOfOrder = 0;
        var previousTime = double.MinValue;
        var expectedSequence = 0;

        foreach (var item in timeline)
        {
            if (item.Sequence != expectedSequence || item.Time < previousTime)
            {
                outOfOrder++;
            }

            expectedSequence++;
            previousTime = item.Time;

            var position = item.Update.Position;
            updateCounts[position] = updateCounts.GetValueOrDefault(position) + 1;
            current.TryGetValue(position, out var previous);
            var next = ApplyBlockUpdate(previous, item.Update);
            if (next == previous)
            {
                duplicateNoOps++;
            }

            if (next.Id == 0)
            {
                current.Remove(position);
            }
            else
            {
                current[position] = next;
            }
        }

        var changedCells = updateCounts
            .Select(item =>
            {
                initial.TryGetValue(item.Key, out var initialState);
                current.TryGetValue(item.Key, out var finalState);
                return new MapStateChangedCell(item.Key, item.Value, initialState, finalState);
            })
            .Where(static item => item.Initial != item.Final)
            .OrderByDescending(static item => item.UpdateCount)
            .ThenBy(static item => item.Position.X)
            .ThenBy(static item => item.Position.Y)
            .ThenBy(static item => item.Position.Z)
            .ToArray();

        var finalCounts = current.Values
            .GroupBy(static item => item.Id)
            .Select(static group => new DecodedMapBlockCount(group.Key, group.Count()))
            .OrderByDescending(static item => item.Count)
            .ThenBy(static item => item.Id)
            .ToArray();

        return new MapStateVerificationData(
            initial.Values.Count(static item => item.Id != 0),
            current.Values.Count(static item => item.Id != 0),
            initial.Count,
            current.Count,
            updateCounts.Count(static item => item.Value > 1),
            duplicateNoOps,
            outOfOrder,
            finalCounts,
            changedCells);
    }

    private static MapBlockState ApplyBlockUpdate(MapBlockState previous, BlockUpdateSample update) => new(
        update.Id ?? previous.Id,
        update.Damage ?? previous.Damage,
        update.Vdata ?? previous.Vdata,
        update.Ldata ?? previous.Ldata);

    private static void WriteMapStateVerification(string path, ReplayAnalysis analysis, MapStateVerificationData verification)
    {
        var lines = new[]
        {
            "Map state timeline verification",
            $"Initial renderable non-air blocks: {verification.InitialNonEmptyBlockCount}",
            $"Final renderable non-air blocks: {verification.FinalNonEmptyBlockCount}",
            $"Initial tracked cells: {verification.InitialTrackedCellCount}",
            $"Final tracked cells: {verification.FinalTrackedCellCount}",
            $"Changed cells: {verification.ChangedCells.Count}",
            $"Repeated cells: {verification.RepeatedCellCount}",
            $"Duplicate no-op updates: {verification.DuplicateNoOpUpdates}",
            $"Out-of-order updates: {verification.OutOfOrderUpdates}",
            $"Final block types: {verification.FinalBlockCounts.Count}",
            "",
            "Top final block counts:",
            string.Join(Environment.NewLine, verification.FinalBlockCounts.Take(25).Select(item => $"{item.Id} {ResolveBlockName(analysis, item.Id)}: {item.Count}")),
            "",
            "Notes:",
            "A duplicate no-op means the timeline wrote the same final state already present at that cell.",
            "Repeated cells are expected during combat, repairs, trap effects, and mining.",
            "Out-of-order updates should be 0; non-zero means sequence/time ordering needs investigation."
        };

        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static int? FindMatchingBuildStart(IReadOnlyList<BuildStartEvent> starts, HashSet<int> usedStarts, DeviceBuiltEvent device, Vector3s cell)
    {
        int? bestIndex = null;
        var bestScore = double.MaxValue;

        for (var i = 0; i < starts.Count; i++)
        {
            if (usedStarts.Contains(i))
            {
                continue;
            }

            var start = starts[i];
            if (start.DeviceKeyHash != device.DeviceKeyHash)
            {
                continue;
            }

            var delta = device.Time - start.Time;
            if (delta < -0.05 || delta > 8)
            {
                continue;
            }

            var hasPosition = IsNonZero(start.InsidePosition) || IsNonZero(start.OutsidePosition);
            var positionMatches = PositionsMatch(start.InsidePosition, cell) || PositionsMatch(start.OutsidePosition, cell);
            if (hasPosition && !positionMatches)
            {
                continue;
            }

            var score = delta + (positionMatches ? 0 : 4);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static (double time, BlockUpdateSample sample)? FindMatchingBlockUpdate(IReadOnlyList<(double time, BlockUpdateSample sample)> updates, double deviceTime, Vector3s cell)
    {
        (double time, BlockUpdateSample sample)? best = null;
        var bestDelta = double.MaxValue;
        foreach (var update in updates)
        {
            if (update.sample.Position != cell)
            {
                continue;
            }

            var delta = Math.Abs(update.time - deviceTime);
            if (delta > 1)
            {
                continue;
            }

            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = update;
            }
        }

        return best;
    }

    private static IReadOnlyList<BuildPlacementFootprintUpdate> FindFootprintBlockUpdates(IReadOnlyList<(double time, BlockUpdateSample sample)> updates, double deviceTime, Vector3s cell)
    {
        var results = new List<BuildPlacementFootprintUpdate>();
        foreach (var update in updates)
        {
            var dt = update.time - deviceTime;
            if (dt < -0.5 || dt > 1)
            {
                continue;
            }

            var dx = update.sample.Position.X - cell.X;
            var dy = update.sample.Position.Y - cell.Y;
            var dz = update.sample.Position.Z - cell.Z;
            var distance = Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));
            if (distance > 2)
            {
                continue;
            }

            results.Add(new BuildPlacementFootprintUpdate(update.time, update.sample, dx, dy, dz, distance));
        }

        return results
            .OrderBy(static item => item.Distance)
            .ThenBy(static item => item.Time)
            .ThenBy(static item => item.Sample.Position.X)
            .ThenBy(static item => item.Sample.Position.Y)
            .ThenBy(static item => item.Sample.Position.Z)
            .ToList();
    }

    private static Vector3s ToBlockCell(Vector3f position) => new(
        (short)Math.Floor(position.X),
        (short)Math.Floor(position.Y),
        (short)Math.Floor(position.Z));

    private static bool PositionsMatch(Vector3s? position, Vector3s cell) =>
        position.HasValue && position.Value == cell;

    private static bool IsNonZero(Vector3s? position) =>
        position.HasValue && position.Value != default;

    private static string FormatVdataLowByte(ushort? vdata) =>
        vdata.HasValue ? ((byte)(vdata.Value & 0xFF)).ToString(CultureInfo.InvariantCulture) : "";

    private static string FormatVdataHighByte(ushort? vdata) =>
        vdata.HasValue ? ((byte)(vdata.Value >> 8)).ToString(CultureInfo.InvariantCulture) : "";

    private static string FormatSlopeExistingCornerCount(ushort? vdata) =>
        CountSlopeExistingCorners(vdata)?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static int? CountSlopeExistingCorners(ushort? vdata)
    {
        if (!vdata.HasValue)
        {
            return null;
        }

        var lowByte = (byte)(vdata.Value & 0xFF);
        var count = 0;
        for (var i = 0; i < SlopeCornerNames.Length; i++)
        {
            if ((lowByte & (1 << i)) == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static string FormatSlopeExistingCorners(ushort? vdata) =>
        string.Join("|", GetSlopeExistingCorners(vdata));

    private static string FormatSlopeMissingCorners(ushort? vdata) =>
        string.Join("|", GetSlopeMissingCorners(vdata));

    private static IReadOnlyList<string> GetSlopeExistingCorners(ushort? vdata)
    {
        if (!vdata.HasValue)
        {
            return [];
        }

        var lowByte = (byte)(vdata.Value & 0xFF);
        var corners = new List<string>(SlopeCornerNames.Length);
        for (var i = 0; i < SlopeCornerNames.Length; i++)
        {
            if ((lowByte & (1 << i)) == 0)
            {
                corners.Add(SlopeCornerNames[i]);
            }
        }

        return corners;
    }

    private static IReadOnlyList<string> GetSlopeMissingCorners(ushort? vdata)
    {
        if (!vdata.HasValue)
        {
            return [];
        }

        var lowByte = (byte)(vdata.Value & 0xFF);
        var corners = new List<string>(SlopeCornerNames.Length);
        for (var i = 0; i < SlopeCornerNames.Length; i++)
        {
            if ((lowByte & (1 << i)) != 0)
            {
                corners.Add(SlopeCornerNames[i]);
            }
        }

        return corners;
    }

    private static string FormatTeamBits(byte? ldata) =>
        ldata.HasValue ? (ldata.Value & 0x03).ToString(CultureInfo.InvariantCulture) : "";

    private static string FormatBlockTeam(byte? ldata)
    {
        if (!ldata.HasValue)
        {
            return "";
        }

        return (ldata.Value & 0x03) switch
        {
            1 => "Team1",
            2 => "Team2",
            _ => "Neutral"
        };
    }

    private static string FormatLdataFlags(byte? ldata) =>
        ldata.HasValue ? (ldata.Value & ~0x03).ToString(CultureInfo.InvariantCulture) : "";

    private static string FormatKeyHashes(IReadOnlyList<uint> keyHashes) =>
        string.Join("|", keyHashes.Select(static key => key.ToString("X8", CultureInfo.InvariantCulture)));

    private static string FormatKeyNames(ReplayAnalysis analysis, IReadOnlyList<uint> keyHashes) =>
        string.Join("|", keyHashes.Select(key => ResolveKeyName(analysis, key, key.ToString("X8", CultureInfo.InvariantCulture))));

    private static string ResolvePlayerNickname(ReplayAnalysis analysis, uint? playerId)
    {
        if (playerId is null)
        {
            return "";
        }

        return analysis.ZoneUpdates
            .SelectMany(static update => update.PlayerInfo)
            .LastOrDefault(player => player.PlayerId == playerId.Value)
            ?.Nickname ?? "";
    }

    private static string FormatStat(EndMatchPlayerStatsData? stats, string name) =>
        stats?.Stats.TryGetValue(name, out var value) == true ? value.ToString(CultureInfo.InvariantCulture) : "";

    private static string FormatAmmo(ReplayAnalysis analysis, IReadOnlyDictionary<uint, IReadOnlyList<AmmoData>> ammo) =>
        string.Join(";", ammo.Select(item => $"{ResolveKeyName(analysis, item.Key, item.Key.ToString("X8", CultureInfo.InvariantCulture))}:{string.Join("|", item.Value.Select(static ammoItem => $"{ammoItem.Index?.ToString(CultureInfo.InvariantCulture) ?? "?"}/{ammoItem.Mag?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""}/{ammoItem.Pool?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""}"))}"));

    private static string FormatEffects(ReplayAnalysis analysis, IReadOnlyDictionary<uint, ulong?> effects) =>
        string.Join("|", effects.Select(item => $"{item.Key.ToString("X8", CultureInfo.InvariantCulture)}:{ResolveKeyName(analysis, item.Key, item.Key.ToString("X8", CultureInfo.InvariantCulture))}:{item.Value?.ToString(CultureInfo.InvariantCulture) ?? ""}"));

    private static string FormatFloatDictionary(IReadOnlyDictionary<string, float> values) =>
        string.Join("|", values.Select(static item => $"{item.Key}:{item.Value.ToString("0.###", CultureInfo.InvariantCulture)}"));

    private static string FormatDevices(ReplayAnalysis analysis, IReadOnlyDictionary<int, DeviceDataRecord> devices) =>
        string.Join("|", devices.Select(item => $"{item.Key}:{item.Value.DeviceKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? ResolveKeyName(analysis, item.Value.DeviceKeyHash, "")}/{item.Value.TotalCost?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""}/{item.Value.CostInc?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""}"));

    private static string FormatSupply(ReplayAnalysis analysis, SupplyInfoData? supply)
    {
        if (supply is null)
        {
            return "";
        }

        var name = ResolveKeyName(analysis, supply.NextSupplyDropKeyHash, supply.NextSupplyDropKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "");
        var time = supply.NextSupplyDropTime?.ToString(CultureInfo.InvariantCulture) ?? "";
        var position = supply.Position is null ? "" : $"{supply.Position.Value.XText}/{supply.Position.Value.YText}/{supply.Position.Value.ZText}";
        return string.Join(" ", new[] { name, time, position }.Where(static item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string FormatTeamStats(MatchTeamStatsData? stats)
    {
        if (stats is null)
        {
            return "";
        }

        return $"W:{stats.Warfare} C:{stats.Construction} T:{stats.Tactics} H:{stats.Healing}";
    }

    private static string FormatShots(IReadOnlyList<ShotData> shots) =>
        string.Join(";", shots.Select(static shot =>
        {
            var target = shot.TargetPosition is null ? "" : $"{shot.TargetPosition.Value.XText}/{shot.TargetPosition.Value.YText}/{shot.TargetPosition.Value.ZText}";
            var id = shot.ShotId?.ToString(CultureInfo.InvariantCulture) ?? "";
            return string.IsNullOrWhiteSpace(id) ? target : $"{target}#{id}";
        }));

    private static string FormatVector(Vector3s? value) =>
        value is null ? "unknown" : $"{value.Value.XText}/{value.Value.YText}/{value.Value.ZText}";

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

internal sealed class ReplayAnalysis
{
    public string SourcePath { get; init; } = "";
    public string Schema { get; set; } = "";
    public string SessionUtc { get; set; } = "";
    public int MaxPayloadBytes { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public double DurationSeconds => EndTime - StartTime;
    public int InitZoneRemainingBytes { get; set; }
    public int InitZonePayloadBytes { get; set; }
    public byte[]? InitZonePayload { get; set; }
    public bool InitZoneFullyCaptured => InitZoneRemainingBytes == InitZonePayloadBytes && InitZonePayloadBytes > 0;
    public string InitZoneFlags { get; set; } = "";
    public int InitZoneUnreadBytes { get; set; }
    public uint? MapKeyHash { get; set; }
    public ZoneInitDataRecord? InitZone { get; set; }
    public DecodedMapData? DecodedMap { get; set; }
    public int EndMatchResultPayloadBytes { get; set; }
    public MatchEnd? EndMatch { get; set; }
    public EndMatchResultData? EndMatchResult { get; set; }
    public List<ReplayPacket> Packets { get; } = [];
    public List<UnitCreateEvent> UnitCreates { get; } = [];
    public List<UnitMoveEvent> UnitMoves { get; } = [];
    public List<UnitUpdateEvent> UnitUpdates { get; } = [];
    public List<UnitDropEvent> UnitDrops { get; } = [];
    public List<UnitManeuverEvent> UnitManeuvers { get; } = [];
    public List<DamageEvent> Damages { get; } = [];
    public List<KillEvent> Kills { get; } = [];
    public List<ImpactEvent> Impacts { get; } = [];
    public List<ZoneEventEvent> ZoneEvents { get; } = [];
    public List<ProjectileCreateEvent> ProjectileCreates { get; } = [];
    public List<ProjectileMoveEvent> ProjectileMoves { get; } = [];
    public List<ProjectileDropEvent> ProjectileDrops { get; } = [];
    public List<CastEvent> Casts { get; } = [];
    public List<AbilityCastEvent> AbilityCasts { get; } = [];
    public List<BuildStartEvent> BuildStarts { get; } = [];
    public List<BuildCancelEvent> BuildCancels { get; } = [];
    public List<DeviceBuiltEvent> DevicesBuilt { get; } = [];
    public List<ZoneUpdateEvent> ZoneUpdates { get; } = [];
    public List<BlockUpdatesEvent> BlockUpdates { get; } = [];
    public List<BlockMinedEvent> BlockMined { get; } = [];
    public List<BarrierUpdateEvent> BarrierUpdates { get; } = [];
    public List<ReloadEvent> ReloadEvents { get; } = [];
    public List<ChannelEvent> ChannelEvents { get; } = [];
    public List<DashChargeEvent> DashChargeEvents { get; } = [];
    public List<PickupTakenEvent> PickupTakenEvents { get; } = [];
    public List<RecallEvent> RecallEvents { get; } = [];
    public List<PortalTeleportEvent> PortalTeleports { get; } = [];
    public List<KickPlayerEvent> KickPlayerEvents { get; } = [];
    public List<RpcResultEvent> RpcResults { get; } = [];
    public List<SurrenderEvent> SurrenderEvents { get; } = [];
    public List<SurrenderProgressEvent> SurrenderProgress { get; } = [];
    public List<DecodeError> DecodeErrors { get; } = [];
    public IReadOnlyDictionary<uint, string> KeyNames { get; set; } = new Dictionary<uint, string>();
    public IReadOnlyDictionary<ushort, string> BlockNames { get; set; } = new Dictionary<ushort, string>();
}

internal static class ReplayValidation
{
    public static ReplayValidationReport Evaluate(ReplayAnalysis analysis)
    {
        var required = new List<ReplayValidationCheck>
        {
            Check("Capture has packets", analysis.Packets.Count > 0, $"{analysis.Packets.Count} packet(s)"),
            Check("InitZone fully captured", analysis.InitZoneFullyCaptured, $"{analysis.InitZonePayloadBytes}/{analysis.InitZoneRemainingBytes} bytes"),
            Check("Map resolved", analysis.MapKeyHash is not null, analysis.MapKeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "missing"),
            Check("No decode errors", analysis.DecodeErrors.Count == 0, $"{analysis.DecodeErrors.Count} error(s)"),
            Check("Units created", analysis.UnitCreates.Count > 0, $"{analysis.UnitCreates.Count} unit create event(s)"),
            Check("Movement captured", analysis.UnitMoves.Count > 0, $"{analysis.UnitMoves.Count} move event(s)"),
            Check("Match end captured", analysis.EndMatch is not null, analysis.EndMatch?.WinnerTeam ?? "missing"),
            Check("End result captured", analysis.EndMatchResultPayloadBytes > 0, $"{analysis.EndMatchResultPayloadBytes} byte(s)"),
            Check("End result decoded", analysis.EndMatchResult is not null, $"{analysis.EndMatchResult?.Players.Count ?? 0} player result(s)")
        };

        var playerInfoCount = analysis.ZoneUpdates.Sum(static update => update.PlayerInfo.Count);
        var playerUnitLinks = ReplayIdentityBuilder.BuildPlayerUnitIdentities(analysis).Count;
        var unitsWithNames = analysis.UnitCreates.Count(unit => unit.KeyHash is not null && analysis.KeyNames.ContainsKey(unit.KeyHash.Value));
        var warningChecks = new List<ReplayValidationCheck>
        {
            Check("Player info available", playerInfoCount > 0, $"{playerInfoCount} player record(s)"),
            Check("Player units linked", playerUnitLinks > 0, $"{playerUnitLinks} player/unit link(s)"),
            Check("Key names resolved", analysis.KeyNames.Count > 0, $"{analysis.KeyNames.Count} key name(s)"),
            Check("Block names resolved", analysis.BlockNames.Count > 0, $"{analysis.BlockNames.Count} block name(s)"),
            Check("Most unit names resolved", analysis.UnitCreates.Count == 0 || unitsWithNames >= Math.Max(1, analysis.UnitCreates.Count / 2), $"{unitsWithNames}/{analysis.UnitCreates.Count} unit key(s)"),
            Check("Combat events captured", analysis.Damages.Count + analysis.Impacts.Count + analysis.ZoneEvents.Count > 0, $"{analysis.Damages.Count} damage, {analysis.Impacts.Count} impacts, {analysis.ZoneEvents.Count} zone events"),
            Check("Health updates captured", analysis.UnitUpdates.Count > 0, $"{analysis.UnitUpdates.Count} update event(s)"),
            Check("Map metadata decoded", analysis.InitZone?.Map?.Size is not null, FormatVector(analysis.InitZone?.Map?.Size)),
            Check("Initial map bytes available", analysis.InitZone?.MapData is { Length: > 0 } || analysis.InitZone?.Map?.BlocksData is { Length: > 0 }, $"{analysis.InitZone?.MapData?.Length ?? 0} init bytes, {analysis.InitZone?.Map?.BlocksData?.Length ?? 0} map bytes"),
            Check("Initial map blocks decoded", analysis.DecodedMap?.NonEmptyBlocks.Count > 0, $"{analysis.DecodedMap?.NonEmptyBlocks.Count ?? 0}/{analysis.DecodedMap?.BlockCount ?? 0} non-empty block(s)")
        };

        var coverage = new List<ReplayCoverageItem>
        {
            new("duration_seconds", analysis.DurationSeconds.ToString("0.000", CultureInfo.InvariantCulture)),
            new("packets", analysis.Packets.Count.ToString(CultureInfo.InvariantCulture)),
            new("packet_types", analysis.Packets.Select(static packet => packet.Event).Distinct(StringComparer.Ordinal).Count().ToString(CultureInfo.InvariantCulture)),
            new("units", analysis.UnitCreates.Count.ToString(CultureInfo.InvariantCulture)),
            new("unit_moves", analysis.UnitMoves.Count.ToString(CultureInfo.InvariantCulture)),
            new("unit_updates", analysis.UnitUpdates.Count.ToString(CultureInfo.InvariantCulture)),
            new("unit_update_ammo_entries", analysis.UnitUpdates.Sum(static item => item.Ammo.Sum(static ammo => ammo.Value.Count)).ToString(CultureInfo.InvariantCulture)),
            new("unit_update_gear_changes", analysis.UnitUpdates.Count(static item => item.CurrentGearKeyHash is not null).ToString(CultureInfo.InvariantCulture)),
            new("unit_update_ability_states", analysis.UnitUpdates.Count(static item => item.AbilityKeyHash is not null || item.AbilityCharges is not null).ToString(CultureInfo.InvariantCulture)),
            new("unit_update_buff_entries", analysis.UnitUpdates.Sum(static item => item.Buffs.Count).ToString(CultureInfo.InvariantCulture)),
            new("unit_update_effect_entries", analysis.UnitUpdates.Sum(static item => item.Effects.Count).ToString(CultureInfo.InvariantCulture)),
            new("unit_update_device_entries", analysis.UnitUpdates.Sum(static item => item.Devices.Count).ToString(CultureInfo.InvariantCulture)),
            new("unit_drops", analysis.UnitDrops.Count.ToString(CultureInfo.InvariantCulture)),
            new("unit_maneuvers", analysis.UnitManeuvers.Count.ToString(CultureInfo.InvariantCulture)),
            new("damage_events", analysis.Damages.Count.ToString(CultureInfo.InvariantCulture)),
            new("kills", analysis.Kills.Count.ToString(CultureInfo.InvariantCulture)),
            new("impacts", analysis.Impacts.Count.ToString(CultureInfo.InvariantCulture)),
            new("zone_events", analysis.ZoneEvents.Count.ToString(CultureInfo.InvariantCulture)),
            new("ability_casts", analysis.AbilityCasts.Count.ToString(CultureInfo.InvariantCulture)),
            new("build_starts", analysis.BuildStarts.Count.ToString(CultureInfo.InvariantCulture)),
            new("build_cancels", analysis.BuildCancels.Count.ToString(CultureInfo.InvariantCulture)),
            new("devices_built", analysis.DevicesBuilt.Count.ToString(CultureInfo.InvariantCulture)),
            new("projectile_creates", analysis.ProjectileCreates.Count.ToString(CultureInfo.InvariantCulture)),
            new("projectile_moves", analysis.ProjectileMoves.Count.ToString(CultureInfo.InvariantCulture)),
            new("projectile_drops", analysis.ProjectileDrops.Count.ToString(CultureInfo.InvariantCulture)),
            new("player_info_records", playerInfoCount.ToString(CultureInfo.InvariantCulture)),
            new("player_unit_links", playerUnitLinks.ToString(CultureInfo.InvariantCulture)),
            new("objective_records", analysis.ZoneUpdates.Sum(static update => update.Objectives.Count).ToString(CultureInfo.InvariantCulture)),
            new("block_updates", analysis.BlockUpdates.Sum(static update => update.Count).ToString(CultureInfo.InvariantCulture)),
            new("blocks_mined", analysis.BlockMined.Count.ToString(CultureInfo.InvariantCulture)),
            new("barrier_updates", analysis.BarrierUpdates.Count.ToString(CultureInfo.InvariantCulture)),
            new("map_size", FormatVector(analysis.InitZone?.Map?.Size)),
            new("map_spawn_points", (analysis.InitZone?.Map?.SpawnPoints.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
            new("map_static_units", (analysis.InitZone?.Map?.Units.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
            new("block_names", analysis.BlockNames.Count.ToString(CultureInfo.InvariantCulture)),
            new("init_map_data_bytes", (analysis.InitZone?.MapData?.Length ?? 0).ToString(CultureInfo.InvariantCulture)),
            new("init_color_data_bytes", (analysis.InitZone?.ColorData?.Length ?? 0).ToString(CultureInfo.InvariantCulture)),
            new("decoded_map_blocks", (analysis.DecodedMap?.BlockCount ?? 0).ToString(CultureInfo.InvariantCulture)),
            new("decoded_non_empty_map_blocks", (analysis.DecodedMap?.NonEmptyBlocks.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
            new("decoded_block_ids", (analysis.DecodedMap?.BlockCounts.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
            new("reload_events", analysis.ReloadEvents.Count.ToString(CultureInfo.InvariantCulture)),
            new("rpc_results", analysis.RpcResults.Count.ToString(CultureInfo.InvariantCulture)),
            new("surrender_events", analysis.SurrenderEvents.Count.ToString(CultureInfo.InvariantCulture)),
            new("surrender_progress", analysis.SurrenderProgress.Count.ToString(CultureInfo.InvariantCulture)),
            new("end_match_result_players", (analysis.EndMatchResult?.Players.Count ?? 0).ToString(CultureInfo.InvariantCulture))
        };

        var requiredPassed = required.Count(static check => check.Passed);
        var warnings = warningChecks.Where(static check => !check.Passed).ToArray();
        var quality = requiredPassed == required.Count && warnings.Length == 0
            ? "good"
            : requiredPassed == required.Count
                ? "usable_with_warnings"
                : "not_usable";

        return new ReplayValidationReport(
            requiredPassed == required.Count,
            quality,
            requiredPassed,
            required.Count,
            required,
            warnings,
            coverage);
    }

    private static ReplayValidationCheck Check(string name, bool passed, string detail) => new(name, passed, detail);

    private static string FormatVector(Vector3s? value) =>
        value is null ? "unknown" : $"{value.Value.XText}/{value.Value.YText}/{value.Value.ZText}";
}

internal sealed record ReplayValidationReport(
    bool UsableForReplay,
    string Quality,
    int RequiredPassed,
    int RequiredTotal,
    IReadOnlyList<ReplayValidationCheck> RequiredChecks,
    IReadOnlyList<ReplayValidationCheck> Warnings,
    IReadOnlyList<ReplayCoverageItem> Coverage);

internal sealed record ReplayValidationCheck(string Name, bool Passed, string Detail);
internal sealed record ReplayCoverageItem(string Name, string Value);

internal static class ReplayIdentityBuilder
{
    public static IReadOnlyList<PlayerUnitIdentity> BuildPlayerUnitIdentities(ReplayAnalysis analysis)
    {
        var players = analysis.ZoneUpdates
            .SelectMany(static update => update.PlayerInfo)
            .GroupBy(static player => player.PlayerId)
            .ToDictionary(static group => group.Key, static group => group.Last());

        var stats = analysis.ZoneUpdates
            .SelectMany(static update => update.Stats?.PlayerStats ?? [])
            .GroupBy(static player => player.PlayerId)
            .ToDictionary(static group => group.Key, static group => group.Last());

        return analysis.UnitCreates
            .Where(static unit => unit.PlayerId is not null)
            .GroupBy(static unit => unit.PlayerId!.Value)
            .OrderBy(static group => group.Key)
            .Select(group =>
            {
                var unit = group.OrderBy(static item => item.Time).First();
                players.TryGetValue(group.Key, out var player);
                stats.TryGetValue(group.Key, out var playerStats);
                var team = string.IsNullOrWhiteSpace(unit.Team) ? playerStats?.Team : unit.Team;
                return new PlayerUnitIdentity(
                    group.Key,
                    player?.Nickname,
                    player?.SteamId,
                    team,
                    unit.UnitId,
                    unit.KeyHash,
                    ReplayReportWriter.ResolveKeyName(analysis, unit.KeyHash),
                    unit.SkinKeyHash,
                    ReplayReportWriter.ResolveKeyName(analysis, unit.SkinKeyHash),
                    unit.GearKeyHashes,
                    unit.GearKeyHashes.Select(key => ReplayReportWriter.ResolveKeyName(analysis, key, key.ToString("X8", CultureInfo.InvariantCulture))).ToArray(),
                    unit.Controlled);
            })
            .ToArray();
    }
}

internal static partial class KeyNameResolver
{
    private static readonly Regex CandidateIdRegex = CandidateIdPattern();

    public static IReadOnlyDictionary<uint, string> Load(string capturePath)
    {
        var cdbPath = FindCatalogueCache(capturePath);
        if (cdbPath is null)
        {
            return new Dictionary<uint, string>();
        }

        try
        {
            using var input = File.OpenRead(cdbPath);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var memory = new MemoryStream();
            zlib.CopyTo(memory);
            var text = Encoding.UTF8.GetString(memory.ToArray());
            var names = new Dictionary<uint, string>();

            foreach (Match match in CandidateIdRegex.Matches(text))
            {
                var id = match.Value;
                var hash = Crc32.Compute(id);
                names.TryAdd(hash, id);
            }

            return names;
        }
        catch
        {
            return new Dictionary<uint, string>();
        }
    }

    private static string? FindCatalogueCache(string capturePath)
    {
        var current = new FileInfo(capturePath).Directory;
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Cache", "cdb");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    [GeneratedRegex(@"\b(?:unit|hero|map|block|device|gear|impact|projectile|effect|ability|tool|skin|material|rubble|mode|gamemode|perk|badge|notification|reward|challenge|achievement|loot|shop|chat|movement|damage|global|settings|league|match|vibration|steam|booster|category|strings)[a-z0-9_]*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CandidateIdPattern();
}

internal static class BlockNameResolver
{
    public static IReadOnlyDictionary<ushort, string> Load(string capturePath)
    {
        var cdbPath = FindCatalogueCache(capturePath);
        if (cdbPath is null)
        {
            return new Dictionary<ushort, string>();
        }

        try
        {
            using var input = File.OpenRead(cdbPath);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var memory = new MemoryStream();
            zlib.CopyTo(memory);
            return ReadBlockCards(memory.ToArray());
        }
        catch
        {
            return new Dictionary<ushort, string>();
        }
    }

    private static IReadOnlyDictionary<ushort, string> ReadBlockCards(byte[] bytes)
    {
        var names = new Dictionary<ushort, string>();

        for (var offset = 0; offset < bytes.Length - 5; offset++)
        {
            if (bytes[offset] != 1)
            {
                continue;
            }

            if (!TryReadBlockCard(bytes, offset, out var blockId, out var cardId))
            {
                continue;
            }

            names.TryAdd(blockId, cardId);
        }

        return names;
    }

    private static bool TryReadBlockCard(byte[] bytes, int offset, out ushort blockId, out string cardId)
    {
        blockId = 0;
        cardId = "";

        var position = offset + 1;
        if (position + 4 > bytes.Length)
        {
            return false;
        }

        var flagsOffset = position;
        position += 4;

        try
        {
            if (GetBit(bytes, flagsOffset, 0))
            {
                cardId = ReadString(bytes, ref position);
            }

            if (!IsBlockCardId(cardId))
            {
                return false;
            }

            if (GetBit(bytes, flagsOffset, 1))
            {
                position += 1;
            }

            for (var bit = 2; bit <= 5; bit++)
            {
                if (GetBit(bytes, flagsOffset, bit))
                {
                    position += 4;
                }
            }

            if (GetBit(bytes, flagsOffset, 6))
            {
                position += 4;
            }

            if (GetBit(bytes, flagsOffset, 7))
            {
                _ = ReadString(bytes, ref position);
            }

            if (!GetBit(bytes, flagsOffset, 8) || position + 2 > bytes.Length)
            {
                return false;
            }

            blockId = BitConverter.ToUInt16(bytes, position);
            return blockId < 10000;
        }
        catch
        {
            return false;
        }
    }

    private static bool GetBit(byte[] bytes, int offset, int index) =>
        (bytes[offset + (index >> 3)] & (0x80 >> (index & 7))) != 0;

    private static string ReadString(byte[] bytes, ref int position)
    {
        var length = ReadSize(bytes, ref position);
        if (length < 0 || length > 512 || position + length > bytes.Length)
        {
            throw new InvalidDataException("Invalid catalogue string length.");
        }

        var value = Encoding.UTF8.GetString(bytes, position, length);
        position += length;
        return value;
    }

    private static int ReadSize(byte[] bytes, ref int position)
    {
        var value = 0;
        var shift = 0;

        for (var index = 0; index < 5; index++)
        {
            if (position >= bytes.Length)
            {
                throw new EndOfStreamException();
            }

            var item = bytes[position++];
            if ((item & 0x80) == 0)
            {
                return value | (item << (shift & 31));
            }

            value |= (item & 0x7F) << (shift & 31);
            shift += 7;
        }

        throw new InvalidDataException("Invalid catalogue size value.");
    }

    private static bool IsBlockCardId(string value) =>
        (value.StartsWith("block_", StringComparison.OrdinalIgnoreCase) || value.StartsWith("device_block_", StringComparison.OrdinalIgnoreCase))
        && value.All(static item => char.IsAsciiLetterOrDigit(item) || item == '_');

    private static string? FindCatalogueCache(string capturePath)
    {
        var current = new FileInfo(capturePath).Directory;
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Cache", "cdb");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}

internal static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(string value)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var item in Encoding.UTF8.GetBytes(value))
        {
            crc = Table[(crc ^ item) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var crc = i;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 1 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }
}

internal sealed class ReplayPacket
{
    public double Time { get; init; }
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string Event { get; init; } = "";
    public int Remaining { get; init; }
    public int PayloadBytes { get; init; }
}

internal sealed record ZoneInitDataRecord(
    uint? MapKeyHash,
    MapDataRecord? Map,
    byte[]? MapData,
    byte[]? ColorData,
    IReadOnlyList<InitialBlockUpdate> Updates,
    bool? CanSwitchHero,
    bool? IsCustomGame);

internal sealed record MapDataRecord(
    string Flags,
    int? Version,
    int? Schema,
    string? Match,
    IReadOnlyList<Color32Data> ColorPalette,
    IReadOnlyList<MapSpawnPointData> SpawnPoints,
    IReadOnlyList<MapUnitData> Units,
    IReadOnlyList<MapCameraData> Cameras,
    IReadOnlyList<MapTriggerData> Triggers,
    MapDataPropsData? Properties,
    Vector3s? Size,
    byte[]? BlocksData,
    byte[]? ColorsData);

internal sealed record Color32Data(byte R, byte G, byte B, byte A);
internal sealed record MapSpawnPointData(string? Team, Vector3f? Position, string? Direction, string? Label);
internal sealed record MapUnitData(Vector3f? Position, Vector3s? Rotation, uint? UnitKeyHash, string? Team);
internal sealed record MapCameraData(Vector3f? Direction, Vector3f? Position, string? Team, IReadOnlyList<string> Labels);
internal sealed record MapTriggerData(string Type, string? Tag, IReadOnlyList<string> Labels, Vector3f? Position, Vector3f? Size, float? Radius);
internal sealed record MapDataPropsData(string? AudioAmbience, string? Render, string? Plane, float? PlanePosition, float? KillPosition, float? Barrier1Team1, float? Barrier1Team2, float? Barrier2Team1, float? Barrier2Team2, float? MinFallHeight, float? MaxFallHeight, float? BuildTime, float? StartingResources);
internal sealed record InitialBlockUpdate(Vector3s Position, ushort? Id, byte? Damage, ushort? Vdata, byte? Ldata);
internal sealed record DecodedMapData(Vector3s Size, int BlockCount, int DecodedBytes, int ColorBytes, IReadOnlyList<DecodedMapBlock> NonEmptyBlocks, IReadOnlyList<DecodedMapBlockCount> BlockCounts);
internal sealed record DecodedMapBlock(Vector3s Position, ushort Id, byte Damage, ushort Vdata, byte Ldata, byte? Color);
internal sealed record DecodedMapBlockCount(ushort Id, int Count);

internal sealed record UnitInitData(uint? KeyHash, ZoneTransformData? Transform, bool Controlled, uint? OwnerId, string Team, uint? PlayerId, uint? SkinKeyHash, IReadOnlyList<uint> GearKeyHashes);
internal sealed record UnitCreateEvent(double Time, uint UnitId, uint? KeyHash, string Team, uint? PlayerId, uint? OwnerId, bool Controlled, ZoneTransformData? Transform, uint? SkinKeyHash, IReadOnlyList<uint> GearKeyHashes)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
    public string KeyHashText => KeyHash?.ToString("X8", CultureInfo.InvariantCulture) ?? "";
}

internal sealed record PlayerUnitIdentity(uint PlayerId, string? Nickname, ulong? SteamId, string? Team, uint? UnitId, uint? UnitKeyHash, string? UnitName, uint? SkinKeyHash, string? SkinName, IReadOnlyList<uint> GearKeyHashes, IReadOnlyList<string> GearNames, bool? Controlled);

internal sealed record UnitMoveEvent(double Time, uint UnitId, ulong ServerTime, ZoneTransformData Transform)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record UnitUpdateEvent(
    double Time,
    uint UnitId,
    string? Team,
    float? Health,
    float? Forcefield,
    float? Shield,
    float? CapturePoints,
    float? Resource,
    bool? MovementActive,
    IReadOnlyDictionary<uint, IReadOnlyList<AmmoData>> Ammo,
    uint? CurrentGearKeyHash,
    uint? AbilityKeyHash,
    int? AbilityCharges,
    ulong? AbilityChargeCooldownEnd,
    IReadOnlyDictionary<uint, ulong?> Effects,
    IReadOnlyDictionary<string, float> Buffs,
    IReadOnlyDictionary<int, DeviceDataRecord> Devices,
    uint? TurretTargetId,
    IReadOnlyList<Vector3s> CloudAffectedBlocks,
    float? ProjectileInitSpeed,
    ulong? BombTimeoutEnd,
    IReadOnlyList<uint> DamageCapturers,
    PortalLinkData? PortalLink,
    string? TeslaCharge,
    string Flags)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}
internal sealed record AmmoData(int? Index, float? Mag, float? Pool);
internal sealed record DeviceDataRecord(uint? DeviceKeyHash, float? TotalCost, float? CostInc);
internal sealed record PortalLinkData(uint? LinkedPortalUnitId);

internal sealed record UnitDropEvent(double Time, uint UnitId)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record UnitManeuverEvent(double Time, uint UnitId, ManeuverData Maneuver)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record ManeuverData(byte Type, string Name, Vector3f? Position, Vector3f? OriginPosition, uint? OriginUnitId, float? Force, float? MidairForce, float? DirectionAngle, float? Distance, float? Time, float? RotationTime, bool? Enabled);

internal sealed record DamageEvent(double Time, uint? TargetUnitId, uint? SourceUnitId, Vector3f? SourcePosition, uint? ImpactKeyHash, float? Damage, float? InitialDamage, bool Crit)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record KillEvent(double Time, uint? DeadUnitId, uint? DeadPlayerId, uint? KillerPlayerId, IReadOnlyList<uint> Assistants, uint? DamageSourceKeyHash, Vector3f? SourcePosition, bool Crit)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record ImpactEvent(double Time, Vector3f? InsidePoint, Vector3s? Normal, uint? CasterUnitId, uint? CasterPlayerId, uint? ImpactKeyHash, uint? SourceKeyHash, IReadOnlyList<uint> HitUnits, Vector3f? ShotPosition, bool Crit)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record ZoneEventEvent(double Time, byte EventType, string Name, uint? UnitId, uint? TurretId, byte? ToolIndex, bool? Active, Vector3f? Position)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record ProjectileInfoData(uint? ProjectileKeyHash, ZoneTransformData? Transform, float? Speed, uint? OwnerUnitId, string? OwnerTeam);

internal sealed record ProjectileCreateEvent(double Time, ulong ProjectileId, ProjectileInfoData Info)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string ProjectileIdText => ProjectileId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record ProjectileMoveEvent(double Time, ulong ProjectileId, ulong ServerTime, ZoneTransformData Transform)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string ProjectileIdText => ProjectileId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record ProjectileDropEvent(double Time, ulong ProjectileId)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string ProjectileIdText => ProjectileId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record ShotData(Vector3f? TargetPosition, ulong? ShotId);
internal sealed record CastData(byte? ToolIndex, Vector3f? ShotPosition, IReadOnlyList<ShotData> Shots, float? UnitProjectileSpeed);

internal sealed record CastEvent(double Time, uint UnitId, CastData Data)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record AbilityCastEvent(double Time, uint UnitId, uint? AbilityKeyHash, Vector3f? ShotPosition, IReadOnlyList<ShotData> Shots)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record ChannelEvent(double Time, string Phase, uint UnitId, byte? ToolIndex, Vector3f? HitPosition, Vector3s? TargetBlock, uint? TargetUnitId)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record DashChargeEvent(double Time, string Phase, uint UnitId, byte ToolIndex)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record PickupTakenEvent(double Time, uint PlayerId, uint PickupKeyHash)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record RecallEvent(double Time, string Phase, uint UnitId, float? Duration, ulong? EndTime)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record PortalTeleportEvent(double Time, uint UnitId, uint PortalFromId, uint PortalToId)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record KickPlayerEvent(double Time, ulong PlayerId, string Reason)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record BuildStartEvent(double Time, uint UnitId, byte? ToolIndex, uint? DeviceKeyHash, Vector3s? InsidePosition, Vector3s? OutsidePosition, string? Direction, bool? ShowGhost)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record BuildCancelEvent(double Time, uint UnitId)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record DeviceBuiltEvent(double Time, uint UnitId, uint DeviceKeyHash, Vector3f Position)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record BuildPlacementData(DeviceBuiltEvent DeviceBuilt, BuildStartEvent? BuildStart, Vector3s Cell, double? BlockUpdateTime, BlockUpdateSample? BlockUpdate, IReadOnlyList<BuildPlacementFootprintUpdate> FootprintUpdates);
internal sealed record BuildPlacementFootprintUpdate(double Time, BlockUpdateSample Sample, int Dx, int Dy, int Dz, int Distance);
internal readonly record struct BlockUpdateKey(double Time, Vector3s Position);
internal sealed record MapStateTimelineItem(int Sequence, double Time, string Source, BlockUpdateSample Update, BuildPlacementData? Placement)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}
internal readonly record struct MapBlockState(ushort Id, byte Damage, ushort Vdata, byte Ldata);
internal sealed record MapStateChangedCell(Vector3s Position, int UpdateCount, MapBlockState Initial, MapBlockState Final);
internal sealed record MapStateVerificationData(
    int InitialNonEmptyBlockCount,
    int FinalNonEmptyBlockCount,
    int InitialTrackedCellCount,
    int FinalTrackedCellCount,
    int RepeatedCellCount,
    int DuplicateNoOpUpdates,
    int OutOfOrderUpdates,
    IReadOnlyList<DecodedMapBlockCount> FinalBlockCounts,
    IReadOnlyList<MapStateChangedCell> ChangedCells);

internal sealed record ZoneUpdateEvent(double Time, string Flags, ZonePhaseData? Phase, MatchStatsData? Stats, IReadOnlyList<SpawnPointData> SpawnPoints, IReadOnlyList<PlayerSpawnPointData> PlayerSpawnPoints, IReadOnlyList<RespawnInfoData> RespawnInfo, IReadOnlyList<ZonePlayerInfoData> PlayerInfo, SupplyInfoData? SupplyInfo, IReadOnlyList<ZoneObjectiveData> Objectives, float? ResourceCap)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record ZonePhaseData(string? PhaseType, long? StartTime, long? EndTime);
internal sealed record MatchStatsData(IReadOnlyList<MatchPlayerStatsData> PlayerStats, MatchTeamStatsData? Team1Stats, MatchTeamStatsData? Team2Stats);
internal sealed record MatchPlayerStatsData(uint PlayerId, string? Team, int? Kills, int? Deaths, int? Assists);
internal sealed record MatchTeamStatsData(int? Warfare, int? Construction, int? Tactics, int? Healing);
internal sealed record SpawnPointData(uint? Id, string? Team, Vector3f? Position, string? LockType, uint? Owner);
internal sealed record PlayerSpawnPointData(uint PlayerId, uint? SpawnPointId);
internal sealed record RespawnInfoData(uint PlayerId, ulong RespawnTime);
internal sealed record ZonePlayerInfoData(uint PlayerId, string? Nickname, ulong? SteamId, ulong? SquadId, bool? LookingForFriends);
internal sealed record SupplyInfoData(uint? NextSupplyDropKeyHash, ulong? NextSupplyDropTime, Vector3f? Position);
internal sealed record ZoneObjectiveData(string? Team, int? Id, int? Counter, int? RequiredCounter);

internal sealed record BlockUpdatesEvent(double Time, int Count, IReadOnlyList<BlockUpdateSample> Samples, IReadOnlyList<BlockUpdateSample> Updates)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record BlockMinedEvent(double Time, uint UnitId, uint BlockKeyHash)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record BarrierUpdateEvent(double Time, IReadOnlyList<string> Labels)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record ReloadEvent(double Time, string Phase, uint UnitId)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
    public string UnitIdText => UnitId.ToString(CultureInfo.InvariantCulture);
}

internal sealed record RpcResultEvent(double Time, string Name, ushort RpcId, string Status, string Value)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record SurrenderEvent(double Time, string Phase, string? Team, ulong? Deadline, bool? Accepted, string? Detail)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record SurrenderProgressEvent(double Time, IReadOnlyList<SurrenderVoteData> Votes)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}

internal sealed record SurrenderVoteData(uint PlayerId, bool? Voted);
internal sealed record EndMatchResultData(
    double Time,
    string Flags,
    float? MatchSeconds,
    IReadOnlyList<EndMatchPlayerData> Players,
    bool? IsWinner,
    bool? IsBackfiller,
    bool? IsAfk,
    uint? HeroKeyHash,
    uint? SkinKeyHash,
    XpInfoData? OldHeroXp,
    XpInfoData? OldPlayerXp,
    XpInfoData? NewHeroXp,
    float? RewardXp,
    IReadOnlyDictionary<string, float> OldCurrency,
    IReadOnlyDictionary<string, float> RewardCurrency,
    IReadOnlyDictionary<string, float> RewardBonuses,
    float? XpBoost,
    float? GoldBoost,
    string? RankedStatus,
    RankedMatchResultData? RankedData,
    IReadOnlyList<ChallengeDiffData> Challenges,
    TimeTrialResultData? TimeTrialData,
    uint? LootCrateKeyHash)
{
    public string TimeText => Time.ToString("0.000", CultureInfo.InvariantCulture);
}
internal sealed record EndMatchPlayerData(uint? PlayerId, ulong? SquadId, bool? Backfiller, bool? Noob, EndMatchPlayerStatsData? Stats, uint? MedalPositiveKeyHash, uint? MedalNegativeKeyHash);
internal sealed record EndMatchPlayerStatsData(IReadOnlyDictionary<string, int> Stats, int? Total);
internal sealed record XpInfoData(int? Level, float? LevelXp, float? XpForNextLevel);
internal sealed record RankedMatchResultData(int? LeagueTierOld, int? LeagueTierNew, int? LeagueDivisionOld, int? LeagueDivisionNew, int? LeaguePointsOld, int? LeaguePointsNew);
internal sealed record ChallengeDiffData(uint? KeyHash, bool? Completed, ChallengeResultData? OldResult, ChallengeResultData? NewResult, ChallengeFriendInfoData? FriendInfo, bool? BetterThanFriend);
internal sealed record ChallengeResultData(float? TotalValue, int? MatchesSpent, float? MatchSecondsSpent);
internal sealed record ChallengeFriendInfoData(uint? Id, string? Name, ChallengeResultData? Result);
internal sealed record TimeTrialResultData(IReadOnlyList<int> OldGoalsCompleted, IReadOnlyList<int> NewGoalsCompleted, float? XpReward, IReadOnlyDictionary<string, float> CurrencyReward, float? ResultTime, float? BestResultTime);
internal sealed record BlockUpdateSample(Vector3s Position, ushort? Id, byte? Damage, ushort? Vdata, byte? Ldata);
internal sealed record BlockUpdateData(ushort? Id, byte? Damage, ushort? Vdata, byte? Ldata);
internal sealed record MatchEnd(double Time, string WinnerTeam);
internal sealed record DecodeError(double Time, string Event, string Message);
internal sealed record TimelineEvent(double Time, string Kind, string Text);

internal sealed record ZoneTransformData(Vector3f? Position, Vector3s? Rotation, Vector3s? LocalVelocity, bool? IsCrouch, bool? IsJump, bool? IsSprint, bool? IsWallClimb, bool? IsDash, bool? IsGroundSlam, bool? NoInterpolation);
internal readonly record struct Vector3f(float X, float Y, float Z)
{
    public string XText => X.ToString("0.###", CultureInfo.InvariantCulture);
    public string YText => Y.ToString("0.###", CultureInfo.InvariantCulture);
    public string ZText => Z.ToString("0.###", CultureInfo.InvariantCulture);
}

internal readonly record struct Vector3s(short X, short Y, short Z)
{
    public string XText => X.ToString(CultureInfo.InvariantCulture);
    public string YText => Y.ToString(CultureInfo.InvariantCulture);
    public string ZText => Z.ToString(CultureInfo.InvariantCulture);
}

internal static class JsonElementExtensions
{
    public static string? GetStringOrDefault(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;

    public static int GetIntOrDefault(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.GetInt32() : 0;

    public static double GetDoubleOrDefault(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.GetDouble() : 0d;
}
