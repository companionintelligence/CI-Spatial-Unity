# Device testing

Which devices can run the scanner today, how to get a build onto them, what to check, and what to
bring back. Nothing below has been run on a device by whoever wrote the code: the checks say what to
look for, and the tests that ran without a headset are listed at the end.

## Status

| Device | Can it run the scanner today? | What stands in the way |
|---|---|---|
| Meta Quest 3 | **Yes.** A debug APK builds and its manifest checks out. | A CV server for the vision part. Reading Space Setup needs none. |
| Android tablet | No | No phone scene and no touch controls, and no ARCore package in the project. |
| iPhone or iPad | No | The same, with ARKit, an Xcode project and a Mac to build it. |
| XREAL Aura | No | An Android XR build profile and a way to get camera frames (below). |
| PICO 4 Pro | No | The PICO SDK builds only on the 6000.3 editor, and whether the device hands an app its camera frames is not established. |

The code is split so that those backends can be added without touching the pipeline: `CompanionScan.Core`
has no Meta SDK in it, `CompanionScan.Quest` holds the Quest backend, and camera, scene, raycast and input
are seams a new backend implements (`docs/quest-capture-gltf.md`, "Assemblies and Backends"). What each
missing backend needs is in the sections below.

## Quest 3

### Get the build on the headset

`CompanionScan → Build Quest APK` in the editor, or the Quest APK Build workflow, gives
`CompanionScan.apk`. `docs/SIDELOAD.md` has the install steps for Meta Quest Developer Hub and for `adb`.
The package is `com.CompanionIntelligence.CompanionCapture`, the library name `Companion Scan`. The first
launch asks for the headset camera and the scene (spatial data) permissions: grant both.

### Have a CV server ready

Capture sends camera snapshots to a CV server (`cv-server/`), so the headset must reach one.

- Enter its URL with the left Menu button, then **A** (edit URL), and its API token with **X**; **B** saves.
  Every route needs the token.
- A release build only talks to `https://`. A debug build may use `http://` to a host that was listed in
  `ServerConfig` under Development Cleartext Hosts **when the APK was built**; on Android that list is also
  written into the network security config. To try a server on the LAN over `http://`, list its name and
  build again, or put the server behind `https://`.
- Without a server you can still check Space Setup, the controls and the export.

### What to check

| # | Do this | You should see | It covers |
|---|---|---|---|
| 1 | Start the app. | The wrist HUD on the left, no crash, and the app answers the controllers. | the split into `Core` and `Quest` assemblies, `ControllerInput` |
| 2 | Run Space Setup in the headset first. Capture with **A**. | Pins for the floor, ceiling and walls, named by Meta's labels. | the Space Setup read through `ISceneSource` |
| 3 | First capture with a server set. | The camera notice appears before anything is sent. **A** agrees and captures, **B** sends nothing. | consent |
| 4 | Point at a chair 2 to 3 m away and capture. | The pin is on the chair. A pin at the middle of the room or at your feet is the old bug (every capture was stamped with the world origin); report it with the log. | the capture pose |
| 5 | Capture the same object again from another side, more than 5 s later. | Still one pin for it, which has moved a little. | the tracker (one object across captures) |
| 6 | Capture a sofa, a TV or a picture frame. | The export names them `COUCH`, `SCREEN` and `WALL_ART` in `semantic_label`. | the tag vocabulary |
| 7 | Save and export (**Y**) twice with no new captures, pull the files (below), open the glTF in a viewer. | The floor is visible from above, each pin is listed once and no member is `null`. | the export merge, the floor plan |
| 8 | Menu, then a grip: withdraw the agreement. Capture again. | The notice comes back and nothing reaches the server (check its log). | consent |
| 9 | Stop the server and capture. | An error in the log, and the app stays responsive. | failure handling |

Pull what the export wrote (the folder is named after the Space Setup room):

```bash
adb pull /sdcard/Android/data/com.CompanionIntelligence.CompanionCapture/files/exports ./exports
```

### What to bring back

- `adb logcat -d -s Unity > quest.log` right after the run. Tags to look for: `[Pipeline-CV]`,
  `[ControllerInput]`, `[Snapshots]`, `[Export]`, `[Raycaster]`.
- The `exports` folder, and a short screen recording of steps 4 and 5.
- The headset OS version, the commit the APK was built from, and the CV server's version.

## Android tablet and iPhone or iPad

The scanner's phone path is in the code (`MobileCameraProvider` reads the camera through AR Foundation, and
`ViewportRaycaster` raycasts AR planes), but the project has no phone scene and no ARCore or ARKit package,
so there is nothing to install yet. The work, in order:

1. Add `com.unity.xr.arcore` and `com.unity.xr.arkit` at 6.6.2, to match AR Foundation 6.6.2, which the
   project already resolves.
2. Limit `CompanionScan.Quest` to the Android, Editor and Windows platforms so an iOS build never compiles
   the Meta SDK, and check that `Core` compiles for iOS.
3. A phone scene: an AR rig (AR Session, a camera with `ARCameraManager`, `ARPlaneManager`,
   `ARRaycastManager`), the pipeline, pins and exporter, and the server and consent panels on a screen
   canvas.
4. `TouchControllerInput`, a `ControllerInput` backend with on-screen buttons for A, B, X, Y, Menu, trigger
   and grip.
5. Platform settings: the iOS camera usage description (empty today), Android camera permission and an ARCore
   "required" manifest entry, an Xcode project export, and a separate Android loader setup from Quest's.
6. A debug APK for the tablet, and an Xcode project for the Mac.

To test on these you will need: the tablet or iPad model and OS version (ARCore must support the device), a
Mac with Xcode for iOS, and an Apple team and provisioning profile of yours. A device without LiDAR still
works for the camera and AR planes; it has no scene mesh.

A stopgap for the tablet while that is built: the web scanner in `web/` is a WebXR app, and Chrome on an ARCore
device may offer it an `immersive-ar` session with camera access. `web/README.md` and `web/vite.config.ts` show
how to serve it over HTTPS with the token added by the dev server's proxy. Not tried on a tablet. Safari on iOS
has no WebXR AR, so there is no such route for the iPhone or iPad.

## XREAL Aura

Unity 6.5 or newer supports it through the Android XR build profile and the OpenXR: Android XR package (one
APK can serve Galaxy XR and Aura). The project is on 6000.6.9f1 and has no Android XR package yet; the registry
has `com.unity.xr.androidxr-openxr` 1.5.1. The open question is the camera. AR Foundation's Android XR provider
does not give the app the camera image, so frames would come from Camera2 with a calibration for the camera's
pose and lens, and I have not found documentation that says an app may open the Aura's camera at all. Establish
that first. The location comes from the puck.

## PICO 4 Pro

The PICO OpenXR SDK fails to compile on 6000.6, so a PICO build is a variant on the 6000.3.19f1 baseline, and
the SDK's licence does not allow committing it to the repository. PICO's SDK lists spatial anchors, spatial mesh
and scene capture for the PICO 4 series; I found nothing that says the PICO 4 Pro gives an app its camera frames
(PICO's camera sample targets its enterprise devices), and the README of the sibling Avatar app says consumer
PICO exposes no per-app RGB frames, so there live-camera scanning falls back to pins from scene semantics. Assume
the same here: without frames there is no vision capture, only scene data. Check PICO's developer documentation
for the device, and for the Camera Pack, before building anything. The Avatar app already has a PICO build route
(its `docs/BUILD.md`), and it falls back to pins from scene semantics where the device gives no camera frames.

## Tests that need no device

These run on a PC and cover everything except the hardware:

```powershell
unity test <project> --mode EditMode --non-interactive
unity test <project> --mode PlayMode --non-interactive
```

`ReplayPipelineTests` run the whole capture pipeline from a recorded room against a server on a loopback port,
and check where the pins land. Run them before a device session: a failure there is cheaper to find than on a
headset.

Sources for the Aura and PICO notes:
[Unity on XREAL AURA](https://unity.com/blog/unity-android-xr-wired-glasses-support),
[PICO developer site](https://developer.picoxr.com/pico4-ultra/),
[PICO Unity Integration SDK releases](https://github.com/Pico-Developer/PICO-Unity-Integration-SDK/releases).
