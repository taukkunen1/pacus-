// Expressoes faciais (secao 7 da espec) via Shape Keys / Blend Shapes
// (morph targets no glTF). Cada expressao e uma mistura de influencias de
// morph target de 0 a 1. Os nomes de morph target abaixo sao um palpite
// razoavel de convencao Blender->glTF (mouthSmile, browUp, etc) — quando o
// glb final chegar, ajuste EXPRESSION_MORPHS pra bater com os nomes reais
// exportados (dá pra conferir com `mesh.morphTargetDictionary`). Ate la, o
// PacusController ignora silenciosamente qualquer morph target que nao
// exista no modelo carregado.

export const EXPRESSIONS = Object.freeze({
  HAPPY: "happy",
  EXCITED: "excited",
  CALM: "calm",
  CURIOUS: "curious",
  PLAYFUL: "playful",
  SURPRISED: "surprised",
  THINKING: "thinking",
  DETERMINED: "determined",
  NEUTRAL: "neutral",
});

// MVP (secao 16): feliz, curiosa, surpresa.
export const MVP_EXPRESSIONS = Object.freeze([
  EXPRESSIONS.HAPPY,
  EXPRESSIONS.CURIOUS,
  EXPRESSIONS.SURPRISED,
]);

// name -> { morphTargetName: influencia (0..1) }
export const EXPRESSION_MORPHS = Object.freeze({
  [EXPRESSIONS.HAPPY]: { mouthSmile: 1, eyeSquintL: 0.3, eyeSquintR: 0.3 },
  [EXPRESSIONS.EXCITED]: { mouthSmile: 1, mouthOpen: 0.5, eyeWideL: 0.4, eyeWideR: 0.4 },
  [EXPRESSIONS.CALM]: { mouthSmile: 0.2, eyeSquintL: 0.15, eyeSquintR: 0.15 },
  [EXPRESSIONS.CURIOUS]: { browUpL: 0.6, browUpR: 0.2, headTilt: 0.4 },
  [EXPRESSIONS.PLAYFUL]: { mouthSmile: 0.7, eyeWinkL: 1 },
  [EXPRESSIONS.SURPRISED]: { mouthOpen: 0.8, eyeWideL: 0.8, eyeWideR: 0.8, browUpL: 0.6, browUpR: 0.6 },
  [EXPRESSIONS.THINKING]: { mouthPucker: 0.4, browUpL: 0.3, eyeLookUpL: 0.3, eyeLookUpR: 0.3 },
  [EXPRESSIONS.DETERMINED]: { browDownL: 0.5, browDownR: 0.5, mouthPress: 0.4 },
  [EXPRESSIONS.NEUTRAL]: {},
});

const DEFAULT_BLEND_SECONDS = 0.4;

// Aplica uma expressao com transicao suave (tween manual por frame, sem
// depender de mixer/actions). `state.current` guarda a expressao ativa pra
// permitir chamadas repetidas sem reiniciar a transicao do zero.
export function createExpressionController(mesh) {
  const dict = mesh?.morphTargetDictionary ?? {};
  const influences = mesh?.morphTargetInfluences;
  let target = {};
  let name = EXPRESSIONS.NEUTRAL;

  function setExpression(exprName, { blend = DEFAULT_BLEND_SECONDS } = {}) {
    name = exprName;
    target = EXPRESSION_MORPHS[exprName] ?? {};
    return blend; // consumido pelo loop de render externo, se quiser usar
  }

  // Chamar a cada frame (delta em segundos) pra suavizar a transicao entre
  // a mistura de morphs atual e a `target`.
  function update(delta, speed = 4) {
    if (!influences) return;
    for (const key of Object.keys(dict)) {
      const idx = dict[key];
      const goal = target[key] ?? 0;
      influences[idx] += (goal - influences[idx]) * Math.min(1, delta * speed);
    }
  }

  return { setExpression, update, get current() { return name; } };
}
