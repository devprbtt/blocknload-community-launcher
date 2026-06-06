## Device healthbar optimization

Date: 2026-06-06

### Why this exists

`map_testing` showed a large FPS drop versus lighter maps because matches with many placed devices create heavy CPU cost in the healthbar population loop.

Diagnostic benchmarking isolated the bottleneck to `GuiHealthbarPopulation.Update`.

### Benchmark summary

- Baseline / most other diagnostic toggles: about `248-264 FPS`
- `disable chunk rebuild`: `248.9 FPS`
- `disable front players`: `249.2 FPS`
- `disable WSI overlay`: `256.7 FPS`
- `disable minimap pop`: `264.2 FPS`
- `disable team FOV`: `248.0 FPS`
- `disable healthbar pop`: `532.1 FPS`

### Conclusion

The meaningful gain comes from reducing overhead caused by device healthbar population, not from chunk rebuilds, FOV, front-player UI, or minimap/WSI loops.

### Permanent feature direction

Replace the old diagnostic selector with a real misc feature:

- `Optimize device health bars`
- keep normal player health bars
- keep base / objective / other always-relevant world elements
- aggressively skip distant device health bars

### Backup

The pre-cleanup diagnostic implementation was backed up to:

`backup/performance-opt-diagnostics-2026-06-06/`
