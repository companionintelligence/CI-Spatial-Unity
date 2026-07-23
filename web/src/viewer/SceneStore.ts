/**
 * Scene store for the World Map Viewer/Editor.
 *
 * Inspired by Pascal Editor's flat Zustand store pattern:
 * - All nodes (pins) are held in a flat Map keyed by id.
 * - Observable via EventTarget ('change', 'selection-change', 'load').
 * - Simple undo/redo history (50-step stack).
 */

import type { SpatialPin } from '../mapping/SpatialPin.js';
import type { GltfExtras } from '../export/GltfExporter.js';

/** A node in the flat scene dictionary (Pascal Editor BaseNode analogue). */
export interface ViewerNode {
  id: string;
  type: 'pin' | 'gltf-mesh';
  pin?: SpatialPin;
  visible: boolean;
}

export type StoreEventType = 'change' | 'selection-change' | 'load';

/**
 * Flat, observable scene store.
 *
 * All create/update/delete operations push a history snapshot for undo/redo,
 * then dispatch a 'change' event so UI can re-render.
 */
export class SceneStore extends EventTarget {
  /** Flat dictionary of all nodes (Pascal Editor nodes: Record<id, Node>). */
  private _nodes: Map<string, ViewerNode> = new Map();

  /** Currently selected node id. */
  selectedId: string | null = null;

  /** Extras parsed from the loaded glTF root. */
  extras: GltfExtras | null = null;

  // ── Undo/redo ──────────────────────────────────────────────────────────────
  private history: Array<Map<string, ViewerNode>> = [];
  private historyIndex = -1;
  private static readonly MAX_HISTORY = 50;

  // ── Read accessors ─────────────────────────────────────────────────────────

  get allNodes(): ReadonlyMap<string, ViewerNode> {
    return this._nodes;
  }

  get allPins(): ViewerNode[] {
    return [...this._nodes.values()].filter((n) => n.type === 'pin');
  }

  // ── Write operations ───────────────────────────────────────────────────────

  addNode(node: ViewerNode): void {
    this.pushHistory();
    this._nodes.set(node.id, node);
    this.emit('change');
  }

  updateNode(id: string, updates: Partial<ViewerNode>): void {
    const existing = this._nodes.get(id);
    if (!existing) return;
    this.pushHistory();
    this._nodes.set(id, { ...existing, ...updates });
    this.emit('change');
  }

  /** Update pin-specific fields only. */
  updatePin(id: string, pinUpdates: Partial<SpatialPin>): void {
    const node = this._nodes.get(id);
    if (!node?.pin) return;
    this.pushHistory();
    this._nodes.set(id, { ...node, pin: { ...node.pin, ...pinUpdates } });
    this.emit('change');
  }

  deleteNode(id: string): void {
    if (!this._nodes.has(id)) return;
    this.pushHistory();
    this._nodes.delete(id);
    if (this.selectedId === id) {
      this.selectedId = null;
      this.emit('selection-change');
    }
    this.emit('change');
  }

  setSelected(id: string | null): void {
    if (this.selectedId === id) return;
    this.selectedId = id;
    this.emit('selection-change');
  }

  /** Replace all nodes with the provided pins (load operation). */
  loadPins(pins: SpatialPin[], extras: GltfExtras): void {
    this.pushHistory();
    this._nodes.clear();
    this.extras = extras;
    for (const pin of pins) {
      const id = `pin_${pin.captureId || 'x'}_${pin.trackId}_${Math.random()
        .toString(36)
        .slice(2, 7)}`;
      this._nodes.set(id, { id, type: 'pin', pin, visible: true });
    }
    this.selectedId = null;
    this.emit('load');
    this.emit('change');
  }

  clear(): void {
    this.pushHistory();
    this._nodes.clear();
    this.selectedId = null;
    this.extras = null;
    this.emit('change');
    this.emit('selection-change');
  }

  // ── Undo / redo ────────────────────────────────────────────────────────────

  undo(): void {
    if (this.historyIndex <= 0) return;
    this.historyIndex--;
    this._nodes = new Map(this.history[this.historyIndex]);
    this.emit('change');
  }

  redo(): void {
    if (this.historyIndex >= this.history.length - 1) return;
    this.historyIndex++;
    this._nodes = new Map(this.history[this.historyIndex]);
    this.emit('change');
  }

  // ── Private helpers ────────────────────────────────────────────────────────

  private pushHistory(): void {
    this.history.splice(this.historyIndex + 1);
    this.history.push(new Map(this._nodes));
    if (this.history.length > SceneStore.MAX_HISTORY) {
      this.history.shift();
    }
    this.historyIndex = this.history.length - 1;
  }

  private emit(type: StoreEventType): void {
    this.dispatchEvent(new Event(type));
  }
}
