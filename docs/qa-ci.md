# QA and CI/CD Plan

## Goals

- Expand Unity Test Runner coverage across UI, mapping, and CV server integration.
- Keep the web client covered with Playwright browser tests for desktop fallback
  and WebXR-adjacent control flow.
- Add visual regression and screenshot artifacts for XR UI and anchors.
- Provide CI pipelines for fast unit tests and optional integration tests.

## Test Tiers

1) EditMode (fast, no Unity runtime)
- Bounding box utilities
- RLE decoding

2) PlayMode (runtime, no external services)
- Controller UI state machine (model, display, preview)
- Controller HUD label binding (auto-discovery)
- BBoxRenderer auto-created preview UI
- Snapshot frame store and frustum visualization
- XR QA scene screenshot (controllers + anchors)
- Meta scene mesh capture (simulator required)

## Simulator Setup (Meta Scene Mesh Tests)

- Enable Meta XR Simulator and ensure `OVRManager` + `OVRSceneManager` are active.
- Load a simulated scene model so mesh anchors exist (Scene Model / Rooms).
- The mesh test will be **Inconclusive** if no scene model data is present.

## CI Skip Guidance

- Mesh simulator tests should be excluded in headless CI runs unless the Meta XR
  Simulator is explicitly launched.
- Use test filters to skip `MetaSceneMeshSimulatorTests` in CI pipelines.

3) PlayMode Integration (requires cv-server)
- CV server connectivity and model endpoints
- Per-image model tests using /test images
- Mapping QA tests: render boxes, simulate anchors

4) Web Browser Automation
- Playwright tests in `web/tests/app.spec.ts`
- Covers desktop startup, HUD state changes, settings persistence, glTF preview,
  export fallback, and mapping overlay import/clear flows

## Artifacts

- Screenshots written to folder set by env var: CompanionScan_TEST_ARTIFACTS
- Default fallback: Application.persistentDataPath/CompanionScanTestArtifacts

## CI/CD Pipeline (GitHub Actions)

Triggers, runners and repository secrets are summarised in the README under
[Continuous integration and releases](../README.md#continuous-integration-and-releases).

- Job A: EditMode + PlayMode unit tests (`unity-tests.yml`, self-hosted GPU runner)
  - Runs on pull requests to `main` that change the Unity project or `test/`,
    and on manual runs; pull requests from forks are skipped
  - No external services required

- Job B: Integration tests with cv-server (`unity-tests.yml`)
  - Manual runs while the push and nightly triggers stay gated (see
    [CI.md](CI.md)); planned for `main` and nightly runs
  - Expects cv-server to run as a service on the runner host and waits for
    `http://localhost:8000/v1/health`, sending the `CV_SERVER_API_TOKEN`
    secret as a bearer token (every cv-server route requires one)
  - Sets CompanionScan_TEST_SERVER_URL to the local server and
    CompanionScan_API_TOKEN to the same token

- Job C: Web client browser tests (`web-tests.yml`)
  - Manual runs
  - Builds the Vite app in `web/`
  - Runs Playwright coverage for the browser client workflows

- Job D: Quest APK build (`quest-build.yml`, GitHub-hosted runner)
  - Manual runs and `v*` tags; a tag also creates a draft GitHub Release
  - Builds the Android APK with GameCI, using the editor version from
    `ProjectSettings/ProjectVersion.txt`
  - Installing the APK on a headset: [SIDELOAD.md](SIDELOAD.md)

## Environment Variables

- CompanionScan_TEST_SERVER_URL
- CompanionScan_API_TOKEN (the cv-server's bearer token)
- CompanionScan_TEST_ARTIFACTS

## Manual QA Scene

- XR QA scene is generated at runtime by tests for screenshot capture.
- Optional: add a dedicated Unity scene asset if you want a stable visual baseline.
