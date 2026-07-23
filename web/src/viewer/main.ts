/**
 * Entry point for the World Map Viewer/Editor page (viewer.html).
 *
 * Wires the WorldMapEditor to all DOM controls:
 * - Toolbar: Load / Export / Clear / Undo / Redo
 * - Scene panel: collapsible pin list with source colour dots
 * - Inspector panel: editable pin fields (label, score, confidence)
 * - Drag-and-drop GLTF onto the canvas
 * - Keyboard shortcuts: Ctrl+Z (undo), Ctrl+Y / Ctrl+Shift+Z (redo)
 */

import { WorldMapEditor } from './WorldMapEditor.js';
import type { SpatialPin } from '../mapping/SpatialPin.js';

let editor: WorldMapEditor | null = null;

// ── DOM references ─────────────────────────────────────────────────────────

function q<T extends HTMLElement>(selector: string): T {
  const el = document.querySelector<T>(selector);
  if (!el) throw new Error(`Element not found: ${selector}`);
  return el;
}

function init(): void {
  const canvas = q<HTMLCanvasElement>('#viewer-canvas');
  editor = new WorldMapEditor(canvas);

  const fileInput = q<HTMLInputElement>('#input-gltf-file');
  const statusEl = q<HTMLElement>('#viewer-status');
  const pinListEl = q<HTMLElement>('#pin-list');
  const pinCountEl = q<HTMLElement>('#pin-count');
  const inspectorEl = q<HTMLElement>('#pin-inspector');
  const sessionInfoEl = q<HTMLElement>('#session-info');

  // ── Toolbar buttons ──────────────────────────────────────────────────────

  q('#btn-load').addEventListener('click', () => fileInput.click());

  q('#btn-export').addEventListener('click', async () => {
    if (!editor) return;
    setStatus('Exporting…', 'info');
    try {
      const blob = await editor.exportGltf();
      triggerDownload(blob, `world_map_${Date.now()}.gltf`);
      setStatus('Exported ✓', 'ok');
    } catch (err) {
      setStatus(`Export failed: ${String(err)}`, 'error');
    }
  });

  q('#btn-clear').addEventListener('click', () => {
    editor?.clear();
    renderPinList([]);
    renderInspector(null);
    sessionInfoEl.textContent = '';
    setStatus('Cleared', 'info');
  });

  q('#btn-undo').addEventListener('click', () => {
    editor?.undo();
  });

  q('#btn-redo').addEventListener('click', () => {
    editor?.redo();
  });

  q('#btn-frame').addEventListener('click', () => {
    editor?.renderer.frameAll();
  });

  // ── File input ───────────────────────────────────────────────────────────

  fileInput.addEventListener('change', async () => {
    const file = fileInput.files?.[0];
    if (!file || !editor) return;
    await handleFileLoad(file);
    fileInput.value = '';
  });

  // ── Drag and drop onto canvas ────────────────────────────────────────────

  const canvasContainer = q<HTMLElement>('#canvas-container');
  canvasContainer.addEventListener('dragover', (e) => {
    e.preventDefault();
    canvasContainer.classList.add('drag-over');
  });
  canvasContainer.addEventListener('dragleave', () => {
    canvasContainer.classList.remove('drag-over');
  });
  canvasContainer.addEventListener('drop', async (e) => {
    e.preventDefault();
    canvasContainer.classList.remove('drag-over');
    const file = e.dataTransfer?.files[0];
    if (!file || !editor) return;
    await handleFileLoad(file);
  });

  // ── Keyboard shortcuts ───────────────────────────────────────────────────

  document.addEventListener('keydown', (e) => {
    if (e.target instanceof HTMLInputElement) return; // don't intercept form inputs
    if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
      e.preventDefault();
      editor?.undo();
    } else if (
      (e.ctrlKey || e.metaKey) &&
      (e.key === 'y' || (e.key === 'z' && e.shiftKey))
    ) {
      e.preventDefault();
      editor?.redo();
    } else if (e.key === 'Delete' || e.key === 'Backspace') {
      if (editor?.store.selectedId) {
        editor.deleteSelectedPin();
        renderInspector(null);
      }
    }
  });

  // ── Store event handlers ─────────────────────────────────────────────────

  editor.store.addEventListener('change', () => {
    if (!editor) return;
    renderPinList(editor.store.allPins);
  });

  editor.store.addEventListener('selection-change', () => {
    if (!editor) return;
    const id = editor.store.selectedId;
    const node = id ? editor.store.allNodes.get(id) ?? null : null;
    renderInspector(node);
    highlightListItem(id);
  });

  // ── Helpers ──────────────────────────────────────────────────────────────

  async function handleFileLoad(file: File): Promise<void> {
    if (!editor) return;
    setStatus(`Loading ${file.name}…`, 'info');
    try {
      const summary = await editor.loadFile(file);
      setStatus(
        `${summary.filename} — ${summary.pinCount} pin${summary.pinCount !== 1 ? 's' : ''}`,
        'ok',
      );
      sessionInfoEl.textContent = `Session: ${summary.sessionId}`;
    } catch (err) {
      setStatus(`Load failed: ${String(err)}`, 'error');
    }
  }

  function setStatus(msg: string, kind: 'ok' | 'error' | 'info'): void {
    statusEl.textContent = msg;
    statusEl.className = `viewer-status viewer-status--${kind}`;
  }

  function renderPinList(
    pins: Array<{ id: string; pin?: SpatialPin; visible: boolean }>,
  ): void {
    pinCountEl.textContent = String(pins.length);
    pinListEl.innerHTML = '';
    for (const node of pins) {
      const pin = node.pin!;
      const li = document.createElement('div');
      li.className = 'pin-item';
      li.setAttribute('data-id', node.id);

      const dot = document.createElement('span');
      dot.className = 'pin-dot';
      dot.style.background = pin.source === 'MetaScene' ? '#00d4ff' : '#ffd700';

      const labelSpan = document.createElement('span');
      labelSpan.className = 'pin-label';
      labelSpan.textContent = pin.label;

      const scoreSpan = document.createElement('span');
      scoreSpan.className = 'pin-score';
      scoreSpan.textContent = `${(pin.score * 100).toFixed(0)}%`;

      li.appendChild(dot);
      li.appendChild(labelSpan);
      li.appendChild(scoreSpan);
      li.addEventListener('click', () => editor?.store.setSelected(node.id));
      pinListEl.appendChild(li);
    }
  }

  function highlightListItem(id: string | null): void {
    document.querySelectorAll('.pin-item').forEach((el) => {
      (el as HTMLElement).classList.toggle(
        'selected',
        el.getAttribute('data-id') === id,
      );
    });
  }

  function renderInspector(
    node: { id: string; pin?: SpatialPin; type: string; visible: boolean } | null,
  ): void {
    if (!node?.pin) {
      inspectorEl.innerHTML = '<p class="inspector-empty">Select a pin to inspect and edit it.</p>';
      return;
    }
    const pin = node.pin;

    inspectorEl.innerHTML = `
      <form id="inspector-form" class="inspector-form">
        <div class="field">
          <label for="pin-label-input">Label</label>
          <input id="pin-label-input" type="text" value="${esc(pin.label)}" />
        </div>
        <div class="field">
          <label>Semantic Label</label>
          <input type="text" value="${esc(pin.semanticLabel)}" disabled />
        </div>
        <div class="field">
          <label for="pin-score-input">Score</label>
          <input id="pin-score-input" type="number" value="${pin.score.toFixed(3)}"
            min="0" max="1" step="0.001" />
        </div>
        <div class="field">
          <label for="pin-conf-input">Placement Confidence</label>
          <input id="pin-conf-input" type="number"
            value="${pin.placementConfidence.toFixed(3)}" min="0" max="1" step="0.001" />
        </div>
        <div class="field">
          <label>Source</label>
          <input type="text" value="${esc(pin.source)}" disabled />
        </div>
        <div class="field">
          <label>Position (m)</label>
          <input type="text" value="[${pin.position.map((v) => v.toFixed(3)).join(', ')}]" disabled />
        </div>
        <div class="field">
          <label>Normal</label>
          <input type="text" value="[${pin.normal.map((v) => v.toFixed(3)).join(', ')}]" disabled />
        </div>
        <div class="field">
          <label>Hit Type</label>
          <input type="text" value="${esc(pin.hitType)}" disabled />
        </div>
        <div class="field">
          <label>Track ID</label>
          <input type="text" value="${pin.trackId}" disabled />
        </div>
        <div class="inspector-actions">
          <button type="submit" class="btn-primary">Save Changes</button>
          <button type="button" id="btn-delete-pin" class="btn-danger">Delete Pin</button>
        </div>
      </form>
    `;

    q<HTMLFormElement>('#inspector-form').addEventListener('submit', (e) => {
      e.preventDefault();
      const labelVal = q<HTMLInputElement>('#pin-label-input').value;
      const scoreVal = parseFloat(q<HTMLInputElement>('#pin-score-input').value);
      const confVal = parseFloat(q<HTMLInputElement>('#pin-conf-input').value);
      editor?.updateSelectedPin({
        label: labelVal,
        score: Number.isFinite(scoreVal) ? scoreVal : pin.score,
        placementConfidence: Number.isFinite(confVal) ? confVal : pin.placementConfidence,
      });
    });

    q('#btn-delete-pin').addEventListener('click', () => {
      editor?.deleteSelectedPin();
      renderInspector(null);
    });
  }

  function esc(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function triggerDownload(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 5000);
  }
}

document.addEventListener('DOMContentLoaded', init);
