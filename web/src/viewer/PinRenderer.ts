/**
 * Pin renderer for the World Map Viewer/Editor.
 *
 * Renders SpatialPins as color-coded 3D spheres with canvas-based label
 * sprites and surface-normal arrows.
 *
 * Color coding (Pascal Editor-style node renderers):
 *   - CvServer pins  → gold  (#ffd700)
 *   - MetaScene pins → cyan  (#00d4ff)
 *   - Selected pin   → emissive red highlight
 *
 * The renderer is driven by SceneStore changes:
 *   1. Call syncPins(store.allNodes) whenever the store emits 'change'.
 *   2. Call setSelected(id) whenever the store emits 'selection-change'.
 *   3. Use getNodeIdForObject(obj) to resolve a clicked mesh back to a node id.
 */

import * as THREE from 'three';
import type { ViewerNode } from './SceneStore.js';
import type { SpatialPin } from '../mapping/SpatialPin.js';

const PIN_RADIUS = 0.04;
const LABEL_CANVAS_W = 256;
const LABEL_CANVAS_H = 64;

const COLOR_CV = 0xffd700; // gold — CV server pins
const COLOR_META = 0x00d4ff; // cyan — MetaScene pins
const EMISSIVE_SELECTED = new THREE.Color(0xff3333);
const EMISSIVE_NORMAL = new THREE.Color(0x000000);

export class PinRenderer {
  private readonly group: THREE.Group;
  /** Map from node id → the group holding sphere + normal arrow + label. */
  private readonly meshes = new Map<string, THREE.Group>();

  constructor(scene: THREE.Scene) {
    this.group = new THREE.Group();
    this.group.name = 'PinRenderer';
    scene.add(this.group);
  }

  /**
   * Sync 3D objects to the current store nodes.
   * Creates new meshes for new pins, removes meshes for deleted pins,
   * and updates visibility for all existing pins.
   *
   * Pascal Editor pattern: equivalent to a NodeRenderer + dirty-node system.
   */
  syncPins(nodes: ReadonlyMap<string, ViewerNode>): void {
    const incoming = new Set<string>();

    for (const [id, node] of nodes) {
      if (node.type !== 'pin' || !node.pin) continue;
      incoming.add(id);

      const existing = this.meshes.get(id);
      if (existing) {
        existing.visible = node.visible;
      } else {
        const g = this.buildPinGroup(id, node.pin);
        this.group.add(g);
        this.meshes.set(id, g);
      }
    }

    // Remove meshes for nodes no longer in the store
    for (const [id, g] of this.meshes) {
      if (!incoming.has(id)) {
        this.group.remove(g);
        disposePinGroup(g);
        this.meshes.delete(id);
      }
    }
  }

  /**
   * Highlight the selected pin with an emissive glow.
   * Pascal Editor analogue: SelectionManager sets hover/selected state.
   */
  setSelected(selectedId: string | null): void {
    for (const [id, g] of this.meshes) {
      const sphere = g.children[0];
      if (sphere instanceof THREE.Mesh) {
        const mat = sphere.material as THREE.MeshStandardMaterial;
        if (id === selectedId) {
          mat.emissive.copy(EMISSIVE_SELECTED);
          mat.emissiveIntensity = 0.8;
        } else {
          mat.emissive.copy(EMISSIVE_NORMAL);
          mat.emissiveIntensity = 0;
        }
      }
    }
  }

  /**
   * Resolve a clicked Object3D back to its node id.
   * Returns null if the object is not part of any pin group.
   */
  getNodeIdForObject(obj: THREE.Object3D): string | null {
    for (const [id, g] of this.meshes) {
      if (obj === g || g.getObjectById(obj.id) !== undefined) return id;
    }
    return null;
  }

  /** Remove all pin meshes and dispose GPU resources. */
  dispose(): void {
    for (const g of this.meshes.values()) {
      disposePinGroup(g);
    }
    this.meshes.clear();
    this.group.parent?.remove(this.group);
  }

  // ── Private builders ───────────────────────────────────────────────────────

  private buildPinGroup(id: string, pin: SpatialPin): THREE.Group {
    const g = new THREE.Group();
    g.name = `pin_${id}`;
    g.position.set(pin.position[0], pin.position[1], pin.position[2]);

    // Sphere
    const color = pin.source === 'MetaScene' ? COLOR_META : COLOR_CV;
    const mat = new THREE.MeshStandardMaterial({
      color,
      roughness: 0.4,
      metalness: 0.1,
    });
    const sphere = new THREE.Mesh(new THREE.SphereGeometry(PIN_RADIUS, 12, 8), mat);
    sphere.castShadow = true;
    g.add(sphere);

    // Surface-normal arrow
    const normalVec = new THREE.Vector3(
      pin.normal[0],
      pin.normal[1],
      pin.normal[2],
    ).normalize();
    if (normalVec.lengthSq() > 0.5) {
      const arrow = new THREE.ArrowHelper(
        normalVec,
        new THREE.Vector3(0, 0, 0),
        PIN_RADIUS * 3,
        color,
        PIN_RADIUS,
        PIN_RADIUS * 0.6,
      );
      g.add(arrow);
    }

    // Canvas-texture label sprite
    const sprite = buildLabelSprite(pin.label);
    sprite.position.set(0, PIN_RADIUS * 3, 0);
    sprite.scale.set(0.28, 0.07, 1);
    g.add(sprite);

    return g;
  }
}

// ── Module-level helpers ───────────────────────────────────────────────────

function buildLabelSprite(text: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = LABEL_CANVAS_W;
  canvas.height = LABEL_CANVAS_H;
  const ctx = canvas.getContext('2d')!;

  // Background pill
  ctx.fillStyle = 'rgba(10, 10, 10, 0.75)';
  ctx.fillRect(0, 0, LABEL_CANVAS_W, LABEL_CANVAS_H);

  // Label text (truncated to 18 chars)
  const label = text.length > 18 ? `${text.slice(0, 16)}…` : text;
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 20px system-ui, sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(label, LABEL_CANVAS_W / 2, LABEL_CANVAS_H / 2);

  const texture = new THREE.CanvasTexture(canvas);
  const spriteMat = new THREE.SpriteMaterial({
    map: texture,
    depthTest: false,
    sizeAttenuation: true,
    transparent: true,
  });
  return new THREE.Sprite(spriteMat);
}

function disposePinGroup(g: THREE.Group): void {
  g.traverse((obj) => {
    if (obj instanceof THREE.Mesh) {
      obj.geometry.dispose();
      if (Array.isArray(obj.material)) {
        obj.material.forEach((m) => m.dispose());
      } else {
        (obj.material as THREE.Material).dispose();
      }
    }
    if (obj instanceof THREE.Sprite) {
      const mat = obj.material as THREE.SpriteMaterial;
      mat.map?.dispose();
      mat.dispose();
    }
  });
}
