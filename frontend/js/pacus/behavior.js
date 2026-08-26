// Comportamento "idle" do tanque — pequenos gestos aleatorios para o PACUS
// nao parecer um sprite parado. Cada attachBehavior() devolve um cleanup:
// SEMPRE chame o cleanup anterior antes de re-renderizar o tanque, senao os
// timers do estagio antigo continuam rodando contra um elemento desmontado.
import { getStageInfo } from "./growth.js";
import { createRandomLoop, pulse, prefersReducedMotion } from "./animation.js";

// [estagio]: [minMs, maxMs] entre gestos
const DART_INTERVAL = {
  baby: [9000, 16000],
  young: [6000, 11000],
  adult: [4000, 9000]
};

const SHAKE_INTERVAL = {
  cracking: [4000, 8000],
  hatching: [2000, 5000]
};

export function attachBehavior(tankEl, stage) {
  if (!tankEl || prefersReducedMotion()) return () => {};

  const { isEgg } = getStageInfo(stage);

  if (isEgg) {
    const interval = SHAKE_INTERVAL[stage];
    const eggEl = tankEl.querySelector(".pacus-egg");
    if (!interval || !eggEl) return () => {};

    return createRandomLoop(interval, () => {
      pulse(eggEl, "pacus-egg--shake", 500);
    });
  }

  const bodyEl = tankEl.querySelector(".pacus-body");
  const interval = DART_INTERVAL[stage] ?? DART_INTERVAL.adult;
  if (!bodyEl) return () => {};

  return createRandomLoop(interval, () => {
    pulse(bodyEl, "pacus-body--dart", 700);
  });
}
