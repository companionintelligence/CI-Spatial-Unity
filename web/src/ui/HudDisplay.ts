/**
 * HUD display — mirrors the wrist HUD from the Unity ControllerMenuUI.
 *
 * Updates the 2-D status overlay in the browser UI based on pipeline state.
 */

import type { PipelineState, PreviewDistance } from '../mapping/ScanningPipeline.js';

export class HudDisplay {
  private readonly modelEl: HTMLElement;
  private readonly displayEl: HTMLElement;
  private readonly previewEl: HTMLElement;
  private readonly statusEl: HTMLElement;

  constructor() {
    this.modelEl = getRequiredElement('hud-model');
    this.displayEl = getRequiredElement('hud-display');
    this.previewEl = getRequiredElement('hud-preview');
    this.statusEl = getRequiredElement('hud-status');
  }

  update(state: Readonly<PipelineState>): void {
    this.modelEl.textContent = state.model;
    this.displayEl.textContent = state.displayMode;
    this.previewEl.textContent = previewLabel(state.previewDistance);

    if (state.busy) {
      this.statusEl.textContent = 'BUSY…';
      this.statusEl.style.color = '#ffd700';
    } else {
      this.statusEl.textContent = `Captures: ${state.captureCount}`;
      this.statusEl.style.color = '#88ff88';
    }
  }

  setStatus(msg: string, color = '#ffffff'): void {
    this.statusEl.textContent = msg;
    this.statusEl.style.color = color;
  }
}

function getRequiredElement(id: string): HTMLElement {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`HudDisplay: required element "#${id}" was not found.`);
  }

  return element;
}

function previewLabel(d: PreviewDistance): string {
  if (d === 'Near') return 'Near (1m)';
  if (d === 'Far') return 'Far (3m)';
  return 'Off';
}
