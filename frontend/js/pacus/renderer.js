// Monta a cena Three.js do PACUS 3D dentro de um elemento host, usando o
// pacus.glb atual (Rodin: geometria + material PBR de alta qualidade, sem
// rig/skin/animacoes — ver README.md e controller.js pra o porque e como
// isso é compensado com animacao procedural). Devolve {dispose()} no
// mesmo formato que o habitat 2D usava, pra encaixar direto em
// home.js/pacus.js sem mudar a chamada.

import { createPacusController } from "./controller.js";
import { createBehavior } from "./behavior.js";

const GLB_URL = new URL("../../assets/pacus/pacus.glb", import.meta.url).href;

const STAGE_LABEL = {
  egg: "Ovo",
  cracking: "Rachando",
  hatching: "Nascendo",
  baby: "Filhote",
  young: "Jovem",
  adult: "Adulto",
};

function normalizeStage(stage) {
  const value = String(stage ?? "adult").trim().toLowerCase();
  if (value.includes("egg") || value.includes("ovo")) return "egg";
  if (value.includes("crack") || value.includes("rach")) return "cracking";
  if (value.includes("hatch") || value.includes("eclos") || value.includes("nasc")) return "hatching";
  if (value.includes("baby") || value.includes("filh")) return "baby";
  if (value.includes("young") || value.includes("jov")) return "young";
  if (value.includes("adult")) return "adult";
  return STAGE_LABEL[value] ? value : "adult";
}

// growth 0..1 por estagio (secao 3 da espec) — usado por controller.grow()
// ate termos um modelo por fase; por enquanto so o adulto foi gerado, entao
// os estagios anteriores aparecem menores (grow) mas com a mesma malha.
const STAGE_GROWTH = { egg: 0.1, cracking: 0.2, hatching: 0.35, baby: 0.55, young: 0.75, adult: 1 };

// Mesmo "shell" visual do habitat 2D (tanque, ondas, bolhas, pill de
// estagio) — so troca o sprite <img> por um host onde o Three.js desenha.
export function renderTank(pacus = {}) {
  const stage = normalizeStage(pacus?.stage);
  const label = STAGE_LABEL[stage];
  return `
    <section class="pacus-tank pacus-tank--3d pacus-tank--${stage}" data-pacus-stage="${stage}" aria-label="Habitat do PACUS">
      <div class="pacus-waterline" aria-hidden="true"></div>
      <div class="pacus-bubbles" aria-hidden="true">
        <span></span><span></span><span></span><span></span>
      </div>
      <div class="pacus-3d-host" data-pacus-3d-host></div>
      <div class="pacus-overlay">
        <span class="pacus-stage-pill">${label}</span>
        <span class="pacus-interaction-hint">Arraste pra girar · toque pra interagir</span>
      </div>
    </section>
  `;
}

async function loadThree() {
  const [THREE, { GLTFLoader }] = await Promise.all([
    import("three"),
    import("three/examples/jsm/loaders/GLTFLoader.js"),
  ]);
  return { THREE, GLTFLoader };
}

export async function mountPacus3D(host, { onReady, stage } = {}) {
  const currentStage = normalizeStage(stage);
  const { THREE, GLTFLoader } = await loadThree();

  const width = host.clientWidth || 320;
  const height = host.clientHeight || 320;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(30, width / height, 0.05, 100);
  camera.position.set(0, 0.75, 2.6);
  camera.lookAt(0, 0.55, 0);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  renderer.setSize(width, height);
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  host.appendChild(renderer.domElement);

  const hemi = new THREE.HemisphereLight(0xeafaf6, 0x0a2b29, 1.0);
  const key = new THREE.DirectionalLight(0xffffff, 1.3);
  key.position.set(2, 3, 4);
  const fill = new THREE.DirectionalLight(0xbfe9e3, 0.5);
  fill.position.set(-3, 1.5, -2);
  scene.add(hemi, key, fill);

  // orbitGroup: rotacao livre controlada por arrastar (mouse/touch) — o
  // pivot continua recebendo bob/sway/tilt do controller por dentro, sem
  // conflitar com o giro manual do usuario.
  const orbitGroup = new THREE.Group();
  scene.add(orbitGroup);

  const pivot = new THREE.Group();
  orbitGroup.add(pivot);

  let controller = null;
  let behavior = null;
  let disposed = false;

  const loader = new GLTFLoader();
  loader.load(
    GLB_URL,
    (gltf) => {
      if (disposed) return;
      // O mesh do Rodin vem "deitado": Y de 0 a ~1.23 (altura), cauda se
      // estendendo em -Z. Centraliza no pivot pra bob/sway girarem em
      // torno do centro do corpo, nao da origem do mesh.
      const box = new THREE.Box3().setFromObject(gltf.scene);
      const center = box.getCenter(new THREE.Vector3());
      const size = box.getSize(new THREE.Vector3());
      const normScale = 1.6 / Math.max(size.x, size.y, size.z);
      gltf.scene.position.set(-center.x, -box.min.y, -center.z);
      gltf.scene.scale.setScalar(normScale);

      pivot.add(gltf.scene);
      pivot.position.y = 0.05;

      controller = createPacusController(THREE, gltf, pivot);
      controller.grow(STAGE_GROWTH[currentStage] ?? 1);
      behavior = createBehavior(controller);
      behavior.start();
      onReady?.(controller);
    },
    undefined,
    (err) => {
      console.error("[pacus3d] falha ao carregar pacus.glb", err);
    }
  );

  // Arrastar gira o PACUS livremente (rotacao 360 - secao 16 da espec,
  // "rotacao 360"). Um toque/clique curto, sem arrastar, continua contando
  // como interacao (behavior.onTouch / controller.react).
  const DRAG_THRESHOLD_PX = 6;
  const DRAG_SENSITIVITY = 0.012; // radianos por pixel arrastado
  let pointerId = null;
  let dragStartX = 0;
  let dragStartRotY = 0;
  let didDrag = false;

  function onPointerDown(event) {
    pointerId = event.pointerId;
    dragStartX = event.clientX;
    dragStartRotY = orbitGroup.rotation.y;
    didDrag = false;
    host.setPointerCapture?.(pointerId);
    host.style.cursor = "grabbing";
  }

  function onPointerMove(event) {
    if (pointerId === null || event.pointerId !== pointerId) return;
    const dx = event.clientX - dragStartX;
    if (!didDrag && Math.abs(dx) > DRAG_THRESHOLD_PX) didDrag = true;
    if (didDrag) orbitGroup.rotation.y = dragStartRotY + dx * DRAG_SENSITIVITY;
  }

  function endDrag(event) {
    if (pointerId === null || event.pointerId !== pointerId) return;
    host.releasePointerCapture?.(pointerId);
    host.style.cursor = "grab";
    if (!didDrag) {
      behavior?.onTouch();
      controller?.react("touch");
    }
    pointerId = null;
  }

  host.style.cursor = "grab";
  host.addEventListener("pointerdown", onPointerDown);
  host.addEventListener("pointermove", onPointerMove);
  host.addEventListener("pointerup", endDrag);
  host.addEventListener("pointercancel", endDrag);

  let raf = null;
  const clock = new THREE.Clock();
  function tick() {
    raf = requestAnimationFrame(tick);
    const delta = Math.min(clock.getDelta(), 0.1);
    controller?.update(delta);
    renderer.render(scene, camera);
  }
  tick();

  function onResize() {
    const w = host.clientWidth || width;
    const h = host.clientHeight || height;
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    renderer.setSize(w, h);
  }
  window.addEventListener("resize", onResize);

  return {
    dispose() {
      disposed = true;
      if (raf) cancelAnimationFrame(raf);
      window.removeEventListener("resize", onResize);
      host.removeEventListener("pointerdown", onPointerDown);
      host.removeEventListener("pointermove", onPointerMove);
      host.removeEventListener("pointerup", endDrag);
      host.removeEventListener("pointercancel", endDrag);
      behavior?.dispose();
      renderer.dispose();
      renderer.domElement.remove();
    },
    get controller() { return controller; },
  };
}

// Drop-in pro mesmo formato que home.js/pacus.js ja chamam:
// mountTank3D(root, pacus) -> { dispose() }. Acha o host 3D dentro do
// markup gerado por renderTank() e monta a cena nele.
export function mountTank3D(root, pacus = {}) {
  const host = root?.querySelector?.("[data-pacus-3d-host]");
  if (!host) return { dispose() {} };

  const runtimePromise = mountPacus3D(host, { stage: pacus?.stage });

  return {
    dispose() {
      // Se o dispose acontecer antes do load/mount terminar (troca rapida
      // de tela), ainda assim desmonta assim que o runtime ficar pronto.
      runtimePromise.then((runtime) => runtime.dispose());
    },
  };
}
