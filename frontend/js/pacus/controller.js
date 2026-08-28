// PacusController: a API conceitual da secao 15 da espec, implementada por
// cima de um THREE.AnimationMixer + morph targets do modelo carregado.
//
//   pacus.play("swim")
//   pacus.expression("happy")
//   pacus.lookAt(target)
//   pacus.grow(0.7)
//   pacus.react("touch")
//
// Feita pra ser tolerante a um glb incompleto: qualquer clip de animacao,
// osso ou morph target que nao exista e simplesmente ignorado (sem
// warnings barulhentos, sem quebrar a cena) — assim da pra plugar o
// resultado de cada iteracao do modelo gerado sem precisar mexer no
// codigo de integracao toda hora.

import { ANIMATIONS, crossfadeTo } from "./animations.js";
import { EXPRESSIONS, createExpressionController } from "./expressions.js";

// Nomes de osso esperados (secao 6). Usados pra achar Head/Eye.L/Eye.R e
// permitir lookAt() olhando a cabeca/olhos na direcao de um alvo.
const BONE_NAMES = Object.freeze({
  ROOT: "Root",
  HEAD: "Head",
  EYE_L: "Eye.L",
  EYE_R: "Eye.R",
});

function findBone(root, name) {
  let found = null;
  root.traverse((node) => {
    if (!found && node.isBone && node.name === name) found = node;
  });
  return found;
}

function findMorphMesh(root) {
  let found = null;
  root.traverse((node) => {
    if (!found && node.isMesh && node.morphTargetDictionary) found = node;
  });
  return found;
}

export function createPacusController(THREE, gltf) {
  const root = gltf.scene;
  const mixer = new THREE.AnimationMixer(root);

  const actions = new Map();
  for (const clip of gltf.animations ?? []) {
    actions.set(clip.name, mixer.clipAction(clip));
  }

  const morphMesh = findMorphMesh(root);
  const expressionCtl = morphMesh ? createExpressionController(morphMesh) : null;

  const headBone = findBone(root, BONE_NAMES.HEAD);
  const lookTarget = new THREE.Vector3();
  let hasLookTarget = false;

  let currentGrowth = 1;
  const finiteActionListeners = new Set();

  mixer.addEventListener("finished", (event) => {
    for (const cb of finiteActionListeners) cb(event.action);
  });

  function play(name, { loop = true, onFinish, returnTo } = {}) {
    const key = ANIMATIONS[name] ?? name; // aceita tanto a chave quanto o valor
    const action = crossfadeTo(actions, key, { loop });
    if (!action) return false; // clip nao existe no glb ainda

    if (!loop && (onFinish || returnTo)) {
      const handler = (finishedAction) => {
        if (finishedAction !== action) return;
        finiteActionListeners.delete(handler);
        onFinish?.();
        if (returnTo) play(returnTo);
      };
      finiteActionListeners.add(handler);
    }
    return true;
  }

  function expression(name) {
    const key = EXPRESSIONS[String(name).toUpperCase()] ?? name;
    return expressionCtl?.setExpression(key) ?? null;
  }

  // point: THREE.Vector3 em coordenadas de mundo, ou null pra soltar o alvo.
  function lookAt(point) {
    if (!point) {
      hasLookTarget = false;
      return;
    }
    lookTarget.copy(point);
    hasLookTarget = true;
  }

  // 0 (recem-nascido) .. 1 (adulto) — ver secao 3. Ate o glb trazer um
  // unico modelo escalavel (via bone scale ou blend shape de crescimento),
  // isso so aplica uma escala uniforme na raiz como aproximacao.
  function grow(value) {
    currentGrowth = Math.min(1, Math.max(0, value));
    const scale = 0.35 + currentGrowth * 0.65; // nunca encolhe a zero
    root.scale.setScalar(scale);
  }

  // Atalho da secao 9: reacao padrao a toque/clique.
  function react(kind, point) {
    if (kind === "touch" || kind === "click") {
      lookAt(point);
      expression("curious");
      play("CURIOUS", { loop: false, returnTo: "IDLE" });
    }
  }

  function update(delta) {
    mixer.update(delta);
    expressionCtl?.update(delta);
    if (hasLookTarget && headBone) {
      const desired = new THREE.Quaternion();
      const m = new THREE.Matrix4().lookAt(headBone.getWorldPosition(new THREE.Vector3()), lookTarget, THREE.Object3D.DEFAULT_UP ?? new THREE.Vector3(0, 1, 0));
      desired.setFromRotationMatrix(m);
      headBone.quaternion.slerp(desired, Math.min(1, delta * 5));
    }
  }

  return {
    object3D: root,
    play,
    expression,
    lookAt,
    grow,
    react,
    update,
    get growth() { return currentGrowth; },
  };
}
