// PacusController — versao "sem rig". O pacus.glb atual (gerado no Rodin)
// tem geometria + material PBR de altissima qualidade, mas NAO tem
// skeleton/skin nem clipes de animacao (ver frontend/js/pacus/README.md
// pra o porque). Em vez de esperar um rig completo, este controller anima
// o personagem por transformacao rigida do objeto inteiro (bob, sway,
// leve rotacao "respirando") + um efeito de dobra na cauda direto no
// vertex shader do material (sem precisar de ossos: a curvatura e
// calculada a partir da posicao Z de cada vertice, que e onde a cauda do
// PACUS se estende — ver bounds do mesh, z negativo = cauda).
//
// Mantem a MESMA API conceitual da secao 15 da espec (play/expression/
// lookAt/grow/react) pra nao quebrar behavior.js nem qualquer coisa que
// já espera essa interface — só que aqui play()/expression() viram
// aproximacoes (mudam amplitude/velocidade do sway, ou inclinam o corpo
// todo) em vez de tocar clipes de verdade. Quando um pacus.glb rigado
// chegar, troca esse arquivo pela versao anterior (ver git log) sem
// precisar mexer no resto.

const TAIL_Z_THRESHOLD = 0.05; // vertices com z < isso (mais perto da cauda) dobram mais
const TAIL_Z_RANGE = 0.9; // extensao aproximada da cauda no eixo Z (bounds ~ -0.95..0.05)

// Presets de "animacao" por transformacao rigida — usados por play().
const MOTION_PRESETS = {
  IDLE: { bobAmp: 0.035, bobSpeed: 1.1, swayAmp: 0.05, swaySpeed: 0.6, tailAmp: 0.12, tailSpeed: 1.4 },
  SWIM: { bobAmp: 0.05, bobSpeed: 1.6, swayAmp: 0.16, swaySpeed: 1.1, tailAmp: 0.32, tailSpeed: 2.2 },
  CURIOUS: { bobAmp: 0.02, bobSpeed: 1.8, swayAmp: 0.02, swaySpeed: 0.3, tailAmp: 0.18, tailSpeed: 2.6, tilt: 0.12 },
  HAPPY: { bobAmp: 0.07, bobSpeed: 2.4, swayAmp: 0.08, swaySpeed: 1.6, tailAmp: 0.4, tailSpeed: 3.2 },
  SURPRISED: { bobAmp: 0.09, bobSpeed: 3.2, swayAmp: 0.02, swaySpeed: 0.4, tailAmp: 0.1, tailSpeed: 1, popScale: 1.08 },
  SLEEP: { bobAmp: 0.012, bobSpeed: 0.35, swayAmp: 0, swaySpeed: 0, tailAmp: 0.03, tailSpeed: 0.3, tiltDown: 0.35 },
  WAKE_UP: { bobAmp: 0.05, bobSpeed: 1.4, swayAmp: 0.06, swaySpeed: 0.8, tailAmp: 0.2, tailSpeed: 1.8 },
};

// Injeta a dobra da cauda direto no vertex shader do material PBR gerado
// pelo glTFLoader (MeshStandardMaterial), via onBeforeCompile — assim a
// gente ganha o efeito sem precisar de skin/bones.
function addTailBend(material, uniforms) {
  material.onBeforeCompile = (shader) => {
    shader.uniforms.uTime = uniforms.uTime;
    shader.uniforms.uTailAmp = uniforms.uTailAmp;
    shader.uniforms.uTailSpeed = uniforms.uTailSpeed;

    shader.vertexShader = `
      uniform float uTime;
      uniform float uTailAmp;
      uniform float uTailSpeed;
      ${shader.vertexShader}
    `.replace(
      "#include <begin_vertex>",
      `
      #include <begin_vertex>
      float tailWeight = clamp((${TAIL_Z_THRESHOLD.toFixed(3)} - position.z) / ${TAIL_Z_RANGE.toFixed(3)}, 0.0, 1.0);
      tailWeight = tailWeight * tailWeight; // mais suave perto do corpo, mais forte na ponta
      float wag = sin(uTime * uTailSpeed - position.z * 3.0) * uTailAmp * tailWeight;
      transformed.x += wag;
      `
    );
    material.userData.shader = shader;
  };
  material.needsUpdate = true;
}

export function createPacusController(THREE, gltf, transformTarget) {
  // transformTarget: o Object3D que recebe bob/sway/tilt (pode ser um
  // pivot externo, pra girar em torno do centro do corpo em vez da
  // origem do mesh cru). Se nao for passado, usa a propria cena do glb.
  const root = transformTarget ?? gltf.scene;

  // Acha o primeiro mesh com material (deve ser so um, o "model" do Rodin).
  let mesh = null;
  root.traverse((node) => {
    if (!mesh && node.isMesh) mesh = node;
  });

  const uniforms = {
    uTime: { value: 0 },
    uTailAmp: { value: MOTION_PRESETS.IDLE.tailAmp },
    uTailSpeed: { value: MOTION_PRESETS.IDLE.tailSpeed },
  };
  if (mesh) {
    const materials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    for (const mat of materials) addTailBend(mat, uniforms);
  }

  let elapsed = 0;
  let preset = MOTION_PRESETS.IDLE;
  let presetTransitionUntil = 0;
  let currentGrowth = 1;
  const baseScale = new THREE.Vector3(1, 1, 1);

  // point: THREE.Vector3 mundo, ou null. Aproximado: gira o corpo todo
  // suavemente na direcao do alvo (nao tem cabeca separada pra girar so ela).
  const lookTarget = new THREE.Vector3();
  let hasLookTarget = false;

  function play(name, { returnTo, holdMs = 1400 } = {}) {
    const key = String(name).toUpperCase();
    const next = MOTION_PRESETS[key];
    if (!next) return false;
    preset = next;
    if (returnTo && MOTION_PRESETS[returnTo.toUpperCase()]) {
      presetTransitionUntil = performance.now() + holdMs;
      const backTo = MOTION_PRESETS[returnTo.toUpperCase()];
      window.setTimeout(() => {
        preset = backTo;
      }, holdMs);
    }
    return true;
  }

  // Sem morph targets nesse glb (so geometria+material) — expression()
  // fica como aproximacao futura; nao quebra quem chama.
  function expression() {
    return null;
  }

  function lookAt(point) {
    if (!point) {
      hasLookTarget = false;
      return;
    }
    lookTarget.copy(point);
    hasLookTarget = true;
  }

  function grow(value) {
    currentGrowth = Math.min(1, Math.max(0, value));
    const scale = 0.35 + currentGrowth * 0.65;
    baseScale.setScalar(scale);
  }

  function react(kind, point) {
    if (kind === "touch" || kind === "click") {
      lookAt(point);
      play("SURPRISED", { returnTo: "IDLE", holdMs: 900 });
    }
  }

  function update(delta) {
    elapsed += delta;
    uniforms.uTime.value = elapsed;
    uniforms.uTailAmp.value += (preset.tailAmp - uniforms.uTailAmp.value) * Math.min(1, delta * 4);
    uniforms.uTailSpeed.value += (preset.tailSpeed - uniforms.uTailSpeed.value) * Math.min(1, delta * 4);

    const bob = Math.sin(elapsed * preset.bobSpeed) * preset.bobAmp;
    const sway = Math.sin(elapsed * preset.swaySpeed * 0.7) * preset.swayAmp;
    root.position.y = bob;
    root.rotation.y = sway + (hasLookTarget ? Math.atan2(lookTarget.x - root.position.x, lookTarget.z - root.position.z || 1) * 0.15 : 0);
    root.rotation.z = Math.sin(elapsed * preset.bobSpeed * 0.5) * 0.02;
    if (preset.tilt) root.rotation.x = preset.tilt * Math.sin(elapsed * 2);
    if (preset.tiltDown) root.rotation.x += preset.tiltDown;

    const pop = preset.popScale ? 1 + (preset.popScale - 1) * Math.max(0, 1 - elapsed % 1) : 1;
    root.scale.set(baseScale.x * pop, baseScale.y * pop, baseScale.z * pop);
  }

  return {
    get object3D() { return root; },
    play,
    expression,
    lookAt,
    grow,
    react,
    update,
    get growth() { return currentGrowth; },
  };
}
