# Performance testing

The launcher can inject an opt-in frame-time collector. It writes one fixed-width binary record for every rendered frame while the game is in the `Zone` match scene to:

`BlockNLoad_Data/bnl-performance-logs`

Lobby, loading, and menu frames are not recorded. Records contain exact frame time plus periodic managed-memory and cumulative garbage-collection data. Primitive binary writes avoid the per-frame string allocations caused by CSV formatting. Disk output is buffered and flushed every five seconds; this does not aggregate or discard frames. The comparison tool reads `.bnlperf` files directly and derives average FPS, 1% and 0.1% lows, frame-time percentiles, worst frames, and hitch counts.

## Comparable A/B runs

1. Use the same map, resolution, graphics settings, bot/player count, route, and test duration.
2. Disable all performance experiments, enable **Performance Telemetry**, and use the label `baseline`.
3. Rebuild, play through the route for at least two minutes, and close the game normally.
4. Enable exactly one experiment, change the label (for example `healthbar-opt`), rebuild, and repeat the route.
5. Prefer three runs per variant. Ignore the warmup period and avoid comparing menu/loading windows with active gameplay.

Compare two files, directories, or wildcard groups with:

```powershell
./tools/ComparePerformanceLogs.ps1 -Baseline 'path/to/*-baseline.bnlperf' -Test 'path/to/*-healthbar-opt.bnlperf'
```

Convert an individual trace to a human-readable CSV when needed:

```powershell
./tools/ConvertPerformanceLog.ps1 -InputPath 'path/to/performance-test.bnlperf'
```

Average FPS is useful, but 1% lows, P95/P99 frame times, 50 ms stalls, and GC counts usually expose gameplay hitching more clearly.
