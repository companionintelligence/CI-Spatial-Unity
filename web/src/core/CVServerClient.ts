/**
 * CV server REST client — mirrors Assets/CompanionScan/Core/CVServerClient.cs.
 *
 * Provides typed wrappers for:
 *   POST /v1/detect   — Grounding DINO object detection
 *   POST /v1/segment  — SAM 2 segmentation
 *   POST /v1/florence — Florence-2 vision tasks
 */

import type { ServerConfig } from './ServerConfig.js';
import type {
  DetectionResult,
  FlorenceResult,
  SegmentationResult,
} from './types.js';

/** Convert a Blob to a base-64 string without the data-URI prefix. */
async function blobToBase64(blob: Blob): Promise<string> {
  const dataUrl = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === 'string') {
        resolve(reader.result);
        return;
      }

      reject(new Error('Unexpected FileReader.result type.'));
    };
    reader.onerror = () => {
      reject(reader.error ?? new Error('Failed to read Blob as data URL.'));
    };
    reader.readAsDataURL(blob);
  });

  const commaIndex = dataUrl.indexOf(',');
  return commaIndex >= 0 ? dataUrl.slice(commaIndex + 1) : '';
}

/** Sleep for `ms` milliseconds. */
function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export class CVServerClient {
  private readonly config: ServerConfig;

  constructor(config: ServerConfig) {
    this.config = config;
  }

  /** Run Grounding DINO object detection on a JPEG image Blob. */
  async detectAsync(
    jpegBlob: Blob,
    prompt: string,
  ): Promise<DetectionResult> {
    const image = await blobToBase64(jpegBlob);
    return this.postJson<DetectionResult>('/v1/detect', {
      image,
      prompt,
      box_threshold: this.config.boxThreshold,
      text_threshold: this.config.textThreshold,
    });
  }

  /** Run SAM 2 segmentation on a JPEG image Blob. */
  async segmentAsync(
    jpegBlob: Blob,
    prompt: string,
    groundingModel = 'grounding_dino',
  ): Promise<SegmentationResult> {
    const image = await blobToBase64(jpegBlob);
    return this.postJson<SegmentationResult>('/v1/segment', {
      image,
      prompt,
      grounding_model: groundingModel,
      return_masks: true,
      mask_format: 'rle',
      box_threshold: this.config.boxThreshold,
      text_threshold: this.config.textThreshold,
    });
  }

  /** Run a Florence-2 vision task on a JPEG image Blob. */
  async florenceAsync(
    jpegBlob: Blob,
    task: string,
    textInput = '',
  ): Promise<FlorenceResult> {
    const image = await blobToBase64(jpegBlob);
    return this.postJson<FlorenceResult>('/v1/florence', {
      image,
      task,
      text_input: textInput,
    });
  }

  /** Check server health. Returns true when the server is reachable. */
  async healthCheck(): Promise<boolean> {
    const baseUrl = this.config.resolveBaseUrl().replace(/\/$/, '');
    try {
      const res = await fetch(`${baseUrl}/v1/health`, {
        signal: AbortSignal.timeout(this.config.requestTimeoutMs),
      });
      return res.ok;
    } catch {
      return false;
    }
  }

  // ── Internal ─────────────────────────────────────────────────────────────

  private async postJson<T>(endpoint: string, body: unknown): Promise<T> {
    const baseUrl = this.config.resolveBaseUrl().replace(/\/$/, '');
    if (!baseUrl) throw new Error('CV server base URL is not configured.');

    const url = `${baseUrl}${endpoint}`;
    const { retryCount, retryDelayMs, requestTimeoutMs } = this.config;

    let lastError: Error = new Error('Unknown error');

    for (let attempt = 0; attempt <= Math.max(0, retryCount); attempt++) {
      try {
        const res = await fetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
          signal: AbortSignal.timeout(requestTimeoutMs),
        });

        if (!res.ok) {
          throw new Error(`CV server returned HTTP ${res.status}: ${res.statusText}`);
        }

        return (await res.json()) as T;
      } catch (err) {
        lastError = err instanceof Error ? err : new Error(String(err));
        if (attempt < retryCount) {
          await sleep(retryDelayMs * (attempt + 1));
        }
      }
    }

    throw lastError;
  }
}
