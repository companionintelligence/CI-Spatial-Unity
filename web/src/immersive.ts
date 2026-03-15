import { mat4, quat, vec3, vec4 } from 'gl-matrix'
import type { MetaSceneAnchor } from './types'
import { WebGLRenderer } from 'three'

const REQUIRED_FEATURES = ['local-floor']
const OPTIONAL_FEATURES = ['hit-test', 'plane-detection', 'anchors', 'dom-overlay']

export class XRCoordinator {
  session?: XRSession
  referenceSpace?: XRReferenceSpace
  lastFrame?: XRFrame
  lastPose?: XRViewerPose

  private renderer: WebGLRenderer
  private overlayRoot?: HTMLElement

  constructor(renderer: WebGLRenderer, overlayRoot?: HTMLElement) {
    this.renderer = renderer
    this.overlayRoot = overlayRoot
  }

  async startSession(): Promise<XRSession> {
    if (!navigator.xr) {
      throw new Error('WebXR not available in this browser')
    }
    const session = await navigator.xr.requestSession('immersive-ar', {
      requiredFeatures: REQUIRED_FEATURES,
      optionalFeatures: OPTIONAL_FEATURES,
      domOverlay: this.overlayRoot ? { root: this.overlayRoot } : undefined,
    } as any)

    this.renderer.xr.enabled = true
    this.renderer.xr.setReferenceSpaceType('local-floor')
    await this.renderer.xr.setSession(session)
    this.referenceSpace = await session.requestReferenceSpace('local-floor')
    this.session = session
    session.addEventListener('end', () => {
      this.session = undefined
      this.referenceSpace = undefined
      this.lastFrame = undefined
      this.lastPose = undefined
    })
    return session
  }

  endSession() {
    this.session?.end()
  }

  handleFrame(frame?: XRFrame) {
    if (!frame || !this.referenceSpace) return
    this.lastFrame = frame
    this.lastPose = frame.getViewerPose(this.referenceSpace) || undefined
  }

  projectFromUv(uv: { x: number; y: number }, distance: number) {
    if (!this.lastPose) return undefined
    const view = this.lastPose.views[0]
    const proj = view.projectionMatrix as Float32Array
    const invProj = mat4.invert(mat4.create(), proj)
    if (!invProj) return undefined

    const clip = vec4.fromValues(uv.x * 2 - 1, (1 - uv.y) * 2 - 1, 1, 1)
    vec4.transformMat4(clip, clip, invProj)
    // Perform perspective divide to get view-space coordinates
    if (clip[3] !== 0) {
      clip[0] /= clip[3]
      clip[1] /= clip[3]
      clip[2] /= clip[3]
    }
    const direction = vec3.fromValues(clip[0], clip[1], clip[2])
    vec3.normalize(direction, direction)

    const o = view.transform.orientation
    const orientation = quat.fromValues(o.x, o.y, o.z, o.w)
    vec3.transformQuat(direction, direction, orientation)

    const p = view.transform.position
    const origin = vec3.fromValues(p.x, p.y, p.z)
    vec3.scaleAndAdd(origin, origin, direction, distance)

    return {
      position: [origin[0], origin[1], origin[2]] as [number, number, number],
      normal: [direction[0], direction[1], direction[2]] as [number, number, number],
    }
  }

  collectPlanes(): MetaSceneAnchor[] {
    if (!this.lastFrame || !this.referenceSpace) return []
    const planes: MetaSceneAnchor[] = []
    const worldInfo: any = (this.lastFrame as any).worldInformation
    const detectedPlanes: Iterable<any> | undefined = worldInfo?.detectedPlanes
    if (!detectedPlanes) return []

    for (const plane of detectedPlanes as any) {
      const pose = this.lastFrame.getPose(plane.planeSpace || plane, this.referenceSpace)
      if (!pose) continue
      const rot = pose.transform.orientation
      const pos = pose.transform.position
      const polygon = Array.from(plane.polygon || [], (pt: DOMPointReadOnly) => [pt.x, pt.y, pt.z] as [number, number, number])
      const bounds = polygon.reduce(
        (acc, pt) => {
          acc.min[0] = Math.min(acc.min[0], pt[0])
          acc.min[1] = Math.min(acc.min[1], pt[1])
          acc.min[2] = Math.min(acc.min[2], pt[2])
          acc.max[0] = Math.max(acc.max[0], pt[0])
          acc.max[1] = Math.max(acc.max[1], pt[1])
          acc.max[2] = Math.max(acc.max[2], pt[2])
          return acc
        },
        {
          min: [Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY],
          max: [Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY],
        },
      )
      const size: [number, number, number] = [
        Math.max(bounds.max[0] - bounds.min[0], 0),
        Math.max(bounds.max[1] - bounds.min[1], 0),
        Math.max(bounds.max[2] - bounds.min[2], 0),
      ]

      planes.push({
        uuid: plane.uuid || plane.uid || crypto.randomUUID(),
        center: [pos.x, pos.y, pos.z],
        rotation: [rot.x, rot.y, rot.z, rot.w],
        size,
        polygon,
      })
    }
    return planes
  }
}
