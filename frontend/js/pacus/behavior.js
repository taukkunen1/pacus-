// Maquina de estados do comportamento automatico do PACUS (secoes 9 e 10 da
// espec): IDLE (respiracao/bob/sway/cauda contínuos), INTERACTION (reage a
// toque/clique) e sono depois de tempo parado. Escrita sem depender de
// nada alem do PacusController (play/lookAt/react) — funciona tanto com a
// versao procedural atual (sem rig) quanto com uma versao futura baseada
// em clipes de verdade, contanto que os presets IDLE/SWIM/CURIOUS/HAPPY/
// SURPRISED/SLEEP/WAKE_UP existam (ver controller.js).

const IDLE_GLANCE_MIN_MS = 4000;
const IDLE_GLANCE_MAX_MS = 9000;
const SLEEP_AFTER_MS = 45000;
const INTERACTION_HOLD_MS = 1400;

function randomBetween(min, max) {
  return min + Math.random() * (max - min);
}

export function createBehavior(controller) {
  let state = "IDLE";
  let lastInteractionAt = Date.now();
  const timers = new Set();

  function schedule(fn, delay) {
    const id = window.setTimeout(() => {
      timers.delete(id);
      fn();
    }, delay);
    timers.add(id);
    return id;
  }

  function clearTimers() {
    for (const id of timers) window.clearTimeout(id);
    timers.clear();
  }

  function scheduleGlance() {
    schedule(() => {
      if (state === "IDLE") controller.play("CURIOUS", { returnTo: "IDLE", holdMs: 1200 });
      scheduleGlance();
    }, randomBetween(IDLE_GLANCE_MIN_MS, IDLE_GLANCE_MAX_MS));
  }

  function checkSleep() {
    schedule(() => {
      if (state === "IDLE" && Date.now() - lastInteractionAt >= SLEEP_AFTER_MS) {
        enterSleep();
      } else {
        checkSleep();
      }
    }, 2000);
  }

  function enterIdle() {
    state = "IDLE";
    controller.play("IDLE");
  }

  function enterSleep() {
    state = "SLEEP";
    controller.play("SLEEP");
  }

  function wakeUpGreeting() {
    state = "INTERACTION";
    controller.play("WAKE_UP");
    schedule(() => enterIdle(), INTERACTION_HOLD_MS);
  }

  function onTouch(point) {
    lastInteractionAt = Date.now();
    if (state === "SLEEP") {
      wakeUpGreeting();
      return;
    }
    state = "INTERACTION";
    if (point) controller.lookAt(point);
    controller.play("HAPPY", { returnTo: "IDLE", holdMs: INTERACTION_HOLD_MS });
    schedule(() => enterIdle(), INTERACTION_HOLD_MS + 100);
  }

  function markActivity() {
    lastInteractionAt = Date.now();
    if (state === "SLEEP") wakeUpGreeting();
  }

  function start() {
    enterIdle();
    scheduleGlance();
    checkSleep();
  }

  function dispose() {
    clearTimers();
  }

  return { start, dispose, onTouch, markActivity, get state() { return state; } };
}
