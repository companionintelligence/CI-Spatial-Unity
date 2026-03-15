/**
 * glTF export and upload — mirrors Assets/CompanionScan/Export/SpatialMapExporter.cs
 * and Assets/CompanionScan/Export/GltfSyncClient.cs.
 *
 * Exports the Three.js scene (or a sub-graph) to a .gltf blob, embeds spatial
 * pin metadata as glTF extras, then POSTs the file to the cv-server /v1/gltf
 * endpoint.
 */

import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import type { SpatialPin } from '../mapping/SpatialPin.js';
import type { ServerConfig } from '../core/ServerConfig.js';

/** Extras blob that is embedded in the exported glTF. */
export interface GltfExtras {
  session_id: string;
  export_timestamp: number;
  pins: SpatialPin[];
  pin_count: number;
  cv_pin_count: number;
  meta_scene_pin_count: number;
}

export interface ExportResult {
  gltfBlob: Blob;
  extras: GltfExtras;
}

export class GltfExporter {
  private readonly exporter = new GLTFExporter();

  /**
   * Export `root` (plus all descendants) to a binary glTF blob, with pin
   * metadata injected into `extras`.
   */
  async exportScene(
    root: THREE.Object3D,
    pins: ReadonlyArray<SpatialPin>,
    sessionId: string,
  ): Promise<ExportResult> {
    const extras: GltfExtras = {
      session_id: sessionId,
      export_timestamp: Date.now() / 1000,
      pins: pins as SpatialPin[],
      pin_count: pins.length,
      cv_pin_count: pins.filter((p) => p.source === 'CvServer').length,
      meta_scene_pin_count: pins.filter((p) => p.source === 'MetaScene').length,
    };

    return new Promise((resolve, reject) => {
      this.exporter.parse(
        root,
        (gltfData) => {
          // Inject extras into the root of the glTF JSON
          if (typeof gltfData === 'object' && !ArrayBuffer.isView(gltfData)) {
            (gltfData as Record<string, unknown>).extras = extras;
          }

          let gltfBlob: Blob;
          if (gltfData instanceof ArrayBuffer) {
            gltfBlob = new Blob([gltfData], { type: 'model/gltf-binary' });
          } else {
            gltfBlob = new Blob([JSON.stringify(gltfData, null, 2)], {
              type: 'model/gltf+json',
            });
          }

          resolve({ gltfBlob, extras });
        },
        (err) => reject(new Error(`GLTFExporter error: ${String(err)}`)),
        {
          binary: false,
          embedImages: true,
          onlyVisible: false,
        },
      );
    });
  }

  /**
   * Upload a glTF blob to the cv-server /v1/gltf endpoint via multipart form.
   *
   * Returns true on success.
   */
  async uploadGltf(
    gltfBlob: Blob,
    extras: GltfExtras,
    config: ServerConfig,
    filename = 'scan.gltf',
  ): Promise<boolean> {
    const baseUrl = config.resolveBaseUrl().replace(/\/$/, '');
    if (!baseUrl) {
      console.error('[GltfExporter] Base URL not configured.');
      return false;
    }

    const form = new FormData();
    form.append('gltf', gltfBlob, filename);
    form.append('extras', JSON.stringify(extras));

    try {
      const res = await fetch(`${baseUrl}/v1/gltf`, {
        method: 'POST',
        body: form,
        signal: AbortSignal.timeout(30_000),
      });
      if (!res.ok) {
        console.error(`[GltfExporter] Upload failed: HTTP ${res.status}`);
        return false;
      }
      console.log('[GltfExporter] Upload complete.');
      return true;
    } catch (err) {
      console.error('[GltfExporter] Upload error:', err);
      return false;
    }
  }

  /** Trigger a browser download of the glTF blob. */
  downloadGltf(gltfBlob: Blob, filename = 'scan.gltf'): void {
    const url = URL.createObjectURL(gltfBlob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 5000);
  }
}
