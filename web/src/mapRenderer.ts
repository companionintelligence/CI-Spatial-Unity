import {
  AmbientLight,
  BoxGeometry,
  CanvasTexture,
  Color,
  DirectionalLight,
  Group,
  Mesh,
  MeshBasicMaterial,
  PerspectiveCamera,
  Scene,
  SphereGeometry,
  Sprite,
  SpriteMaterial,
  WebGLRenderer,
} from 'three'
import type { DisplayMode, SpatialPin } from './types'

const COLORS = {
  CvServer: new Color('#ff7a00'),
  MetaScene: new Color('#14c972'),
}

export class MapRenderer {
  readonly renderer: WebGLRenderer
  readonly scene: Scene
  readonly camera: PerspectiveCamera

  private pinGroup = new Group()
  private pinObjects = new Map<string, Group>()
  private displayMode: DisplayMode = 'both'
  private running = false
  private fallbackLoop?: number
  private canvas: HTMLCanvasElement

  constructor(canvas: HTMLCanvasElement) {
    this.canvas = canvas
    this.renderer = new WebGLRenderer({ canvas, alpha: true, antialias: true })
    this.renderer.setSize(canvas.clientWidth, canvas.clientHeight)
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    this.renderer.xr.enabled = true

    this.scene = new Scene()
    this.camera = new PerspectiveCamera(70, canvas.clientWidth / canvas.clientHeight, 0.01, 50)

    const amb = new AmbientLight(0xffffff, 0.4)
    const dir = new DirectionalLight(0xffffff, 0.8)
    dir.position.set(0.4, 1, 0.5)
    this.scene.add(amb, dir, this.pinGroup)

    window.addEventListener('resize', () => this.onResize())
  }

  onResize() {
    const { clientWidth, clientHeight } = this.canvas
    this.camera.aspect = clientWidth / Math.max(clientHeight, 1)
    this.camera.updateProjectionMatrix()
    this.renderer.setSize(clientWidth, clientHeight)
  }

  setDisplayMode(mode: DisplayMode) {
    this.displayMode = mode
    this.pinObjects.forEach((obj) => this.applyDisplayMode(obj))
  }

  start(session?: XRSession, onFrame?: (frame?: XRFrame) => void) {
    if (session) {
      this.renderer.setAnimationLoop((_: number, frame?: XRFrame | null) => {
        onFrame?.(frame || undefined)
        this.renderer.render(this.scene, this.camera)
      })
      this.running = true
      return
    }

    const loop = () => {
      onFrame?.(undefined)
      this.renderer.render(this.scene, this.camera)
      this.fallbackLoop = requestAnimationFrame(loop)
    }
    loop()
    this.running = true
  }

  stop() {
    if (this.running) {
      this.renderer.setAnimationLoop(null)
      if (this.fallbackLoop) cancelAnimationFrame(this.fallbackLoop)
    }
    this.running = false
  }

  clearPins() {
    this.pinObjects.forEach((group) => {
      group.traverse((child) => {
        const mesh = child as Mesh
        if (mesh.geometry) mesh.geometry.dispose()
        if (mesh.material) {
          const mat = mesh.material as MeshBasicMaterial | SpriteMaterial
          mat.dispose()
          if ((mat as SpriteMaterial).map) (mat as SpriteMaterial).map?.dispose()
        }
      })
      this.pinGroup.remove(group)
    })
    this.pinObjects.clear()
  }

  syncPins(pins: SpatialPin[]) {
    const nextIds = new Set(pins.map((p) => p.id))

    // Remove stale
    for (const [id, obj] of this.pinObjects.entries()) {
      if (!nextIds.has(id)) {
        this.pinGroup.remove(obj)
        this.pinObjects.delete(id)
      }
    }

    // Upsert
    pins.forEach((pin) => {
      const existing = this.pinObjects.get(pin.id)
      const color = COLORS[pin.source]
      if (existing) {
        existing.position.set(pin.position[0], pin.position[1], pin.position[2])
        this.updateLabel(existing, pin)
        this.updateBox(existing, pin)
        return
      }
      const group = new Group()
      group.position.set(pin.position[0], pin.position[1], pin.position[2])

      const sphere = new Mesh(new SphereGeometry(0.04), new MeshBasicMaterial({ color }))
      sphere.name = 'pin-sphere'

      const label = this.makeLabel(pin, color)
      label.position.set(0, 0.08, 0)
      label.name = 'pin-label'

      const boxSize = this.boxSizeFromPin(pin)
      const box = new Mesh(
        new BoxGeometry(boxSize[0], boxSize[1], boxSize[2]),
        new MeshBasicMaterial({ color, wireframe: true, transparent: true, opacity: 0.35 }),
      )
      box.name = 'pin-box'

      group.add(sphere, label, box)
      this.applyDisplayMode(group)
      this.pinGroup.add(group)
      this.pinObjects.set(pin.id, group)
    })
  }

  private applyDisplayMode(group: Group) {
    const sphere = group.getObjectByName('pin-sphere')
    const label = group.getObjectByName('pin-label')
    const box = group.getObjectByName('pin-box')

    if (this.displayMode === 'bbox') {
      sphere && (sphere.visible = true)
      label && (label.visible = false)
      box && (box.visible = true)
    } else if (this.displayMode === 'labels') {
      sphere && (sphere.visible = false)
      label && (label.visible = true)
      box && (box.visible = false)
    } else {
      sphere && (sphere.visible = true)
      label && (label.visible = true)
      box && (box.visible = true)
    }
  }

  private makeLabel(pin: SpatialPin, color: Color) {
    const canvas = document.createElement('canvas')
    canvas.width = 256
    canvas.height = 64
    const ctx = canvas.getContext('2d')!
    ctx.fillStyle = 'rgba(0,0,0,0.65)'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
    ctx.fillStyle = '#ffffff'
    ctx.font = '28px sans-serif'
    ctx.fillText(pin.semanticLabel || pin.label, 12, 38)
    ctx.fillStyle = color.getStyle()
    ctx.fillRect(0, 0, 6, canvas.height)

    const texture = new CanvasTexture(canvas)
    texture.needsUpdate = true
    const material = new SpriteMaterial({ map: texture, depthTest: false })
    const sprite = new Sprite(material)
    sprite.scale.set(0.6, 0.15, 1)
    return sprite
  }

  private updateLabel(group: Group, pin: SpatialPin) {
    const label = group.getObjectByName('pin-label') as Sprite | null
    if (!label) return
    const material = label.material as SpriteMaterial
    const map = material.map as CanvasTexture | null
    if (!map) return
    const canvas = map.image as HTMLCanvasElement
    const ctx = canvas.getContext('2d')!
    ctx.clearRect(0, 0, canvas.width, canvas.height)
    ctx.fillStyle = 'rgba(0,0,0,0.65)'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
    ctx.fillStyle = '#ffffff'
    ctx.font = '28px sans-serif'
    ctx.fillText(pin.semanticLabel || pin.label, 12, 38)
    ctx.fillStyle = (COLORS[pin.source] || new Color('#ffffff')).getStyle()
    ctx.fillRect(0, 0, 6, canvas.height)
    map.needsUpdate = true
  }

  private updateBox(group: Group, pin: SpatialPin) {
    const mesh = group.getObjectByName('pin-box') as Mesh | null
    if (!mesh) return
    const size = this.boxSizeFromPin(pin)
    const geometry = mesh.geometry as BoxGeometry

    // Avoid unnecessary work if the geometry already has the desired size
    const { width, height, depth } = geometry.parameters
    if (width === size[0] && height === size[1] && depth === size[2]) {
      return
    }

    // Reset scale to avoid compounding dimensions and recreate geometry at the new size
    mesh.scale.set(1, 1, 1)
    geometry.dispose()
    mesh.geometry = new BoxGeometry(size[0], size[1], size[2])
  }

  private boxSizeFromPin(pin: SpatialPin): [number, number, number] {
    if (pin.planeSize.some((v) => v > 0)) return pin.planeSize
    if (pin.volumeSize.some((v) => v > 0)) return pin.volumeSize
    const width = Math.max(pin.bboxNormalized[2] - pin.bboxNormalized[0], 0.05)
    const height = Math.max(pin.bboxNormalized[3] - pin.bboxNormalized[1], 0.05)
    const depth = 0.2
    return [width, height, depth]
  }
}
