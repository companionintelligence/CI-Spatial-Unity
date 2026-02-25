# QA and CI/CD Plan

## Goals

- Expand Unity Test Runner coverage across UI, mapping, and CV server integration.
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

## Artifacts

- Screenshots written to folder set by env var: CompanionScan_TEST_ARTIFACTS
- Default fallback: Application.persistentDataPath/CompanionScanTestArtifacts

## CI/CD Pipeline (GitHub Actions)

- Job A: EditMode + PlayMode unit tests
  - Runs on every PR
  - No external services required

- Job B: Integration tests with cv-server
  - Runs on main or scheduled (nightly)
  - Starts cv-server via docker compose
  - Sets CompanionScan_TEST_SERVER_URL to the local server

## Environment Variables

- CompanionScan_TEST_SERVER_URL
- CompanionScan_TEST_ARTIFACTS

## Manual QA Scene

- XR QA scene is generated at runtime by tests for screenshot capture.
- Optional: add a dedicated Unity scene asset if you want a stable visual baseline.
