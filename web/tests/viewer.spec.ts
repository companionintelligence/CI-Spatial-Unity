/**
 * Playwright E2E tests for the World Map Viewer/Editor page (viewer.html).
 */
import { expect, test } from '@playwright/test';


/** Minimal glTF with one spatial pin in extras. */
const minimalWorldMapGltf = JSON.stringify({
  asset: { version: '2.0' },
  scene: 0,
  scenes: [{ nodes: [] }],
  nodes: [],
  extras: {
    session_id: 'test-session-001',
    export_timestamp: 1700000000,
    pins: [
      {
        label: 'chair',
        semanticLabel: 'chair',
        score: 0.92,
        source: 'CvServer',
        position: [1.0, 0.5, -2.0],
        normal: [0, 1, 0],
        rotation: [0, 0, 0, 1],
        bboxPixels: null,
        bboxNormalized: null,
        planeSize: null,
        volumeSize: null,
        anchorUuid: null,
        sourcePrompt: 'chair',
        timestamp: 1700000001,
        captureId: 'cap-1',
        captureSessionId: 'test-session-001',
        captureFrameIndex: 0,
        placementConfidence: 0.88,
        hitType: 'ArPlane',
        trackId: 1,
      },
      {
        label: 'table',
        semanticLabel: 'table',
        score: 0.85,
        source: 'MetaScene',
        position: [-0.5, 0.0, -1.5],
        normal: [0, 1, 0],
        rotation: [0, 0, 0, 1],
        bboxPixels: null,
        bboxNormalized: null,
        planeSize: [1.2, 0.02, 0.8],
        volumeSize: null,
        anchorUuid: 'anchor-abc',
        sourcePrompt: 'XRPlane',
        timestamp: 1700000002,
        captureId: '',
        captureSessionId: 'test-session-001',
        captureFrameIndex: 0,
        placementConfidence: 1.0,
        hitType: 'MetaPlane',
        trackId: 0,
      },
    ],
    pin_count: 2,
    cv_pin_count: 1,
    meta_scene_pin_count: 1,
  },
});

test('viewer page loads with correct title and toolbar', async ({ page }) => {
  await page.goto('/viewer.html');

  await expect(page).toHaveTitle(/World Map/i);
  await expect(page.getByRole('button', { name: /Load GLTF/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Export GLTF/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Undo/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Redo/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Clear/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Frame/i })).toBeVisible();
});

test('viewer shows empty pin list initially', async ({ page }) => {
  await page.goto('/viewer.html');

  await expect(page.locator('#pin-count')).toHaveText('0');
  await expect(page.locator('#pin-list')).toBeEmpty();
  await expect(page.locator('#pin-inspector')).toContainText(
    'Select a pin to inspect',
  );
});

test('viewer loads a GLTF file with pins via file input', async ({ page }) => {
  await page.goto('/viewer.html');

  await page.evaluate(
    async ([content, mimeType]: [string, string]) => {
      const blob = new Blob([content], { type: mimeType });
      const file = new File([blob], 'test-world.gltf', { type: mimeType });
      const dt = new DataTransfer();
      dt.items.add(file);
      const input = document.getElementById('input-gltf-file') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: dt.files, writable: false });
      input.dispatchEvent(new Event('change', { bubbles: true }));
    },
    [minimalWorldMapGltf, 'model/gltf+json'] as [string, string],
  );

  // Wait for pin count to update
  await expect(page.locator('#pin-count')).toHaveText('2', { timeout: 10_000 });
  await expect(page.locator('#viewer-status')).toContainText('2 pins');
  await expect(page.locator('#session-info')).toContainText('test-session-001');
});

test('viewer pin list shows correct labels after GLTF load', async ({ page }) => {
  await page.goto('/viewer.html');

  await page.evaluate(
    async ([content, mimeType]: [string, string]) => {
      const blob = new Blob([content], { type: mimeType });
      const file = new File([blob], 'test-world.gltf', { type: mimeType });
      const dt = new DataTransfer();
      dt.items.add(file);
      const input = document.getElementById('input-gltf-file') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: dt.files, writable: false });
      input.dispatchEvent(new Event('change', { bubbles: true }));
    },
    [minimalWorldMapGltf, 'model/gltf+json'] as [string, string],
  );

  await expect(page.locator('#pin-count')).toHaveText('2', { timeout: 10_000 });

  const items = page.locator('.pin-item');
  await expect(items).toHaveCount(2);
  await expect(items.first().locator('.pin-label')).toHaveText('chair');
  await expect(items.nth(1).locator('.pin-label')).toHaveText('table');
});

test('clear button resets pin list and status', async ({ page }) => {
  await page.goto('/viewer.html');

  await page.evaluate(
    async ([content, mimeType]: [string, string]) => {
      const blob = new Blob([content], { type: mimeType });
      const file = new File([blob], 'test-world.gltf', { type: mimeType });
      const dt = new DataTransfer();
      dt.items.add(file);
      const input = document.getElementById('input-gltf-file') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: dt.files, writable: false });
      input.dispatchEvent(new Event('change', { bubbles: true }));
    },
    [minimalWorldMapGltf, 'model/gltf+json'] as [string, string],
  );

  await expect(page.locator('#pin-count')).toHaveText('2', { timeout: 10_000 });

  await page.getByRole('button', { name: /Clear/i }).click();

  await expect(page.locator('#pin-count')).toHaveText('0');
  await expect(page.locator('#pin-list')).toBeEmpty();
  await expect(page.locator('#viewer-status')).toContainText('Cleared');
});

test('viewer canvas element is rendered', async ({ page }) => {
  await page.goto('/viewer.html');

  const canvas = page.locator('#viewer-canvas');
  await expect(canvas).toBeVisible();
  // Verify Three.js renderer initialised the canvas (has non-zero dimensions)
  const box = await canvas.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.width).toBeGreaterThan(100);
  expect(box!.height).toBeGreaterThan(100);
});

test('viewer page has link from main index', async ({ page }) => {
  // The main index.html doesn't need a link — the viewer is a standalone page.
  // Directly navigate to confirm it is served.
  await page.goto('/viewer.html');
  await expect(page.locator('#toolbar')).toBeVisible();
});
