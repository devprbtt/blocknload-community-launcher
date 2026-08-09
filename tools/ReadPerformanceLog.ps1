function Import-BnlPerformanceLog {
    param([Parameter(Mandatory = $true)] [string]$Path)

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if ([IO.Path]::GetExtension($resolved) -eq '.csv') {
        return @(Import-Csv -LiteralPath $resolved | ForEach-Object {
            [pscustomobject]@{
                UtcTicks = [long]$_.utc_ticks
                Label = $_.label
                ElapsedSeconds = [double]::Parse($_.elapsed_seconds, [Globalization.CultureInfo]::InvariantCulture)
                FrameMs = [double]::Parse($_.frame_ms, [Globalization.CultureInfo]::InvariantCulture)
                ManagedMB = [double]::Parse($_.managed_mb, [Globalization.CultureInfo]::InvariantCulture)
                GC0 = [int]$_.gc0_total; GC1 = [int]$_.gc1_total; GC2 = [int]$_.gc2_total
            }
        })
    }

    $stream = [IO.File]::Open($resolved, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $magic = [Text.Encoding]::ASCII.GetString($reader.ReadBytes(8))
        if ($magic -ne 'BNLPRF01') { throw "Unsupported performance log header in $resolved" }
        $version = $reader.ReadInt32()
        if ($version -ne 1) { throw "Unsupported performance log version $version in $resolved" }
        $label = $reader.ReadString()
        $result = [Collections.Generic.List[object]]::new()
        while (($stream.Length - $stream.Position) -ge 32) {
            $result.Add([pscustomobject]@{
                UtcTicks = $reader.ReadInt64(); Label = $label
                ElapsedSeconds = [double]$reader.ReadSingle(); FrameMs = [double]$reader.ReadSingle()
                ManagedMB = [double]$reader.ReadSingle()
                GC0 = $reader.ReadInt32(); GC1 = $reader.ReadInt32(); GC2 = $reader.ReadInt32()
            })
        }
        return $result.ToArray()
    }
    finally { $reader.Dispose(); $stream.Dispose() }
}
