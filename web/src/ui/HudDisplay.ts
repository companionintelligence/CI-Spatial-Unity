/**
 * HUD display — mirrors the wrist HUD from the Unity ControllerMenuUI.
 *
 * Updates the 2-D status overlay in the browser UI based on pipeline state.
 */

import type { PipelineState } from '../mapping/ScanningPipeline.js';

export class HudDisplay {
  private readonly modelEl: HTMLElement;
  private readonly displayEl: HTMLElement;
  private readonly previewEl: HTMLElement;
  private readonly statusEl: HTMLElement;

  constructor() {
    this.modelEl = document.getElementById('hud-model')!;
    this.displayEl = document.getElementById('hud-display')!;
    this.previewEl = document.getElementById('hud-preview')!;
    this.statusEl = document.getElementById('hud-status')!;
  }

  update(state: Readonly<PipelineState>): void {
    if (this.modelEl) this.modelEl.textContent = state.model;
    if (this.displayEl) this.displayEl.textContent = state.displayMode;
    if (this.previewEl) this.previewEl.textContent = previewLabel(state.previewDistance);
    if (this.statusEl) {
      if (state.busy) {
        this.statusEl.textContent = 'BUSY…';
        this.statusEl.style.color = '#ffd700';
      } else {
        this.statusEl.textContent = `Captures: ${state.captureCount}`;
        this.statusEl.style.color = '#88ff88';
      }
    }
  }

  setStatus(msg: string, color = '#ffffff'): void {
    if (this.statusEl) {
      this.statusEl.textContent = msg;
      this.statusEl.style.color = color;
    }
  }
}

function previewLabel(d: string): string {
  if (d === 'Near') return 'Near (1m)';
  if (d === 'Far') return 'Far (3m)';
  return 'Off';
}
