import type { CaptureFrame } from './types'

export class CameraCapture {
  private video: HTMLVideoElement
  private canvas: HTMLCanvasElement
  private ctx: CanvasRenderingContext2D
  private stream?: MediaStream

  constructor(video: HTMLVideoElement) {
    this.video = video
    this.canvas = document.createElement('canvas')
    const ctx = this.canvas.getContext('2d')
    if (!ctx) {
      throw new Error('Unable to create capture canvas context')
    }
    this.ctx = ctx
  }

  async start(): Promise<void> {
    if (this.stream) return
    this.stream = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: 'environment',
        width: { ideal: 1280 },
        height: { ideal: 720 },
      },
      audio: false,
    })
    this.video.srcObject = this.stream
    await this.video.play()
  }

  stop(): void {
    this.stream?.getTracks().forEach((t) => t.stop())
    this.stream = undefined
    this.video.srcObject = null
  }

  async captureFrame(): Promise<CaptureFrame> {
    if (!this.video.videoWidth || !this.video.videoHeight) {
      throw new Error('Camera not ready')
    }
    this.canvas.width = this.video.videoWidth
    this.canvas.height = this.video.videoHeight
    this.ctx.drawImage(this.video, 0, 0)
    const dataUrl = this.canvas.toDataURL('image/jpeg', 0.9)
    return {
      dataUrl,
      width: this.canvas.width,
      height: this.canvas.height,
      timestamp: performance.now(),
    }
  }
}
