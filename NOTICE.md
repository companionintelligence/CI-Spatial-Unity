# NOTICE

Companion Scan
Copyright LifeScope INC, DBA Companion Intelligence (<https://ci.computer>)

Required Notice: Copyright LifeScope INC, DBA Companion Intelligence (https://ci.computer)

The first-party code and assets in this repository are licensed under the
[PolyForm Noncommercial License 1.0.0](LICENSE). That licence does not cover
the third-party components named below. Each of them stays under its own
licence, and some of those licences are **not** open source.

This file lists the licence terms and the attribution notices that apply to
the things we build from this repository. The full inventory, with versions,
sources and where each component ends up, is in
[THIRD_PARTY.md](THIRD_PARTY.md).

This file is maintained by hand. `generate-attribution.mjs` (CI-Engineering)
only reads npm and PyPI manifests, so it cannot see the Unity packages,
vendored assets or model weights listed here. Do not overwrite this file with
its output.

## What to ship with a build

The MIT, BSD, Apache-2.0 and SIL Open Font licences below require their
notices to travel with binary copies. When you distribute the Quest APK, the
web bundle (`web/dist`) or a CV-server container image, include this file and
the licence texts it points to, for example as an "Open-source licences"
screen in the app or as a text file inside the package.

## 1. Components that are not open source

These licences are proprietary or restrict how the component may be used. They
allow us to build and sell Companion Scan, but only on the terms shown.

| Component | Licence | Ends up in | Terms that matter |
| --- | --- | --- | --- |
| Meta XR SDK 85.0.0: `com.meta.xr.sdk.all`, `.core`, `.interaction`, `.interaction.ovr`, `.platform`, `.audio`, `.haptics`, `.voice`, `com.meta.xr.mrutilitykit` | [Meta Platform Technologies SDK License Agreement](https://developers.meta.com/horizon/licenses/oculussdk/) (formerly the Oculus SDK License Agreement; each package's `LICENSE.md` names one of the two) | Quest APK | Use only "to develop Applications in connection with MPT approved hardware and software products" (§1.1). Do not use or redistribute the SDK in a way that would make it subject to an open-source licence (§1.2.8). Meta reserves all rights not granted (§3.1). Redistribution inside our application is allowed. Not vendored: Unity resolves it from `packages.unity.com`. |
| Unity engine runtime and engine modules (`com.unity.modules.*`) | [Unity Editor Software Terms](https://unity.com/legal/terms-of-service/software) | Quest APK | Proprietary. "You may distribute the Unity Runtime as an integrated part of your Projects" (§2.2), subject to the fees and eligibility of your Unity plan. |
| Unity packages (`com.unity.*`, versions in [THIRD_PARTY.md](THIRD_PARTY.md)) | [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) for most packages and package source; [Unity Package Distribution License](https://unity3d.com/legal/licenses/Unity_Package_Distribution_License) for binaries and for `com.unity.2d.sprite`, `com.unity.ext.nunit`, `com.unity.multiplayer.center` and `com.unity.visualscripting` (see each package's `LICENSE.md`) | Runtime parts in the Quest APK | Source-available, not open source: use is limited to authoring and distributing content made with Unity. |
| TextMesh Pro Essential Resources (`Assets/TextMesh Pro/`, except Liberation Sans, see section 3.1) and the URP project template files (`Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/InputSystem_Actions.inputactions`) | Unity Companion License (from `com.unity.ugui` and `com.unity.template.urp-blank`) | TMP resources and the input actions asset in the Quest APK; Readme and TutorialInfo Editor only | As above. |
| NVIDIA CUDA base image `nvidia/cuda:12.1.1-cudnn8-devel-ubuntu22.04` | [NVIDIA Deep Learning Container License](https://developer.nvidia.com/ngc/nvidia-deep-learning-container-license); the Dockerfiles themselves are BSD-3-Clause (Docker Hub `nvidia/cuda` description) | CV-server image only | Pulling and using the images means accepting NVIDIA's licences (Docker Hub description). Publishing a built image redistributes NVIDIA software: review the licence first. The repository only contains our Dockerfile. |
| PyTorch CUDA 12.1 wheels (`torch`, `torchvision`, `torchaudio` from `download.pytorch.org/whl/cu121`) | PyTorch: BSD-3-Clause, with bundled third-party code under other permissive licences; the CUDA wheels also carry NVIDIA runtime libraries under NVIDIA's licences | CV-server image only | Same caution as the base image if you publish the image. |

## 2. Copyleft and weak-copyleft components

- **Quest APK:** the Meta MR Utility Kit `NOTICE.txt` includes the Mozilla
  Public License 2.0 for a component it does not name. It is part of Meta's
  binary; the notice is reproduced in that package. No GPL or LGPL code is
  known to ship in the APK.
- **Web bundle:** none. Everything bundled is MIT, apart from the Inter font
  (SIL OFL 1.1).
- **CV-server image only** (relevant if you publish the image, not if you
  publish the Dockerfile):
  - `opencv-python` 4.9.0.80 wheels "ship with FFmpeg licensed under the
    LGPLv2.1", and non-headless Linux wheels ship Qt 5 under the LGPLv3
    (opencv-python README, "Licensing").
  - `numpy` 1.26.4 Linux wheels bundle the GCC runtime library
    (GPL-3.0 with the GCC Runtime Library Exception) and libquadmath
    (LGPL-2.1-or-later) (numpy `tools/wheels/LICENSE_linux.txt`); `scipy`
    wheels bundle the same kind of runtime.
  - `tqdm` (pulled in by SAM 2): MPL-2.0 AND MIT.
  - Ubuntu 22.04 system packages in the base image: various, including GPL.
    NVIDIA archives the sources at
    <https://developer.download.nvidia.com/compute/cuda/opensource/image>.

## 3. Attribution notices

### 3.1 Quest app (APK)

| Component | Licence | Copyright | Full text |
| --- | --- | --- | --- |
| NaughtyAttributes 2.1.4 | MIT | Copyright (c) 2017 Denis Rizov | `Assets/NaughtyAttributes/LICENSE.md` |
| UnityGLTF 2.18.8 | MIT | Copyright (c) 2017 AltspaceVR; Copyright (c) 2020-2023 prefrontal cortex; Copyright (c) 2022-2023 Needle | package `LICENSE` |
| Json.NET (Newtonsoft.Json), via `com.unity.nuget.newtonsoft-json` 3.2.2 | MIT | Copyright (c) 2007 James Newton-King | package `Third Party Notices.md` |
| Microsoft.Bcl.AsyncInterfaces 9.0.5, System.IO.Pipelines 9.0.5, System.Net.Http.Json 9.0.5, System.Runtime.CompilerServices.Unsafe 6.0.0, System.Text.Encodings.Web 9.0.5, System.Text.Json 9.0.5 | MIT | Copyright (c) .NET Foundation and Contributors | `Assets/Packages/<package>/LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` |
| Liberation Sans 2.00.1 (TextMesh Pro default font) | SIL Open Font License 1.1 | Digitized data copyright (c) 2010 Google Corporation, with Reserved Font Arimo, Tinos and Cousine. Copyright (c) 2012 Red Hat, Inc., with Reserved Font Name Liberation. | `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` |
| Khronos OpenXR loader (OpenXR-SDK-Source), via `com.unity.xr.openxr` and `com.unity.xr.meta-openxr` | Apache-2.0 | Copyright (c) The Khronos Group Inc. | package `Third Party Notices.md` |
| Third-party code inside the Meta XR SDK: OpenVR (BSD-3-Clause, Valve), SimpleJSON (MIT), glTF loader (MIT, AltspaceVR); Boost (BSL-1.0), earcut.hpp (ISC), JSON for Modern C++ (MIT), folly (Apache-2.0), Protocol Buffers (BSD-3-Clause), an MPL-2.0 component; Kiss FFT, libjpeg, OOURA FFT, PFFFT, sfft, TinyThread++, zlib; Rust crates under Apache-2.0, MIT, ISC, BSL-1.0 and the Unlicense | as listed | as listed | `NOTICE.txt` in `com.meta.xr.sdk.core` and `com.meta.xr.mrutilitykit`; `THIRD_PARTY_NOTICES.txt` in `com.meta.xr.sdk.audio` and `com.meta.xr.sdk.haptics` |
| Third-party code inside Unity packages: FXAA 3.11 (NVIDIA), RadeonRays (MIT), Bullet Physics (zlib), Sobol sampler (MIT), arm models (Apache-2.0), Visual Scripting components (MIT, MS-PL, BSD, CC-BY-3.0 icons, public domain) | as listed | as listed | each package's `Third Party Notices.md` |

The Unity Editor tools MCP for Unity (`com.coplaydev.unity-mcp`, MIT) and
NuGetForUnity (MIT) are development dependencies and are not in the APK;
`EditorOnlyPackageAssemblyFilter` removes the MCP for Unity runtime assembly
from player builds.

### 3.2 Web client bundle (`web/dist`)

| Component | Licence | Copyright |
| --- | --- | --- |
| three.js 0.183.2 | MIT | Copyright © 2010-2026 three.js authors |
| Immersive Web SDK 0.3.1 (`@iwsdk/core`, `@iwsdk/glxf`, `@iwsdk/locomotor`, `@iwsdk/xr-input`) | MIT | Copyright (c) Meta Platforms, Inc. and affiliates. |
| `@pmndrs/uikit` 1.0.64, `@pmndrs/uikit-pub-sub` 1.0.64, `@pmndrs/uikitml` 0.1.12, `@pmndrs/handle` 6.6.29, `@pmndrs/pointer-events` 6.6.29, `@pmndrs/msdfonts` 1.0.64 | MIT | Copyright 2024 Bela Bohlender; Copyright 2023 Coconut Capital |
| Inter font, embedded as MSDF atlases by `@pmndrs/msdfonts` | SIL Open Font License 1.1 | Copyright (c) 2016-2020 The Inter Project Authors. "Inter" is a trademark of Rasmus Andersson. |
| Havok Physics for Babylon.js (`@babylonjs/havok` 1.3.11) | MIT | Copyright (c) 2023 Babylon.js |
| `@preact/signals-core` 1.14.0 | MIT | Copyright (c) 2022-present Preact Team |
| elics 3.4.2 | MIT | Copyright (c) 2022 - present EliXR Games |
| parse5 7.3.0 | MIT | Copyright (c) 2013-2019 Ivan Nikulin |
| three-mesh-bvh 0.9.9 | MIT | Copyright (c) 2018 Garrett Johnson |
| yoga-layout 3.2.1 | MIT | Copyright (c) Meta Platforms, Inc. and affiliates. |

Loaded at run time from public CDNs, not part of our bundle: WebXR Input
Profiles controller models (`@webxr-input-profiles/assets` 1.0, MIT, from
jsDelivr; the models depict device makers' trademarks, which the licence does
not cover) and, only when a Draco- or KTX2-compressed glTF is loaded, the
three.js Draco and Basis Universal decoders (Apache-2.0, from unpkg).

### 3.3 CV server and model weights

The weights are not in this repository. The operator downloads them into
`cv-server/checkpoints/` or the Hugging Face cache.

| Model | Licence | Copyright / publisher | Source |
| --- | --- | --- | --- |
| Grounding DINO Swin-T OGC (`groundingdino_swint_ogc.pth`) and its config | Apache-2.0 | IDEA Research | <https://github.com/IDEA-Research/GroundingDINO> (release `v0.1.0-alpha`) |
| BERT base, uncased (text encoder that Grounding DINO downloads on first load) | Apache-2.0 | Google | <https://huggingface.co/google-bert/bert-base-uncased> |
| SAM 2.1 Hiera Tiny (`sam2.1_hiera_tiny.pt`) and its config | Apache-2.0 | Meta Platforms, Inc. | <https://github.com/facebookresearch/sam2> |
| Florence-2-large, including the model code it loads with `trust_remote_code` | MIT | Microsoft | <https://huggingface.co/microsoft/Florence-2-large> |

None of these weights carries research-only or non-commercial terms. The
Python packages in the server image are under MIT, BSD, Apache-2.0, HPND
(Pillow, `MIT-CMU`) or the licences in section 2; see
[THIRD_PARTY.md](THIRD_PARTY.md#3-cv-server-cv-server).

## 4. Licence texts

- MIT: the text below, with the copyright lines from section 3.
- Apache-2.0: <https://www.apache.org/licenses/LICENSE-2.0>
- SIL Open Font License 1.1: `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`
  and <https://openfontlicense.org>
- BSD, ISC, zlib, BSL-1.0, MPL-2.0 and the other texts named above: in the
  `NOTICE.txt`, `THIRD_PARTY_NOTICES.txt`, `Third Party Notices.md` or
  `THIRD-PARTY-NOTICES.TXT` file of the package that contains the component.

### MIT License

```text
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
