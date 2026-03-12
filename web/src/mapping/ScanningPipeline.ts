/**
 * Scanning pipeline — mirrors Assets/CompanionScan/Mapping/SpatialCapturePipeline.cs.
 *
 * Orchestrates:
 *   1. Camera frame capture
 *   2. CV server calls (Florence-2 / Grounding DINO / SAM 2)
 *   3. World-space placement via WebXR hit-test or fallback depth
 *   4. SpatialPin creation and insertion into the manager
 */

import * as THREE from 'three';
import { CVServerClient } from '../core/CVServerClient.js';
import type { DetectionResult } from '../core/types.js';
import type { CaptureFrame } from '../core/types.js';
import { makeSpatialPin } from './SpatialPin.js';
import type { SpatialPin } from './SpatialPin.js';
import type { SpatialPinManager } from './SpatialPinManager.js';

/** Available AI model presets — mirrors Unity ModelPreset enum. */
export type ModelPreset = 'Florence' | 'GDino' | 'SAM2';

/** Display mode for pin visualization. */
export type DisplayMode = 'BoundingBox2D' | 'SpatialLabel3D' | 'Both';

/** Preview distance for fallback raycasting. */
export type PreviewDistance = 'Near' | 'Far' | 'Off';

const PREVIEW_DISTANCE_METRES: Record<PreviewDistance, number> = {
  Near: 1.0,
  Far: 3.0,
  Off: 0.0,
};

export interface PipelineState {
  model: ModelPreset;
  displayMode: DisplayMode;
  previewDistance: PreviewDistance;
  busy: boolean;
  captureCount: number;
  sessionId: string;
}

export type PipelineStateChangedCallback = (state: Readonly<PipelineState>) => void;

export class ScanningPipeline {
  private readonly client: CVServerClient;
  private readonly pinManager: SpatialPinManager;
  private readonly camera: THREE.Camera;
  private readonly onStateChanged: PipelineStateChangedCallback;

  private state: PipelineState;
  private frameCounter = 0;
  private readonly modelCycle: ModelPreset[] = ['Florence', 'GDino', 'SAM2'];
  private readonly displayCycle: DisplayMode[] = ['BoundingBox2D', 'SpatialLabel3D', 'Both'];
  private readonly distanceCycle: PreviewDistance[] = ['Near', 'Far', 'Off'];

  constructor(
    client: CVServerClient,
    pinManager: SpatialPinManager,
    camera: THREE.Camera,
    onStateChanged: PipelineStateChangedCallback,
  ) {
    this.client = client;
    this.pinManager = pinManager;
    this.camera = camera;
    this.onStateChanged = onStateChanged;
    this.state = {
      model: 'Florence',
      displayMode: 'Both',
      previewDistance: 'Near',
      busy: false,
      captureCount: 0,
      sessionId: generateSessionId(),
    };
  }

  get currentState(): Readonly<PipelineState> {
    return this.state;
  }

  /** Register WebXR hit-test source for AR plane-based raycasting (future use). */
  setXRHitTestSource(_src: XRHitTestSource | null, _refSpace: XRReferenceSpace | null): void {
    // Reserved for future hit-test plane intersection support
  }

  // ── Actions (mirror ControllerMenuUI public methods) ──────────────────────

  /** Capture a frame and run the active AI model. */
  async doCapture(frame: CaptureFrame): Promise<void> {
    if (this.state.busy) return;
    this.setState({ busy: true });

    try {
      const frameIdx = ++this.frameCounter;
      const captureId = generateCaptureId();

      switch (this.state.model) {
        case 'Florence':
          await this.runFlorence(frame, captureId, frameIdx);
          break;
        case 'GDino':
          await this.runGDino(frame, captureId, frameIdx);
          break;
        case 'SAM2':
          await this.runSAM2(frame, captureId, frameIdx);
          break;
      }

      this.setState({ captureCount: this.state.captureCount + 1 });
    } catch (err) {
      console.error('[ScanningPipeline] Capture failed:', err);
    } finally {
      this.setState({ busy: false });
    }
  }

  cycleModel(): void {
    const idx = this.modelCycle.indexOf(this.state.model);
    const next = this.modelCycle[(idx + 1) % this.modelCycle.length];
    this.setState({ model: next });
  }

  cycleDisplayMode(): void {
    const idx = this.displayCycle.indexOf(this.state.displayMode);
    const next = this.displayCycle[(idx + 1) % this.displayCycle.length];
    this.setState({ displayMode: next });
  }

  cyclePreviewDistance(): void {
    const idx = this.distanceCycle.indexOf(this.state.previewDistance);
    const next = this.distanceCycle[(idx + 1) % this.distanceCycle.length];
    this.setState({ previewDistance: next });
  }

  // ── Private pipeline steps ────────────────────────────────────────────────

  private async runFlorence(frame: CaptureFrame, captureId: string, frameIdx: number): Promise<void> {
    const result = await this.client.florenceAsync(frame.jpegBlob, '<OD>');
    if (!result.results) return;

    // Florence OD returns { bboxes, labels } in results
    const od = result.results as { bboxes?: number[][]; labels?: string[] };
    if (!od.bboxes?.length) return;

    for (let i = 0; i < od.bboxes.length; i++) {
      const bbox = od.bboxes[i];
      const label = od.labels?.[i] ?? 'object';
      const position = this.projectBboxToWorld(bbox, frame);
      if (!position) continue;

      this.pinManager.addPin(
        makeSpatialPin({
          label,
          semanticLabel: label,
          score: 1.0,
          source: 'CvServer',
          position,
          bboxNormalized: bboxArrayToNorm(bbox),
          sourcePrompt: '<OD>',
          captureId,
          captureSessionId: this.state.sessionId,
          captureFrameIndex: frameIdx,
          trackId: i,
        }),
      );
    }
  }

  private async runGDino(frame: CaptureFrame, captureId: string, frameIdx: number): Promise<void> {
    const result: DetectionResult = await this.client.detectAsync(frame.jpegBlob, '.');
    for (let i = 0; i < result.bboxes.length; i++) {
      const bbox = result.bboxes[i];
      const label = result.labels[i] ?? 'object';
      const score = result.scores[i] ?? 1.0;
      const position = this.projectBboxToWorld(bbox, frame);
      if (!position) continue;

      this.pinManager.addPin(
        makeSpatialPin({
          label,
          semanticLabel: label,
          score,
          source: 'CvServer',
          position,
          bboxNormalized: bboxArrayToNorm(bbox),
          sourcePrompt: '.',
          captureId,
          captureSessionId: this.state.sessionId,
          captureFrameIndex: frameIdx,
          trackId: i,
        }),
      );
    }
  }

  private async runSAM2(frame: CaptureFrame, captureId: string, frameIdx: number): Promise<void> {
    const result = await this.client.segmentAsync(frame.jpegBlob, '.');
    for (let i = 0; i < result.annotations.length; i++) {
      const ann = result.annotations[i];
      const position = this.projectBboxToWorld(ann.bbox, frame);
      if (!position) continue;

      this.pinManager.addPin(
        makeSpatialPin({
          label: ann.label,
          semanticLabel: ann.label,
          score: ann.score,
          source: 'CvServer',
          position,
          bboxNormalized: bboxArrayToNorm(ann.bbox),
          sourcePrompt: '.',
          captureId,
          captureSessionId: this.state.sessionId,
          captureFrameIndex: frameIdx,
          trackId: i,
        }),
      );
    }
  }

  /**
   * Project the centre of a normalised bounding box into world space.
   *
   * Uses the camera direction + preview distance as a simple fallback
   * (no WebXR hit-test plane intersection available in this path).
   */
  private projectBboxToWorld(
    bbox: number[],
    _frame: CaptureFrame,
  ): SpatialPin['position'] | null {
    const dist = PREVIEW_DISTANCE_METRES[this.state.previewDistance];
    if (dist <= 0) return null;

    // Bbox: [x0, y0, x1, y1] normalised 0-1
    const cx = ((bbox[0] + bbox[2]) / 2) * 2 - 1; // NDC x
    const cy = -((bbox[1] + bbox[3]) / 2) * 2 + 1; // NDC y (inverted)

    const dir = new THREE.Vector3(cx, cy, -1)
      .unproject(this.camera)
      .sub(this.camera.position)
      .normalize();

    const worldPos = this.camera.position.clone().addScaledVector(dir, dist);
    return [worldPos.x, worldPos.y, worldPos.z];
  }

  private setState(partial: Partial<PipelineState>): void {
    this.state = { ...this.state, ...partial };
    this.onStateChanged(this.state);
  }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function generateSessionId(): string {
  return `web-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`;
}

function generateCaptureId(): string {
  return `cap-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 5)}`;
}

function bboxArrayToNorm(bbox: number[]): import('./SpatialPin.js').BBoxNorm {
  const [x0, y0, x1, y1] = bbox;
  return { x: x0, y: y0, width: x1 - x0, height: y1 - y0 };
}
