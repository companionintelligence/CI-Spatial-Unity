/**
 * GLTF importer for the World Map Viewer/Editor.
 *
 * Loads a .gltf or .glb file (from a File or URL), returns:
 * - The Three.js scene graph
 * - SpatialPins extracted from the glTF root-level extras
 *   (as exported by GltfExporter)
 */

import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import type { GLTF } from 'three/examples/jsm/loaders/GLTFLoader.js';
import type { SpatialPin } from '../mapping/SpatialPin.js';
import type { GltfExtras } from '../export/GltfExporter.js';

export interface ImportResult {
  /** The loaded Three.js scene group. */
  scene: THREE.Group;
  /** Spatial pins found in the glTF extras (may be empty). */
  pins: SpatialPin[];
  /** Full extras object from the glTF root (null if absent). */
  extras: GltfExtras | null;
}

export class GltfImporter {
  private readonly loader = new GLTFLoader();

  /** Load from a browser File object (uses an object URL internally). */
  async loadFromFile(file: File): Promise<ImportResult> {
    const url = URL.createObjectURL(file);
    try {
      return await this.loadFromUrl(url);
    } finally {
      URL.revokeObjectURL(url);
    }
  }

  /** Load from a URL string. */
  loadFromUrl(url: string): Promise<ImportResult> {
    return new Promise((resolve, reject) => {
      this.loader.load(
        url,
        (gltf: GLTF) => {
          const extras = this.extractExtras(gltf);
          const pins: SpatialPin[] = extras?.pins ?? [];
          resolve({ scene: gltf.scene, pins, extras });
        },
        undefined,
        (err) => reject(new Error(`GLTFLoader error: ${String(err)}`)),
      );
    });
  }

  // ── Private ────────────────────────────────────────────────────────────────

  private extractExtras(gltf: GLTF): GltfExtras | null {
    try {
      const raw = (gltf.parser.json as Record<string, unknown>)['extras'];
      if (!raw || typeof raw !== 'object') return null;
      return raw as GltfExtras;
    } catch {
      return null;
    }
  }
}
