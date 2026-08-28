// Monta a cena Three.js do PACUS 3D dentro de um elemento host, usando o
// pacus.glb atual (Rodin: geometria + material PBR de alta qualidade, sem
// rig/skin/animacoes — ver README.md e controller.js pra o porque e como
// isso é compensado com animacao procedural). Devolve {dispose()} no
// mesmo formato que o habitat 2D usava, pra encaixar direto em
// home.js/pacus.js sem mudar a chamada.
//
// IMPORTANTE: a cena/renderer/glb sao um SINGLETON em memoria (modulo).
// home.js/pacus.js chamam mountTank3D a cada draw() (toda vez que o
// usuario marca uma tarefa, troca de aba, etc), o que recria o <div
// data-pacus-3d-host> do zero no DOM. Sem esse singleton, cada draw()
// recarregaria o .glb inteiro da rede de novo (lento, pisca) e qualquer
// animacao de transicao (crescimento jovem->adulto) nunca teria tempo de
// aparecer. Com o singleton, so o <canvas> muda de "pai" (reparent) entre
// hosts — o WebGLRenderer, a cena e o controller continuam vivos.

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
// A transicao entre estagios agora e animada (ver controller.js grow()) —
// e essa animacao de escala QUE FAZ as vezes de "animacao de crescimento"
// enquanto nao existe um modelo/rig por fase.
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

// --- singleton da cena (ver comentario no topo do arquivo) ---
let singleton = null; // { THREE, scene, camera, renderer, orbitGroup, pivot, controller, behavior, clock, raf, currentHost, ...handlers }
let singletonLoading = null; // Promise em andamento, pra nao iniciar duas cargas do glb em paralelo

function detachFromCurrentHost() {
  if (!singleton?.currentHost) return;
  const s = singleton;
  s.currentHost.removeEventListener("pointerdown", s.onPointerDown);
  s.currentHost.removeEventListener("pointermove", s.onPointerMove);
  s.currentHost.removeEventListener("pointerup", s.endDrag);
  s.currentHost.removeEventListener("pointercancel", s.endDrag);
  window.removeEventListener("resize", s.onResize);
  s.renderer.domElement.remove();
  s.currentHost = null;
}

function attachToHost(host) {
  const s = singleton;
  detachFromCurrentHost();
  s.currentHost = host;
  host.appendChild(s.renderer.domElement);
  host.style.cursor = "grab";
  host.addEventListener("pointerdown", s.onPointerDown);
  host.addEventListener("pointermove", s.onPointerMove);
  host.addEventListener("pointerup", s.endDrag);
  host.addEventListener("pointercancel", s.endDrag);
  window.addEventListener("resize", s.onResize);
  s.onResize(); // ajusta pro tamanho do novo host imediatamente
}

async function ensureSingleton(initialStage) {
  if (singleton) return singleton;
  if (singletonLoading) return singletonLoading;

  singletonLoading = (async () => {
    const { THREE, GLTFLoader } = await loadThree();

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(30, 1, 0.05, 100);
    camera.position.set(0, 0.75, 2.6);
    camera.lookAt(0, 0.55, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;

    const hemi = new THREE.HemisphereLight(0xeafaf6, 0x0a2b29, 1.0);
    const key = new THREE.DirectionalLight(0xffffff, 1.3);
    key.position.set(2, 3, 4);
    const fill = new THREE.DirectionalLight(0xbfe9e3, 0.5);
    fill.position.set(-3, 1.5, -2);
    scene.add(hemi, key, fill);

    const orbitGroup = new THREE.Group();
    scene.add(orbitGroup);
    const pivot = new THREE.Group();
    orbitGroup.add(pivot);

    const s = {
      THREE, scene, camera, renderer, orbitGroup, pivot,
      controller: null, behavior: null, currentHost: null,
      clock: new THREE.Clock(), raf: null,
    };
    singleton = s;

    // --- interacao: arrastar gira (rotacao 360), toque curto = reacao ---
    const DRAG_THRESHOLD_PX = 6;
    const DRAG_SENSITIVITY = 0.012;
    let pointerId = null;
    let dragStartX = 0;
    let dragStartRotY = 0;
    let didDrag = false;

    s.onPointerDown = (event) => {
      pointerId = event.pointerId;
      dragStartX = event.clientX;
      dragStartRotY = orbitGroup.rotation.y;
      didDrag = false;
      s.currentHost?.setPointerCapture?.(pointerId);
      if (s.currentHost) s.currentHost.style.cursor = "grabbing";
    };
    s.onPointerMove = (event) => {
      if (pointerId === null || event.pointerId !== pointerId) return;
      const dx = event.clientX - dragStartX;
      if (!didDrag && Math.abs(dx) > DRAG_THRESHOLD_PX) didDrag = true;
      if (didDrag) orbitGroup.rotation.y = dragStartRotY + dx * DRAG_SENSITIVITY;
    };
    s.endDrag = (event) => {
      if (pointerId === null || event.pointerId !== pointerId) return;
      s.currentHost?.releasePointerCapture?.(pointerId);
      if (s.currentHost) s.currentHost.style.cursor = "grab";
      if (!didDrag) {
        s.behavior?.onTouch();
        s.controller?.react("touch");
      }
      pointerId = null;
    };
    s.onResize = () => {
      if (!s.currentHost) return;
      const w = s.currentHost.clientWidth || 320;
      const h = s.currentHost.clientHeight || 320;
      s.camera.aspect = w / h;
      s.camera.updateProjectionMatrix();
      s.renderer.setSize(w, h);
    };

    function tick() {
      s.raf = requestAnimationFrame(tick);
      const delta = Math.min(s.clock.getDelta(), 0.1);
      s.controller?.update(delta);
      if (s.currentHost) s.renderer.render(scene, camera);
    }
    tick();

    const loader = new GLTFLoader();
    await new Promise((resolve) => {
      loader.load(
        GLB_URL,
        (gltf) => {
          const box = new THREE.Box3().setFromObject(gltf.scene);
          const center = box.getCenter(new THREE.Vector3());
          const size = box.getSize(new THREE.Vector3());
          const normScale = 1.6 / Math.max(size.x, size.y, size.z);
          gltf.scene.position.set(-center.x, -box.min.y, -center.z);
          gltf.scene.scale.setScalar(normScale);

          pivot.add(gltf.scene);
          pivot.position.y = 0.05;

          s.controller = createPacusController(THREE, gltf, pivot);
          s.controller.grow(STAGE_GROWTH[initialStage] ?? 1, { immediate: true });
          s.behavior = createBehavior(s.controller);
          s.behavior.start();
          resolve();
        },
        undefined,
        (err) => {
          console.error("[pacus3d] falha ao carregar pacus.glb", err);
          resolve(); // nao trava o app — so fica sem o modelo
        }
      );
    });

    return s;
  })();

  return singletonLoading;
}

export async function mountPacus3D(host, { onReady, stage } = {}) {
  const targetStage = normalizeStage(stage);
  const s = await ensureSingleton(targetStage);
  attachToHost(host);
  s.controller?.grow(STAGE_GROWTH[targetStage] ?? 1); // anima ate o estagio atual, se ja tinha carregado com outro
  onReady?.(s.controller);

  return {
    dispose() {
      // Nao destroi o WebGL/glb (singleton) — so tira o canvas desse host.
      // A proxima chamada a mountPacus3D reaproveita tudo.
      if (singleton?.currentHost === host) detachFromCurrentHost();
    },
    get controller() { return singleton?.controller ?? null; },
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
      runtimePromise.then((runtime) => runtime.dispose());
    },
  };
}
