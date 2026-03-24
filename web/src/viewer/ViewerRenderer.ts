/**
 * Three.js-based viewer renderer for the World Map Editor.
 *
 * Provides a 3D scene with:
 * - WebGL renderer with antialias
 * - PerspectiveCamera + OrbitControls (with damping)
 * - Grid floor + coordinate axes helper
 * - Ambient + directional lighting
 * - Raycaster-based click-to-select
 * - Responsive resize handling
 *
 * Architecture inspired by the Pascal Editor @pascal-app/viewer package:
 * sensible defaults; extensible via the scene/camera/renderer properties.
 */

import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

export class ViewerRenderer {
  readonly renderer: THREE.WebGLRenderer;
  readonly scene: THREE.Scene;
  readonly camera: THREE.PerspectiveCamera;
  readonly controls: OrbitControls;

  private animId = -1;
  private readonly raycaster = new THREE.Raycaster();
  private readonly clickHandlers: Array<(obj: THREE.Object3D | null) => void> = [];
  private resizeObserver: ResizeObserver | null = null;

  constructor(canvas: HTMLCanvasElement) {
    // ── Renderer ─────────────────────────────────────────────────────────────
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(canvas.clientWidth || 800, canvas.clientHeight || 600, false);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    // ── Scene ─────────────────────────────────────────────────────────────────
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x1a1a2e);
    this.scene.fog = new THREE.FogExp2(0x1a1a2e, 0.015);

    // ── Camera ────────────────────────────────────────────────────────────────
    this.camera = new THREE.PerspectiveCamera(
      60,
      (canvas.clientWidth || 800) / (canvas.clientHeight || 600),
      0.01,
      200,
    );
    this.camera.position.set(3, 3, 5);
    this.camera.lookAt(0, 0, 0);

    // ── Orbit controls ────────────────────────────────────────────────────────
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.05;
    this.controls.minDistance = 0.1;
    this.controls.maxDistance = 100;
    this.controls.screenSpacePanning = true;

    // ── Lighting ──────────────────────────────────────────────────────────────
    const ambient = new THREE.AmbientLight(0xffffff, 0.5);
    this.scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xffffff, 1.0);
    sun.position.set(5, 10, 5);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    this.scene.add(sun);

    const fill = new THREE.DirectionalLight(0x8899ff, 0.3);
    fill.position.set(-5, 3, -5);
    this.scene.add(fill);

    // ── Grid + axes ───────────────────────────────────────────────────────────
    const grid = new THREE.GridHelper(20, 20, 0x444466, 0x333344);
    this.scene.add(grid);

    const axes = new THREE.AxesHelper(0.5);
    this.scene.add(axes);

    // ── Resize observer ───────────────────────────────────────────────────────
    this.resizeObserver = new ResizeObserver(() => this.onResize());
    this.resizeObserver.observe(canvas.parentElement ?? canvas);

    // ── Click selection ───────────────────────────────────────────────────────
    canvas.addEventListener('click', (e) => this.onCanvasClick(e));
  }

  /** Start the animation/render loop. */
  start(): void {
    const loop = () => {
      this.animId = requestAnimationFrame(loop);
      this.controls.update();
      this.renderer.render(this.scene, this.camera);
    };
    loop();
  }

  /** Stop the loop and dispose GPU resources. */
  dispose(): void {
    cancelAnimationFrame(this.animId);
    this.resizeObserver?.disconnect();
    this.controls.dispose();
    this.renderer.dispose();
  }

  /**
   * Register a callback that receives the first clicked Object3D (or null if
   * the click lands on the background).
   */
  onObjectClick(handler: (obj: THREE.Object3D | null) => void): void {
    this.clickHandlers.push(handler);
  }

  /**
   * Reframe the camera to nicely fit all meshes in the scene.
   * Inspired by Pascal Editor's "node focusing" behaviour.
   */
  frameAll(): void {
    const box = new THREE.Box3();
    this.scene.traverse((obj) => {
      if (obj instanceof THREE.Mesh) box.expandByObject(obj);
    });
    if (box.isEmpty()) return;

    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const maxDim = Math.max(size.x, size.y, size.z, 0.5);

    this.camera.position
      .copy(center)
      .add(new THREE.Vector3(maxDim, maxDim * 0.8, maxDim * 1.5));
    this.controls.target.copy(center);
    this.controls.update();
  }

  // ── Private helpers ────────────────────────────────────────────────────────

  private onResize(): void {
    const canvas = this.renderer.domElement;
    const w = canvas.clientWidth;
    const h = canvas.clientHeight;
    if (w === 0 || h === 0) return;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  private onCanvasClick(event: MouseEvent): void {
    const canvas = this.renderer.domElement;
    const rect = canvas.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
    this.raycaster.setFromCamera(new THREE.Vector2(x, y), this.camera);
    const hits = this.raycaster.intersectObjects(this.scene.children, true);
    const obj = hits.length > 0 ? hits[0].object : null;
    for (const handler of this.clickHandlers) handler(obj);
  }
}
