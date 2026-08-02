# Unity 5.1 Asset-Bundle Experiments

Block N Load runs on Unity 5.1.4f1 and the legacy built-in rendering pipeline. The
community launcher can inject managed runtime components into `Assembly-CSharp.dll`,
but C# alone cannot add a ShaderLab program to a built player. New shaders, materials,
prefabs, textures, and other Unity-native objects must be compiled into a compatible
asset bundle.

## Proven pipeline

1. Author assets in `prototype-unity-motionblur/`.
2. Build them with the installed Unity 5.1.4f1 editor.
3. Target the required player platform. Windows currently targets
   `StandaloneWindows64`.
4. Use `UncompressedAssetBundle`. Unity 5.1's `AssetBundle.CreateFromFile` rejects the
   default compressed bundle format.
5. Embed the resulting bundle in the launcher.
6. Extract it into the launcher's patching directory.
7. When the feature bundle is built, deploy it under
   `BlockNLoad_Data/CommunityFixes/`.
8. Load it in the injected runtime with `AssetBundle.CreateFromFile`.
9. Keep the bundle loaded for as long as any material or prefab from it is in use.

The motion-blur bundle build entry point is
`BuildMotionBlurBundle.BuildWindows`. Its output is
`assets/patching/motion-blur-windows.bundle`.

## Motion blur architecture

The released player contains old Standard Assets motion-blur classes, but its required
`Hidden/MotionBlur` shader and serialized camera effect were stripped. The original
per-object velocity system is also too narrowly registered to produce general camera
blur.

The working prototype therefore uses:

- a Unity 5.1-compiled, full-resolution directional sampling shader;
- per-frame camera yaw and pitch deltas;
- a frame-time-based response filter;
- configurable strength and sample quality;
- a center-focus mask that keeps the crosshair area clearer;
- the main world camera, allowing the later weapon and overlay cameras to remain sharp.

This is camera-direction blur, not a complete velocity buffer. Translating objects do
not generate independent vectors, and camera translation cannot be reconstructed
correctly without depth.

## Compatibility rules

- Build Windows and Linux bundles separately and test both Direct3D and OpenGL.
- Do not assume modern AssetBundle APIs exist; Unity 5.1 uses
  `AssetBundle.CreateFromFile`.
- Avoid SRP, compute-shader-only solutions, command buffers, and modern post-processing
  packages.
- Validate shader model support on older GPUs.
- Keep a plain `Graphics.Blit` fallback so a missing asset never produces a black frame.
- Log bundle loading, shader selection, and unsupported-platform failures.
- Treat camera lifecycles as transient: respawn, spectating, replays, and scene changes
  may create new cameras.
- Default potentially uncomfortable effects to off and expose strength controls.

## Rebuilding the Windows bundle

```powershell
& 'C:\Program Files\Unity\Editor\Unity.exe' `
  -batchmode -quit -nographics `
  -projectPath 'J:\github\blocknload-community-launcher\prototype-unity-motionblur' `
  -executeMethod BuildMotionBlurBundle.BuildWindows `
  -logFile 'J:\github\blocknload-community-launcher\prototype-unity-motionblur\build.log'
```

The editor requires a valid Unity Personal activation. Verify the build log says the
bundle is uncompressed before publishing it.

## Recommended next experiments

1. **Color grading and accessibility filters** — low risk, one full-screen pass, and
   useful presets for contrast, color blindness, and map visibility.
2. **CAS-style sharpening** — inexpensive and particularly useful after anti-aliasing
   or at low render resolutions.
3. **Replay/cinematic effects** — letterboxing, exposure, vignette, depth-of-field, and
   free-camera overlays without affecting competitive play.
4. **Depth-aware outlines** — highlight objectives, teammates, or replay subjects while
   respecting occlusion.
5. **Improved bloom** — controlled threshold and intensity using a downsample chain.
6. **Weather and atmosphere** — depth fog, rain/snow particles, and map-specific color
   palettes.
7. **True camera velocity blur** — reconstruct world positions from the depth texture
   and previous/current view-projection matrices.
8. **Per-object velocity blur** — render moving objects into a velocity buffer with a
   secondary camera. This is the most complex option because blocks, animated units,
   particles, and skinned meshes need compatible replacement passes.

Start with single-pass effects before multi-camera or depth reconstruction work. They
are easier to validate across Windows/Linux and establish the packaging conventions
future bundles should follow.
