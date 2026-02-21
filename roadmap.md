# Formal Software Development Plan
## Cross-Platform House Mapping System with Grounding DINO, Grounded-SAM 2 &amp; Florence-2

**Version:** 2.0  
**Date:** February 21, 2026  
**Author:** AI Architecture Assistant  
**Client Hardware:** Linux server with NVIDIA RTX 4080 (16 GB GDDR6X VRAM)[1][2]
**Target Platforms:** Meta Quest 3, iOS (ARKit), Android (ARCore)

***

## 1. Executive Summary

This plan extends the original Grounding DINO zero-shot detection server into a **multi-model computer vision pipeline** that supports three complementary model families behind a single FastAPI REST interface:

| Model | Purpose | Parameters | Approx. VRAM (FP16) |
|---|---|---|---|
| Grounding DINO (Swin-Tiny) | Open-vocabulary bounding-box detection | ~172M | ~2–4 GB [3][1] |
| SAM 2 (Hiera-Tiny) | Promptable pixel-level segmentation | ~38.9M | ~1–2 GB [4][5] |
| Florence-2-base | Unified vision-language (caption, detect, ground, OCR) | ~230M | ~0.7 GB [6] |
| Florence-2-large | Higher-accuracy variant | ~770M | ~1.2 GB [6][7] |

All three model families fit comfortably within the RTX 4080's 16 GB VRAM, even when loaded simultaneously. The Unity client is extended with mask-overlay rendering and richer annotation types while maintaining a single shared codebase across Quest 3, iOS, and Android via the `ICameraProvider` abstraction and `UnityWebRequest`.[8][2]

***

## 2. System Architecture Overview

```
┌──────────────────────────────────────────────────┐
│                 Unity Client (Shared)             │
│  ┌─────────────┐ ┌──────────┐ ┌───────────────┐  │
│  │ICameraProvider│ │CVServer  │ │AnnotationRenderer│
│  │(Quest/iOS/   │ │Client    │ │(Boxes + Masks │  │
│  │ Android)     │ │(HTTP)    │ │ + Labels)     │  │
│  └──────┬──────┘ └────┬─────┘ └───────┬───────┘  │
│         │ frames       │ JSON req/res   │ overlay  │
└─────────┼──────────────┼───────────────┼──────────┘
          │    WiFi 5GHz │               │
          ▼              ▼               │
┌──────────────────────────────────────────────────┐
│            Linux CV Server (RTX 4080)            │
│  ┌──────────────────────────────────────────┐    │
│  │           FastAPI + Uvicorn               │    │
│  │  ┌─────────┐ ┌────────┐ ┌────────────┐  │    │
│  │  │ /detect  │ │/segment│ │ /florence   │  │    │
│  │  │ (G-DINO) │ │(G-SAM2)│ │ (Flor-2)   │  │    │
│  │  └────┬────┘ └───┬────┘ └─────┬──────┘  │    │
│  │       │          │            │          │    │
│  │  ┌────▼──────────▼────────────▼───────┐  │    │
│  │  │        ModelManager (Singleton)     │  │    │
│  │  │  - Grounding DINO (Swin-T)         │  │    │
│  │  │  - SAM 2 (Hiera-Tiny/Base+)        │  │    │
│  │  │  - Florence-2 (base or large)      │  │    │
│  │  │  - GPU memory monitor              │  │    │
│  │  └────────────────────────────────────┘  │    │
│  └──────────────────────────────────────────┘    │
│  Docker (NVIDIA Container Toolkit, CUDA 12.x)    │
└──────────────────────────────────────────────────┘
```

***

## 3. Model Descriptions and Rationale

### 3.1 Grounding DINO (Retained from v1.0)

Grounding DINO remains the primary **open-vocabulary object detector**. It marries a Swin Transformer visual backbone with BERT-based text encoding through bidirectional cross-attention, enabling zero-shot detection of arbitrary objects described in natural language. The Swin-Tiny variant processes images at approximately 100–300 ms on consumer GPUs and occupies only 2–4 GB VRAM during inference.[3][1]

### 3.2 SAM 2 (Segment Anything Model 2)

SAM 2 extends the original SAM with a **memory-based streaming architecture** that enables both image segmentation and video object tracking. When paired with Grounding DINO in the Grounded-SAM 2 pipeline, the workflow is a cascade: Grounding DINO outputs bounding boxes, and SAM 2 converts each box into a precise pixel-level mask. This is critical for house mapping — knowing not just *where* an object is, but its *exact boundary* for accurate 3D reconstruction.[9][10][11]

Key architectural features relevant to this project:[4][11]
- **Promptable segmentation**: accepts box, point, or mask prompts from the detection stage
- **Memory bank**: maintains object state across frames via a FIFO queue, enabling temporal consistency
- **Model variants**: Tiny (38.9M params, ~47 FPS on A100), Small, Base+, and Large (225M params, ~30 FPS on A100)[4]
- **VRAM**: The Large checkpoint uses approximately 5 GB on GPU; the Tiny variant uses significantly less (~1–2 GB)[5]

For the RTX 4080 deployment, **SAM 2 Hiera-Tiny** is recommended for real-time use, with the option to upgrade to Hiera-Base+ for higher mask quality in batch/offline modes.

### 3.3 Florence-2

Florence-2 is Microsoft's unified vision-language model that converts diverse vision tasks into a single sequence-to-sequence format using text prompts. Unlike Grounding DINO (which only does detection), Florence-2 supports:[12][7]

- **Object detection** (`<OD>`)
- **Dense region captioning** (`<DENSE_REGION_CAPTION>`)
- **Phrase grounding** (`<CAPTION_TO_PHRASE_GROUNDING>`)
- **Open vocabulary detection** (`<OPEN_VOCABULARY_DETECTION>`)
- **OCR / text recognition**
- **Image captioning** (multiple granularities)[10]

This makes Florence-2 extremely valuable for house mapping: it can **auto-caption rooms** (describing what it sees), **read labels on items**, and serve as an **alternative grounding backbone** to Grounding DINO within the Grounded-SAM 2 pipeline.[9][10]

Florence-2-base (230M params) uses only ~658 MB VRAM; Florence-2-large (770M params) uses ~1.2 GB. Both are trivially small relative to the 4080's 16 GB budget.[6][2]

***

## 4. VRAM Budget (RTX 4080 — 16 GB)

| Component | Model Variant | Est. VRAM | Notes |
|---|---|---|---|
| Grounding DINO | Swin-Tiny + BERT-base | ~3.0 GB | Includes deformable attention buffers [3] |
| SAM 2 | Hiera-Tiny | ~1.5 GB | Image-only mode; video mode adds memory bank overhead [5][4] |
| Florence-2 | Large (0.77B) | ~1.2 GB | HuggingFace transformers, FP16 [6] |
| PyTorch/CUDA overhead | — | ~1.5 GB | Context, kernel cache, allocator fragmentation |
| **Total (all loaded)** | — | **~7.2 GB** | |
| **Remaining headroom** | — | **~8.8 GB** | Available for batch processing, larger SAM 2 variants, or future models |

This budget confirms all three models can be **loaded simultaneously** in GPU memory with substantial headroom. No model swapping or offloading is required.[2][1]

***

## 5. Server Implementation

### 5.1 Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Container | Docker + NVIDIA Container Toolkit | Latest |
| Base Image | `nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04` | — |
| Runtime | Python 3.10+, PyTorch ≥ 2.3.1, CUDA 12.1 | [13] |
| Framework | FastAPI + Uvicorn | Latest |
| Models | Grounding DINO (IDEA-Research), SAM 2 (Meta), Florence-2 (Microsoft/HuggingFace) | [14][10][7] |

### 5.2 Project Structure

```
cv-server/
├── Dockerfile
├── docker-compose.yml
├── requirements.txt
├── app/
│   ├── main.py                 # FastAPI app, startup/shutdown
│   ├── config.py               # Environment variables, model paths, thresholds
│   ├── model_manager.py        # Singleton: loads/holds all models on GPU
│   ├── routers/
│   │   ├── detect.py           # /v1/detect  (Grounding DINO only)
│   │   ├── segment.py          # /v1/segment (Grounded-SAM 2 pipeline)
│   │   ├── florence.py         # /v1/florence (Florence-2 multi-task)
│   │   └── health.py           # /v1/health  (status, VRAM, latency)
│   ├── pipelines/
│   │   ├── grounding_dino.py   # Grounding DINO inference wrapper
│   │   ├── grounded_sam2.py    # G-DINO → SAM 2 cascade pipeline
│   │   ├── florence2.py        # Florence-2 task dispatcher
│   │   └── florence_sam2.py    # Florence-2 → SAM 2 cascade pipeline
│   ├── schemas/
│   │   ├── detect.py           # Pydantic models for detection requests/responses
│   │   ├── segment.py          # Pydantic models for segmentation (includes RLE masks)
│   │   └── florence.py         # Pydantic models for Florence-2 tasks
│   └── utils/
│       ├── image_utils.py      # Base64 decode, resize, tensor conversion
│       ├── mask_encoding.py    # RLE encode/decode, mask → polygon
│       └── gpu_monitor.py      # VRAM usage, temperature monitoring
├── checkpoints/
│   ├── groundingdino_swint_ogc.pth
│   ├── sam2.1_hiera_tiny.pt
│   └── (Florence-2 auto-downloaded from HuggingFace on first run)
├── configs/
│   ├── GroundingDINO_SwinT_OGC.py
│   └── sam2.1_hiera_t.yaml
└── tests/
    ├── test_detect.py
    ├── test_segment.py
    └── test_florence.py
```

### 5.3 Model Manager (Singleton)

```python
# app/model_manager.py (conceptual)
class ModelManager:
    _instance = None

    def __init__(self):
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        self.gdino_model = None
        self.sam2_predictor = None
        self.florence_model = None
        self.florence_processor = None

    @classmethod
    def get_instance(cls):
        if cls._instance is None:
            cls._instance = cls()
        return cls._instance

    def load_all(self):
        # 1. Grounding DINO
        self.gdino_model = load_model(
            "configs/GroundingDINO_SwinT_OGC.py",
            "checkpoints/groundingdino_swint_ogc.pth",
            device=self.device
        )
        # 2. SAM 2
        self.sam2_predictor = build_sam2(
            "configs/sam2.1_hiera_t.yaml",
            "checkpoints/sam2.1_hiera_tiny.pt",
            device=self.device
        )
        # 3. Florence-2
        self.florence_model = AutoModelForCausalLM.from_pretrained(
            "microsoft/Florence-2-large",
            torch_dtype=torch.float16,
            trust_remote_code=True
        ).to(self.device)
        self.florence_processor = AutoProcessor.from_pretrained(
            "microsoft/Florence-2-large",
            trust_remote_code=True
        )
        log_vram_usage()
```

All models load at FastAPI startup (`@app.on_event("startup")`) and persist for the lifetime of the container.[15]

### 5.4 API Endpoints

#### 5.4.1 Detection — `POST /v1/detect`

Performs Grounding DINO zero-shot detection (backward-compatible with the original plan).

**Request:**
```json
{
  "image": "<base64-encoded JPEG>",
  "prompt": "chair . table . lamp .",
  "box_threshold": 0.35,
  "text_threshold": 0.25
}
```

**Response:**
```json
{
  "bboxes": [[x1,y1,x2,y2], ...],
  "labels": ["chair", "table", ...],
  "scores": [0.87, 0.72, ...]
}
```

#### 5.4.2 Segmentation — `POST /v1/segment`

Runs the full Grounded-SAM 2 cascade: Grounding DINO detects → SAM 2 segments.

**Request:**
```json
{
  "image": "<base64-encoded JPEG>",
  "prompt": "couch . window . door .",
  "box_threshold": 0.35,
  "text_threshold": 0.25,
  "grounding_model": "grounding_dino",  // or "florence2"
  "return_masks": true,
  "mask_format": "rle"                  // "rle" | "polygon" | "base64_png"
}
```

**Response:**
```json
{
  "annotations": [
    {
      "label": "couch",
      "score": 0.91,
      "bbox": [120, 200, 450, 480],
      "mask": {
        "size": [720, 1280],
        "counts": "<RLE-encoded string>"
      }
    }
  ],
  "image_size": [720, 1280]
}
```

The `grounding_model` parameter allows the client to choose between Grounding DINO and Florence-2 as the detection backbone for the SAM 2 cascade. This is a key extensibility point — the Grounded-SAM 2 repo explicitly supports swapping in Florence-2 for detection.[10][9]

#### 5.4.3 Florence-2 Multi-Task — `POST /v1/florence`

Exposes Florence-2's full range of vision-language capabilities.

**Request:**
```json
{
  "image": "<base64-encoded JPEG>",
  "task": "<DENSE_REGION_CAPTION>",
  "text_input": ""  // optional, required for phrase grounding / open vocab
}
```

**Supported tasks** (maps directly to Florence-2 task prompts):[10]

| Task Prompt | Requires Text Input | Description |
|---|---|---|
| `<OD>` | No | Object detection with single category names |
| `<DENSE_REGION_CAPTION>` | No | Detect objects with short descriptions |
| `<REGION_PROPOSAL>` | No | Generate bounding box proposals |
| `<CAPTION>` | No | Image caption |
| `<DETAILED_CAPTION>` | No | Detailed image caption |
| `<MORE_DETAILED_CAPTION>` | No | Highly detailed image caption |
| `<CAPTION_TO_PHRASE_GROUNDING>` | Yes | Ground phrases from caption in image |
| `<OPEN_VOCABULARY_DETECTION>` | Yes | Detect objects by arbitrary text |
| `<OCR>` | No | Optical character recognition |

**Response** (varies by task, example for `<DENSE_REGION_CAPTION>`):
```json
{
  "task": "<DENSE_REGION_CAPTION>",
  "results": {
    "labels": ["a wooden dining table", "a blue cushion chair", ...],
    "bboxes": [[100, 150, 400, 350], ...]
  }
}
```

#### 5.4.4 Health — `GET /v1/health`

```json
{
  "status": "ok",
  "gpu": "NVIDIA RTX 4080",
  "vram_used_mb": 7200,
  "vram_total_mb": 16384,
  "models_loaded": ["grounding_dino", "sam2", "florence2"],
  "uptime_seconds": 86400
}
```

### 5.5 Grounded-SAM 2 Pipeline Implementation

The core cascade logic follows the architecture documented in the Grounded-SAM 2 repository:[9][10]

```python
# app/pipelines/grounded_sam2.py (conceptual)
def grounded_sam2_inference(image, prompt, box_threshold, text_threshold,
                            grounding_model="grounding_dino"):
    mm = ModelManager.get_instance()

    # Step 1: Grounding (choose backbone)
    if grounding_model == "grounding_dino":
        boxes, labels, scores = gdino_predict(
            mm.gdino_model, image, prompt, box_threshold, text_threshold
        )
    elif grounding_model == "florence2":
        boxes, labels, scores = florence2_detect(
            mm.florence_model, mm.florence_processor, image, prompt
        )

    # Step 2: SAM 2 segmentation from box prompts
    mm.sam2_predictor.set_image(image_np)
    masks = []
    for box in boxes:
        mask, score, _ = mm.sam2_predictor.predict(
            box=box,
            multimask_output=False
        )
        masks.append(rle_encode(mask))

    return annotations  # boxes + labels + scores + RLE masks
```

This follows the exact pattern used in `grounded_sam2_local_demo.py` and `grounded_sam2_florence2_image_demo.py` from the official repo.[10]

### 5.6 Dockerfile

```dockerfile
FROM nvidia/cuda:12.1.1-cudnn8-devel-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive
ENV PYTHONUNBUFFERED=1
ENV CUDA_HOME=/usr/local/cuda

RUN apt-get update && apt-get install -y \
    python3.10 python3-pip git build-essential \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Install PyTorch with CUDA 12.1
RUN pip3 install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

# Install Grounding DINO dependencies
COPY requirements.txt .
RUN pip3 install --no-cache-dir -r requirements.txt

# Install SAM 2 from source (requires CUDA compilation)
RUN git clone https://github.com/facebookresearch/sam2.git /tmp/sam2 \
    && cd /tmp/sam2 && pip3 install -e . \
    && rm -rf /tmp/sam2/.git

# Install Grounding DINO from source
RUN git clone https://github.com/IDEA-Research/GroundingDINO.git /tmp/gdino \
    && cd /tmp/gdino && pip3 install --no-build-isolation -e . \
    && rm -rf /tmp/gdino/.git

# Install Florence-2 dependencies
RUN pip3 install transformers accelerate einops timm

# Copy application code and checkpoints
COPY app/ /app/app/
COPY configs/ /app/configs/
COPY checkpoints/ /app/checkpoints/

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

**docker-compose.yml:**
```yaml
version: "3.8"
services:
  cv-server:
    build: .
    runtime: nvidia
    environment:
      - NVIDIA_VISIBLE_DEVICES=all
    ports:
      - "8000:8000"
    volumes:
      - ./checkpoints:/app/checkpoints
      - huggingface_cache:/root/.cache/huggingface
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    restart: unless-stopped

volumes:
  huggingface_cache:
```

### 5.7 Network Configuration

- **Server static LAN IP:** e.g., `192.168.1.100`
- **Port:** `8000`
- **WiFi:** 5 GHz band required for Quest 3 to minimize latency
- **Firewall:** Allow TCP 8000 inbound from LAN only
- **mDNS (optional):** Avahi service for `cv-server.local` discovery

***

## 6. Unity Client Architecture

### 6.1 Shared Codebase Strategy

The Unity project maintains a **single C# codebase** that compiles for all three targets. Platform differences are isolated behind interfaces:[16][8]

```
Assets/
├── HouseMapper/
│   ├── Core/
│   │   ├── ICameraProvider.cs          # Interface for camera frame capture
│   │   ├── CVServerClient.cs           # HTTP client (UnityWebRequest)
│   │   ├── DetectionResult.cs          # Shared data models
│   │   ├── SegmentationResult.cs       # Mask data model (new)
│   │   ├── FlorenceResult.cs           # Florence-2 response model (new)
│   │   └── ServerConfig.cs             # Server URL, thresholds
│   ├── Platform/
│   │   ├── QuestCameraProvider.cs      # WebCamTexture via Passthrough Camera API
│   │   ├── MobileCameraProvider.cs     # ARFoundation camera image
│   │   └── DesktopCameraProvider.cs    # WebCamTexture (testing)
│   ├── Rendering/
│   │   ├── BBoxRenderer.cs             # 3D bounding box overlay (retained)
│   │   ├── MaskRenderer.cs             # Segmentation mask overlay (new)
│   │   ├── LabelRenderer.cs            # Floating text labels (new)
│   │   └── CaptionPanel.cs             # Room description UI (new, Florence)
│   ├── Mapping/
│   │   ├── ObjectMapper.cs             # 2D→3D projection + scene graph
│   │   ├── RoomAnalyzer.cs             # Florence-2 auto-captioning per room (new)
│   │   └── MappedObjectDatabase.cs     # Persistent object storage
│   ├── UI/
│   │   ├── PromptInputUI.cs            # Text prompt entry
│   │   ├── ModelSelectorUI.cs          # Choose detect/segment/florence (new)
│   │   ├── MaskToggleUI.cs             # Show/hide segmentation overlays (new)
│   │   └── SettingsPanel.cs            # Server config, thresholds
│   ├── Prefabs/
│   ├── Scenes/
│   └── Editor/
```

### 6.2 Extended CVServerClient

The HTTP client class expands from a single detection call to support all three server endpoints:

```csharp
public class CVServerClient
{
    private string baseUrl;  // e.g., "http://192.168.1.100:8000"

    // === DETECTION (Grounding DINO only) ===
    public async Task<DetectionResult> DetectAsync(
        byte[] jpegBytes, string prompt,
        float boxThreshold = 0.35f, float textThreshold = 0.25f)
    {
        var payload = new DetectRequest {
            image = Convert.ToBase64String(jpegBytes),
            prompt = prompt,
            box_threshold = boxThreshold,
            text_threshold = textThreshold
        };
        return await PostAsync<DetectionResult>("/v1/detect", payload);
    }

    // === SEGMENTATION (Grounded-SAM 2) ===
    public async Task<SegmentationResult> SegmentAsync(
        byte[] jpegBytes, string prompt,
        string groundingModel = "grounding_dino",
        string maskFormat = "rle")
    {
        var payload = new SegmentRequest {
            image = Convert.ToBase64String(jpegBytes),
            prompt = prompt,
            grounding_model = groundingModel,
            return_masks = true,
            mask_format = maskFormat
        };
        return await PostAsync<SegmentationResult>("/v1/segment", payload);
    }

    // === FLORENCE-2 (Multi-task) ===
    public async Task<FlorenceResult> FlorenceAsync(
        byte[] jpegBytes, string task, string textInput = "")
    {
        var payload = new FlorenceRequest {
            image = Convert.ToBase64String(jpegBytes),
            task = task,
            text_input = textInput
        };
        return await PostAsync<FlorenceResult>("/v1/florence", payload);
    }

    // Shared HTTP transport using UnityWebRequest
    private async Task<T> PostAsync<T>(string endpoint, object payload) { ... }
}
```

`UnityWebRequest` is used as the underlying HTTP transport because it works identically across Quest 3, iOS, and Android with no platform-specific code.[8]

### 6.3 Mask Rendering (New Component)

For segmentation results, the client must render pixel-accurate mask overlays on the camera feed. The approach:

1. **Server returns RLE-encoded masks** per detected object
2. **Client decodes RLE → Texture2D** (C# RLE decoder, runs on CPU in ~1–2 ms)
3. **Mask rendered as semi-transparent quad** in camera-aligned space with per-object color coding
4. **Quest 3**: Quad overlaid in passthrough space using MRUK raycasting for depth-correct placement
5. **iOS/Android**: Quad overlaid on ARFoundation camera background using screen-space projection

```csharp
public class MaskRenderer : MonoBehaviour
{
    [SerializeField] private Material maskMaterial;  // Unlit, alpha-blended
    private static readonly Color[] palette = { /* 20 distinct colors */ };

    public void RenderMasks(SegmentationResult result, Camera cam)
    {
        ClearPreviousMasks();
        for (int i = 0; i < result.annotations.Length; i++)
        {
            var ann = result.annotations[i];
            Texture2D maskTex = RLEDecoder.Decode(ann.mask, result.image_size);
            Color color = palette[i % palette.Length];
            color.a = 0.4f;  // 40% opacity

            // Create screen-aligned quad with mask texture
            var quad = CreateMaskQuad(maskTex, color, cam);
            quad.name = $"Mask_{ann.label}_{i}";
        }
    }
}
```

### 6.4 Room Analysis Flow (Florence-2 Integration)

A new **RoomAnalyzer** component uses Florence-2's captioning capability to automatically describe rooms:

```
User enters room → QuestCameraProvider captures frame
    → RoomAnalyzer sends to /v1/florence with <MORE_DETAILED_CAPTION>
    → Server returns natural language description
    → CaptionPanel displays: "A living room with a brown leather sofa,
       a glass coffee table, two floor lamps, and a mounted TV on
       the east wall."
    → Optionally: cascade caption → phrase grounding → SAM 2
       for fully automatic room inventory
```

This enables a **zero-prompt mapping mode** where the user simply walks through the house and Florence-2 automatically identifies and labels everything it sees.[7][10]

### 6.5 Platform-Specific Camera Implementations

| Platform | Camera Source | Resolution | Notes |
|---|---|---|---|
| Quest 3 | WebCamTexture via Passthrough Camera API | Up to 1280×960 @ ~30 fps | Requires HorizonOS v74+, `horizonos.permission.HEADSET_CAMERA` [16][17] |
| iOS | ARFoundation `XRCpuImage` | Device-dependent (typically 1920×1080) | Uses ARKit pose for 3D projection |
| Android | ARFoundation `XRCpuImage` | Device-dependent | Uses ARCore pose for 3D projection |

The `ICameraProvider` interface remains unchanged from v1.0:

```csharp
public interface ICameraProvider
{
    bool IsAvailable { get; }
    void StartCapture();
    void StopCapture();
    byte[] GetCurrentFrameJPEG(int quality = 75);
    Matrix4x4 GetCameraIntrinsics();
    Pose GetCameraPose();
}
```

***

## 7. Data Flow — Three Operating Modes

### Mode 1: Detection Only (Fast Scan)
```
Camera Frame → JPEG encode → /v1/detect → bounding boxes + labels
→ 3D placement via raycasting → BBoxRenderer overlays
```
**Latency:** ~150–350 ms round-trip  
**Use Case:** Quick room survey, counting objects

### Mode 2: Detection + Segmentation (Precise Mapping)
```
Camera Frame → JPEG encode → /v1/segment → boxes + masks + labels
→ MaskRenderer overlays + ObjectMapper 3D placement
```
**Latency:** ~300–600 ms round-trip  
**Use Case:** Accurate object boundaries for 3D reconstruction

### Mode 3: Auto-Captioning (Hands-Free Inventory)
```
Camera Frame → JPEG encode → /v1/florence (caption)
→ Caption text → /v1/florence (phrase grounding)
→ Grounded boxes → /v1/segment (SAM 2 refinement)
→ Full inventory with descriptions + masks
```
**Latency:** ~800–1500 ms (3-step cascade)  
**Use Case:** Comprehensive room documentation, automatic floor-plan annotation

***

## 8. Development Roadmap (8-Week Sprint Plan)

### Phase 1: Server Foundation (Weeks 1–2)

**Sprint 1 (Week 1): Core Server + Grounding DINO**

| Task | Est. Hours | Deliverable |
|---|---|---|
| Set up Docker environment with CUDA 12.1 | 4 | Working container with GPU passthrough |
| Implement `ModelManager` singleton (GDINO only) | 6 | Model loads on startup, VRAM verified |
| Implement `/v1/detect` endpoint | 6 | Parity with original NVIDIA API format |
| Implement `/v1/health` endpoint | 2 | Status, VRAM, uptime reporting |
| Unit tests for detection pipeline | 4 | pytest suite with sample images |
| **Sprint 1 Total** | **22** | |

**Sprint 2 (Week 2): SAM 2 + Florence-2 Integration**

| Task | Est. Hours | Deliverable |
|---|---|---|
| Add SAM 2 to `ModelManager` (build CUDA extension) | 6 | SAM 2 loads alongside GDINO [13] |
| Implement Grounded-SAM 2 cascade pipeline | 8 | `/v1/segment` with RLE mask output |
| Add Florence-2 to `ModelManager` | 4 | HuggingFace model loads, FP16 [7] |
| Implement `/v1/florence` multi-task endpoint | 6 | All task prompts supported [10] |
| Implement Florence-2 → SAM 2 cascade variant | 4 | `grounding_model=florence2` in `/v1/segment` |
| RLE encoding/decoding utilities | 3 | Tested encode/decode round-trip |
| Integration tests for all endpoints | 4 | Full test coverage |
| **Sprint 2 Total** | **35** | |

### Phase 2: Unity Client Core (Weeks 3–4)

**Sprint 3 (Week 3): Shared Client Architecture**

| Task | Est. Hours | Deliverable |
|---|---|---|
| Fork `Unity-MetaXR-AI-ZeroShot` repository | 2 | Clean project with Meta XR SDK |
| Implement `ICameraProvider` interface + Quest impl | 8 | `QuestCameraProvider` with WebCamTexture [16] |
| Implement `CVServerClient` (all 3 endpoints) | 8 | Detection, segmentation, Florence calls |
| Implement `DetectionResult`, `SegmentationResult`, `FlorenceResult` models | 4 | JSON deserialization from server |
| Implement RLE decoder in C# | 4 | Converts server masks to Texture2D |
| `BBoxRenderer` (retained + enhanced) | 4 | 3D box overlays with labels |
| **Sprint 3 Total** | **30** | |

**Sprint 4 (Week 4): Mask Rendering + Florence UI**

| Task | Est. Hours | Deliverable |
|---|---|---|
| `MaskRenderer` — segmentation overlay system | 10 | Color-coded transparent mask quads |
| `LabelRenderer` — floating text labels | 4 | World-space TextMeshPro labels |
| `CaptionPanel` — Florence-2 room description UI | 4 | HUD panel showing auto-captions |
| `ModelSelectorUI` — mode toggle (detect/segment/caption) | 4 | Runtime mode switching |
| `PromptInputUI` — text prompt entry | 4 | Virtual keyboard + presets |
| End-to-end Quest 3 integration test | 6 | Detection + segmentation working on Quest |
| **Sprint 4 Total** | **32** | |

### Phase 3: Cross-Platform + 3D Mapping (Weeks 5–6)

**Sprint 5 (Week 5): iOS/Android + Mapping**

| Task | Est. Hours | Deliverable |
|---|---|---|
| `MobileCameraProvider` (ARFoundation) | 8 | XRCpuImage capture for iOS/Android |
| ARFoundation raycasting for 3D placement | 6 | Boxes/masks projected into AR world |
| `ObjectMapper` — persistent scene graph | 8 | Deduplicate detections, build world map |
| iOS build configuration + testing | 4 | XCode build, device test |
| Android build configuration + testing | 4 | APK build, device test |
| **Sprint 5 Total** | **30** | |

**Sprint 6 (Week 6): Advanced Mapping Features**

| Task | Est. Hours | Deliverable |
|---|---|---|
| `RoomAnalyzer` — auto-captioning pipeline | 8 | Walk-through auto-inventory mode |
| `MappedObjectDatabase` — persistent storage | 6 | JSON/SQLite export of mapped objects |
| Multi-frame deduplication + temporal smoothing | 6 | Stable object tracking across frames |
| Floor plan export (2D top-down view) | 8 | PNG/SVG floor plan with labeled objects |
| **Sprint 6 Total** | **28** | |

### Phase 4: Polish + Optimization (Weeks 7–8)

**Sprint 7 (Week 7): Performance Optimization**

| Task | Est. Hours | Deliverable |
|---|---|---|
| Server: batch inference for multiple frames | 6 | Higher throughput for walk-through mode |
| Server: JPEG quality/resolution negotiation | 4 | Client sends resolution preference |
| Client: frame skip + adaptive rate throttling | 6 | Send frames only when movement detected |
| Client: async mask rendering (background thread) | 6 | No main-thread hitching during decode |
| WebSocket upgrade for streaming mode (optional) | 8 | Lower latency via persistent connection [18] |
| **Sprint 7 Total** | **30** | |

**Sprint 8 (Week 8): Testing + Documentation**

| Task | Est. Hours | Deliverable |
|---|---|---|
| End-to-end testing on all 3 platforms | 12 | Quest 3, iPhone, Android device tests |
| Latency profiling + optimization | 6 | Target &lt;500 ms for detection, &lt;800 ms for segmentation |
| User documentation / README | 4 | Setup guide, API docs, troubleshooting |
| Server monitoring dashboard (optional Grafana) | 4 | VRAM, latency, request count metrics |
| Final code review + cleanup | 4 | Production-ready codebase |
| **Sprint 8 Total** | **30** | |

**Total Estimated Effort: ~237 hours across 8 weeks**

***

## 9. Testing Strategy

### 9.1 Server Testing

| Level | Tool | Scope |
|---|---|---|
| Unit | pytest | Individual model inference, image encoding, RLE codec |
| Integration | pytest + httpx | Full endpoint round-trips against running server |
| Load | Locust or k6 | Sustained requests to measure throughput + VRAM stability |
| Smoke | curl scripts | Quick sanity check for CI/CD |

**Key test cases:**
- Grounding DINO detects known objects (chair, table, door) with score > 0.5
- Grounded-SAM 2 returns valid RLE masks that decode to correct dimensions
- Florence-2 + SAM 2 cascade produces same masks as Grounding DINO + SAM 2 for equivalent prompts
- All three models loaded simultaneously stay under 10 GB VRAM
- 100 sequential requests don't cause VRAM growth (leak detection)[19]
- Server returns `503 Unavailable` if GPU is unreachable

### 9.2 Client Testing

| Level | Method | Scope |
|---|---|---|
| Unit | Unity Test Framework | RLE decoder, JSON parsers, data models |
| Integration | In-editor play mode | CVServerClient against local server |
| Device | Manual | Quest 3, iPhone, Android real-device walkthrough |
| Regression | Screenshot comparison | Mask overlay accuracy across builds |

***

## 10. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| SAM 2 VRAM leak on long video streams | Medium | High | Use image-mode predictor (not video); call `torch.cuda.empty_cache()` between requests [20][19] |
| Grounding DINO deformable attention CUDA build fails | Medium | High | Pre-build in Docker with pinned CUDA 12.1 + PyTorch 2.3.1 [13] |
| Florence-2 HuggingFace download fails at container start | Low | Medium | Pre-download weights into volume mount; set `HF_HUB_OFFLINE=1` |
| Quest 3 Passthrough Camera API latency too high | Low | Medium | Use 640×480 resolution; send every 3rd frame; pre-compress JPEG quality 60 [17] |
| Three models exceed 16 GB VRAM simultaneously | Very Low | High | Budget shows ~7.2 GB total — 55% headroom; can drop to Florence-2-base if needed [6] |
| WebCamTexture permission denied on Quest | Medium | Medium | Implement graceful fallback UI; check `horizonos.permission.HEADSET_CAMERA` at runtime [16] |
| Cross-platform JSON serialization inconsistencies | Low | Low | Use `JsonUtility` for simple types; Newtonsoft.Json for complex (RLE masks) |
| Network unreachable (server offline) | Medium | Medium | Client caches last known detections; offline mode with local Sentis fallback |

***

## 11. Advanced Considerations

### 11.1 Model Hot-Swapping

For future flexibility, the `ModelManager` can support **dynamic model loading/unloading** via an admin endpoint:

```
POST /v1/admin/models
{
  "action": "load",
  "model": "sam2_hiera_large"
}
```

This allows upgrading from SAM 2 Tiny to SAM 2 Large for higher-quality masks when latency is less critical (e.g., batch processing overnight).

### 11.2 Streaming / WebSocket Mode

For continuous walk-through mapping, a WebSocket endpoint provides lower latency than repeated HTTP POST cycles:[18]

```
ws://192.168.1.100:8000/v1/stream
```

Client sends JPEG frames continuously; server responds with JSON annotations per frame. The NativeWebSocket Unity library supports this across all platforms.[18]

### 11.3 Unity Sentis Fallback

For offline/portable use, lighter models can run directly on-device using Unity Sentis (ONNX runtime). This provides degraded but functional detection when the server is unreachable. Florence-2-base (230M params) is particularly promising for on-device deployment given its small size.[7]

### 11.4 SAM 2 Video Tracking Mode

For continuous object tracking during house walk-throughs, SAM 2's video predictor mode can propagate masks across frames using its memory bank. However, this requires careful VRAM management — the memory bank grows with each frame. For the RTX 4080, limit the memory bank to ~50 recent frames and use `offload_video_to_cpu=True` for longer sequences.[11][20][10]

### 11.5 Floor Plan Generation Pipeline

A complete house mapping session produces:

1. **Object inventory** (JSON): all detected objects with labels, bounding boxes, masks, 3D positions, and room assignments
2. **Room descriptions** (text): Florence-2 auto-generated captions per room
3. **Floor plan** (SVG/PNG): top-down 2D projection of mapped objects
4. **3D scene graph** (JSON): spatial relationships between objects

***

## 12. Security Considerations

| Concern | Mitigation |
|---|---|
| Server exposed on LAN | Bind to LAN IP only; firewall blocks WAN access; optional API key header |
| Camera frames contain private data | All processing is local; no data leaves the home network; Docker volumes use host filesystem |
| Model weights licensing | Grounding DINO (Apache 2.0), SAM 2 (Apache 2.0), Florence-2 (MIT) — all permissive for private use [10][7] |
| Docker container privilege escalation | Run as non-root user inside container; limit GPU device access |

***

## 13. Dependencies and Versions

| Dependency | Version | Source |
|---|---|---|
| Python | 3.10+ | System |
| PyTorch | ≥ 2.3.1 | pytorch.org |
| CUDA Toolkit | 12.1 | NVIDIA |
| FastAPI | Latest | PyPI |
| Uvicorn | Latest | PyPI |
| Grounding DINO | Main branch | github.com/IDEA-Research/GroundingDINO [14] |
| SAM 2 | v2.1 | github.com/facebookresearch/sam2 |
| Grounded-SAM 2 | Main branch | github.com/IDEA-Research/Grounded-SAM-2 [10] |
| Florence-2 | Large (0.77B) | huggingface.co/microsoft/Florence-2-large [7] |
| HuggingFace Transformers | Latest | PyPI |
| Unity | 6.x (LTS) | Unity Hub |
| Meta XR SDK | Latest | Unity Asset Store |
| ARFoundation | 6.x | Unity Package Manager |
| NativeWebSocket | Latest | github.com/endel/NativeWebSocket [18] |

***

## 14. Reference Repositories

| Repository | Purpose |
|---|---|
| [IDEA-Research/Grounded-SAM-2](https://github.com/IDEA-Research/Grounded-SAM-2) [10] | Official Grounded-SAM 2 with Florence-2 demos, Docker support |
| [IDEA-Research/GroundingDINO](https://github.com/IDEA-Research/GroundingDINO) [14] | Grounding DINO model and weights |
| [facebookresearch/sam2](https://github.com/facebookresearch/sam2) | SAM 2 model, checkpoints, CUDA extension |
| [lucas-martinic/Unity-MetaXR-AI-ZeroShot](https://github.com/lucas-martinic/Unity-MetaXR-AI-ZeroShot) | Base Unity project to fork |
| [xrdevrob/QuestCameraKit](https://github.com/xrdevrob/QuestCameraKit) [17] | Quest 3 camera access reference patterns |
| [oculus-samples/Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) [16] | Official Meta Quest camera samples |
| [edgarwelteKIT/groundingdino_localapi](https://github.com/edgarwelteKIT/groundingdino_localapi) [21] | Docker REST API reference for Grounding DINO |
| [endel/NativeWebSocket](https://github.com/endel/NativeWebSocket) [18] | Cross-platform WebSocket for Unity |

***

## 15. Glossary

| Term | Definition |
|---|---|
| **Grounding DINO** | Open-vocabulary object detector using vision-language cross-attention [3] |
| **SAM 2** | Segment Anything Model 2 — promptable image/video segmentation from Meta [11] |
| **Grounded-SAM 2** | Pipeline cascading a grounding detector (GDINO/Florence-2) with SAM 2 [9][10] |
| **Florence-2** | Microsoft's unified vision-language model supporting detection, captioning, OCR, and grounding [12][7] |
| **RLE** | Run-Length Encoding — compact binary mask representation |
| **MRUK** | Meta Room Understanding Kit — Quest 3 spatial understanding for raycasting |
| **ICameraProvider** | Unity interface abstracting platform-specific camera access |
| **Passthrough Camera API** | Quest 3 API providing access to headset front cameras [16][17] |