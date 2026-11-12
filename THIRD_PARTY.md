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
| Fonts | Liberation Sans (OFL-1.1, APK); Inter (OFL-1.1, Web) |
| Other notice licences | MS-PL (AQN Parser, DotNetZip), CC-BY-3.0 (FatCow icons) and the Iconmonstr icon licence, all inside `com.unity.visualscripting` (its `Third Party Notices.md`) |
| Permissive | Everything else: MIT, BSD-2/3-Clause, Apache-2.0, ISC, zlib, BSL-1.0, HPND |
| Unknown or forbidden | None remaining. The assets removed for this reason are listed in [section 5](#5-removed-assets) |

## 1. Quest app (Unity project)

### 1.1 Unity packages

From `Packages/manifest.json`, resolved in `Packages/packages-lock.json`.
Registry packages come from `https://packages.unity.com`; built-in packages
ship with the Unity Editor. The licence column quotes each package's
`LICENSE.md` (or `LICENSE`).

| Package | Version | Source | Licence | Ends up in | Third-party notices in the package |
| --- | --- | --- | --- | --- | --- |
| `com.meta.xr.sdk.all` | 85.0.0 | registry | Meta Platform Technologies SDK License Agreement | (meta-package) | |
| `com.meta.xr.sdk.core` | 85.0.0 | registry | Oculus SDK License Agreement (now the Meta Platform Technologies SDK License Agreement) | APK | `NOTICE.txt`: OpenVR (BSD-3-Clause), SimpleJSON (MIT), glTF loader (MIT, AltspaceVR) |
| `com.meta.xr.sdk.interaction` | 85.0.0 | registry | Oculus SDK License Agreement | APK | |
| `com.meta.xr.sdk.interaction.ovr` | 85.0.0 | registry | Oculus SDK License Agreement | APK | |
| `com.meta.xr.mrutilitykit` | 85.0.0 | registry | Oculus SDK License Agreement | APK | `NOTICE.txt`: Boost (BSL-1.0), earcut.hpp (ISC), Unity built-in shaders (MIT), an unnamed MPL-2.0 component, JSON for Modern C++ (MIT), folly (Apache-2.0), Protocol Buffers (BSD-3-Clause) |
| `com.meta.xr.sdk.platform` | 85.0.0 | registry | Meta Platform Technologies SDK License Agreement | APK | |
| `com.meta.xr.sdk.audio` | 85.0.0 | registry | Oculus SDK License Agreement | APK | `THIRD_PARTY_NOTICES.txt`: Kiss FFT, libjpeg, OOURA FFT, PFFFT, sfft, TinyThread++, zlib |
| `com.meta.xr.sdk.haptics` | 85.0.0 | registry | Oculus SDK License Agreement | APK | `THIRD_PARTY_NOTICES.txt`: Rust crates under Apache-2.0, MIT, ISC, BSL-1.0, Unlicense |
| `com.meta.xr.sdk.voice` | 85.0.0 | registry | Meta Platform Technologies SDK License Agreement | APK | |
| `org.khronos.unitygltf` (UnityGLTF) | 2.18.8 | git `KhronosGroup/UnityGLTF`, tag `release/2.18.8` = `84a8eb1` | MIT | APK | |
| `com.coplaydev.unity-mcp` (MCP for Unity) | 10.3.0 | git `CoplayDev/unity-mcp`, commit `aa5fc63` (tag `v10.3.0`) | MIT (repository `LICENSE`; the package has no licence file of its own and links to it from `package.json`) | Editor (see note 1) | |
| `com.github-glitchenzo.nugetforunity` (NuGetForUnity) | 4.4.0 | git `GlitchEnzo/NuGetForUnity`, commit `a22299e` | MIT | Editor | |
| `com.unity.render-pipelines.universal` | 17.3.0 | built-in | Unity Companion License | APK | FXAA 3.11 (NVIDIA licence) |
| `com.unity.render-pipelines.core` | 17.3.0 | built-in | Unity Companion License | APK | RadeonRays 4.1 (MIT), Bullet Physics (zlib), Sobol sampler (MIT) |
| `com.unity.render-pipelines.universal-config` | 17.0.3 | built-in | Unity Companion License | APK | |
| `com.unity.shadergraph` | 17.3.0 | built-in | Unity Companion License | APK (generated shaders) | |
| `com.unity.ugui` (includes TextMesh Pro) | 2.0.0 | built-in | Unity Companion License | APK | |
| `com.unity.textmeshpro` | 5.0.0 | built-in (empty; merged into uGUI) | none | none | |
| `com.unity.inputsystem` | 1.18.0 | registry | Unity Companion License | APK | |
| `com.unity.xr.management` | 4.5.4 | registry | Unity Companion License | APK | |
| `com.unity.xr.openxr` | 1.16.1 | registry | Unity Companion License (source), Unity Package Distribution License (binaries) | APK | OpenXR-SDK-Source (Apache-2.0); Oculus OpenXR Mobile SDK (Oculus SDK License Agreement) |
| `com.unity.xr.meta-openxr` | 2.4.0 | registry | Unity Companion License (source), Unity Package Distribution License (binaries) | APK | OpenXR-SDK-Source (Apache-2.0); Oculus OpenXR Mobile SDK and AudioPluginOculusSpatializer (Oculus SDK License Agreement) |
| `com.unity.xr.arfoundation` | 6.4.0 | registry | Unity Companion License | APK | |
| `com.unity.xr.core-utils` | 2.5.3 | registry | Unity Companion License | APK | |
| `com.unity.xr.compositionlayers` | 2.3.0 | registry | Unity Companion License (source), Unity Package Distribution License (binaries) | APK | |
| `com.unity.xr.legacyinputhelpers` | 2.1.13 | registry | Unity Companion License | APK | Arm models (Apache-2.0, Google) |
| `com.unity.ai.navigation` | 2.0.11 | registry | Unity Companion License | APK | |
| `com.unity.burst` | 1.8.28 | registry | Unity Companion License (source), Unity Package Distribution License (binaries) | APK (Burst-compiled code); compiler Editor only | LLVM (Apache-2.0 with LLVM exception), NCSA, Mono.Cecil (MIT), Smash and xxHash (BSD-2-Clause) |
| `com.unity.collections` | 2.6.2 | registry | Unity Companion License | APK | |
| `com.unity.mathematics` | 1.3.3 | registry | Unity Companion License | APK | |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | registry | Unity Companion License | APK | Newtonsoft.Json and its Unity forks (MIT) |
| `com.unity.timeline` | 1.8.10 | registry | Unity Companion License | APK | |
| `com.unity.visualscripting` | 1.9.9 | registry | Unity Package Distribution License | APK (runtime), Editor | AQN Parser and DotNetZip (MS-PL), Deep Copy, Full Serializer, MD4, Reorderable List, SQLite .NET, YamlDotNet, Ensure.That, NCalc (MIT), FatCow icons (CC-BY-3.0), Iconmonstr icons (Iconmonstr licence), SQLite (public domain), ANTLR 3 runtime (BSD) |
| `com.unity.2d.sprite` | 1.0.0 | built-in | Unity Package Distribution License | Editor | |
| `com.unity.test-framework` | 1.6.0 | built-in | Unity Companion License | Dev | |
| `com.unity.ext.nunit` | 2.0.5 | built-in | Unity Package Distribution License | Dev | NUnit 3.5 (MIT) |
| `com.unity.test-framework.performance` | 3.2.0 | registry | Unity Companion License | Dev | Perfolizer (MIT) |
| `com.unity.ide.rider` | 3.0.39 | registry | MIT (Unity Technologies, JetBrains) | Editor | |
| `com.unity.ide.visualstudio` | 2.0.27 | registry | MIT (Unity Technologies, Microsoft) | Editor | VSWhere (MIT), EnvDTE (0BSD) |
| `com.unity.mobile.android-logcat` | 1.4.7 | registry | Unity Companion License | Editor | |
| `com.unity.multiplayer.center` | 1.0.1 | built-in | Unity Package Distribution License | Editor | |
| `com.unity.editorcoroutines` | 1.0.1 | registry | Unity Companion License | Editor | |
| `com.unity.searcher` | 4.9.4 | registry | Unity Companion Package License v1.0 | Editor | |
| `com.unity.nuget.mono-cecil` | 1.11.6 | registry | Unity Companion License | Editor | Mono.Cecil (MIT) |
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
The package is pinned to a release commit instead of the moving `#main`
branch.

### 1.2 Vendored in `Assets/` and `Packages/`

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

## 2. Web client (`web/`)

From `web/package.json` and `web/package-lock.json`. The `Web` column was
checked against the source maps of a production build (`vite build`): only the
packages marked `Web` are in the bundle.

| Package | Version | Licence | Ends up in |
| --- | --- | --- | --- |
| `three` | 0.183.2 | MIT | Web |
| `@iwsdk/core`, `@iwsdk/glxf`, `@iwsdk/locomotor`, `@iwsdk/xr-input` | 0.3.1 | MIT | Web |
| `@babylonjs/havok` | 1.3.11 | MIT | Web (`HavokPhysics.wasm`) |
| `@pmndrs/uikit` | 1.0.64 | MIT (the package says "SEE LICENSE IN LICENSE"; that file is the MIT text) | Web |
| `@pmndrs/uikit-pub-sub` | 1.0.64 | MIT | Web |
| `@pmndrs/uikitml` | 0.1.12 | MIT | Web |
| `@pmndrs/handle`, `@pmndrs/pointer-events` | 6.6.29 | MIT (same "SEE LICENSE IN LICENSE" file) | Web |
| `@pmndrs/msdfonts` | 1.0.64 | MIT for the code; the fonts keep their own licences (OFL-1.1 for Inter and most families, Apache-2.0 for Roboto) | Web: only the Inter atlases are bundled |
| `@preact/signals-core` | 1.14.0 | MIT | Web |
| `elics` | 3.4.2 | MIT | Web |
| `parse5` | 7.3.0 | MIT | Web |
| `three-mesh-bvh` | 0.9.9 | MIT | Web |
| `yoga-layout` | 3.2.1 | MIT (declared in `package.json`; upstream `facebook/yoga` is MIT) | Web |
| `entities` 6.0.1 (BSD-2-Clause), `comlink` 4.4.2 (Apache-2.0), `react` 19.2.4, `use-sync-external-store` 1.6.0, `zustand` 4.5.7, `@zappar/msdf-generator` 1.2.4, `chai` 5.3.3, `assertion-error`, `check-error`, `deep-eql`, `loupe`, `pathval`, `inline-style-parser`, `@types/emscripten` (MIT) | as listed | as listed | Installed with the runtime dependencies; not in the bundle |
| `vite` 6.4.4, `esbuild` 0.25.12, `rollup` 4.59.0, `postcss` 8.5.32, `@types/node` 24.12.0, `@types/three` 0.177.0 (MIT); `typescript` 5.9.3, `@playwright/test` 1.58.2 (Apache-2.0) | as listed | as listed | Dev |

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
| Ubuntu packages from `apt-get` (`python3.10`, `git`, `build-essential`, `ninja-build`, `cmake`, `libgl1`, `libglib2.0-0`) | Ubuntu 22.04 | Various (PSF, GPL, LGPL, MIT, BSD) | Build and runtime tools inside the image. |

### 3.2 Python packages

Licences from the PyPI metadata of the pinned version (`license_expression`,
`license` or classifiers).

| Package | Version | Licence | Notes |
| --- | --- | --- | --- |
| `torch`, `torchvision`, `torchaudio` | unpinned, CUDA 12.1 wheels from `download.pytorch.org` | BSD-3-Clause, with bundled third-party code under other permissive licences (current `torch` metadata: Apache-2.0 AND BSD-2-Clause AND BSD-3-Clause AND BSL-1.0 AND ISC AND MIT AND Zlib) | The CUDA wheels carry NVIDIA runtime libraries under NVIDIA licences. |
| `flash-attn` | unpinned | BSD-3-Clause | Installed in the image for Florence-2's model code. |
| `packaging` | unpinned | Apache-2.0 OR BSD-2-Clause | |
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
| `transformers` | 5.10.1 | Apache-2.0 | |
| `accelerate` | 1.15.0 | Apache-2.0 | |
| `einops` | 0.7.0 | MIT | |
| `timm` | 0.9.16 | Apache-2.0 | |
| GroundingDINO | git `IDEA-Research/GroundingDINO`, default branch, unpinned | Apache-2.0 | Brings `supervision` (MIT), `addict` (MIT), `yapf` (Apache-2.0). |
| SAM 2 (`sam2`) | git `facebookresearch/sam2`, default branch, unpinned | Apache-2.0 | Brings `hydra-core` (MIT), `iopath` (MIT), `tqdm` (MPL-2.0 AND MIT). |
| `locust` (`cv-server/load/requirements.txt`) | 2.27.0 | MIT | Dev (load testing). |

### 3.3 Model weights and configs

None of these files is in the repository. The operator puts the checkpoints
and configs in `cv-server/checkpoints/` and `cv-server/configs/` (both
ignored by git); Hugging Face models download into the container's Hugging
Face cache.

| Model | File or ID | Source | Licence (where stated) | Research-only or non-commercial terms? |
| --- | --- | --- | --- | --- |
| Grounding DINO, Swin-T OGC | `groundingdino_swint_ogc.pth`; config `GroundingDINO_SwinT_OGC.py` | GitHub release `v0.1.0-alpha` of <https://github.com/IDEA-Research/GroundingDINO>; mirror <https://huggingface.co/ShilongLiu/GroundingDINO> | Apache-2.0 (repository `LICENSE`; Hugging Face card `license: apache-2.0`) | No |
| BERT base, uncased | `bert-base-uncased`, set as `text_encoder_type` in the Grounding DINO config and downloaded on first load | <https://huggingface.co/google-bert/bert-base-uncased> | Apache-2.0 (model card) | No |
| SAM 2.1 Hiera Tiny | `sam2.1_hiera_tiny.pt`; config `configs/sam2.1/sam2.1_hiera_t.yaml` (ships in the `sam2` package) | <https://github.com/facebookresearch/sam2> (also <https://huggingface.co/facebook/sam2.1-hiera-tiny>) | Apache-2.0: "The SAM 2 model checkpoints ... are licensed under Apache 2.0" (README, "License") | No |
| Florence-2-large | `microsoft/Florence-2-large`, loaded with `trust_remote_code=True` | <https://huggingface.co/microsoft/Florence-2-large> | MIT (model card `license: mit`; covers the remote model code in the same repository) | No |

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

These files had no recorded source, author or licence, or their licence was
unclear, so they were removed.

| Removed | Why | Replacement |
| --- | --- | --- |
| 14 photos in `test/` (`How-To-Organize-ACloset.jpg`, `bedroom.jpg`, `bedroom2.jpg`, `closet2.jpg`, `closet3.jpeg`, `food-storage-containers.jpg`, `food-storage-jars.jpg`, `laundry-room1.jpg`, `laundry-room2.jpg`, `living-room.jpg`, `pantry-organization.jpg`, `pantry-organization2.jpg`, `pantry.jpg`, `walk-in-pantry-design.jpg`) and 11 copies in `Assets/CompanionScan/Tests/Fixtures/` | Web photographs with no recorded source, author or licence | Six synthetic room renders (`synthetic_*_1280x1024.jpg`) drawn by `tools/generate_synthetic_fixtures.py` with Pillow only; `PerImageModelTests` uses them |
| `Assets/TextMesh Pro/Sprites/EmojiOne.png`, `EmojiOne.json`, `EmojiOne Attribution.txt` and `Resources/Sprite Assets/EmojiOne.asset` | TextMesh Pro sample emoji. The attribution file names no licence: it says to "review their licensing terms" on EmojiOne's site | None. The project uses no `<sprite>` tags; TMP Settings no longer names a default sprite asset |
| `Assets/number_atlas.png` | Digit atlas from the initial commit with no source or licence, referenced by nothing | None |
| `Assets/Plugins/Demigiant.meta`, `Assets/Plugins/Demigiant/DOTween.meta`, `Assets/Plugins/Demigiant/DOTween/Editor.meta` | Folder metadata for DOTween (an Asset Store package) whose files were never committed | None; DOTween is not used |
| `Assets/CompanionScanLegacy.meta` | Folder metadata for a legacy sample whose files are not in the repository | None |
| `Assets/Resources/PerformanceTestRunInfo.json`, `PerformanceTestRunSettings.json` | Generated Performance Testing output that shipped in every player through `Resources` | None |

## 6. Open items for the owner

1. Ship the notices with each build: an "Open-source licences" screen in the
   Quest app and a notices file in `web/dist`. Nothing in the builds carries
   them today.
2. Meta XR SDK: apps built with it may target only Meta-approved hardware
   and software (licence §1.1). `com.meta.xr.sdk.all` also pulls in Voice,
   Audio, Haptics and Platform. If the app does not use them, depending on
   only the packages it needs shrinks the APK and the licence surface.
3. CV-server image: publishing a built image redistributes NVIDIA CUDA and
   cuDNN (NVIDIA Deep Learning Container License) and LGPL FFmpeg and Qt
   (`opencv-python`). Publishing the Dockerfile alone does not. Switching to
   `opencv-python-headless` drops Qt and `libgl1`.
4. Reproducibility: GroundingDINO and SAM 2 are installed from unpinned git
   branches, `torch`/`flash-attn` are unpinned, and Florence-2 is loaded with
   `trust_remote_code=True` without a pinned `revision`. Pin each to a commit
   or version so the licence and the code you ship cannot change underneath
   you.
5. The npm/PyPI attribution generator must not overwrite this file or
   [NOTICE.md](NOTICE.md); it cannot see Unity packages, assets or weights.
