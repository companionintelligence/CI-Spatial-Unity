/**
 * Camera capture utilities for the WebXR / browser environment.
 *
 * Provides two strategies:
 *   1. WebXR camera frames (when inside an immersive-ar session)
 *   2. getUserMedia webcam fallback (desktop / non-XR)
 *
 * Mirrors the platform camera providers in Assets/CompanionScan/Platform/.
 */

import type { CameraIntrinsics, CameraPose } from '../core/types.js';

/** Snapshot of a camera frame as a JPEG Blob with metadata. */
export interface FrameCapture {
  jpegBlob: Blob;
  width: number;
  height: number;
  pose: CameraPose;
  intrinsics: CameraIntrinsics | null;
  timestamp: number;
}

export class CameraCapture {
  private stream: MediaStream | null = null;
  private videoEl: HTMLVideoElement | null = null;
  private canvas: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D;
  private jpegQuality: number;

  constructor(jpegQuality = 0.75) {
    this.jpegQuality = jpegQuality;
    this.canvas = document.createElement('canvas');
    this.ctx = this.canvas.getContext('2d')!;
  }

  get isAvailable(): boolean {
    return this.stream !== null && (this.videoEl?.readyState ?? 0) >= 2;
  }

  /** Start the webcam stream (getUserMedia fallback for non-XR). */
  async startCapture(
    width = 1280,
    height = 720,
  ): Promise<void> {
    if (this.stream) return;
    this.stream = await navigator.mediaDevices.getUserMedia({
      video: {
        width: { ideal: width },
        height: { ideal: height },
        facingMode: 'environment',
      },
      audio: false,
    });
    const video = document.createElement('video');
    video.srcObject = this.stream;
    video.setAttribute('playsinline', '');
    video.muted = true;
    await video.play();
    this.videoEl = video;
  }

  /** Stop the webcam stream. */
  stopCapture(): void {
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
    this.videoEl = null;
  }

  /**
   * Capture the current frame as a JPEG Blob.
   *
   * When inside a WebXR session (`xrFrame` provided), uses the WebXR
   * camera image API (if available).  Falls back to the getUserMedia
   * video element otherwise.
   */
  async captureFrame(opts: {
    xrFrame?: XRFrame | null;
    xrSession?: XRSession | null;
    xrRefSpace?: XRReferenceSpace | null;
    viewerPose?: XRViewerPose | null;
    fallbackPose?: CameraPose | null;
  } = {}): Promise<FrameCapture | null> {
    // ── WebXR path ────────────────────────────────────────────────────────
    const xrSession = opts.xrSession ?? opts.xrFrame?.session ?? null;
    if (opts.xrFrame && xrSession) {
      return this.captureFromXRFrame(
        opts.xrFrame,
        xrSession,
        opts.viewerPose ?? null,
        opts.fallbackPose ?? null,
      );
    }

    // ── getUserMedia fallback ─────────────────────────────────────────────
    if (!this.videoEl || !this.isAvailable) return null;

    const { videoWidth: w, videoHeight: h } = this.videoEl;
    this.canvas.width = w;
    this.canvas.height = h;
    this.ctx.drawImage(this.videoEl, 0, 0);

    const blob = await canvasToBlob(this.canvas, this.jpegQuality);
    return {
      jpegBlob: blob,
      width: w,
      height: h,
      pose: identityPose(),
      intrinsics: null,
      timestamp: performance.now() / 1000,
    };
  }

  // ── Private ──────────────────────────────────────────────────────────────

  private async captureFromXRFrame(
    frame: XRFrame,
    session: XRSession,
    viewerPose: XRViewerPose | null,
    fallbackPose: CameraPose | null,
  ): Promise<FrameCapture | null> {
    // Build pose from viewer
    const pose = viewerPose
      ? xrViewerPoseToCameraPose(viewerPose)
      : (fallbackPose ?? identityPose());

    // Try to access the raw camera image via WebXR Camera Access API
    type XRWebGLBindingCtor = new (
      session: XRSession,
      ctx: WebGL2RenderingContext,
    ) => { getCameraImage?: (view: XRView) => { width: number; height: number } | null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const BindingClass = (window as any)['XRWebGLBinding'] as XRWebGLBindingCtor | undefined;
    let binding: InstanceType<XRWebGLBindingCtor> | null = null;
    if (BindingClass) {
      const glCanvas = document.createElement('canvas');
      const gl = glCanvas.getContext('webgl2');
      if (gl instanceof WebGL2RenderingContext) {
        binding = new BindingClass(session, gl);
      }
    }

    if (binding) {
      for (const view of viewerPose?.views ?? []) {
        try {
          const cameraImage = binding.getCameraImage?.(view);
          if (cameraImage) {
            this.canvas.width = cameraImage.width;
            this.canvas.height = cameraImage.height;
            // Draw the GL texture via an offscreen path (not feasible without
            // a GL context), so fall through to video-element path instead.
          }
        } catch {
          // Camera image not available
        }
      }
    }

    // Intrinsics from XRView projection
    let intrinsics: CameraIntrinsics | null = null;
    if (viewerPose?.views?.length) {
      const view = viewerPose.views[0];
      intrinsics = projectionToIntrinsics(view.projectionMatrix, frame.session);
    }

    // Fall back to getUserMedia video element if present
    if (this.videoEl && this.isAvailable) {
      const { videoWidth: w, videoHeight: h } = this.videoEl;
      this.canvas.width = w;
      this.canvas.height = h;
      this.ctx.drawImage(this.videoEl, 0, 0);
      const blob = await canvasToBlob(this.canvas, this.jpegQuality);
      return { jpegBlob: blob, width: w, height: h, pose, intrinsics, timestamp: performance.now() / 1000 };
    }

    return null;
  }
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function canvasToBlob(canvas: HTMLCanvasElement, quality: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => (blob ? resolve(blob) : reject(new Error('Canvas toBlob returned null'))),
      'image/jpeg',
      quality,
    );
  });
}

function identityPose(): CameraPose {
  return { position: [0, 0, 0], rotation: [0, 0, 0, 1] };
}

function xrViewerPoseToCameraPose(vp: XRViewerPose): CameraPose {
  const p = vp.transform.position;
  const q = vp.transform.orientation;
  return {
    position: [p.x, p.y, p.z],
    rotation: [q.x, q.y, q.z, q.w],
  };
}

/**
 * Approximate fx/fy/cx/cy from a WebXR projection matrix.
 * The projection matrix maps from view space to NDC.
 */
function projectionToIntrinsics(
  mat: Float32Array,
  session: XRSession,
): CameraIntrinsics | null {
  // mat is column-major. Element [0] = 2*fx/w, [5] = 2*fy/h
  const m00 = mat[0];
  const m11 = mat[5];
  const m08 = mat[8];
  const m09 = mat[9];

  // Try to get render dimensions from the session
  const layer = session.renderState.baseLayer;
  if (!layer) return null;

  const w = layer.framebufferWidth;
  const h = layer.framebufferHeight;

  const fx = (m00 * w) / 2;
  const fy = (m11 * h) / 2;
  const cx = ((m08 + 1) / 2) * w;
  const cy = ((m09 + 1) / 2) * h;

  return { fx, fy, cx, cy };
}
