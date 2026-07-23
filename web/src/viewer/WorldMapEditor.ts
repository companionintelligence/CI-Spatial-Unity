/**
 * World Map Editor — main orchestrator.
 *
 * Composes SceneStore + ViewerRenderer + PinRenderer + GltfImporter +
 * GltfExporter into a complete GLTF world-map viewer/editor experience.
 *
 * Architecture mirrors the Pascal Editor's separation of concerns:
 *   - SceneStore  ← useScene (node CRUD, undo/redo, persistence)
 *   - ViewerRenderer ← @pascal-app/viewer (Three.js, OrbitControls, defaults)
 *   - PinRenderer    ← NodeRenderer + Systems (dirty-node reactive updates)
 *   - GltfImporter   ← load world map from file / URL
 *   - GltfExporter   ← export edited scene back to glTF
 */

import * as THREE from 'three';
import { SceneStore } from './SceneStore.js';
import { ViewerRenderer } from './ViewerRenderer.js';
import { GltfImporter } from './GltfImporter.js';
import { PinRenderer } from './PinRenderer.js';
import { GltfExporter } from '../export/GltfExporter.js';
import type { SpatialPin } from '../mapping/SpatialPin.js';

export interface LoadSummary {
  pinCount: number;
  sessionId: string;
  filename: string;
}

export class WorldMapEditor {
  readonly store: SceneStore;
  readonly renderer: ViewerRenderer;

  private readonly importer: GltfImporter;
  private readonly pinRenderer: PinRenderer;
  private readonly exporter: GltfExporter;

  /** The currently loaded world-map geometry (transparent overlay). */
  private loadedScene: THREE.Group | null = null;

  constructor(canvas: HTMLCanvasElement) {
    this.store = new SceneStore();
    this.renderer = new ViewerRenderer(canvas);
    this.importer = new GltfImporter();
    this.pinRenderer = new PinRenderer(this.renderer.scene);
    this.exporter = new GltfExporter();

    // React to store changes — Pascal Editor dirty-node pattern:
    // when any node is created/updated/deleted, re-sync the 3D renderers.
    this.store.addEventListener('change', () => this.onStoreChange());
    this.store.addEventListener('selection-change', () => this.onSelectionChange());

    // Click-to-select: resolve clicked mesh → node id → store selection
    this.renderer.onObjectClick((obj) => {
      if (!obj) {
        this.store.setSelected(null);
        return;
      }
      const id = this.pinRenderer.getNodeIdForObject(obj);
      this.store.setSelected(id);
    });

    this.renderer.start();
  }

  // ── Loading ────────────────────────────────────────────────────────────────

  /** Load a .gltf / .glb file into the editor. */
  async loadFile(file: File): Promise<LoadSummary> {
    const result = await this.importer.loadFromFile(file);
    return this.applyImportResult(result, file.name);
  }

  /** Load a GLTF from a URL (useful for demo/test data). */
  async loadUrl(url: string, filename = 'map.gltf'): Promise<LoadSummary> {
    const result = await this.importer.loadFromUrl(url);
    return this.applyImportResult(result, filename);
  }

  // ── Export ─────────────────────────────────────────────────────────────────

  /** Export the current scene + edited pins to a glTF Blob. */
  async exportGltf(): Promise<Blob> {
    const pins = this.store.allPins
      .map((n) => n.pin)
      .filter((p): p is SpatialPin => p != null);
    const root = this.loadedScene ?? new THREE.Group();
    const sessionId =
      this.store.extras?.session_id ?? `editor_${Date.now()}`;
    const { gltfBlob } = await this.exporter.exportScene(root, pins, sessionId);
    return gltfBlob;
  }

  // ── Edit operations ────────────────────────────────────────────────────────

  /** Update pin metadata for the currently selected pin. */
  updateSelectedPin(updates: Partial<SpatialPin>): void {
    if (!this.store.selectedId) return;
    this.store.updatePin(this.store.selectedId, updates);
  }

  /** Delete the currently selected pin. */
  deleteSelectedPin(): void {
    if (!this.store.selectedId) return;
    this.store.deleteNode(this.store.selectedId);
  }

  undo(): void {
    this.store.undo();
  }

  redo(): void {
    this.store.redo();
  }

  /** Reset the editor to an empty state. */
  clear(): void {
    if (this.loadedScene) {
      this.renderer.scene.remove(this.loadedScene);
      this.loadedScene = null;
    }
    this.store.clear();
  }

  dispose(): void {
    this.renderer.dispose();
    this.pinRenderer.dispose();
  }

  // ── Private helpers ────────────────────────────────────────────────────────

  private applyImportResult(
    result: import('./GltfImporter.js').ImportResult,
    filename: string,
  ): LoadSummary {
    // Replace world-geometry overlay
    if (this.loadedScene) {
      this.renderer.scene.remove(this.loadedScene);
      this.loadedScene = null;
    }
    this.loadedScene = result.scene;

    // Apply semi-transparent overlay material (mirrors GltfMappingLayer)
    result.scene.traverse((obj) => {
      if (obj instanceof THREE.Mesh) {
        obj.material = new THREE.MeshBasicMaterial({
          color: 0x00aaff,
          transparent: true,
          opacity: 0.3,
          side: THREE.DoubleSide,
          depthWrite: false,
        });
      }
    });

    this.renderer.scene.add(result.scene);

    // Load pins into store
    if (result.extras) {
      this.store.loadPins(result.pins, result.extras);
    } else {
      const fallbackExtras = {
        session_id: `file_${Date.now()}`,
        export_timestamp: Date.now() / 1000,
        pins: result.pins,
        pin_count: result.pins.length,
        cv_pin_count: result.pins.filter((p) => p.source === 'CvServer').length,
        meta_scene_pin_count: result.pins.filter((p) => p.source === 'MetaScene')
          .length,
      };
      this.store.loadPins(result.pins, fallbackExtras);
    }

    this.renderer.frameAll();

    return {
      pinCount: result.pins.length,
      sessionId: result.extras?.session_id ?? 'unknown',
      filename,
    };
  }

  private onStoreChange(): void {
    this.pinRenderer.syncPins(this.store.allNodes);
  }

  private onSelectionChange(): void {
    this.pinRenderer.setSelected(this.store.selectedId);
  }
}
