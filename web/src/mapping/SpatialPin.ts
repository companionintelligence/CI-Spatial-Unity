/**
 * Spatial pin types — mirrors Assets/CompanionScan/Mapping/SpatialPin.cs.
 */

/** Which pipeline produced the pin. */
export type PinSource = 'CvServer' | 'MetaScene';

/** How the 3-D position was determined. */
export type RaycastHitType =
  | 'ArPlane'
  | 'Environment'
  | 'MetaPlane'
  | 'Physics'
  | 'Fallback';

/** Normalised bounding-box rectangle [0, 1]. */
export interface BBoxNorm {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** A spatial observation from either CV server or scene understanding. */
export interface SpatialPin {
  // ── Identity ──────────────────────────────────────────────────────────
  label: string;
  semanticLabel: string;
  score: number;
  source: PinSource;

  // ── Spatial ───────────────────────────────────────────────────────────
  /** World-space position [x, y, z]. */
  position: [number, number, number];
  /** Surface normal [x, y, z]. */
  normal: [number, number, number];
  /** Rotation quaternion [x, y, z, w]. */
  rotation: [number, number, number, number];

  // ── 2-D (CV-only) ──────────────────────────────────────────────────
  bboxPixels: BBoxNorm | null;
  bboxNormalized: BBoxNorm | null;

  // ── Geometry (MetaScene-only) ───────────────────────────────────────
  planeSize: [number, number, number] | null;
  volumeSize: [number, number, number] | null;
  anchorUuid: string | null;

  // ── Metadata ──────────────────────────────────────────────────────
  sourcePrompt: string;
  timestamp: number;
  captureId: string;
  captureSessionId: string;
  captureFrameIndex: number;
  placementConfidence: number;
  hitType: RaycastHitType;
  trackId: number;
}

/** Construct a minimal SpatialPin with sensible defaults. */
export function makeSpatialPin(partial: Partial<SpatialPin> & Pick<SpatialPin, 'label' | 'position' | 'source'>): SpatialPin {
  return {
    semanticLabel: partial.semanticLabel ?? partial.label,
    score: partial.score ?? 1.0,
    normal: partial.normal ?? [0, 1, 0],
    rotation: partial.rotation ?? [0, 0, 0, 1],
    bboxPixels: partial.bboxPixels ?? null,
    bboxNormalized: partial.bboxNormalized ?? null,
    planeSize: partial.planeSize ?? null,
    volumeSize: partial.volumeSize ?? null,
    anchorUuid: partial.anchorUuid ?? null,
    sourcePrompt: partial.sourcePrompt ?? '',
    timestamp: partial.timestamp ?? performance.now() / 1000,
    captureId: partial.captureId ?? '',
    captureSessionId: partial.captureSessionId ?? '',
    captureFrameIndex: partial.captureFrameIndex ?? 0,
    placementConfidence: partial.placementConfidence ?? 1.0,
    hitType: partial.hitType ?? 'Fallback',
    trackId: partial.trackId ?? 0,
    ...partial,
  };
}
