/**
 * glTF mapping layer — visualises loaded glTF mapping files in the scene.
 *
 * Mirrors the GLTF-based mapping overlay from the Unity pipeline.
 * Uses Three.js GLTFLoader to load remote GLTF files and applies a
 * transparent overlay material so spatial mapping meshes are visible
 * while the user sees passthrough reality.
 */

import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

export interface MappingLayerOptions {
  /** Opacity of the spatial mapping mesh overlay (default: 0.4). */
  opacity?: number;
  /** Colour of the mapping mesh (default: #00aaff). */
  color?: string | number;
  /** Whether to render as wireframe (default: false). */
  wireframe?: boolean;
}

export class GltfMappingLayer {
  private readonly loader = new GLTFLoader();
  private readonly root: THREE.Group;
  private readonly opts: Required<MappingLayerOptions>;

  constructor(scene: THREE.Scene, opts: MappingLayerOptions = {}) {
    this.opts = {
      opacity: opts.opacity ?? 0.4,
      color: opts.color ?? 0x00aaff,
      wireframe: opts.wireframe ?? false,
    };
    this.root = new THREE.Group();
    this.root.name = 'GltfMappingLayer';
    scene.add(this.root);
  }

  /** Load a glTF from a URL or Blob object-URL and add it to the scene. */
  async loadMapping(urlOrBlob: string | Blob): Promise<void> {
    const url =
      urlOrBlob instanceof Blob
        ? URL.createObjectURL(urlOrBlob)
        : urlOrBlob;

    return new Promise((resolve, reject) => {
      this.loader.load(
        url,
        (gltf) => {
          this.applyOverlayMaterial(gltf.scene);
          this.root.add(gltf.scene);
          if (urlOrBlob instanceof Blob) URL.revokeObjectURL(url);
          resolve();
        },
        undefined,
        (err) => {
          if (urlOrBlob instanceof Blob) URL.revokeObjectURL(url);
          reject(err);
        },
      );
    });
  }

  /** Remove all loaded mapping meshes from the scene. */
  clearMappings(): void {
    while (this.root.children.length > 0) {
      const child = this.root.children[0];
      this.root.remove(child);
      this.disposeMesh(child);
    }
  }

  /** Toggle wireframe rendering on all mapping meshes. */
  setWireframe(wireframe: boolean): void {
    this.opts.wireframe = wireframe;
    this.root.traverse((obj) => {
      if (obj instanceof THREE.Mesh) {
        const mat = obj.material as THREE.MeshBasicMaterial;
        if (mat) mat.wireframe = wireframe;
      }
    });
  }

  /** Set mesh overlay opacity. */
  setOpacity(opacity: number): void {
    this.opts.opacity = opacity;
    this.root.traverse((obj) => {
      if (obj instanceof THREE.Mesh) {
        const mat = obj.material as THREE.MeshBasicMaterial;
        if (mat) mat.opacity = opacity;
      }
    });
  }

  // ── Private ────────────────────────────────────────────────────────────────

  private applyOverlayMaterial(obj: THREE.Object3D): void {
    obj.traverse((child) => {
      if (child instanceof THREE.Mesh) {
        child.material = new THREE.MeshBasicMaterial({
          color: new THREE.Color(this.opts.color),
          transparent: true,
          opacity: this.opts.opacity,
          wireframe: this.opts.wireframe,
          side: THREE.DoubleSide,
          depthWrite: false,
        });
      }
    });
  }

  private disposeMesh(obj: THREE.Object3D): void {
    obj.traverse((child) => {
      if (child instanceof THREE.Mesh) {
        child.geometry?.dispose();
        if (Array.isArray(child.material)) {
          child.material.forEach((m) => m.dispose());
        } else {
          child.material?.dispose();
        }
      }
    });
  }
}
