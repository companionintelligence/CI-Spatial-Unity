/**
 * Core data types mirroring the Unity C# models in Assets/CompanionScan/Core/.
 */

/** Result returned by the Grounding DINO detection endpoint `/v1/detect`. */
export interface DetectionResult {
  bboxes: number[][];
  labels: string[];
  scores: number[];
}

/** RLE-encoded segmentation mask from SAM 2. */
export interface RleMask {
  size: [number, number];
  counts: string;
}

/** Per-object annotation returned by `/v1/segment`. */
export interface SegmentAnnotation {
  label: string;
  score: number;
  bbox: [number, number, number, number];
  mask: RleMask;
}

/** Result returned by the SAM 2 segmentation endpoint `/v1/segment`. */
export interface SegmentationResult {
  annotations: SegmentAnnotation[];
  image_size: [number, number];
}

/** Result returned by the Florence-2 endpoint `/v1/florence`. */
export interface FlorenceResult {
  task: string;
  results: unknown;
  text: string;
}

/** Camera intrinsics matrix (column-major, same convention as Unity Matrix4x4). */
export interface CameraIntrinsics {
  fx: number;
  fy: number;
  cx: number;
  cy: number;
}

/** Camera pose in world space. */
export interface CameraPose {
  position: [number, number, number];
  rotation: [number, number, number, number]; // quaternion xyzw
}

/** A captured frame from the camera. */
export interface CaptureFrame {
  id: string;
  sessionId: string;
  frameIndex: number;
  timestamp: number;
  jpegBlob: Blob;
  imageWidth: number;
  imageHeight: number;
  cameraPose: CameraPose;
  intrinsics: CameraIntrinsics | null;
}
