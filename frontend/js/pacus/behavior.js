// Maquina de estados do comportamento automatico do PACUS (secoes 9 e 10 da
// espec): IDLE (respiracao, cauda, branquias, piscar aleatorio, olhar pros
// lados), INTERACTION (reage a toque/clique) e NEEDS (fica cansado e dorme
// depois de tempo parado). Escrita sem depender de nada alem do
// PacusController (play/expression/lookAt) pra ficar facil de testar e de
// religar quando o glb final trocar.

const IDLE_GLANCE_MIN_MS = 4000;
const IDLE_GLANCE_MAX_MS = 9000;
const BLINK_MIN_MS = 2500;
const BLINK_MAX_MS = 6000;
const SLEEP_AFTER_MS = 45000; // tempo parado ate cochilar
const INTERACTION_HOLD_MS = 1600; // quanto tempo fica na expressao de reacao antes de voltar pro IDLE

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

  function scheduleBlink() {
    schedule(() => {
      if (state !== "SLEEP") controller.play("BLINK", { loop: false });
      scheduleBlink();
    }, randomBetween(BLINK_MIN_MS, BLINK_MAX_MS));
  }

  function scheduleGlance() {
    schedule(() => {
      if (state === "IDLE") controller.play("LOOK_AROUND", { loop: false, returnTo: "IDLE" });
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
    controller.expression("neutral");
  }

  function enterSleep() {
    state = "SLEEP";
    controller.play("SLEEP");
  }

  // Seção 9: usuário abre o PACUS enquanto ele "estava dormindo" — acorda,
  // olha pro usuário e sorri. Chame isso uma vez ao montar a cena.
  function wakeUpGreeting() {
    state = "INTERACTION";
    controller.play("WAKE_UP", {
      loop: false,
      onFinish: () => {
        controller.expression("happy");
        schedule(() => enterIdle(), INTERACTION_HOLD_MS);
      },
    });
  }

  // Seção 9: toque/clique — olha pro ponto tocado, expressão curiosa,
  // reação, volta pro IDLE.
  function onTouch(point) {
    lastInteractionAt = Date.now();
    if (state === "SLEEP") {
      wakeUpGreeting();
      return;
    }
    state = "INTERACTION";
    if (point) controller.lookAt(point);
    controller.expression("curious");
    controller.play("CURIOUS", { loop: false });
    schedule(() => {
      controller.expression("happy");
      schedule(() => enterIdle(), INTERACTION_HOLD_MS);
    }, 500);
  }

  function markActivity() {
    lastInteractionAt = Date.now();
    if (state === "SLEEP") wakeUpGreeting();
  }

  function start() {
    enterIdle();
    scheduleBlink();
    scheduleGlance();
    checkSleep();
  }

  function dispose() {
    clearTimers();
  }

  return { start, dispose, onTouch, markActivity, get state() { return state; } };
}
