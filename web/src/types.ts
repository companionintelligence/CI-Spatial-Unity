export type PinSource = 'CvServer' | 'MetaScene'

export type ModelPreset = 'Florence' | 'GDino' | 'SAM2'
export type DisplayMode = 'bbox' | 'labels' | 'both'

export interface CaptureFrame {
  dataUrl: string
  width: number
  height: number
  timestamp: number
}

export interface SpatialPin {
  id: string
  label: string
  semanticLabel: string
  score: number
  source: PinSource
  position: [number, number, number]
  normal: [number, number, number]
  rotation: [number, number, number, number]
  bboxPixels: [number, number, number, number]
  bboxNormalized: [number, number, number, number]
  planeSize: [number, number, number]
  volumeSize: [number, number, number]
  anchorUuid?: string
  sourcePrompt?: string
  timestamp: number
  captureId?: string
  captureSessionId?: string
  captureFrameIndex?: number
  placementConfidence?: number
  hitType?: string
  trackId?: number
}

export interface MetaSceneAnchor {
  uuid: string
  center: [number, number, number]
  rotation: [number, number, number, number]
  size: [number, number, number]
  polygon?: Array<[number, number, number]>
}

export interface CaptureState {
  model: ModelPreset
  displayMode: DisplayMode
  previewDistance: number
  captureSessionId: string
  frameIndex: number
}

export interface Bounds3 {
  min: [number, number, number]
  max: [number, number, number]
}
