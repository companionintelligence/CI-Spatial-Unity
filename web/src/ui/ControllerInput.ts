/**
 * XR controller input — mirrors Assets/CompanionScan/UI/XRControllerInput.cs.
 *
 * Reads WebXR gamepad buttons for Meta Quest controllers and fires the
 * corresponding pipeline actions.
 *
 * Button mapping (follows Meta Quest layout via WebXR Gamepad API):
 *
 *   Right controller (gamepad 0 in Meta Quest browser):
 *     Button 4 (A) → Capture
 *     Button 5 (B) → Cycle model
 *     Button 0 (Trigger) → Cycle display mode
 *     Button 1 (Grip / hand trigger) → Preview glTF
 *
 *   Left controller (gamepad 1):
 *     Button 4 (X) → Cycle preview distance
 *     Button 5 (Y) → Save & Export glTF
 *     Button 0 (Trigger) → Cycle display mode
 *     Button 1 (Grip) → Preview glTF
 */

export interface ControllerActions {
  doCapture(): void;
  cycleModel(): void;
  cyclePreviewDistance(): void;
  doSaveExport(): void;
  cycleDisplayMode(): void;
  previewGltf(): void;
}

interface ButtonState {
  wasPressed: boolean;
}

export class ControllerInput {
  private readonly actions: ControllerActions;
  // [gamepadIndex][buttonIndex]
  private readonly buttonStates = new Map<string, ButtonState>();

  constructor(actions: ControllerActions) {
    this.actions = actions;
  }

  /**
   * Poll all connected gamepads.
   * Call this once per animation frame.
   */
  update(): void {
    const gamepads = navigator.getGamepads?.() ?? [];
    for (let gIdx = 0; gIdx < gamepads.length; gIdx++) {
      const gp = gamepads[gIdx];
      if (!gp) continue;

      for (let bIdx = 0; bIdx < gp.buttons.length; bIdx++) {
        const key = `${gIdx}:${bIdx}`;
        const pressed = gp.buttons[bIdx].pressed;
        const prev = this.buttonStates.get(key) ?? { wasPressed: false };

        if (pressed && !prev.wasPressed) {
          this.handleButtonDown(gIdx, bIdx);
        }

        this.buttonStates.set(key, { wasPressed: pressed });
      }
    }
  }

  private handleButtonDown(gamepadIndex: number, buttonIndex: number): void {
    const isRight = gamepadIndex === 0;
    const isLeft = gamepadIndex === 1;

    // A (right gamepad button 4) → Capture
    if (isRight && buttonIndex === 4) {
      this.actions.doCapture();
      return;
    }

    // B (right gamepad button 5) → Cycle model
    if (isRight && buttonIndex === 5) {
      this.actions.cycleModel();
      return;
    }

    // X (left gamepad button 4) → Cycle preview distance
    if (isLeft && buttonIndex === 4) {
      this.actions.cyclePreviewDistance();
      return;
    }

    // Y (left gamepad button 5) → Save & Export
    if (isLeft && buttonIndex === 5) {
      this.actions.doSaveExport();
      return;
    }

    // Index trigger (button 0) on either controller → Cycle display mode
    if (buttonIndex === 0 && (isRight || isLeft)) {
      this.actions.cycleDisplayMode();
      return;
    }

    // Hand / grip trigger (button 1) on either controller → Preview glTF
    if (buttonIndex === 1 && (isRight || isLeft)) {
      this.actions.previewGltf();
      return;
    }
  }
}
