// Nomes de clipes de animação esperados no pacus.glb, conforme a secao 8 da
// espec (PACUS_3D_Especificacao_Completa.docx). O nome do clip dentro do glb
// deve bater exatamente com um destes valores (case-sensitive) para que o
// PacusController consiga tocar ele por play("swim") etc. Se o clip nao
// existir no arquivo, play() simplesmente ignora (nao quebra a cena).

export const ANIMATIONS = Object.freeze({
  IDLE: "IDLE",
  BLINK: "BLINK",
  LOOK_AROUND: "LOOK_AROUND",
  CURIOUS: "CURIOUS",
  HAPPY: "HAPPY",
  EXCITED: "EXCITED",
  SAD: "SAD",
  SURPRISED: "SURPRISED",
  THINKING: "THINKING",
  DETERMINED: "DETERMINED",
  SLEEP: "SLEEP",
  WAKE_UP: "WAKE_UP",
  WALK: "WALK",
  RUN: "RUN",
  JUMP: "JUMP",
  SWIM: "SWIM",
  TURN_LEFT: "TURN_LEFT",
  TURN_RIGHT: "TURN_RIGHT",
  LOOK_UP: "LOOK_UP",
  LOOK_DOWN: "LOOK_DOWN",
});

// MVP (secao 16): so precisa funcionar de verdade com este subconjunto no
// primeiro corte. As demais ficam mapeadas mas podem nao existir ainda no
// glb entregue pelo usuario.
export const MVP_ANIMATIONS = Object.freeze([
  ANIMATIONS.IDLE,
  ANIMATIONS.BLINK,
  ANIMATIONS.HAPPY,
  ANIMATIONS.CURIOUS,
  ANIMATIONS.SURPRISED,
]);

const DEFAULT_FADE_SECONDS = 0.35;

// Troca suavemente (crossfade) de um AnimationAction pra outro num
// THREE.AnimationMixer. `actions` e um Map<nomeDoClip, THREE.AnimationAction>
// (montado pelo PacusController a partir de mixer.clipAction). Clipes que
// nao existem no glb simplesmente nao tem entrada no Map, entao chamar
// crossfadeTo com um nome ausente e um no-op seguro.
export function crossfadeTo(actions, name, { fade = DEFAULT_FADE_SECONDS, loop = true } = {}) {
  const next = actions.get(name);
  if (!next) return null;

  next.reset();
  next.setLoop(loop ? Infinity : 1, loop ? Infinity : 1);
  next.clampWhenFinished = !loop;
  next.enabled = true;
  next.play();

  for (const [otherName, action] of actions) {
    if (otherName === name || !action.isRunning()) continue;
    action.crossFadeTo(next, fade, false);
  }

  if (![...actions.values()].some((a) => a !== next && a.isRunning())) {
    next.fadeIn(fade);
  }

  return next;
}
