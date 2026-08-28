// Monta a cena Three.js do PACUS 3D dentro de um elemento host. Carrega
// frontend/assets/pacus/pacus.glb (troque pelo arquivo novo assim que ele
// chegar — mesmo caminho, mesmo nome, zero mudanca de codigo necessaria) e
// devolve um runtime com {dispose()} pra ficar compativel com o que
// home.js/pacus.js ja chamam hoje (pacusRuntime?.dispose()).
//
// IMPORTANTE: este modulo ainda nao esta ligado nas telas (home.js/
// pacus.js continuam usando o habitat 2D via mountTank3D em habitat.js).
// Ele fica pronto pra ligar assim que o pacus.glb novo, gerado a partir da
// PACUS_3D_Especificacao_Completa.docx, chegar e for validado (malha,
// skin weights, nomes de osso/clipe/morph). Ver frontend/js/pacus/README.md.

import { createPacusController } from "./controller.js";
import { createBehavior } from "./behavior.js";

const GLB_URL = new URL("../../assets/pacus/pacus.glb", import.meta.url).href;

async function loadThree() {
  const [THREE, { GLTFLoader }] = await Promise.all([
    import("three"),
    import("three/examples/jsm/loaders/GLTFLoader.js"),
  ]);
  return { THREE, GLTFLoader };
}

export async function mountPacus3D(host, { onReady } = {}) {
  const { THREE, GLTFLoader } = await loadThree();

  const width = host.clientWidth || 320;
  const height = host.clientHeight || 320;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(28, width / height, 0.1, 100);
  camera.position.set(0, 0.12, 5.2);
  camera.lookAt(0, -0.08, 0);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  renderer.setSize(width, height);
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  host.appendChild(renderer.domElement);

  const hemi = new THREE.HemisphereLight(0xeafaf6, 0x0a2b29, 0.9);
  const key = new THREE.DirectionalLight(0xffffff, 1.1);
  key.position.set(2, 3, 4);
  const fill = new THREE.DirectionalLight(0xbfe9e3, 0.4);
  fill.position.set(-3, 1, -2);
  scene.add(hemi, key, fill);

  let controller = null;
  let behavior = null;
  let disposed = false;

  const loader = new GLTFLoader();
  loader.load(
    GLB_URL,
    (gltf) => {
      if (disposed) return;
      scene.add(gltf.scene);
      controller = createPacusController(THREE, gltf);
      controller.grow(1);
      behavior = createBehavior(controller);
      behavior.start();
      onReady?.(controller);
    },
    undefined,
    (err) => {
      console.error("[pacus3d] falha ao carregar pacus.glb", err);
    }
  );

  function onPointerDown(event) {
    behavior?.onTouch();
    controller?.react("touch");
    void event;
  }
  host.addEventListener("pointerdown", onPointerDown);

  let raf = null;
  const clock = new THREE.Clock();
  function tick() {
    raf = requestAnimationFrame(tick);
    const delta = clock.getDelta();
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
      behavior?.dispose();
      renderer.dispose();
      renderer.domElement.remove();
    },
    get controller() { return controller; },
  };
}
