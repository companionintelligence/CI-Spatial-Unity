import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';

const minimalMappingGltf = JSON.stringify({
  asset: { version: '2.0' },
  scene: 0,
  scenes: [{ nodes: [] }],
  nodes: [],
});

test.beforeEach(async ({ page }) => {
  await page.route('**/v1/florence', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        results: {
          bboxes: [[0.2, 0.2, 0.45, 0.45]],
          labels: ['chair'],
        },
      }),
    });
  });
});

async function startDesktop(page: Page): Promise<void> {
  await page.goto('/');
  await page.getByRole('button', { name: /Start Desktop/i }).click();
  await expect(page.locator('#launch-overlay')).toBeHidden();
  await expect(page.locator('#hud-status')).toHaveText('Desktop mode active');
}

test('desktop mode can be launched without immersive XR', async ({ page }) => {
  await startDesktop(page);

  await expect(page.getByRole('button', { name: /Capture/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Preview glTF/i })).toBeVisible();
  await expect(page.locator('#hud-model')).toHaveText('Florence');
  await expect(page.locator('#hud-display')).toHaveText('Both');
  await expect(page.locator('#hud-preview')).toHaveText('Near (1m)');
});

test('desktop controls cycle Unity-style HUD state', async ({ page }) => {
  await startDesktop(page);

  await page.getByRole('button', { name: /Cycle Model/i }).click();
  await expect(page.locator('#hud-model')).toHaveText('GDino');
  await page.getByRole('button', { name: /Cycle Model/i }).click();
  await expect(page.locator('#hud-model')).toHaveText('SAM2');

  await page.getByRole('button', { name: /Display/i }).click();
  await expect(page.locator('#hud-display')).toHaveText('BoundingBox2D');
  await page.getByRole('button', { name: /Preview Dist/i }).click();
  await expect(page.locator('#hud-preview')).toHaveText('Far (3m)');
  await page.getByRole('button', { name: /Preview Dist/i }).click();
  await expect(page.locator('#hud-preview')).toHaveText('Off');
});

test('settings changes are persisted and used for GDino capture requests', async ({ page }) => {
  const requests: Array<Record<string, unknown>> = [];
  await page.route('http://example.test:9999/v1/detect', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    requests.push(body);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        bboxes: [[0.1, 0.1, 0.3, 0.3]],
        labels: ['table'],
        scores: [0.9],
      }),
    });
  });

  await startDesktop(page);
  await page.getByRole('button', { name: /Cycle Model/i }).click();
  await expect(page.locator('#hud-model')).toHaveText('GDino');
  await page.getByLabel('CV Server Base URL').fill('http://example.test:9999');
  await page.getByLabel('CV Server Base URL').dispatchEvent('change');
  await page.getByLabel('Box Threshold').fill('0');
  await page.getByLabel('Box Threshold').dispatchEvent('change');
  await page.getByLabel('Text Threshold').fill('0');
  await page.getByLabel('Text Threshold').dispatchEvent('change');

  await page.getByRole('button', { name: /Capture/i }).click();
  await expect.poll(() => requests.length).toBe(1);
  await expect(page.locator('#hud-status')).toHaveText('Captures: 1');

  expect(requests[0]).toMatchObject({
    prompt: '.',
    box_threshold: 0,
    text_threshold: 0,
  });
  expect(requests[0].image).toEqual(expect.any(String));
  expect((requests[0].image as string).length).toBeGreaterThan(10);

  await page.reload();
  await expect(page.getByLabel('CV Server Base URL')).toHaveValue('http://example.test:9999');
});

test('captured pins appear in glTF preview metadata', async ({ page }) => {
  await startDesktop(page);

  await page.getByRole('button', { name: /Capture/i }).click();
  await expect(page.locator('#hud-status')).toHaveText('Captures: 1');

  await page.getByRole('button', { name: /Preview glTF/i }).click();
  await expect(page.locator('#gltf-preview')).toHaveClass(/visible/);
  await expect(page.locator('#gltf-preview-content')).toContainText('"pin_count": 1');
  await expect(page.locator('#gltf-preview-content')).toContainText('"label": "chair"');
});

test('export falls back to browser download when glTF upload fails', async ({ page }) => {
  await page.route('**/v1/gltf', async (route) => {
    await route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'upload failed' }),
    });
  });

  await startDesktop(page);
  await page.getByRole('button', { name: /Capture/i }).click();
  await expect(page.locator('#hud-status')).toHaveText('Captures: 1');

  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: /Export glTF/i }).click();
  const download = await downloadPromise;

  expect(download.suggestedFilename()).toMatch(/^scan_\d+\.gltf$/);
  await expect(page.locator('#hud-status')).toHaveText('Exported (downloaded)');
});

test('mapping overlays can be loaded and cleared from the desktop UI', async ({ page }) => {
  await startDesktop(page);

  await page.locator('#input-mapping-file').setInputFiles({
    name: 'mapping.gltf',
    mimeType: 'model/gltf+json',
    buffer: Buffer.from(minimalMappingGltf, 'utf8'),
  });

  await expect(page.locator('#hud-status')).toHaveText('Loaded mapping overlay');
  await page.getByRole('button', { name: /Clear Mapping/i }).click();
  await expect(page.locator('#hud-status')).toHaveText('Cleared mapping overlays');
});
