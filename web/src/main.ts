import './style.css'
import { CameraCapture } from './camera'
import { CVClient, buildPrompt, dataUrlToBase64, pinsToExtras } from './cvClient'
import { XRCoordinator } from './immersive'
import { MapRenderer } from './mapRenderer'
import { exportSpatialMap } from './gltfExporter'
import type { CaptureState, DisplayMode, ModelPreset, SpatialPin } from './types'

const app = document.querySelector<HTMLDivElement>('#app')!
app.innerHTML = `
  <div class="page">
    <header class="hero">
      <div>
        <p class="eyebrow">Companion Scan — Web</p>
        <h1>Immersive Web SDK client</h1>
        <p class="lede">Capture the scene, run Florence → GDino → SAM2 via the CV server, and export a unified glTF map from the browser.</p>
      </div>
      <div class="badges" id="state-badges"></div>
    </header>

    <section class="grid">
      <div class="viewport">
        <canvas id="xr-canvas"></canvas>
        <video id="camera-preview" playsinline muted></video>
        <div class="hud" id="hud"></div>
      </div>

      <div class="panel">
        <div class="row">
          <label>CV Server</label>
          <input id="server-url" type="text" value="http://localhost:8000" />
        </div>
        <div class="controls">
          <button id="start">Start immersive session</button>
          <button id="capture">Capture</button>
          <button id="export">Save + upload glTF</button>
          <button id="clear">Clear pins</button>
        </div>
        <div class="controls secondary">
          <button id="model">Cycle model</button>
          <button id="display">Cycle display</button>
          <button id="preview">Cycle preview distance</button>
        </div>
        <div class="status" id="status"></div>
        <div class="list" id="pin-list"></div>
        <div class="log" id="log"></div>
      </div>
    </section>
  </div>
`

const canvas = document.querySelector<HTMLCanvasElement>('#xr-canvas')!
const previewVideo = document.querySelector<HTMLVideoElement>('#camera-preview')!
const serverInput = document.querySelector<HTMLInputElement>('#server-url')!
const logEl = document.querySelector<HTMLDivElement>('#log')!
const statusEl = document.querySelector<HTMLDivElement>('#status')!
const pinListEl = document.querySelector<HTMLDivElement>('#pin-list')!
const badgesEl = document.querySelector<HTMLDivElement>('#state-badges')!
const hudEl = document.querySelector<HTMLDivElement>('#hud')!

const renderer = new MapRenderer(canvas)
const xr = new XRCoordinator(renderer.renderer, document.body)
const camera = new CameraCapture(previewVideo)
const cvClient = new CVClient(serverInput.value.trim())

let cvPins: SpatialPin[] = []
let metaPins: SpatialPin[] = []
let captionText = ''

const PIN_SYNC_INTERVAL_MS = 250
let lastPinSyncTime = 0

const state: CaptureState = {
  model: 'Florence',
  displayMode: 'both',
  previewDistance: 1,
  captureSessionId: crypto.randomUUID(),
  frameIndex: 0,
}

function log(message: string) {
  const time = new Date().toLocaleTimeString()
  const entry = document.createElement('div')
  entry.textContent = `[${time}] ${message}`
  logEl.prepend(entry)
}

function setStatus(message: string) {
  statusEl.textContent = message
}

function combinedPins() {
  return [...metaPins, ...cvPins]
}

function renderPins() {
  renderer.setDisplayMode(state.displayMode)
  renderer.syncPins(combinedPins())

  // Clear existing pin list content
  while (pinListEl.firstChild) {
    pinListEl.removeChild(pinListEl.firstChild)
  }

  // Safely render pins using DOM APIs to avoid XSS from label content
  combinedPins().forEach((p) => {
    const rowEl = document.createElement('div')
    rowEl.className = 'pin-row'

    const pillEl = document.createElement('div')
    pillEl.className = `pill ${p.source === 'CvServer' ? 'cv' : 'meta'}`
    pillEl.textContent = String(p.source)

    const infoContainerEl = document.createElement('div')

    const labelEl = document.createElement('div')
    labelEl.className = 'pin-label'
    labelEl.textContent = p.semanticLabel || p.label

    const metaEl = document.createElement('div')
    metaEl.className = 'pin-meta'
    metaEl.textContent = p.position.map((n) => n.toFixed(2)).join(', ')

    infoContainerEl.appendChild(labelEl)
    infoContainerEl.appendChild(metaEl)

    rowEl.appendChild(pillEl)
    rowEl.appendChild(infoContainerEl)

    pinListEl.appendChild(rowEl)
  })
}

function updateBadges() {
  badgesEl.innerHTML = `
    <div class="pill">Model: ${state.model}</div>
    <div class="pill">Display: ${state.displayMode}</div>
    <div class="pill">Preview: ${state.previewDistance === 0 ? 'Off' : `${state.previewDistance}m`}</div>
    <div class="pill">Pins: ${combinedPins().length}</div>
  `
}

async function startSession() {
  try {
    setStatus('Starting camera…')
    await camera.start()
    setStatus('Starting immersive session…')
    const session = await xr.startSession()
    renderer.start(session, (frame) => {
      xr.handleFrame(frame)
      const now = performance.now()
      if (now - lastPinSyncTime >= PIN_SYNC_INTERVAL_MS) {
        const anchors = xr.collectPlanes()
        metaPins = anchors.map((a) => anchorToPin(a))
        renderPins()
        updateHud()
        lastPinSyncTime = now
      }
    })
    setStatus('Session ready — capture when ready')
    log('Immersive session started')
  } catch (err: any) {
    setStatus(err?.message || 'Failed to start session')
    log(err?.message || 'Failed to start session')
  }
}

function anchorToPin(anchor: { uuid: string; center: [number, number, number]; rotation: [number, number, number, number]; size: [number, number, number] }): SpatialPin {
  return {
    id: `meta-${anchor.uuid}`,
    label: 'PLANE',
    semanticLabel: 'PLANE',
    score: 1,
    source: 'MetaScene',
    position: anchor.center,
    normal: [0, 1, 0],
    rotation: anchor.rotation,
    bboxPixels: [0, 0, 0, 0],
    bboxNormalized: [0, 0, 0, 0],
    planeSize: anchor.size,
    volumeSize: [0, 0, 0],
    anchorUuid: anchor.uuid,
    timestamp: performance.now(),
  }
}

async function capture() {
  try {
    setStatus('Capturing frame…')
    const frame = await camera.captureFrame()
    const imageBase64 = dataUrlToBase64(frame.dataUrl)

    setStatus(`Captioning with model preset: ${state.model}…`)
    const florence = await cvClient.runFlorence(imageBase64)
    captionText = florence.caption || captionText

    const prompt = buildPrompt(florence.labels || [], 'object . room . floor . wall . table . chair .')
    setStatus(`Running detection with model preset: ${state.model}…`)
    const detections = await cvClient.detect(imageBase64, prompt)
    const pins = detections.bboxes.map((bbox, i) => detectionToPin(bbox, detections.labels[i], detections.scores[i], frame, prompt))

    cvPins = [...cvPins, ...pins]
    state.frameIndex += 1
    renderPins()
    setStatus(`Captured ${pins.length} objects`)
    log(`Capture ${state.frameIndex}: ${pins.length} detections`)
  } catch (err: any) {
    setStatus(err?.message || 'Capture failed')
    log(err?.message || 'Capture failed')
  }
}

function detectionToPin(bbox: number[], rawLabel: string, score: number, frame: { width: number; height: number; timestamp: number }, prompt: string): SpatialPin {
  const [x1, y1, x2, y2] = bbox
  const uv = { x: (x1 + x2) / (2 * frame.width), y: (y1 + y2) / (2 * frame.height) }
  const rawProjection = xr.projectFromUv(uv, state.previewDistance || 1)
  const projection = rawProjection || {
    position: [0, 0, -state.previewDistance || -1] as [number, number, number],
    normal: [0, 0, -1] as [number, number, number],
  }

  const norm: [number, number, number, number] = [
    x1 / frame.width,
    y1 / frame.height,
    x2 / frame.width,
    y2 / frame.height,
  ]

  return {
    id: `cv-${state.frameIndex}-${rawLabel}-${Math.random().toString(16).slice(2)}`,
    label: rawLabel,
    semanticLabel: rawLabel.toUpperCase(),
    score,
    source: 'CvServer',
    position: projection.position,
    normal: projection.normal,
    rotation: [0, 0, 0, 1],
    bboxPixels: [x1, y1, x2, y2],
    bboxNormalized: norm,
    planeSize: [0, 0, 0],
    volumeSize: [0, 0, 0],
    timestamp: frame.timestamp,
    captureId: crypto.randomUUID(),
    captureSessionId: state.captureSessionId,
    captureFrameIndex: state.frameIndex,
    sourcePrompt: prompt,
    placementConfidence: score,
    hitType: rawProjection ? 'projected' : 'fallback',
  }
}

async function exportGltf() {
  try {
    const extras = pinsToExtras(combinedPins(), captionText)
    const { gltf } = await exportSpatialMap(renderer.scene, extras)
    setStatus('Uploading glTF…')
    const result = await cvClient.uploadGltf(gltf, extras)
    setStatus('Upload complete')
    log(`glTF uploaded: ${result.gltf_path || 'server saved'}`)
  } catch (err: any) {
    setStatus(err?.message || 'Export failed')
    log(err?.message || 'Export failed')
  }
}

function clearPins() {
  cvPins = []
  metaPins = []
  renderer.clearPins()
  renderPins()
  log('Pins cleared')
}

function cycleModel() {
  const order: ModelPreset[] = ['Florence', 'GDino', 'SAM2']
  const idx = order.indexOf(state.model)
  state.model = order[(idx + 1) % order.length]
  updateBadges()
}

function cycleDisplay() {
  const order: DisplayMode[] = ['bbox', 'labels', 'both']
  const idx = order.indexOf(state.displayMode)
  state.displayMode = order[(idx + 1) % order.length]
  renderPins()
  updateBadges()
}

function cyclePreview() {
  const options = [1, 3, 0]
  const idx = options.indexOf(state.previewDistance)
  state.previewDistance = options[(idx + 1) % options.length]
  updateBadges()
}

function updateHud() {
  const pose = xr.lastPose
  const position = pose?.transform?.position
  hudEl.textContent = pose
    ? `Viewer: ${position?.x.toFixed(2)}, ${position?.y.toFixed(2)}, ${position?.z.toFixed(2)}`
    : 'Viewer: n/a'
}

function wireUi() {
  document.querySelector<HTMLButtonElement>('#start')?.addEventListener('click', startSession)
  document.querySelector<HTMLButtonElement>('#capture')?.addEventListener('click', capture)
  document.querySelector<HTMLButtonElement>('#export')?.addEventListener('click', exportGltf)
  document.querySelector<HTMLButtonElement>('#clear')?.addEventListener('click', clearPins)
  document.querySelector<HTMLButtonElement>('#model')?.addEventListener('click', cycleModel)
  document.querySelector<HTMLButtonElement>('#display')?.addEventListener('click', cycleDisplay)
  document.querySelector<HTMLButtonElement>('#preview')?.addEventListener('click', cyclePreview)
  serverInput.addEventListener('change', () => cvClient.setBaseUrl(serverInput.value.trim()))
}

wireUi()
updateBadges()
setStatus('Ready. Start session to begin capturing.')
