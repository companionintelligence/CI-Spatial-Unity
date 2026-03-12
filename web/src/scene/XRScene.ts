/**
 * XR scene setup using the Meta Immersive Web SDK (@iwsdk/core).
 *
 * Builds a WebXR immersive-ar (passthrough) or immersive-vr scene.
 * Integrates all sub-systems:
 *   - CameraCapture (WebXR or getUserMedia)
 *   - ScanningPipeline (CV server orchestration)
 *   - SpatialPinManager (label rendering)
 *   - GltfMappingLayer (GLTF overlay)
 *   - GltfExporter (export + upload)
 *   - ControllerInput (Quest gamepad buttons)
 *   - HudDisplay (2-D overlay HUD)
 */

import {
  initializeWorld,
  SessionMode,
  ReferenceSpaceType,
  launchXR,
  createSystem,
  XRPlane,
  XRMesh,
} from '@iwsdk/core';
import type { World } from '@iwsdk/core';
import * as THREE from 'three';

import { ServerConfig } from '../core/ServerConfig.js';
import { CVServerClient } from '../core/CVServerClient.js';
import { SpatialPinManager } from '../mapping/SpatialPinManager.js';
import { ScanningPipeline } from '../mapping/ScanningPipeline.js';
import { CameraCapture } from './CameraCapture.js';
import { GltfMappingLayer } from './GltfMappingLayer.js';
import { GltfExporter } from '../export/GltfExporter.js';
import { ControllerInput } from '../ui/ControllerInput.js';
import { HudDisplay } from '../ui/HudDisplay.js';
import type { CaptureFrame } from '../core/types.js';

export class XRScene {
  private world: World | null = null;
  private scene!: THREE.Scene;
  private camera!: THREE.Camera;

  private serverConfig!: ServerConfig;
  private cvClient!: CVServerClient;
  /** @internal */ pinManager!: SpatialPinManager;
  /** @internal */ pipeline!: ScanningPipeline;
  private cameraCapture!: CameraCapture;
  /** Exposes the glTF mapping layer for loading external GLTF overlays. */
  mappingLayer!: GltfMappingLayer;
  private gltfExporter!: GltfExporter;
  private controllerInput!: ControllerInput;
  private hud!: HudDisplay;

  // Latest XR frame tracking for camera capture inside animation loop
  private latestXrFrame: XRFrame | null = null;

  // GLTF preview overlay element
  private gltfPreviewEl!: HTMLElement;
  private gltfPreviewContent!: HTMLElement;

  async init(container: HTMLDivElement, config: ServerConfig): Promise<void> {
    this.serverConfig = config;
    this.cvClient = new CVServerClient(config);
    this.hud = new HudDisplay();
    this.gltfExporter = new GltfExporter();
    this.cameraCapture = new CameraCapture(0.8);

    // GLTF preview overlay
    this.gltfPreviewEl = document.getElementById('gltf-preview')!;
    this.gltfPreviewContent = document.getElementById('gltf-preview-content')!;
    document.getElementById('btn-close-preview')?.addEventListener('click', () => {
      this.gltfPreviewEl.classList.remove('visible');
    });

    // ── IWSDK World ─────────────────────────────────────────────────────────
    this.world = await initializeWorld(container, {
      xr: {
        sessionMode: SessionMode.ImmersiveAR,
        referenceSpace: ReferenceSpaceType.LocalFloor,
        features: {
          planeDetection: true,
          meshDetection: true,
          anchors: true,
          hitTest: true,
        },
        offer: 'none', // We control launch manually
      },
      render: {
        defaultLighting: false,
      },
      features: {
        sceneUnderstanding: true,
      },
    });

    this.scene = this.world.scene;
    this.camera = this.world.camera;

    // ── Ambient light ────────────────────────────────────────────────────────
    const ambient = new THREE.AmbientLight(0xffffff, 0.6);
    this.scene.add(ambient);
    const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
    dirLight.position.set(1, 3, 2);
    this.scene.add(dirLight);

    // ── Pin manager ──────────────────────────────────────────────────────────
    this.pinManager = new SpatialPinManager(this.scene, () => undefined);

    // ── Scanning pipeline ────────────────────────────────────────────────────
    this.pipeline = new ScanningPipeline(
      this.cvClient,
      this.pinManager,
      this.camera,
      (state) => this.hud.update(state),
    );

    // ── GLTF mapping layer ───────────────────────────────────────────────────
    this.mappingLayer = new GltfMappingLayer(this.scene, {
      opacity: 0.35,
      color: 0x00aaff,
    });

    // ── Controller input ─────────────────────────────────────────────────────
    this.controllerInput = new ControllerInput({
      doCapture: () => void this.doCapture(),
      cycleModel: () => this.pipeline.cycleModel(),
      cyclePreviewDistance: () => this.pipeline.cyclePreviewDistance(),
      doSaveExport: () => void this.doSaveExport(),
      cycleDisplayMode: () => this.pipeline.cycleDisplayMode(),
      previewGltf: () => void this.previewGltf(),
    });

    // ── ECS systems ──────────────────────────────────────────────────────────
    this.registerEcsSystems(this.world);

    this.hud.update(this.pipeline.currentState);
  }

  /** Launch an immersive-AR session. */
  async launchAR(): Promise<void> {
    if (!this.world) return;
    launchXR(this.world, {
      sessionMode: SessionMode.ImmersiveAR,
      features: {
        planeDetection: true,
        meshDetection: true,
        anchors: true,
        hitTest: true,
      },
    });
    await this.startCameraCapture();
  }

  /** Launch an immersive-VR session. */
  async launchVR(): Promise<void> {
    if (!this.world) return;
    launchXR(this.world, { sessionMode: SessionMode.ImmersiveVR });
    await this.startCameraCapture();
  }

  /** Start the non-immersive desktop/webcam mode. */
  async startDesktop(): Promise<void> {
    await this.startCameraCapture();
    this.hud.setStatus('Desktop mode active', '#88ff88');
  }

  cycleModel(): void {
    this.pipeline.cycleModel();
  }

  cycleDisplayMode(): void {
    this.pipeline.cycleDisplayMode();
  }

  cyclePreviewDistance(): void {
    this.pipeline.cyclePreviewDistance();
  }

  async loadMappingFile(file: Blob): Promise<void> {
    await this.mappingLayer.loadMapping(file);
    this.hud.setStatus('Loaded mapping overlay', '#88ff88');
  }

  clearMappingLayers(): void {
    this.mappingLayer.clearMappings();
    this.hud.setStatus('Cleared mapping overlays', '#88ff88');
  }

  // ── Actions wired to DOM buttons and controller ───────────────────────────

  async doCapture(): Promise<void> {
    const frame = await this.buildCaptureFrame();
    if (!frame) {
      this.hud.setStatus('No camera frame', '#ff6666');
      return;
    }
    await this.pipeline.doCapture(frame);
  }

  async doSaveExport(): Promise<void> {
    this.hud.setStatus('Exporting…', '#ffd700');
    try {
      const { gltfBlob, extras } = await this.gltfExporter.exportScene(
        this.scene,
        this.pinManager.allPins,
        this.pipeline.currentState.sessionId,
      );

      const uploaded = await this.gltfExporter.uploadGltf(
        gltfBlob,
        extras,
        this.serverConfig,
      );

      if (!uploaded) {
        this.gltfExporter.downloadGltf(gltfBlob, `scan_${Date.now()}.gltf`);
        this.hud.setStatus('Exported (downloaded)', '#88ff88');
      } else {
        this.hud.setStatus('Exported & uploaded ✓', '#88ff88');
      }
    } catch (err) {
      console.error('[XRScene] Export failed:', err);
      this.hud.setStatus('Export failed', '#ff6666');
    }
  }

  async previewGltf(): Promise<void> {
    try {
      const { extras } = await this.gltfExporter.exportScene(
        this.scene,
        this.pinManager.allPins,
        this.pipeline.currentState.sessionId,
      );

      if (this.gltfPreviewContent) {
        this.gltfPreviewContent.textContent = JSON.stringify(extras, null, 2);
      }
      this.gltfPreviewEl?.classList.add('visible');
    } catch (err) {
      console.error('[XRScene] Preview failed:', err);
    }
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private async startCameraCapture(): Promise<void> {
    try {
      await this.cameraCapture.startCapture();
    } catch (err) {
      console.warn('[XRScene] Camera capture unavailable:', err);
    }
  }

  private async buildCaptureFrame(): Promise<CaptureFrame | null> {
    const raw = await this.cameraCapture.captureFrame({
      xrFrame: this.latestXrFrame,
      xrSession: this.world?.session ?? null,
      fallbackPose: cameraToPose(this.camera),
    });
    if (!raw) return null;

    const state = this.pipeline.currentState;
    return {
      id: `cap-${Date.now()}`,
      sessionId: state.sessionId,
      frameIndex: state.captureCount,
      timestamp: raw.timestamp,
      jpegBlob: raw.jpegBlob,
      imageWidth: raw.width,
      imageHeight: raw.height,
      cameraPose: raw.pose,
      intrinsics: raw.intrinsics,
    };
  }

  /**
   * Register ECS systems using IWSDK's class-based createSystem API.
   * Systems extend the constructor returned by createSystem() and are
   * registered with world.registerSystem().
   */
  private registerEcsSystems(world: World): void {
    // Capture `this` for use inside system closures
    const self = this;

    // ── Tick system: per-frame controller polling + XR frame tracking ────────
    class TickSystem extends createSystem({}) {
      override update(_delta: number, _time: number): void {
        // Track current XR frame for camera capture
        self.latestXrFrame = world.renderer.xr.getFrame() ?? null;
        // Poll gamepad buttons
        self.controllerInput.update();
      }
    }

    // ── Scene understanding: ingest XR planes as MetaScene pins ─────────────
    class PlaneIngestSystem extends createSystem({
      planes: { required: [XRPlane] },
    }) {
      override init(): void {
        // Subscribe to newly qualified plane entities
        this.queries.planes.subscribe(
          'qualify',
          (entity) => {
            try {
              const obj = entity.object3D;
              const pos = obj ? obj.getWorldPosition(new THREE.Vector3()) : new THREE.Vector3();
              self.pinManager.addPin({
                label: 'plane',
                semanticLabel: 'plane',
                score: 1.0,
                source: 'MetaScene',
                position: [pos.x, pos.y, pos.z],
                normal: [0, 1, 0],
                rotation: [0, 0, 0, 1],
                bboxPixels: null,
                bboxNormalized: null,
                planeSize: null,
                volumeSize: null,
                anchorUuid: null,
                sourcePrompt: 'XRPlane',
                timestamp: performance.now() / 1000,
                captureId: '',
                captureSessionId: self.pipeline.currentState.sessionId,
                captureFrameIndex: 0,
                placementConfidence: 1.0,
                hitType: 'MetaPlane',
                trackId: 0,
              });
            } catch {
              // Ignore errors during plane ingestion
            }
          },
          true,
        );
      }
      override update(_delta: number, _time: number): void {
        // Handled via subscribe callbacks in init()
      }
    }

    // ── Scene understanding: ingest XR meshes as overlay geometry ───────────
    class MeshIngestSystem extends createSystem({
      meshes: { required: [XRMesh] },
    }) {
      override init(): void {
        this.queries.meshes.subscribe(
          'qualify',
          (entity) => {
            try {
              const obj = entity.object3D;
              if (obj) {
                // Clone the mesh and add it to our mapping layer root
                const clone = obj.clone();
                clone.traverse((child) => {
                  if (child instanceof THREE.Mesh) {
                    child.material = new THREE.MeshBasicMaterial({
                      color: 0x00aaff,
                      transparent: true,
                      opacity: 0.3,
                      wireframe: false,
                      side: THREE.DoubleSide,
                      depthWrite: false,
                    });
                  }
                });
                self.mappingLayer.addObject(clone, false);
              }
            } catch {
              // Ignore errors during mesh ingestion
            }
          },
          true,
        );
      }
      override update(_delta: number, _time: number): void {
        // Handled via subscribe callbacks in init()
      }
    }

    world.registerSystem(TickSystem);
    world.registerSystem(PlaneIngestSystem);
    world.registerSystem(MeshIngestSystem);
  }
}

function cameraToPose(camera: THREE.Camera): import('../core/types.js').CameraPose {
  const position = new THREE.Vector3();
  const rotation = new THREE.Quaternion();
  camera.getWorldPosition(position);
  camera.getWorldQuaternion(rotation);

  return {
    position: [position.x, position.y, position.z],
    rotation: [rotation.x, rotation.y, rotation.z, rotation.w],
  };
}
