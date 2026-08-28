import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.185.1/build/three.module.js";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js";
import {
  addHabitatDecor,
  configureSoftLighting,
  createPacusCharacter,
  disposePacusCharacter,
  playPacusAnimation,
  applyPacusPalette,
  prepareLoadedPacusCharacter,
} from "./character.js";
import { configureAnimationState, playOneShot } from "./animation-3d.js";
import { configureMovement, updateMovement, stopMovement } from "./movement-3d.js";
import { applyGrowth } from "./growth-3d.js";
import { configureBehavior, reactToInteraction } from "./behavior-3d.js";

const activeRuntimes = new Set();
const gltfLoader = new GLTFLoader();
const PACUS_ASSET_URL = new URL("../../assets/pacus/pacus.glb", import.meta.url).href;

export function disposeAllPacus3D() {
  for (const runtime of [...activeRuntimes]) runtime.dispose();
  activeRuntimes.clear();
}

function configureCharacterRuntime(character, pacus, { isFallback = false } = {}) {
  if (!character.userData.mixer) configureAnimationState(character);
  configureMovement(character, { speed: 0.55, roam: true });
  configureBehavior(character);
  applyGrowth(character, pacus);
  applyPacusPalette(character, pacus);
  character.userData.asset = isFallback ? "procedural-fallback" : "glb";
  playPacusAnimation(character, "swim", { fade: 0.08 });
  return character;
}

function loadRiggedPacus() {
  return new Promise((resolve, reject) => {
    gltfLoader.load(PACUS_ASSET_URL, resolve, undefined, reject);
  });
}

export function mountPacus3D(host, pacus = {}) {
  if (!host) return null;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 100);
  camera.position.set(0, 0.22, 3.4);
  camera.lookAt(0, -0.02, 0);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: "high-performance" });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.setClearColor(0x000000, 0);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  host.appendChild(renderer.domElement);

  configureSoftLighting(scene);
  addHabitatDecor(scene);

  let character = configureCharacterRuntime(createPacusCharacter(pacus.stage ?? "baby"), pacus, { isFallback: true });
  scene.add(character);

  const clock = new THREE.Clock();
  let elapsed = 0;
  let raf = 0;
  let disposed = false;
  let assetLoadInFlight = true;

  function resize() {
    const width = Math.max(host.clientWidth, 1);
    const height = Math.max(host.clientHeight, 1);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  }

  const observer = new ResizeObserver(resize);
  observer.observe(host);
  resize();

  const runtime = {
    character,
    play(name) { return playPacusAnimation(character, name); },
    interact(kind = "tap") { reactToInteraction(character, kind); },
    calm() {
      stopMovement(character);
      playPacusAnimation(character, "gentle");
    },
    dispose() {
      if (disposed) return;
      disposed = true;
      cancelAnimationFrame(raf);
      observer.disconnect();
      renderer.domElement.removeEventListener("pointerdown", pointerDown);
      window.removeEventListener("pacus:task-completed", onTaskCompleted);
      character.userData.mixer?.stopAllAction();
      scene.remove(character);
      if (character.userData.asset === "glb") {
        character.traverse((object) => {
          if (object.geometry) object.geometry.dispose();
          if (object.material) {
            if (Array.isArray(object.material)) object.material.forEach((material) => material.dispose());
            else object.material.dispose();
          }
        });
      } else {
        disposePacusCharacter(character);
      }
      renderer.dispose();
      host.replaceChildren();
      activeRuntimes.delete(runtime);
    },
  };
  activeRuntimes.add(runtime);

  // TEMPORARIO: o pacus.glb atual tem um problema de skinning (todas as
  // malhas, inclusive o eggShell, estao vinculadas ao mesmo skin/skeleton
  // com pesos aparentemente quebrados) que faz o corpo renderizar como uma
  // bolha lisa em vez do axolote articulado, em qualquer estagio. Ate o
  // asset ser reexportado corretamente, ficamos so no personagem
  // procedural (formas THREE.js simples, sem skinning, com bracos/cauda/
  // guelras/olhos de verdade). Para reativar o glb assim que ele for
  // corrigido, descomente o bloco abaixo.
  assetLoadInFlight = false;
  /*
  loadRiggedPacus().then((gltf) => {
    if (disposed) return;
    const nextCharacter = prepareLoadedPacusCharacter(gltf.scene, gltf, pacus);
    configureCharacterRuntime(nextCharacter, pacus);
    nextCharacter.position.copy(character.position);
    nextCharacter.rotation.copy(character.rotation);
    nextCharacter.scale.copy(character.scale);
    scene.add(nextCharacter);
    scene.remove(character);
    disposePacusCharacter(character);
    character = nextCharacter;
    runtime.character = character;
  }).catch((error) => {
    console.warn("PACUS 3D asset unavailable; using procedural fallback.", error);
  }).finally(() => {
    assetLoadInFlight = false;
  });
  */

  function tick() {
    if (disposed || !document.body.contains(host)) {
      runtime.dispose();
      return;
    }
    const delta = Math.min(clock.getDelta(), 0.05);
    elapsed += delta;
    character.userData.mixer?.update(delta);
    updateMovement(character, delta, elapsed);
    renderer.render(scene, camera);
    raf = requestAnimationFrame(tick);
  }
  tick();

  function pointerDown(event) {
    event.preventDefault();
    reactToInteraction(character, "tap");
    playOneShot(character, "wave");
  }

  renderer.domElement.addEventListener("pointerdown", pointerDown, { passive: false });

  const onTaskCompleted = () => {
    reactToInteraction(character, "task");
  };
  window.addEventListener("pacus:task-completed", onTaskCompleted);

  return runtime;
}
