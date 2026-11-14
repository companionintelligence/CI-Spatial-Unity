# Third-party components

Every third-party package, asset, library and model weight that this
repository depends on or contains, with its licence, its source and where it
ends up. Attribution text that the licences require is in
[NOTICE.md](NOTICE.md).

- **Versions** are the ones pinned on this branch: `Packages/packages-lock.json`,
  `Assets/packages.config`, `web/package-lock.json`,
  `cv-server/requirements.txt` and `cv-server/Dockerfile`.
- **Licences** were checked against the licence file shipped in each package,
  the upstream repository's licence file, or the publisher's model card.
  The source is named in each row.
- This file is maintained by hand (the npm/PyPI attribution generator cannot
  see Unity packages, vendored assets or model weights). Update it whenever a
  manifest, lockfile or asset changes.

**Ends up in:** `APK` = Quest player build. `Web` = `web/dist` bundle.
`Server` = CV-server container image. `Editor` = Unity Editor only.
`Dev` = tests, build and CI tooling. `Fetched` = downloaded at run time, not
part of any build we produce.

## Summary

| Licence class | Components |
| --- | --- |
| Proprietary or restricted | Meta XR SDK (Meta Platform Technologies SDK License Agreement); Unity Runtime (Unity Editor Software Terms) and Unity packages (Unity Companion License, Unity Package Distribution License); NVIDIA CUDA base image (NVIDIA Deep Learning Container License) |
| Weak copyleft or copyleft | One MPL-2.0 component inside Meta MR Utility Kit (APK). Server image only: FFmpeg (LGPL-2.1) and Qt 5 (LGPL-3.0) inside `opencv-python` wheels, GCC runtime (GPL-3.0 with GCC Runtime Library Exception) and libquadmath (LGPL-2.1+) inside `numpy`/`scipy` wheels, `tqdm` (MPL-2.0 AND MIT), Ubuntu system packages |
| Fonts | Liberation Sans (OFL-1.1, APK); Roboto Mono (Apache-2.0, APK, inside Meta XR Core); Perfect DOS VGA 437 (no licence stated, APK, inside Unity's RP Core; open item 8); Inter (OFL-1.1, Web) |
| Other notice licences | MS-PL (AQN Parser, DotNetZip), CC-BY-3.0 (FatCow icons) and the Iconmonstr icon licence, all inside `com.unity.visualscripting` (its `Third Party Notices.md`); the A.M.P.A.S. ACES licence (shader code in `com.unity.render-pipelines.core`); the NVIDIA FXAA licence (URP) |
| Permissive | Everything else: MIT, BSD-2/3-Clause, Apache-2.0, ISC, zlib, BSL-1.0, HPND |
| Unknown or forbidden | None in our own or vendored files. One font inside a Unity package states no licence (Perfect DOS VGA 437, open item 8). The assets and the package removed for this reason are listed in [section 5](#5-removed-assets), among them the Meta Voice SDK, whose Wit.ai licence covers only use with Wit.ai services |

The notices columns below come from a scan of every licence, notice and
copying file at any depth inside each package, and of the copyright headers
in package source and shader files, not only the top-level notice files.

## 1. Quest app (Unity project)

### 1.1 Unity packages

From `Packages/manifest.json`, resolved in `Packages/packages-lock.json`.
Registry packages come from `https://packages.unity.com`; built-in packages
ship with the Unity Editor. The licence column quotes each package's
`LICENSE.md` (or `LICENSE`).

| Package | Version | Source | Licence | Ends up in | Third-party notices in the package |
| --- | --- | --- | --- | --- | --- |
| `com.meta.xr.sdk.core` | 209.0.0 | registry | Oculus SDK License Agreement (now the Meta Platform Technologies SDK License Agreement) | APK | `NOTICE.txt`: OpenVR (BSD-3-Clause), SimpleJSON (MIT), glTF loader (MIT, AltspaceVR). Not in `NOTICE.txt`, and shipped in every player because they sit in `Resources` folders of the Immersive Debugger: capsule ray-intersection shader code by Inigo Quilez (MIT, header of `Scripts/ImmersiveDebugger/Gizmos/DebugGizmos/Resources/CapsuleRayIntersect.cginc`) and the Roboto Mono font (Apache-2.0, name table of `Scripts/ImmersiveDebugger/UserInterface/Resources/Fonts/RobotoMono-VariableFont_wght.ttf`). Also not in `NOTICE.txt`: `Plugins/AndroidOpenXR/OVRPlugin.aar` puts a folly logging build (`libxplat_folly_logging__logging.so`, Apache-2.0, Meta; folly is listed in MRUK's `NOTICE.txt`), a Zstandard build (`libxplat_third-party_zstd__zstd.so`, BSD-3-Clause, Meta) and the OpenXR loader (`libopenxr_loader.so`, Apache-2.0, Khronos) into the APK, next to Meta's own libraries (`libOVRPlugin.so`, `libloaderimpl.so`, `libOVRMetricsTool.so`, the `libxplat_perflogger_*` libraries, and `LibRuntimeOptimizer.so` from `Plugins/Android/RuntimeOptimizer_Plugin.aar`). SDK 209 no longer ships `libdouble-conversion.so`, and the folly library does not contain it (checked against the 6000.6.9f1 APK) |
| `com.meta.xr.mrutilitykit` | 209.0.0 | registry | Oculus SDK License Agreement | APK | `NOTICE.txt`: Boost (BSL-1.0), earcut.hpp (ISC), Unity built-in shaders (MIT), an unnamed MPL-2.0 component, JSON for Modern C++ (MIT), folly (Apache-2.0), Protocol Buffers (BSD-3-Clause) |
| `com.meta.xr.sdk.platform` | 209.0.0 | registry | Meta Platform Technologies SDK License Agreement | APK | |
| `com.meta.xr.sdk.audio` | 209.0.0 | registry | Oculus SDK License Agreement | APK | `THIRD_PARTY_NOTICES.txt`: Kiss FFT, libjpeg, OOURA FFT, PFFFT, sfft, TinyThread++, zlib |
| `com.meta.xr.sdk.haptics` | 209.0.0 | registry | Oculus SDK License Agreement | APK | `THIRD_PARTY_NOTICES.txt`: Rust crates under Apache-2.0, MIT, ISC, zlib, Unlicense. The haptic packs under `Samples~` (CC-BY-4.0) are not imported |
| `org.khronos.unitygltf` (UnityGLTF) | 2.20.0 | git `KhronosGroup/UnityGLTF`, tag `release/2.20.0` = `74d7064` | MIT | APK | |
| `com.coplaydev.unity-mcp` (MCP for Unity) | 10.3.0 | git `CoplayDev/unity-mcp`, commit `aa5fc63` (tag `v10.3.0`) | MIT (repository `LICENSE`; the package has no licence file of its own and links to it from `package.json`) | Editor (see note 1) | `Editor/External/Tommy.cs` (MIT, Denis Zhidkikh) |
| `com.github-glitchenzo.nugetforunity` (NuGetForUnity) | 4.4.0 | git `GlitchEnzo/NuGetForUnity`, commit `a22299e` | MIT | Editor | |
| `com.unity.render-pipelines.universal` | 17.6.0 | built-in | Unity Companion License | APK | `Third Party Notices.md`: FXAA 3.11 (NVIDIA licence). Not listed there: SMAA (MIT; Jorge Jimenez, Jose I. Echevarria, Belen Masia, Fernando Navarro, Diego Gutierrez; `Shaders/PostProcessing/SubpixelMorphologicalAntialiasing.hlsl`) and Clipper (BSL-1.0, Angus Johnson; `Runtime/2D/Clipper.cs`) |
| `com.unity.render-pipelines.core` | 17.6.0 | built-in | Unity Companion License | APK | `THIRD PARTY NOTICES.md`: RadeonRays 4.1 (MIT), Bullet Physics (zlib), Sobol sampler (MIT). Not listed there: ACES reference code (A.M.P.A.S. licence, `ShaderLibrary/ACES.hlsl`), AMD FidelityFX shader code (MIT, `Runtime/PostProcessing/Shaders/ffx/`), coverage code by Kleber Garcia (MIT, `ShaderLibrary/Coverage.hlsl`), and the Rendering Debugger font `Runtime/Debugging/Prefabs/Fonts/PerfectDOSVGA437.ttf` (Perfect DOS VGA 437 by Zeh Fernando, 2003), whose name table states no licence; it ships in every player because `DebugUICanvas.prefab` sits in a `Resources` folder (open item 8) |
| `com.unity.render-pipelines.universal-config` | 17.6.0 | built-in | Unity Companion License | APK | |
| `com.unity.shadergraph` | 17.6.0 | built-in | Unity Companion License | APK (generated shaders) | |
| `com.unity.ugui` (includes TextMesh Pro) | 2.6.0 | built-in | Unity Companion License | APK | |
| `com.unity.textmeshpro` | 5.0.0 | built-in (empty; merged into uGUI) | none | none | |
| `com.unity.inputsystem` | 1.20.1 | registry | Unity Companion License | APK | |
| `com.unity.xr.management` | 4.7.0 | registry | Unity Companion License | APK | |
| `com.unity.xr.openxr` | 1.18.0 | registry | Unity Companion License (source), Unity Package Distribution License (binaries) | APK | OpenXR-SDK-Source (Apache-2.0); Oculus OpenXR Mobile SDK (Oculus SDK License Agreement) |
| `com.unity.xr.meta-openxr` | 2.6.1 | registry | Unity Companion License (source), Unity Package Distribution License (binaries) | APK | OpenXR-SDK-Source (Apache-2.0); Oculus OpenXR Mobile SDK and AudioPluginOculusSpatializer (Oculus SDK License Agreement) |
| `com.unity.xr.arfoundation` | 6.6.2 | registry | Unity Companion License | APK | |
| `com.unity.xr.core-utils` | 2.6.0 | registry | Unity Companion License | APK | |
| `com.unity.xr.compositionlayers` | 2.6.1 | registry | Unity Companion License (source), Unity Package Distribution License (binaries) | APK | |
| `com.unity.xr.hands` | 1.9.0 | registry (dependency of `com.meta.xr.sdk.core` 209) | Unity Package Distribution License | APK (`libUnityOpenXRHands.so`) | `Third Party Notices.md` |
| `com.unity.xr.legacyinputhelpers` | 3.0.1 | registry | Unity Companion License | APK | Arm models (Apache-2.0, Google) |
| `com.unity.ai.navigation` | 2.0.16 | registry | Unity Companion License | APK | |
| `com.unity.burst` | 2.0.0 | built-in | No licence file: since Unity 6000.6 the package is a runtime stub and the Burst compiler ships with the editor (`Editor/Data/Tools/Burst/`, with its own `LICENSE.md` and `Third Party Notices.md`) | APK (Burst-compiled code, `lib_burst_generated.so`); compiler Editor only | Editor install `Tools/Burst/Third Party Notices.md`: LLVM (Apache-2.0 with LLVM exception) and the other components 1.8.28 listed |
| `com.unity.collections` | 6.6.0 | built-in | Unity Companion License | APK | |
| `com.unity.mathematics` | 1.4.0 | built-in | Unity Companion License | APK | Noise functions by Ashima Arts and Stefan Gustavson (MIT, `Unity.Mathematics/Noise/LICENSE`) |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | registry | Unity Companion License | APK | Newtonsoft.Json and its Unity forks (MIT) |
| `com.unity.timeline` | 6.6.0 | built-in | Unity Companion License | APK | |
| `com.unity.visualscripting` | 1.9.13 | registry | Unity Package Distribution License | APK (runtime), Editor | AQN Parser and DotNetZip (MS-PL), Deep Copy, Full Serializer, MD4, Reorderable List, SQLite .NET, YamlDotNet, Ensure.That, NCalc (MIT), FatCow icons (CC-BY-3.0), Iconmonstr icons (Iconmonstr licence), SQLite (public domain), ANTLR 3 runtime (BSD) |
| `com.unity.2d.sprite` | 1.0.0 | built-in | Unity Package Distribution License | Editor | |
| `com.unity.test-framework` | 1.8.0 | built-in | Unity Companion License | Dev | |
| `com.unity.ext.nunit` | 2.1.0 | built-in | Unity Package Distribution License | Dev | NUnit 3.5 (MIT) |
| `com.unity.test-framework.performance` | 6.6.0 | built-in (dependency of `com.unity.collections`) | Unity Companion License | Dev (its generated run files used to ship; see note 2) | Perfolizer (MIT) |
| `com.unity.ide.rider` | 3.1.0 | registry | MIT (Unity Technologies, JetBrains) | Editor | |
| `com.unity.ide.visualstudio` | 2.0.27 | registry | MIT (Unity Technologies, Microsoft) | Editor | VSWhere (MIT), EnvDTE (0BSD) |
| `com.unity.mobile.android-logcat` | 1.4.7 | registry | Unity Companion License | Editor | |
| `com.unity.multiplayer.center` | 2.1.0 | built-in | Unity Package Distribution License | Editor | |
| `com.unity.editorcoroutines` | 6.6.0 | built-in | Unity Companion License | Editor | |
| `com.unity.searcher` | 4.9.5 | registry | Unity Companion Package License v1.0 | Editor | |
| `com.unity.nuget.mono-cecil` | 1.11.6 | registry | Unity Companion License | Editor | Mono.Cecil (MIT) |
| `com.unity.profiling.core` | 1.0.3 | built-in (dependency of `com.unity.render-pipelines.core` 17.6) | Unity Companion License | APK | |
| `com.unity.graph-authoring` | 1.0.0 | built-in (dependency of `com.unity.shadergraph` 17.6) | No licence file in the package; ships with the Unity 6000.6 editor | Editor | |
| `com.unity.modules.*` | 1.0.0 | built-in engine modules | [Unity Editor Software Terms](https://unity.com/legal/terms-of-service/software) (Unity Runtime) | APK | |

Licence references:
[Meta Platform Technologies SDK License Agreement](https://developers.meta.com/horizon/licenses/oculussdk/),
[Unity Companion License](https://unity.com/legal/licenses/unity-companion-license),
[Unity Package Distribution License](https://unity3d.com/legal/licenses/Unity_Package_Distribution_License).
The "Unity AI" scoped registry in the manifest points at Unity's own registry
(`https://packages.unity.com`).

Note 1: MCP for Unity is an Editor automation bridge used while developing
the project. Its `MCPForUnity.Runtime` assembly has no platform restriction,
so Unity used to copy it into every player.
`Assets/CompanionScan/Editor/EditorOnlyPackageAssemblyFilter.cs` now removes
`MCPForUnity*.dll` from player builds, and EditMode tests cover the filter.
In an Android IL2CPP build with the `Meta Quest` profile,
`ScriptingAssemblies.json`, `global-metadata.dat` and `libil2cpp.so` contain
no MCP for Unity assembly, type or string. The build report still lists the
MonoScript records of the seven `.cs` files in `MCPForUnity.Runtime` among
the packed assets: file, class and assembly names without code, which the
player cannot resolve. Only an Editor-only asmdef in the package itself would
drop them. The package is pinned to a release commit instead of the moving
`#main` branch.

Note 2: `com.unity.test-framework.performance` is not a direct dependency;
`com.unity.collections` pulls it in. At the start of every player build it
writes `PerformanceTestRunInfo.json` (the full package list, MCP for Unity
included, plus the Editor version) and `PerformanceTestRunSettings.json` into
`Assets/Resources`, which Unity then packs into the player.
`Assets/CompanionScan/Editor/PerformanceTestRunFileStripper.cs` deletes both
files again before Unity collects `Resources`, except in the players the Test
Runner builds for on-device tests, which need them.

Note 3: the project used to depend on `com.meta.xr.sdk.all`, which also
installs the Voice SDK and the Interaction SDK. The manifest now lists the
Meta packages one by one, without those two (see
[section 5](#5-removed-assets)).

### 1.2 Added by the Android build

| Library | Source | Licence | Ends up in |
| --- | --- | --- | --- |
| `libunity.so`, `libmain.so`, `libgame.so`, `libil2cpp.so` | Unity Android player and the IL2CPP output of our scripts and packages | Unity Editor Software Terms (Unity Runtime), plus the licences of the code compiled into `libil2cpp.so` | APK |
| `libc++_shared.so` | LLVM libc++ from the Android NDK r27c that Unity uses | Apache-2.0 with LLVM Exceptions (NDK `toolchains/llvm/prebuilt/*/NOTICE`) | APK |

### 1.3 Vendored in `Assets/` and `Packages/`

| Path | Component | Version | Source | Licence | Ends up in |
| --- | --- | --- | --- | --- | --- |
| `Assets/NaughtyAttributes/` | NaughtyAttributes | 2.1.4 | <https://github.com/dbrizov/NaughtyAttributes> | MIT (`LICENSE.md` in the folder) | APK (Core and Test assemblies), Editor |
| `Assets/TextMesh Pro/` (shaders, settings, style sheet, line-breaking rules, SDF font assets) | TextMesh Pro Essential Resources | from uGUI 2.0.0 | Unity | Unity Companion License | APK |
| `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` | Liberation Sans | 2.00.1 | Red Hat, via TextMesh Pro | SIL OFL 1.1 (font name table and `LiberationSans - OFL.txt`) | APK (as the `LiberationSans SDF` font asset) |
| `Assets/Packages/` | Microsoft.Bcl.AsyncInterfaces 9.0.5, System.IO.Pipelines 9.0.5, System.Net.Http.Json 9.0.5, System.Runtime.CompilerServices.Unsafe 6.0.0, System.Text.Encodings.Web 9.0.5, System.Text.Json 9.0.5 | as listed | nuget.org, installed by NuGetForUnity (`Assets/packages.config`) | MIT, .NET Foundation (`LICENSE.TXT`, `THIRD-PARTY-NOTICES.TXT` in each folder) | APK (the plugins are enabled for players) |
| `Packages/<Name>.<Version>/`, `packages.config`, `app.config` (repository root) | The same .NET packages plus System.Buffers 4.5.1, System.Memory 4.5.5, System.Numerics.Vectors 4.5.0, System.Threading.Tasks.Extensions 4.5.4, System.ValueTuple 4.5.0 | as listed | nuget.org (a Visual Studio `net471` restore) | MIT, .NET Foundation | Not used: Unity ignores these folders (no `package.json`). Safe to delete. |
| `Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/InputSystem_Actions.inputactions` | URP project template files | `com.unity.template.urp-blank` | Unity project template | Unity Companion License (template `LICENSE.md`) | APK (`InputSystem_Actions` is the project-wide input actions asset); Editor (Readme, TutorialInfo) |
| `Assets/Settings/`, `Assets/XR/`, `Assets/Oculus/`, `Assets/MetaXR/`, `Assets/CompositionLayers/`, `Assets/Resources/*.asset`, `Assets/Plugins/Android/AndroidManifest.xml` | Project settings generated by Unity and Meta tooling | n/a | generated | Project configuration, no third-party content | APK |
| `Assets/CompanionScan/Tests/Fixtures/*.jpg`, `cv-server/tests/fixtures/*.jpg`, `test/*.jpg` | Synthetic room renders | n/a | first-party: `tools/generate_synthetic_fixtures.py` (Pillow drawing only) and the labelled-box renders from February 2026 | Repository [LICENSE](LICENSE) | Dev |
| `Assets/CompanionScan/Icons/AppIcon.png` | Placeholder launcher icon | n/a | first-party: `tools/generate_app_icon.py` (Pillow drawing only, no fonts or images) | Repository [LICENSE](LICENSE) | APK (Default Icon in the global Player Settings and in the `Meta Quest` build profile) |

## 2. Web client (`web/`)

From `web/package.json` and `web/package-lock.json`. The `Web` column was
checked against a production build (`vite build`): every emitted file with a
source map through the sources the map lists, and every emitted file without
one by comparing it byte for byte with the files in `node_modules`. Only the
packages marked `Web` are in the bundle. Minification strips the licence
comments of most of them, so the notices must ship separately (open item 1).

| Package | Version | Licence | Ends up in |
| --- | --- | --- | --- |
| `three` | 0.183.2 | MIT | Web. Also bundles the example modules listed in the next table |
| `@iwsdk/core`, `@iwsdk/glxf`, `@iwsdk/locomotor`, `@iwsdk/xr-input` | 0.3.1 | MIT | Web. The `@iwsdk/locomotor` physics worker, emitted as `assets/worker-*.js` with the worker code base64-encoded in a data URL, embeds its own copy of three.js r181 and of three-mesh-bvh code (both MIT) |
| `@babylonjs/havok` | 1.3.11 | MIT | Web (`HavokPhysics.wasm`) |
| `@pmndrs/uikit` | 1.0.64 | MIT (the package says "SEE LICENSE IN LICENSE"; that file is the MIT text) | Web |
| `@pmndrs/uikit-pub-sub` | 1.0.64 | MIT | Web |
| `@pmndrs/uikitml` | 0.1.12 | MIT | Web |
| `@pmndrs/handle`, `@pmndrs/pointer-events` | 6.6.29 | MIT (same "SEE LICENSE IN LICENSE" file) | Web |
| `@pmndrs/msdfonts` | 1.0.64 | No licence of its own: `package.json` says "SEE LICENSE IN LICENSE", and that file holds only the licences of the fonts it redistributes (OFL-1.1 for Inter and most families, Apache-2.0 for Roboto). The package is font data only (`dist/index.js` exports MSDF atlases and glyph metrics) | Web: only the four Inter atlases are bundled (OFL-1.1) |
| `@zappar/msdf-generator` | 1.2.4 | MIT (Zappar Limited). Its `LICENSE` also lists msdfgen (MIT), Skia PathOps (BSD-3-Clause) and stb_truetype (MIT or public domain), which are compiled into `msdfgen_wasm.wasm` | Web: `dist/worker.js` is copied unchanged to `assets/worker-*.js`. Nothing in the bundle references it, and the `.wasm` file is not emitted |
| `@preact/signals-core` | 1.14.0 | MIT | Web |
| `elics` | 3.4.2 | MIT | Web |
| `parse5` | 7.3.0 | MIT | Web |
| `three-mesh-bvh` | 0.9.9 | MIT | Web |
| `yoga-layout` | 3.2.1 | MIT (declared in `package.json`; upstream `facebook/yoga` is MIT) | Web (includes the Yoga WebAssembly as base64) |
| `entities` 6.0.1 (BSD-2-Clause), `comlink` 4.4.2 (Apache-2.0), `react` 19.2.4, `use-sync-external-store` 1.6.0, `zustand` 4.5.7, `chai` 5.3.3, `assertion-error`, `check-error`, `deep-eql`, `loupe`, `pathval`, `inline-style-parser`, `@types/emscripten` (MIT) | as listed | as listed | Installed with the runtime dependencies; not in the bundle (the msdf-generator worker imports `comlink` by bare name, which the copied file does not resolve) |
| `vite` 6.4.4, `esbuild` 0.25.12, `rollup` 4.59.0, `postcss` 8.5.32, `@types/node` 24.12.0, `@types/three` 0.177.0 (MIT); `typescript` 5.9.3, `@playwright/test` 1.58.2 (Apache-2.0) | as listed | as listed | Dev |

three.js example modules in the bundle (from `three/examples/jsm/`, pulled in
by `@iwsdk/core`, and `SVGLoader` by `@pmndrs/uikit`) that carry third-party
code under its own notice. The other example modules in the bundle are
three.js code (MIT).

| Module | Third-party code | Licence | Source of the licence text |
| --- | --- | --- | --- |
| `loaders/EXRLoader.js` | TinyEXR (Syoyo Fujita) and OpenEXR (Industrial Light & Magic) | BSD-3-Clause, both | Comment block at the top of `EXRLoader.js` |
| `libs/fflate.module.js` | fflate 0.8.2 (Arjun Barrett) | MIT | <https://github.com/101arrowz/fflate/blob/v0.8.2/LICENSE> |
| `libs/ktx-parse.module.js` | ktx-parse (Don McCurdy) | MIT | <https://github.com/donmccurdy/KTX-Parse/blob/main/LICENSE> |
| `libs/zstddec.module.js` | zstddec (Don McCurdy), with the Zstandard decoder compiled to WebAssembly and embedded as base64 (Yann Collet, Facebook, Inc.) | MIT (zstddec); BSD-3-Clause (Zstandard) | <https://github.com/donmccurdy/zstddec/blob/main/LICENSE> (holds both texts) |
| `loaders/SVGLoader.js` | Number parser from svg-numbers (Peter-Paul van Gemerden) | MIT | <https://github.com/ppvg/svg-numbers/blob/master/LICENSE> |
| `environments/RoomEnvironment.js` | Based on `EnvironmentScene` from Google's model-viewer | Apache-2.0 (model-viewer) | <https://github.com/google/model-viewer/blob/master/LICENSE> |

Fetched at run time from public CDNs by the SDK, not by our bundle:

| Asset | Source | Licence | Notes |
| --- | --- | --- | --- |
| WebXR Input Profiles controller models | `https://cdn.jsdelivr.net/npm/@webxr-input-profiles/assets@1.0` | MIT ("Assets are available under MIT license", package README) | The models depict device makers' trademarks, which the licence does not grant. |
| three.js Draco and Basis Universal decoders | `https://unpkg.com/three@0.x.0/examples/jsm/libs/` | Apache-2.0 (google/draco, BinomialLLC/basis_universal) | Only when a compressed glTF is loaded. |

`cv-server/package-lock.json` is an empty npm stub with no dependencies.

## 3. CV server (`cv-server/`)

### 3.1 Base image and system packages

| Component | Version | Licence | Notes |
| --- | --- | --- | --- |
| `nvidia/cuda` base image | `12.1.1-cudnn8-devel-ubuntu22.04` | NVIDIA Deep Learning Container License; Dockerfiles BSD-3-Clause | Includes CUDA and cuDNN under NVIDIA licences and Ubuntu packages (various, including GPL). NVIDIA archives the open-source parts at <https://developer.download.nvidia.com/compute/cuda/opensource/image>. |
| Ubuntu packages from `apt-get` (`python3.11`, `python3.11-dev`, `python3.11-venv` from the deadsnakes PPA, 3.11.18 when the release candidate was built; `software-properties-common`, `git`, `build-essential`, `ninja-build`, `cmake`, `libgl1`, `libglib2.0-0` from Ubuntu) | Ubuntu 22.04; `ppa:deadsnakes/ppa` | Various (PSF-2.0 for Python, GPL, LGPL, MIT, BSD) | Build and runtime tools inside the image. Ubuntu's own `python3.11` (3.11.0rc1) is not used. |

### 3.2 Python packages

Licences from the PyPI metadata of the pinned version (`license_expression`,
`license` or classifiers).

| Package | Version | Licence | Notes |
| --- | --- | --- | --- |
| `torch`, `torchvision`, `torchaudio` | 2.5.1, 0.20.1, 2.5.1 (CUDA 12.1 wheels from `download.pytorch.org`; the last release with CUDA 12.1 wheels) | BSD-3-Clause, with bundled third-party code under other permissive licences (current `torch` metadata: Apache-2.0 AND BSD-2-Clause AND BSD-3-Clause AND BSL-1.0 AND ISC AND MIT AND Zlib) | The CUDA wheels carry NVIDIA runtime libraries under NVIDIA licences. |
| `flash-attn` | 2.8.3.post2 | BSD-3-Clause | Installed for Florence-2's repository code, which the server no longer runs; transformers' Florence-2 implementation uses PyTorch's attention, so nothing needs it now (open item 9). Installs the prebuilt `cu12torch2.5cxx11abiFALSE-cp311` wheel from the project's GitHub release. |
| `packaging` | 26.3 | Apache-2.0 OR BSD-2-Clause | |
| `wheel` | 0.48.0 | MIT | Build tool for flash-attn and the git packages. |
| `fastapi` | 0.110.0 | MIT | |
| `uvicorn[standard]` | 0.27.1 | BSD-3-Clause | |
| `pydantic` | 2.6.1 | MIT | |
| `pydantic-settings` | 2.2.1 | MIT | |
| `numpy` | 1.26.4 | BSD-3-Clause | Wheels bundle OpenBLAS (BSD-3-Clause), LAPACK, GCC runtime (GPL-3.0 with GCC Runtime Library Exception), libquadmath (LGPL-2.1+). |
| `pillow` | 13.0.0 | MIT-CMU (HPND) | |
| `scipy` | 1.11.4 | BSD-3-Clause | Wheels bundle the same kind of BLAS and Fortran runtime as numpy. |
| `cython` | 3.0.10 | Apache-2.0 | |
| `pycocotools` | 2.0.7 | BSD-2-Clause ("FreeBSD") | |
| `opencv-python` | 4.9.0.80 | Apache-2.0 (OpenCV), MIT (packaging scripts) | Wheels ship FFmpeg (LGPL-2.1); non-headless Linux wheels ship Qt 5 (LGPL-3.0). The server does not use OpenCV's GUI. |
| `pytest` | 9.0.3 | MIT | Test-only, listed in the runtime requirements. |
| `httpx` | 0.27.0 | BSD-3-Clause | |
| `python-multipart` | 0.0.31 | Apache-2.0 | |
| `transformers` | 5.17.0 | Apache-2.0 | No published advisories (OSV, 13 November 2026); every 4.x release has advisories fixed only in 5.x. The last release that needs nothing newer than torch 2.5.1: 5.18 adds a call to `torch.accelerator` (torch 2.6) to its device helper, 5.19 makes that call on import, 5.20 and later require torch 2.6, and 5.10.x needs `torch.float8_e8m0fnu` (torch 2.7). GroundingDINO runs with the server's own BERT text encoder (`app/gdino_text_encoder.py`) in place of GroundingDINO's wrapper, which calls methods transformers 5 removed. Florence-2 runs on transformers' own implementation. |
| `tokenizers` | 0.23.3 | Apache-2.0 | Pinned with `transformers`. |
| `huggingface-hub` | 1.33.0 | Apache-2.0 | Downloads BERT and Florence-2 at their pinned commits (section 3.3). |
| `hf-xet` | 1.8.0 | Apache-2.0 | Download backend of `huggingface-hub` for Xet-stored files. |
| `safetensors` | 0.8.0 | Apache-2.0 | Both Hugging Face models load from `model.safetensors`, not from pickle files. |
| `accelerate` | 1.15.0 | Apache-2.0 | |
| `einops` | 0.7.0 | MIT | |
| `timm` | 0.9.16 | Apache-2.0 | |
| GroundingDINO | git `IDEA-Research/GroundingDINO`, commit `856dde20aee659246248e20734ef9ba5214f5e44` (2024-08-12) | Apache-2.0 | Brings `supervision` (MIT), `addict` (MIT), `yapf` (Apache-2.0). |
| SAM 2 (`sam2`) | git `facebookresearch/sam2`, commit `2b90b9f5ceec907a1c18123530e92e794ad901a4` (2024-12-16) | Apache-2.0 | Brings `hydra-core` (MIT), `iopath` (MIT), `tqdm` (MPL-2.0 AND MIT). |
| `locust` (`cv-server/load/requirements.txt`) | 2.27.0 | MIT | Dev (load testing). |

### 3.3 Model weights and configs

None of these files is in the repository. The operator puts the checkpoints
and configs in `cv-server/checkpoints/` and `cv-server/configs/` (both
ignored by git); Hugging Face models download into the container's Hugging
Face cache.

| Model | File or ID | Source | Licence (where stated) | Research-only or non-commercial terms? |
| --- | --- | --- | --- | --- |
| Grounding DINO, Swin-T OGC | `groundingdino_swint_ogc.pth`; config `GroundingDINO_SwinT_OGC.py` | GitHub release `v0.1.0-alpha` of <https://github.com/IDEA-Research/GroundingDINO>; mirror <https://huggingface.co/ShilongLiu/GroundingDINO> | Apache-2.0 (repository `LICENSE`; Hugging Face card `license: apache-2.0`) | No |
| BERT base, uncased | `google-bert/bert-base-uncased` at commit `86b5e0934494bd15c9632b12f734a8a67f723594`, Grounding DINO's text encoder (its config names the old alias `bert-base-uncased`); five files downloaded on first load | <https://huggingface.co/google-bert/bert-base-uncased> | Apache-2.0 (model card) | No |
| SAM 2.1 Hiera Tiny | `sam2.1_hiera_tiny.pt`; config `configs/sam2.1/sam2.1_hiera_t.yaml` (ships in the `sam2` package) | <https://github.com/facebookresearch/sam2> (also <https://huggingface.co/facebook/sam2.1-hiera-tiny>) | Apache-2.0: "The SAM 2 model checkpoints ... are licensed under Apache 2.0" (README, "License") | No |
| Florence-2-large | `florence-community/Florence-2-large` at commit `4271c66b88cdbc05735372ec13b2360108de5317`: Microsoft's Florence-2-large weights converted by Hugging Face for transformers' own Florence-2 implementation (weights, configs and tokenizer files; no code). `florence-community/Florence-2-base` is pinned too, at `00921df66db728a9ceb750f5eca43e5c203a2051`, for `FLORENCE_MODEL_ID`; the ids `microsoft/Florence-2-large` and `-base` load these | <https://huggingface.co/florence-community/Florence-2-large>, converted from <https://huggingface.co/microsoft/Florence-2-large> | MIT (model card `license: mit`, linking Microsoft's licence) | No |

All four allow commercial use, so no substitute is needed. The licences cover
the published weights; the datasets the publishers trained on carry their own
terms, which these licences do not pass on to us. Ask counsel if that matters
for a specific market.

## 4. Development tooling (not shipped)

| Tool | Version | Licence | Where |
| --- | --- | --- | --- |
| `actions/checkout`, `actions/setup-node`, `actions/upload-artifact` | v4 | MIT | `.github/workflows/` |
| `game-ci/unity-test-runner` | v4 | MIT | `.github/workflows/unity-tests.yml`; runs Unity Editor images under a Unity licence |
| `markitdown-mcp` | 0.0.1a4 | MIT (microsoft/markitdown) | `.vscode/mcp.json` |

## 5. Removed assets

These files had no recorded source, author or licence, their licence was
unclear, or their licence does not cover how this project used them, so they
were removed. The Interaction SDK (last row) is the exception: it was
removed because it was unused and broke the APK build.

| Removed | Why | Replacement |
| --- | --- | --- |
| 14 photos in `test/` (`How-To-Organize-ACloset.jpg`, `bedroom.jpg`, `bedroom2.jpg`, `closet2.jpg`, `closet3.jpeg`, `food-storage-containers.jpg`, `food-storage-jars.jpg`, `laundry-room1.jpg`, `laundry-room2.jpg`, `living-room.jpg`, `pantry-organization.jpg`, `pantry-organization2.jpg`, `pantry.jpg`, `walk-in-pantry-design.jpg`) and 11 copies in `Assets/CompanionScan/Tests/Fixtures/` | Web photographs with no recorded source, author or licence. They were not test-only: since `ebd1535` (2026-05-18) five of the Unity copies (`pantry-organization.jpg`, `How-To-Organize-ACloset.jpg`, `food-storage-containers.jpg`, `closet3.jpeg`, `closet2.jpg`) were the Android adaptive and round launcher icons in the global Player Settings, so APKs built with those settings distributed them | Six synthetic room renders (`synthetic_*_1280x1024.jpg`) drawn by `tools/generate_synthetic_fixtures.py` with Pillow only; `PerImageModelTests` uses them. The Android icon slots are cleared, and the new first-party `Assets/CompanionScan/Icons/AppIcon.png` is the Default Icon |
| `Assets/CompanionScanLegacy/Textures/cats.png` (deleted in `a862495`, 2026-02-23) | Legacy sample texture with no recorded source. The `Meta Quest` build profile still named it (GUID `d12b75aa…`) as its Default Icon, so builds with that profile had no valid icon | The profile's Default Icon is now `Assets/CompanionScan/Icons/AppIcon.png` |
| `com.meta.xr.sdk.voice` 85.0.0 (installed by `com.meta.xr.sdk.all`) | Its Wit.ai library carries its own licence (`Lib/Wit.ai/LICENSE`), which grants use and distribution only "for use in connection with the web services and APIs provided by Wit.ai" and subjects use to the Wit.ai Terms of Service. Companion Scan uses neither Wit.ai nor any Voice SDK feature (no code, scene, prefab or asset references the package), yet the SDK shipped in the APK, with NativeWebSocket (Apache-2.0), NLayer (MIT), UnityOpus (MIT) and its native libopus 1.1.0 (BSD-3-Clause), voice and assistant Java libraries, and a telemetry library (`Lib/Telemetry/Plugins/SDKTelemetry.aar`) | None needed. `Packages/manifest.json` lists the other Meta packages directly. Add the Voice SDK back only for a feature that uses Wit.ai, and attribute its notices then |
| `Assets/TextMesh Pro/Sprites/EmojiOne.png`, `EmojiOne.json`, `EmojiOne Attribution.txt` and `Resources/Sprite Assets/EmojiOne.asset` | TextMesh Pro sample emoji. The attribution file names no licence: it says to "review their licensing terms" on EmojiOne's site | None. The project uses no `<sprite>` tags; TMP Settings no longer names a default sprite asset |
| `Assets/number_atlas.png` | Digit atlas from the initial commit with no source or licence, referenced by nothing | None |
| `Assets/Plugins/Demigiant.meta`, `Assets/Plugins/Demigiant/DOTween.meta`, `Assets/Plugins/Demigiant/DOTween/Editor.meta` | Folder metadata for DOTween (an Asset Store package) whose files were never committed | None; DOTween is not used |
| `Assets/CompanionScanLegacy/Prefabs/SpatialLabel.prefab` with the scripts, animator controller, Roboto font asset and material it used, and `Textures/loading.png` with its `ConstantRotation` script (all deleted in `a862495`, 2026-02-23) | Legacy sample files with no recorded source or licence (the initial commit's `XR-AI-Florence2` sample). `CompanionCapture.unity` still pointed at them: the pin and label prefab of `SpatialPinManager` and `LabelRenderer`, and an inactive `LoadingIcon` with a missing script and texture, so the app showed no pins or 3D labels | First-party `Assets/CompanionScan/Prefabs/SpatialPin.prefab` and `SpatialLabel.prefab`, made in this repository from Unity's built-in sphere, cylinder and quad meshes, URP Unlit materials and TextMesh Pro's Liberation Sans (OFL-1.1, listed above). The unused `LoadingIcon` is gone from the scene |
| `Assets/CompanionScanLegacy.meta` | Folder metadata for a legacy sample whose files are not in the repository | None |
| `Assets/Resources/PerformanceTestRunInfo.json`, `PerformanceTestRunSettings.json` | Performance Testing output. Deleting the committed copies was not enough: the package writes both files at the start of every player build (note 2), so they shipped in every player and listed every package, MCP for Unity included | `PerformanceTestRunFileStripper` removes them before Unity packs `Resources`; `.gitignore` keeps stray copies out of commits |
| `com.meta.xr.sdk.interaction` and `com.meta.xr.sdk.interaction.ovr` 85.0.0 (installed by `com.meta.xr.sdk.all`) | Not a licence problem. Nothing uses them: no code, asmdef, scene, prefab or asset references them (MR Utility Kit and Core reference them only through optional version defines). Their `InteractionSdk.aar` declares the same Android namespace as Core's `OVRPlugin.aar` (`com.oculus.Integration`), and Unity 6000.3.17f1 and later build with Gradle 9.1 and AGP 9.0, which reject duplicate namespaces, so the APK build failed in the manifest merger. They also put InterfaceSupport (MIT), code adapted from Leap Motion UnityModules (Apache-2.0) and shader code by Inigo Quilez (MIT) into the APK | None needed. If the app adopts the Interaction SDK later, use a Meta release whose AAR namespaces are unique, or build with a Unity version older than 6000.3.17f1 |

## 6. Open items for the owner

1. Ship the notices with each build. The Quest APK carries them: the Android
   build copies `LICENSE`, `NOTICE.md`, this file and the licence and notice
   files of the vendored assets and of every resolved package into the APK
   under `assets/licenses`, and Settings shows them in the headset (Menu,
   then a trigger). Only `web/dist` still needs them, as a notices file in the
   bundle.
2. Meta XR SDK: apps built with it may target only Meta-approved hardware
   and software (licence §1.1). The Voice and Interaction SDKs are gone
   (section 5). The project's code, scenes, prefabs and assets reference only
   Core, MR Utility Kit and the Audio settings assets in `Assets/Resources`;
   nothing references Platform or Haptics. Removing those two as well would
   shrink the APK and the list of notices to ship.
3. CV-server image: publishing a built image redistributes NVIDIA CUDA and
   cuDNN (NVIDIA Deep Learning Container License) and LGPL FFmpeg and Qt
   (`opencv-python`). Publishing the Dockerfile alone does not. Switching to
   `opencv-python-headless` drops Qt (and the need for `libgl1`); FFmpeg
   stays.
4. Reproducibility: `torch`, `flash-attn`, GroundingDINO and SAM 2 are pinned
   to the versions and commits the release-candidate image was built with, and
   `transformers` with its Hugging Face packages to the versions the image was
   checked with. The Hugging Face downloads (Florence-2 and Grounding DINO's
   BERT) are pinned to repository commits in `cv-server/app/hub_pins.py`.
   Still floating: the other transitive Python packages (for example `regex`,
   `requests`, `PyYAML`) and the apt packages. A constraints file generated
   from the built image would pin those too.
5. The npm/PyPI attribution generator must not overwrite this file or
   [NOTICE.md](NOTICE.md); it cannot see Unity packages, assets or weights.
   [CREDITS.md](CREDITS.md) is still its output and credits only the npm and
   PyPI dependencies.
6. Hardware statistics: `submitAnalytics` is now 0 in the global Player
   Settings (`ProjectSettings/ProjectSettings.asset`) and in the Player
   Settings override of the `Meta Quest` build profile
   (`Assets/Settings/Build Profiles/Meta Quest.asset`). Unity applies a
   profile's override to every build made while that profile is active,
   including `CompanionScan > Build Quest APK`, so a profile with its own
   Player Settings can switch the statistics back on: set
   `submitAnalytics: 0` in any build profile you add. Unity's documentation
   for `Analytics.deviceStatsEnabled` says device statistics stop only when
   the Analytics and Performance Reporting services are also disabled; both
   are off in `ProjectSettings/UnityConnectSettings.asset`. Keep them off.
7. Launcher icon: `Assets/CompanionScan/Icons/AppIcon.png` is a generated
   placeholder. Replace the PNG with the product icon (keep the file name and
   its `.meta`, so both icon references stay valid). APKs built from `main`
   with the global Player Settings since `ebd1535` (2026-05-18) used five of
   the unlicensed web photos as launcher icons; withdraw or rebuild any such
   APK that is still being distributed.
8. Perfect DOS VGA 437: Unity's `com.unity.render-pipelines.core` ships this
   font in every URP player, through the Rendering Debugger prefabs in its
   `Resources` folder, so it cannot be left out without forking the package.
   Neither the font file nor the package's notice file names a licence. It
   reaches us under the Unity Companion License as part of the package. Ask
   Unity to confirm the font's terms before a commercial release.
9. CV-server torch: torch 2.5.1 is the last release with CUDA 12.1 wheels, and
   it is why `transformers` stops at 5.17.0. It has published advisories of its
   own (19 CVEs on OSV, 13 November 2026, including CVE-2025-32434 in
   `torch.load` with `weights_only=True`; the server loads only checkpoints
   pinned by SHA-256). torch 2.13.0 and later have none; moving needs a newer
   CUDA base image and new builds of GroundingDINO's and SAM 2's CUDA
   extensions. `flash-attn` is unused since Florence-2 moved to transformers'
   own implementation and can go with that change.
