import type { SpatialPin } from './types'

export interface DetectResult {
  bboxes: number[][]
  labels: string[]
  scores: number[]
}

export interface FlorenceResult {
  caption?: string
  labels?: string[]
}

export class CVClient {
  private baseUrl: string

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl
  }

  setBaseUrl(url: string) {
    this.baseUrl = url
  }

  private async postJson<T>(path: string, body: Record<string, unknown>): Promise<T> {
    const resp = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
    if (!resp.ok) {
      throw new Error(`Request to ${path} failed: ${resp.status}`)
    }
    return resp.json()
  }

  async runFlorence(imageBase64: string): Promise<FlorenceResult> {
    const payload = {
      image: imageBase64,
      task: '<MORE_DETAILED_CAPTION>',
      text_input: '',
    }
    const result = await this.postJson<any>('/v1/florence', payload)
    const caption = result?.results?.caption || result?.text || ''
    const labels: string[] = result?.results?.od?.labels || []
    return { caption, labels }
  }

  async detect(imageBase64: string, prompt: string): Promise<DetectResult> {
    const payload = { image: imageBase64, prompt }
    const result = await this.postJson<DetectResult>('/v1/detect', payload)
    return result
  }

  async segment(imageBase64: string, prompt: string) {
    return this.postJson('/v1/segment', {
      image: imageBase64,
      prompt,
      return_masks: false,
    })
  }

  async uploadGltf(
    gltf: Blob,
    extras?: object,
    bin?: Blob,
  ): Promise<{ ok: boolean; gltf_path?: string; bin_path?: string }> {
    const form = new FormData()
    form.append('gltf', gltf, 'spatial-map.gltf')
    if (bin) {
      form.append('bin', bin, 'spatial-map.bin')
    }
    if (extras) {
      form.append('extras', JSON.stringify(extras))
    }

    const resp = await fetch(`${this.baseUrl}/v1/gltf`, {
      method: 'POST',
      body: form,
    })
    if (!resp.ok) {
      throw new Error(`Upload failed: ${resp.status}`)
    }
    return resp.json()
  }
}

export function buildPrompt(labels: string[], fallback = 'object . furniture .'): string {
  if (!labels.length) return fallback
  return labels.map((l) => `${l} .`).join(' ')
}

export function pinsToExtras(pins: SpatialPin[], caption?: string) {
  const bounds =
    pins.length > 0
      ? pins.reduce(
          (acc, pin) => {
            const [x, y, z] = pin.position
            acc.min[0] = Math.min(acc.min[0], x)
            acc.min[1] = Math.min(acc.min[1], y)
            acc.min[2] = Math.min(acc.min[2], z)
            acc.max[0] = Math.max(acc.max[0], x)
            acc.max[1] = Math.max(acc.max[1], y)
            acc.max[2] = Math.max(acc.max[2], z)
            return acc
          },
          {
            min: [Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY],
            max: [Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY],
          },
        )
      : {
          min: [0, 0, 0] as [number, number, number],
          max: [0, 0, 0] as [number, number, number],
        }

  const center: [number, number, number] = [
    (bounds.min[0] + bounds.max[0]) / 2,
    (bounds.min[1] + bounds.max[1]) / 2,
    (bounds.min[2] + bounds.max[2]) / 2,
  ]
  const size: [number, number, number] = [
    Math.max(bounds.max[0] - bounds.min[0], 0),
    Math.max(bounds.max[1] - bounds.min[1], 0),
    Math.max(bounds.max[2] - bounds.min[2], 0),
  ]

  return {
    schema_version: '1.0-web',
    timestamp_utc: new Date().toISOString(),
    units: 'meters',
    caption,
    cv_mapping: {
      bounds: { center, size },
      pins: pins.map((p) => ({
        label: p.label,
        semantic_label: p.semanticLabel,
        score: p.score,
        source: p.source,
        position: p.position,
        normal: p.normal,
        rotation: p.rotation,
        bbox_pixels: p.bboxPixels,
        bbox_normalized: p.bboxNormalized,
        plane_size: p.planeSize,
        volume_size: p.volumeSize,
        anchor_uuid: p.anchorUuid,
        prompt: p.sourcePrompt,
        timestamp: p.timestamp,
      })),
    },
  }
}

export function dataUrlToBase64(dataUrl: string): string {
  return dataUrl.split(',')[1] || ''
}
