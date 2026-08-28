// Padrao de movimento do tanque, por estagio. Nao duplica os @keyframes do
// habitat.css — so ajusta variaveis CSS (velocidade/amplitude) no elemento
// que ja tem a animacao declarada (ver frontend/css/pacus/habitat.css).
import { getStageInfo } from "./growth.js";
import { prefersReducedMotion } from "./animation.js";

// [estagio]: { duracao do "nado", alcance horizontal/vertical do nado }
const SWIM_PROFILES = {
  baby: { duration: "9s", range: "38px", lift: "8px" },
  young: { duration: "7.5s", range: "56px", lift: "11px" },
  adult: { duration: "6.5s", range: "70px", lift: "14px" }
};

const EGG_BOB_DURATION = {
  egg: "5.5s",
  cracking: "3.2s",
  hatching: "1.8s"
};

export function attachMovement(tankEl, stage) {
  if (!tankEl) return () => {};

  const { isEgg } = getStageInfo(stage);
  const reduced = prefersReducedMotion();

  if (isEgg) {
    const eggEl = tankEl.querySelector(".pacus-egg");
    if (eggEl) {
      eggEl.style.setProperty(
        "--egg-bob-duration",
        reduced ? "0s" : EGG_BOB_DURATION[stage] ?? "5s"
      );
    }
    return () => {};
  }

  const bodyEl = tankEl.querySelector(".pacus-body");
  if (bodyEl) {
    const profile = SWIM_PROFILES[stage] ?? SWIM_PROFILES.adult;
    bodyEl.style.setProperty(
      "--swim-duration",
      reduced ? "0s" : profile.duration
    );
    bodyEl.style.setProperty("--swim-range", profile.range);
    bodyEl.style.setProperty("--swim-lift", profile.lift);
  }

  return () => {};
}
