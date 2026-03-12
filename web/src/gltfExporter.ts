import { Scene } from 'three'
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js'

export async function exportSpatialMap(scene: Scene, extras?: object): Promise<{ gltf: Blob; bin?: Blob }> {
  const exporter = new GLTFExporter()
  return new Promise((resolve, reject) => {
    exporter.parse(
      scene,
      (gltf: ArrayBuffer | object) => {
        if (gltf instanceof ArrayBuffer) {
          const blob = new Blob([gltf], { type: 'model/gltf-binary' })
          resolve({ gltf: blob })
          return
        }
        const json = gltf as any
        if (extras) {
          json.extras = extras
        }
        const text = JSON.stringify(json, null, 2)
        resolve({ gltf: new Blob([text], { type: 'application/json' }) })
      },
      (err: unknown) => reject(err),
      { binary: false, embedImages: true },
    )
  })
}
