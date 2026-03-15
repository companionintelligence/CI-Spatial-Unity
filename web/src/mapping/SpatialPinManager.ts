/**
 * Spatial pin manager — mirrors Assets/CompanionScan/Mapping/SpatialPinManager.cs.
 *
 * Central in-memory store for SpatialPins from all capture sources.
 * Provides deduplication, track-id merging, and optional lifecycle pruning.
 * Also manages Three.js label objects rendered in the XR scene.
 */

import * as THREE from 'three';
import type { SpatialPin, PinSource } from './SpatialPin.js';

export interface PinManagerConfig {
  /** Enable distance-based deduplication (default: true). */
  enableDeduplication?: boolean;
  /** Maximum distance in metres for deduplication (default: 0.15). */
  dedupDistance?: number;
  /** Enable stale-pin pruning (default: true). */
  enablePinLifecycle?: boolean;
  /** Age in seconds after which a pin is pruned (default: 0 = disabled). */
  pinMaxAgeSeconds?: number;
  /** Prefer track-id based merging when trackId != 0 (default: true). */
  preferTrackIds?: boolean;
}

/** Callback fired whenever pins change. */
export type PinsChangedCallback = (pins: ReadonlyArray<SpatialPin>) => void;

export class SpatialPinManager {
  private readonly pins: SpatialPin[] = [];
  private readonly objects: THREE.Object3D[] = [];
  private readonly scene: THREE.Scene;
  private readonly onChange: PinsChangedCallback;
  private readonly cfg: Required<PinManagerConfig>;

  constructor(
    scene: THREE.Scene,
    onChange: PinsChangedCallback,
    config: PinManagerConfig = {},
  ) {
    this.scene = scene;
    this.onChange = onChange;
    this.cfg = {
      enableDeduplication: config.enableDeduplication ?? true,
      dedupDistance: config.dedupDistance ?? 0.15,
      enablePinLifecycle: config.enablePinLifecycle ?? true,
      pinMaxAgeSeconds: config.pinMaxAgeSeconds ?? 0,
      preferTrackIds: config.preferTrackIds ?? true,
    };
  }

  get allPins(): ReadonlyArray<SpatialPin> {
    return this.pins;
  }

  get cvPins(): SpatialPin[] {
    return this.pins.filter((p) => p.source === 'CvServer');
  }

  get metaScenePins(): SpatialPin[] {
    return this.pins.filter((p) => p.source === 'MetaScene');
  }

  addPin(pin: SpatialPin): void {
    if (this.cfg.enablePinLifecycle && this.cfg.pinMaxAgeSeconds > 0) {
      this.pruneStalePins(pin.timestamp);
    }

    if (this.cfg.preferTrackIds && pin.trackId !== 0 && this.tryMergeTrack(pin)) {
      this.onChange(this.pins);
      return;
    }

    if (this.cfg.enableDeduplication && this.tryMergePin(pin)) {
      this.onChange(this.pins);
      return;
    }

    this.pins.push(pin);
    this.spawnLabel(pin, this.pins.length - 1);
    this.onChange(this.pins);
  }

  clearPins(source?: PinSource): void {
    if (source == null) {
      // Remove all objects from scene
      for (const obj of this.objects) {
        this.disposeObject(obj);
        this.scene.remove(obj);
      }
      this.objects.length = 0;
      this.pins.length = 0;
    } else {
      for (let i = this.pins.length - 1; i >= 0; i--) {
        if (this.pins[i].source === source) {
          this.removeObjectAt(i);
          this.pins.splice(i, 1);
        }
      }
    }
    this.onChange(this.pins);
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private pruneStalePins(now: number): void {
    const maxAge = this.cfg.pinMaxAgeSeconds;
    for (let i = this.pins.length - 1; i >= 0; i--) {
      if (now - this.pins[i].timestamp > maxAge) {
        this.removeObjectAt(i);
        this.pins.splice(i, 1);
      }
    }
  }

  private tryMergePin(pin: SpatialPin): boolean {
    if (!pin.semanticLabel) return false;
    for (let i = 0; i < this.pins.length; i++) {
      const ex = this.pins[i];
      if (ex.source !== pin.source) continue;
      if (ex.semanticLabel.toLowerCase() !== pin.semanticLabel.toLowerCase()) continue;
      if (distance3(ex.position, pin.position) > this.cfg.dedupDistance) continue;
      this.mergeInto(i, pin);
      return true;
    }
    return false;
  }

  private tryMergeTrack(pin: SpatialPin): boolean {
    for (let i = 0; i < this.pins.length; i++) {
      if (this.pins[i].trackId === pin.trackId) {
        this.mergeInto(i, pin);
        return true;
      }
    }
    return false;
  }

  private mergeInto(index: number, incoming: SpatialPin): void {
    const ex = this.pins[index];
    const merged: SpatialPin = {
      ...ex,
      position: lerp3(ex.position, incoming.position, 0.5),
      normal: normalize3(lerp3(ex.normal, incoming.normal, 0.5)),
      score: Math.max(ex.score, incoming.score),
      timestamp: Math.max(ex.timestamp, incoming.timestamp),
      placementConfidence: Math.max(ex.placementConfidence, incoming.placementConfidence),
      captureId: incoming.captureId || ex.captureId,
      captureSessionId: incoming.captureSessionId || ex.captureSessionId,
      captureFrameIndex: incoming.captureFrameIndex || ex.captureFrameIndex,
      trackId: incoming.trackId || ex.trackId,
      hitType: incoming.hitType,
    };
    this.pins[index] = merged;
    const obj = this.objects[index];
    if (obj) {
      obj.position.set(...merged.position);
    }
  }

  private spawnLabel(pin: SpatialPin, index: number): void {
    const obj = buildLabelObject(pin);
    this.scene.add(obj);
    this.objects[index] = obj;
  }

  private removeObjectAt(index: number): void {
    const obj = this.objects[index];
    if (obj) {
      this.disposeObject(obj);
      this.scene.remove(obj);
    }

    this.objects.splice(index, 1);
  }

  private disposeObject(obj: THREE.Object3D): void {
    obj.traverse((child) => {
      if (child instanceof THREE.Sprite) {
        const material = child.material;
        if (Array.isArray(material)) {
          for (const entry of material) {
            entry.map?.dispose();
            entry.dispose();
          }
          return;
        }

        material.map?.dispose();
        material.dispose();
      }
    });
  }
}

// ── Three.js label sprite ─────────────────────────────────────────────────────

function buildLabelObject(pin: SpatialPin): THREE.Object3D {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 64;
  const ctx = canvas.getContext('2d')!;

  const bg = pin.source === 'CvServer' ? '#1e3a5f' : '#2d4a1e';
  ctx.fillStyle = bg;
  ctx.beginPath();
  ctx.roundRect(4, 4, 248, 56, 8);
  ctx.fill();

  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 20px sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(truncate(pin.label, 22), 128, 28);

  ctx.fillStyle = '#aaaaaa';
  ctx.font = '14px sans-serif';
  ctx.fillText(
    `${pin.source} · ${(pin.score * 100).toFixed(0)}%`,
    128,
    48,
  );

  const texture = new THREE.CanvasTexture(canvas);
  texture.needsUpdate = true;

  const mat = new THREE.SpriteMaterial({
    map: texture,
    depthTest: false,
    transparent: true,
  });
  const sprite = new THREE.Sprite(mat);
  sprite.scale.set(0.5, 0.125, 1);
  sprite.position.set(...pin.position);
  sprite.position.y += 0.15;
  sprite.name = `pin_${pin.label}`;
  return sprite;
}

function truncate(s: string, max: number): string {
  return s.length > max ? `${s.slice(0, max - 1)}…` : s;
}

function lerp3(
  a: [number, number, number],
  b: [number, number, number],
  t: number,
): [number, number, number] {
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

function normalize3(v: [number, number, number]): [number, number, number] {
  const len = Math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]) || 1;
  return [v[0] / len, v[1] / len, v[2] / len];
}

function distance3(
  a: [number, number, number],
  b: [number, number, number],
): number {
  return Math.sqrt(
    (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2,
  );
}
