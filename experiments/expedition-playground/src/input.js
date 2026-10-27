(function (D) {
  "use strict";
  D.Input = function (canvas) {
    this.keys = new Set(); this.pressed = new Set(); this.touch = new Set();
    this.pointer = { x: 650, y: 390 }; this.mouseDown = false; this.canvas = canvas;
    const input = this;
    document.addEventListener("keydown", function (event) {
      if (["INPUT", "TEXTAREA", "SELECT"].includes(event.target.tagName)) return;
      if (!input.keys.has(event.code)) input.pressed.add(event.code);
      input.keys.add(event.code);
      if (["Space", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Tab"].includes(event.code)) event.preventDefault();
      if (!event.repeat && D.handleShortcut) D.handleShortcut(event.code);
    });
    document.addEventListener("keyup", function (event) { input.keys.delete(event.code); });
    canvas.addEventListener("pointermove", function (event) { input.updatePointer(event); });
    canvas.addEventListener("pointerdown", function (event) {
      input.updatePointer(event); canvas.focus({ preventScroll: true });
      if (event.button === 0 && event.pointerType !== "touch") input.mouseDown = true;
    });
    document.addEventListener("pointerup", function () { input.mouseDown = false; });
    canvas.addEventListener("contextmenu", function (event) { event.preventDefault(); });
    window.addEventListener("blur", function () { input.clear(); if (D.world) D.world.paused = true; });
    document.addEventListener("visibilitychange", function () {
      if (document.hidden) { input.clear(); if (D.world) D.world.paused = true; }
    });
    document.querySelectorAll("[data-hold]").forEach(function (button) {
      const action = button.dataset.hold;
      button.addEventListener("pointerdown", function (event) {
        event.preventDefault(); button.setPointerCapture(event.pointerId);
        input.touch.add(action); input.pressed.add("touch-" + action); button.classList.add("pressed");
      });
      function release() { input.touch.delete(action); button.classList.remove("pressed"); }
      button.addEventListener("pointerup", release); button.addEventListener("pointercancel", release);
      button.addEventListener("lostpointercapture", release);
    });
  };
  D.Input.prototype.clear = function () {
    this.keys.clear(); this.pressed.clear(); this.touch.clear(); this.mouseDown = false;
    document.querySelectorAll("[data-hold]").forEach(function (b) { b.classList.remove("pressed"); });
  };
  D.Input.prototype.updatePointer = function (event) {
    const box = this.canvas.getBoundingClientRect();
    this.pointer.x = (event.clientX - box.left) * 960 / box.width;
    this.pointer.y = (event.clientY - box.top) * 540 / box.height;
  };
  D.Input.prototype.sample = function (w, dt) {
    const key = this.keys, press = this.pressed, touch = this.touch;
    const inputs = [{}, {}];
    const primary = {
      left: key.has("KeyA") || touch.has("left"), right: key.has("KeyD") || touch.has("right"),
      up: key.has("KeyW") || touch.has("up"), down: key.has("KeyS") || touch.has("down"),
      jump: press.has("Space") || press.has("touch-jump"),
      sprint: key.has("ShiftLeft"), fire: this.mouseDown || touch.has("fire"),
      interact: key.has("KeyE") || press.has("KeyE") || touch.has("interact") || press.has("touch-interact"),
      interactPressed: press.has("KeyE") || press.has("touch-interact"),
      aim: { x: this.pointer.x / (w.zoom || 1) + w.camera.x, y: this.pointer.y / (w.zoom || 1) + w.camera.y }
    };
    inputs[w.active] = primary;
    if (w.coop) {
      const q = w.players[1 - w.active], aim = q.aim;
      aim.x += ((key.has("KeyL") ? 1 : 0) - (key.has("KeyJ") ? 1 : 0)) * 320 * dt;
      aim.y += ((key.has("KeyK") ? 1 : 0) - (key.has("KeyI") ? 1 : 0)) * 320 * dt;
      inputs[1 - w.active] = {
        left: key.has("ArrowLeft"), right: key.has("ArrowRight"),
        up: key.has("ArrowUp"), down: key.has("ArrowDown"),
        jump: press.has("ArrowUp"), fire: key.has("ShiftRight"),
        interact: key.has("Enter") || press.has("Enter"), interactPressed: press.has("Enter"), aim: aim
      };
    }
    this.pressed.clear();
    return inputs;
  };
})(window.DN);
