namespace BnlCommunityFixes
{
    public static class MatchReplayRecorderRuntime
    {
        private static bool enabled;
        private static bool capturePayload;
        private static bool recordCustomGames = true;
        private static bool recordCasualGames = true;
        private static bool recordRankedGames = true;
        private static int maxPayloadBytes = 262144;
        private static string sessionId;
        private static string outputPath;
        private static System.IO.StreamWriter writer;
        private static int failureCount;
        private static bool shutdownHooksRegistered;

        public static void Configure(bool isEnabled, int payloadByteLimit, bool shouldCapturePayload, bool shouldRecordCustomGames, bool shouldRecordCasualGames, bool shouldRecordRankedGames)
        {
            enabled = isEnabled;
            maxPayloadBytes = UnityEngine.Mathf.Clamp(payloadByteLimit, 0, 1048576);
            capturePayload = shouldCapturePayload && maxPayloadBytes > 0;
            recordCustomGames = shouldRecordCustomGames;
            recordCasualGames = shouldRecordCasualGames;
            recordRankedGames = shouldRecordRankedGames;
            RegisterShutdownHooks();

            if (!enabled)
            {
                EndSession("disabled");
            }
        }

        private static void RegisterShutdownHooks()
        {
            if (shutdownHooksRegistered)
            {
                return;
            }

            shutdownHooksRegistered = true;
            try
            {
                System.AppDomain.CurrentDomain.ProcessExit += delegate { EndSession("process_exit"); };
                System.AppDomain.CurrentDomain.DomainUnload += delegate { EndSession("domain_unload"); };
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }

        public static void RecordPacket(string eventName, System.IO.BinaryReader reader)
        {
            if (!enabled || failureCount > 5)
            {
                return;
            }

            if (eventName == "Recv_InitZone")
            {
                EndSession("new_init_zone");
                string matchScope;
                if (!ShouldRecordInitZone(reader, out matchScope))
                {
                    UnityEngine.Debug.Log("BNL match replay recorder skipped " + matchScope + " match by filter.");
                    return;
                }

                StartSession(eventName, matchScope);
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                long position = -1;
                long length = -1;
                long remaining = -1;
                string payload = string.Empty;
                int payloadBytes = 0;

                System.IO.Stream stream = reader == null ? null : reader.BaseStream;
                if (stream != null && stream.CanSeek)
                {
                    position = stream.Position;
                    length = stream.Length;
                    remaining = length - position;

                    if (capturePayload && remaining > 0)
                    {
                        int bytesToRead = (int)System.Math.Min(System.Math.Min(remaining, maxPayloadBytes), int.MaxValue);
                        byte[] buffer = reader.ReadBytes(bytesToRead);
                        payloadBytes = buffer.Length;
                        payload = System.Convert.ToBase64String(buffer);
                        stream.Position = position;
                    }
                }

                string line = "{\"kind\":\"zone_packet\",\"utc\":\"" +
                    Escape(System.DateTime.UtcNow.ToString("o")) + "\",\"t\":" +
                    UnityEngine.Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"event\":\"" + Escape(eventName) + "\",\"pos\":" +
                    position.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"len\":" +
                    length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"remaining\":" +
                    remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"payloadBytes\":" +
                    payloadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (!string.IsNullOrEmpty(payload))
                {
                    line += ",\"payloadBase64\":\"" + payload + "\"";
                }

                WriteLine(line + "}");

                if (IsTerminalMatchEvent(eventName))
                {
                    EndSession(eventName);
                }
            }
            catch (System.Exception ex)
            {
                failureCount++;
                if (failureCount <= 3)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        private static void StartSession(string startEvent, string matchScope)
        {
            if (!string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                failureCount = 0;
                sessionId = System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string baseDir = System.IO.Path.GetDirectoryName(typeof(MatchReplayRecorderRuntime).Assembly.Location);
                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = ".";
                }

                string replayDir = System.IO.Path.Combine(System.IO.Path.Combine(baseDir, ".."), "bnl-match-replays");
                replayDir = System.IO.Path.GetFullPath(replayDir);
                System.IO.Directory.CreateDirectory(replayDir);
                outputPath = System.IO.Path.Combine(replayDir, "zone-capture-" + sessionId + ".jsonl");
                var fileStream = new System.IO.FileStream(outputPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
                writer = new System.IO.StreamWriter(fileStream, System.Text.Encoding.UTF8);
                writer.AutoFlush = false;

                WriteLine("{\"schema\":\"bnl-zone-capture-v0\",\"kind\":\"session_start\",\"utc\":\"" +
                    Escape(System.DateTime.UtcNow.ToString("o")) + "\",\"unityTime\":" +
                    UnityEngine.Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"startEvent\":\"" + Escape(startEvent) + "\"" +
                    ",\"matchScope\":\"" + Escape(matchScope) + "\"" +
                    ",\"capturePayload\":" + (capturePayload ? "true" : "false") +
                    ",\"recordCustomGames\":" + (recordCustomGames ? "true" : "false") +
                    ",\"recordCasualGames\":" + (recordCasualGames ? "true" : "false") +
                    ",\"recordRankedGames\":" + (recordRankedGames ? "true" : "false") +
                    ",\"maxPayloadBytes\":" + maxPayloadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");

                UnityEngine.Debug.Log("BNL match replay recorder writing to: " + outputPath);
            }
            catch (System.Exception ex)
            {
                enabled = false;
                UnityEngine.Debug.LogException(ex);
            }
        }

        private static bool ShouldRecordInitZone(System.IO.BinaryReader reader, out string matchScope)
        {
            matchScope = "unknown";
            if (reader == null || reader.BaseStream == null || !reader.BaseStream.CanSeek)
            {
                matchScope = "casual";
                return recordCasualGames;
            }

            long position = reader.BaseStream.Position;
            try
            {
                Protocol.ZoneInitData data = new Protocol.ZoneInitData();
                data.Read(reader);

                if (data.IsCustomGame)
                {
                    matchScope = "custom";
                    return recordCustomGames;
                }

                if (IsRankedMatch())
                {
                    matchScope = "ranked";
                    return recordRankedGames;
                }

                matchScope = "casual";
                return recordCasualGames;
            }
            catch (System.Exception ex)
            {
                matchScope = "unknown";
                UnityEngine.Debug.Log("BNL match replay recorder could not classify InitZone, using casual filter: " + ex.Message);
                return recordCasualGames;
            }
            finally
            {
                try
                {
                    reader.BaseStream.Position = position;
                }
                catch
                {
                }
            }
        }

        private static bool IsRankedMatch()
        {
            try
            {
                ZoneData zoneData = Singleton<ZoneData>.Instance;
                if (zoneData != null && zoneData.IsRankedGame)
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                MatchmakerData matchmakerData = Singleton<MatchmakerData>.Instance;
                if (matchmakerData != null && matchmakerData.State != null && matchmakerData.State.QueueGameMode.HasValue)
                {
                    string queue = matchmakerData.State.QueueGameMode.Value.ToString();
                    if (!string.IsNullOrEmpty(queue) && queue.IndexOf("rank", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void EndSession(string reason)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                WriteLine("{\"kind\":\"session_end\",\"utc\":\"" +
                    Escape(System.DateTime.UtcNow.ToString("o")) + "\",\"unityTime\":" +
                    UnityEngine.Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"reason\":\"" + Escape(reason) + "\"}");
            }
            catch
            {
            }

            try
            {
                if (writer != null)
                {
                    writer.Flush();
                    writer.Dispose();
                }
            }
            catch
            {
            }

            UnityEngine.Debug.Log("BNL match replay recorder closed: " + outputPath + " reason=" + reason);
            writer = null;
            outputPath = null;
            sessionId = null;
        }

        private static bool IsTerminalMatchEvent(string eventName)
        {
            return eventName == "Recv_EndMatchResult" ||
                eventName == "Recv_Kicked" ||
                eventName == "Recv_Disconnect" ||
                eventName == "Recv_Disconnected";
        }

        private static void WriteLine(string line)
        {
            if (writer != null)
            {
                writer.WriteLine(line);
                writer.Flush();
                return;
            }

            System.IO.File.AppendAllText(outputPath, line + System.Environment.NewLine);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
