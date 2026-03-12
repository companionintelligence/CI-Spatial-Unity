/**
 * Companion Scan Web — entry point.
 *
 * Wires up the DOM, reads settings, and delegates to XRScene.
 */

import { ServerConfig } from './core/ServerConfig.js';
import { XRScene } from './scene/XRScene.js';

const xrScene = new XRScene();
let config = new ServerConfig();
let initialised = false;

// ── DOM references ────────────────────────────────────────────────────────────
const launchOverlay = document.getElementById('launch-overlay')!;
const canvasContainer = document.getElementById('xr-canvas') as HTMLDivElement;
const inputBaseUrl = document.getElementById('input-base-url') as HTMLInputElement;
const inputBoxThresh = document.getElementById('input-box-thresh') as HTMLInputElement;
const inputTextThresh = document.getElementById('input-text-thresh') as HTMLInputElement;
const fileInputMapping = document.getElementById('input-mapping-file') as HTMLInputElement;

// ── Settings initialisation ───────────────────────────────────────────────────
function readConfig(): ServerConfig {
  const baseUrl = inputBaseUrl.value.trim() || config.resolveBaseUrl();
  const parsedBoxThreshold = parseFloat(inputBoxThresh.value);
  const boxThreshold = Number.isNaN(parsedBoxThreshold) ? 0.35 : parsedBoxThreshold;
  const parsedTextThreshold = parseFloat(inputTextThresh.value);
  const textThreshold = Number.isNaN(parsedTextThreshold) ? 0.25 : parsedTextThreshold;
  const cfg = new ServerConfig({ baseUrl, boxThreshold, textThreshold });
  cfg.setBaseUrlOverride(baseUrl);
  return cfg;
}

// Pre-fill inputs from stored config
window.addEventListener('DOMContentLoaded', () => {
  inputBaseUrl.value = config.resolveBaseUrl();
  inputBoxThresh.value = String(config.boxThreshold);
  inputTextThresh.value = String(config.textThreshold);
});

// ── Scene initialisation (lazy, triggered on first launch) ───────────────────
async function ensureInitialised(): Promise<void> {
  if (initialised) return;
  config = readConfig();
  try {
    await xrScene.init(canvasContainer, config);
    wireButtons();
    initialised = true;
  } catch (error) {
    initialised = false;
    throw error;
  }
}

// ── XR launch buttons ─────────────────────────────────────────────────────────
document.getElementById('btn-ar')?.addEventListener('click', async () => {
  launchOverlay.style.display = 'none';
  await ensureInitialised();
  await xrScene.launchAR();
});

document.getElementById('btn-vr')?.addEventListener('click', async () => {
  launchOverlay.style.display = 'none';
  await ensureInitialised();
  await xrScene.launchVR();
});

document.getElementById('btn-desktop')?.addEventListener('click', async () => {
  launchOverlay.style.display = 'none';
  await ensureInitialised();
  await xrScene.startDesktop();
});

// ── Desktop / non-XR action buttons ──────────────────────────────────────────
function wireButtons(): void {
  document.getElementById('btn-capture')?.addEventListener('click', () => {
    void xrScene.doCapture();
  });

  document.getElementById('btn-cycle-model')?.addEventListener('click', () => {
    xrScene.cycleModel();
  });

  document.getElementById('btn-cycle-display')?.addEventListener('click', () => {
    xrScene.cycleDisplayMode();
  });

  document.getElementById('btn-cycle-preview')?.addEventListener('click', () => {
    xrScene.cyclePreviewDistance();
  });

  document.getElementById('btn-export')?.addEventListener('click', () => {
    void xrScene.doSaveExport();
  });

  document.getElementById('btn-preview-gltf')?.addEventListener('click', () => {
    void xrScene.previewGltf();
  });

  document.getElementById('btn-load-mapping')?.addEventListener('click', () => {
    fileInputMapping.click();
  });

  document.getElementById('btn-clear-mapping')?.addEventListener('click', () => {
    xrScene.clearMappingLayers();
  });

  fileInputMapping.addEventListener('change', () => {
    const file = fileInputMapping.files?.[0];
    if (!file) return;
    void xrScene.loadMappingFile(file);
    fileInputMapping.value = '';
  });

  // Settings inputs update config in real time
  inputBaseUrl.addEventListener('change', () => {
    config = readConfig();
  });
  inputBoxThresh.addEventListener('change', () => {
    config = readConfig();
  });
  inputTextThresh.addEventListener('change', () => {
    config = readConfig();
  });
}
