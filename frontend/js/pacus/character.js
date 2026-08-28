import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.185.1/build/three.module.js";
import { cmToSceneUnits, getPacusStageSizeCm, HABITAT_DIMENSIONS_CM } from "./dimensions.js";

const TAU = Math.PI * 2;

export const PACUS_STAGE_SCALE = Object.freeze({
  egg: 0.32,
  cracking: 0.42,
  hatching: 0.52,
  baby: 0.68,
  young: 0.84,
  adult: 1.0,
});

// Escala extra aplicada apenas ao osso "head", separada do scale geral do
// personagem. Referência: filhotes de axolote têm cabeça proporcionalmente
// maior que o corpo; no adulto a proporção normaliza. Ver docs/pacus-3d-estrutura-modelo.md.
export const PACUS_HEAD_SCALE = Object.freeze({
  egg: 1.0,
  cracking: 1.0,
  hatching: 1.18,
  baby: 1.35,
  young: 1.15,
  adult: 1.0,
});

export function normalizeStage(stage) {
  const value = String(stage ?? "baby").trim().toLowerCase();
  if (value.includes("egg") || value.includes("ovo")) return "egg";
  if (value.includes("crack") || value.includes("rach")) return "cracking";
  if (value.includes("hatch") || value.includes("eclos")) return "hatching";
  if (value.includes("baby") || value.includes("filh")) return "baby";
  if (value.includes("young") || value.includes("jov")) return "young";
  if (value.includes("adult") || value.includes("adult")) return "adult";
  return "baby";
}

function makeMaterial(color, roughness = 0.72, role = null) {
  const material = new THREE.MeshStandardMaterial({
    color,
    roughness,
    metalness: 0.0,
  });
  if (role) material.userData.pacusRole = role;
  return material;
}

export const PACUS_COLOR_VARIANTS = Object.freeze({
  coral: { name: "Coral", skin: 0xf2a8b9, belly: 0xffc9d3, gill: 0xe87999 },
  lavender: { name: "Lavanda", skin: 0xb9a8e8, belly: 0xe5dcff, gill: 0x9b81d5 },
  mint: { name: "Menta", skin: 0x8fd8c4, belly: 0xd8f5e8, gill: 0x58b69d },
  sky: { name: "Céu", skin: 0x8ec7ed, belly: 0xdaf0ff, gill: 0x5fa9dc },
  peach: { name: "Pêssego", skin: 0xf2ad83, belly: 0xffdcc8, gill: 0xdd7e58 },
  sunshine: { name: "Sol", skin: 0xf1cf75, belly: 0xffebbc, gill: 0xd5a83b },
  rose: { name: "Rosa", skin: 0xd98ab4, belly: 0xf4c7dc, gill: 0xb65a8d },
  aqua: { name: "Água", skin: 0x6fcac7, belly: 0xc9f0eb, gill: 0x3ca5a0 },
});

function hashString(value = "") {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

export function getPacusColorVariant(pacus = {}) {
  const explicit = String(pacus?.colorVariant ?? pacus?.color ?? "").trim().toLowerCase();
  if (PACUS_COLOR_VARIANTS[explicit]) return explicit;
  const identity = String(pacus?.id ?? pacus?.Id ?? pacus?.userId ?? pacus?.UserId ?? pacus?.name ?? "pacus");
  const keys = Object.keys(PACUS_COLOR_VARIANTS);
  return keys[hashString(identity) % keys.length];
}

export function getPacusPalette(pacus = {}) {
  return PACUS_COLOR_VARIANTS[getPacusColorVariant(pacus)] ?? PACUS_COLOR_VARIANTS.coral;
}

function cloneAndColorMaterial(material, role, palette) {
  const next = material.clone();
  const colorKey = role === "belly" ? "belly" : role === "gill" ? "gill" : "skin";
  next.color.setHex(palette[colorKey]);
  next.userData.pacusRole = role;
  return next;
}

export function applyPacusPalette(character, pacus = {}) {
  const palette = getPacusPalette(pacus);
  character.userData.colorVariant = getPacusColorVariant(pacus);
  character.userData.colorName = palette.name;

  character.traverse((object) => {
    if (!object.isMesh || !object.material) return;
    const materialName = String(object.material?.name ?? "").toLowerCase();
    const objectName = String(object.name ?? "").toLowerCase();
    const resolvedRole = object.material?.userData?.pacusRole
      ?? (materialName.includes("belly") || objectName.includes("belly") || objectName.includes("snout") ? "belly" : null)
      ?? (materialName.includes("gill") || objectName.includes("gill") ? "gill" : null)
      ?? ((materialName.includes("skin") || objectName.includes("body") || objectName.includes("arm") || objectName.includes("leg") || objectName.includes("tail") || objectName.includes("head")) ? "skin" : null);
    if (resolvedRole) object.material = Array.isArray(object.material)
      ? object.material.map((item) => cloneAndColorMaterial(item, resolvedRole, palette))
      : cloneAndColorMaterial(object.material, resolvedRole, palette);
  });

  return palette;
}

function addMesh(parent, geometry, material, bone = parent) {
  const mesh = new THREE.Mesh(geometry, material);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  bone.add(mesh);
  return mesh;
}

function addGill(parent, material, side, index) {
  const bone = new THREE.Bone();
  bone.name = `${side}_gill_${index + 1}`;
  bone.position.set(side === "left" ? -0.38 : 0.38, 0.14 + index * 0.05, 0.1 - index * 0.05);
  parent.add(bone);

  // Fronde principal, mais grossa e vistosa que antes.
  const geometry = new THREE.CapsuleGeometry(0.045, 0.24 + index * 0.03, 5, 8);
  const gill = addMesh(bone, geometry, material, bone);
  gill.position.y = 0.12;
  gill.rotation.z = side === "left" ? -0.78 - index * 0.12 : 0.78 + index * 0.12;
  gill.scale.z = 0.62;

  // Bolinhas na ponta (frisado), como nas guelras externas de um axolote de verdade.
  const tip = addMesh(bone, new THREE.SphereGeometry(0.05, 8, 6), material, bone);
  tip.position.set(0, 0.12 + (0.24 + index * 0.03) * 0.92 * (side === "left" ? -Math.sin(0.78 + index * 0.12) : Math.sin(0.78 + index * 0.12)), 0);
  return bone;
}

function addLimb(parent, material, side, index) {
  const bone = new THREE.Bone();
  bone.name = `${side}_arm_${index}`;
  bone.position.set(side === "left" ? -0.42 : 0.42, -0.16, index === 1 ? 0.22 : -0.16);
  parent.add(bone);

  const upper = addMesh(bone, new THREE.CapsuleGeometry(0.07, 0.22, 4, 8), material, bone);
  upper.rotation.z = side === "left" ? -0.42 : 0.42;
  upper.position.y = -0.12;

  const handBone = new THREE.Bone();
  handBone.name = `${side}_hand_${index}`;
  handBone.position.y = -0.23;
  bone.add(handBone);
  addMesh(handBone, new THREE.SphereGeometry(0.075, 10, 8), material, handBone);
  return bone;
}

function addEye(parent, x) {
  const eye = addMesh(parent, new THREE.SphereGeometry(0.1, 16, 12), makeMaterial(0x0d0f14, 0.15), parent);
  eye.position.set(x, 0.2, 0.46);

  // Brilho pequeno no olho — sem isso o axolote parece "morto"/sem vida.
  const highlight = addMesh(
    parent,
    new THREE.SphereGeometry(0.03, 8, 6),
    new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.1, emissive: 0x333333 }),
    parent
  );
  highlight.position.set(x + (x < 0 ? 0.03 : -0.03), 0.23, 0.51);
  return eye;
}

function addCheekBlush(parent, x, belly) {
  // Bochecha rosada logo abaixo do olho — traço bem caracteristico do axolote fofo.
  const blushMaterial = new THREE.MeshStandardMaterial({
    color: belly.color.getHex(),
    roughness: 0.8,
    transparent: true,
    opacity: 0.55,
  });
  const blush = addMesh(parent, new THREE.SphereGeometry(0.1, 12, 8), blushMaterial, parent);
  blush.position.set(x * 1.15, 0.03, 0.42);
  blush.scale.set(1.15, 0.7, 0.55);
  return blush;
}

function createRiggedAxolotl() {
  const root = new THREE.Group();
  root.name = "PACUS_RIG_ROOT";

  const skeletonRoot = new THREE.Bone();
  skeletonRoot.name = "root";
  skeletonRoot.position.y = 0.18;
  root.add(skeletonRoot);

  const pelvis = new THREE.Bone();
  pelvis.name = "pelvis";
  pelvis.position.y = 0.02;
  skeletonRoot.add(pelvis);

  const spine = new THREE.Bone();
  spine.name = "spine";
  spine.position.z = 0;
  pelvis.add(spine);

  const head = new THREE.Bone();
  head.name = "head";
  head.position.z = 0.48;
  head.position.y = 0.05;
  spine.add(head);

  const tail = new THREE.Bone();
  tail.name = "tail";
  tail.position.z = -0.48;
  spine.add(tail);

  const tail2 = new THREE.Bone();
  tail2.name = "tail_02";
  tail2.position.z = -0.42;
  tail.add(tail2);

  const tail3 = new THREE.Bone();
  tail3.name = "tail_03";
  tail3.position.z = -0.36;
  tail2.add(tail3);

  const skin = makeMaterial(0xf2a8b9, 0.62, "skin");
  const belly = makeMaterial(0xffc9d3, 0.7, "belly");
  const gill = makeMaterial(0xe87999, 0.58, "gill");
  const dark = makeMaterial(0x2b1a26, 0.5);
  const mouthMat = makeMaterial(0x7a304d, 0.65);
  const eggMaterial = new THREE.MeshStandardMaterial({ color: 0xf8d6df, roughness: 0.55, transparent: true, opacity: 0.82 });


  const eggShell = addMesh(root, new THREE.SphereGeometry(0.44, 24, 18), eggMaterial, root);
  eggShell.position.set(0, -0.02, 0.12);
  eggShell.scale.set(0.92, 1.18, 0.92);

  const torso = addMesh(spine, new THREE.CapsuleGeometry(0.42, 0.76, 8, 18), skin, spine);
  torso.rotation.x = Math.PI / 2;
  torso.scale.set(1.0, 0.88, 0.75);

  const bellyMesh = addMesh(spine, new THREE.SphereGeometry(0.34, 16, 12), belly, spine);
  bellyMesh.position.set(0, -0.02, 0.22);
  bellyMesh.scale.set(0.9, 0.58, 0.82);

  // Cabeca larga e achatada: a marca registrada do axolote (bem diferente
  // da cabeca quase redonda de antes).
  const headMesh = addMesh(head, new THREE.SphereGeometry(0.44, 24, 18), skin, head);
  headMesh.position.z = 0.14;
  headMesh.scale.set(1.32, 0.72, 1.05);

  const snout = addMesh(head, new THREE.SphereGeometry(0.27, 16, 12), belly, head);
  snout.position.set(0, -0.05, 0.52);
  snout.scale.set(1.18, 0.6, 0.68);

  // Sorriso mais largo e curvado pra cima (o "smile" caracteristico do axolote).
  const mouth = addMesh(head, new THREE.TorusGeometry(0.15, 0.02, 8, 20, Math.PI * 0.92), mouthMat, head);
  mouth.position.set(0, -0.09, 0.7);
  mouth.rotation.set(Math.PI / 2, 0, Math.PI * 0.04);

  addEye(head, -0.24);
  addEye(head, 0.24);
  addCheekBlush(head, -0.24, belly);
  addCheekBlush(head, 0.24, belly);

  const leftGill = [];
  const rightGill = [];
  for (let i = 0; i < 3; i += 1) {
    leftGill.push(addGill(head, gill, "left", i));
    rightGill.push(addGill(head, gill, "right", i));
  }

  const leftArmFront = addLimb(spine, skin, "left", 1);
  const rightArmFront = addLimb(spine, skin, "right", 1);
  const leftArmBack = addLimb(spine, skin, "left", 2);
  const rightArmBack = addLimb(spine, skin, "right", 2);

  const tailFin = addMesh(tail3, new THREE.ConeGeometry(0.26, 0.88, 8, 1, true), skin, tail3);
  tailFin.rotation.x = -Math.PI / 2;
  tailFin.scale.set(0.75, 0.72, 1.25);
  tailFin.position.z = -0.32;

  const dorsalFin = addMesh(spine, new THREE.ConeGeometry(0.16, 0.42, 7), skin, spine);
  dorsalFin.position.set(0, 0.38, -0.1);
  dorsalFin.rotation.x = Math.PI / 2;
  dorsalFin.scale.set(0.7, 0.85, 0.9);

  const footLeft = addMesh(leftArmBack, new THREE.SphereGeometry(0.08, 10, 8), skin, leftArmBack);
  footLeft.position.y = -0.32;
  const footRight = addMesh(rightArmBack, new THREE.SphereGeometry(0.08, 10, 8), skin, rightArmBack);
  footRight.position.y = -0.32;

  const bones = [];
  root.traverse((object) => {
    if (object.isBone) bones.push(object);
  });
  const skeleton = new THREE.Skeleton(bones);
  root.userData.skeleton = skeleton;
  root.userData.bones = Object.fromEntries(bones.map((bone) => [bone.name, bone]));
  root.userData.parts = { torso, headMesh, tailFin, mouth, gills: [...leftGill, ...rightGill], eggShell };

  const helper = new THREE.SkeletonHelper(skeletonRoot);
  helper.visible = false;
  root.add(helper);

  const mixer = new THREE.AnimationMixer(root);
  const clips = createAnimationClips(root.userData.bones);

  root.userData.mixer = mixer;
  root.userData.actions = Object.fromEntries(
    Object.entries(clips).map(([name, clip]) => [name, mixer.clipAction(clip)])
  );

  return root;
}

function quatTrack(name, values, times) {
  return new THREE.QuaternionKeyframeTrack(name, times, values);
}

function vecTrack(name, values, times) {
  return new THREE.VectorKeyframeTrack(name, times, values);
}

function createAnimationClips(bones) {
  const clips = {};

  const q = (x, y, z) => new THREE.Quaternion().setFromEuler(new THREE.Euler(x, y, z));
  const values = (...quaternions) => quaternions.flatMap((value) => [value.x, value.y, value.z, value.w]);

  clips.gentle = new THREE.AnimationClip("gentle", 2.4, [
    quatTrack("PACUS_RIG_ROOT/root.quaternion", values(q(0, 0, -0.025), q(0, 0, 0.025), q(0, 0, -0.025)), [0, 1.2, 2.4]),
    quatTrack("PACUS_RIG_ROOT/spine.quaternion", values(q(0.03, 0, 0), q(-0.03, 0, 0), q(0.03, 0, 0)), [0, 1.2, 2.4]),
    quatTrack("PACUS_RIG_ROOT/head.quaternion", values(q(0, 0, -0.03), q(0, 0, 0.05), q(0, 0, -0.03)), [0, 1.2, 2.4]),
  ]);

  clips.walk = new THREE.AnimationClip("walk", 1.1, [
    quatTrack("PACUS_RIG_ROOT/spine.quaternion", values(q(0.06, 0, 0), q(-0.05, 0, 0), q(0.06, 0, 0)), [0, 0.55, 1.1]),
    quatTrack("PACUS_RIG_ROOT/spine/left_arm_1.quaternion", values(q(0, 0, 0.45), q(0, 0, -0.45), q(0, 0, 0.45)), [0, 0.55, 1.1]),
    quatTrack("PACUS_RIG_ROOT/spine/right_arm_1.quaternion", values(q(0, 0, -0.45), q(0, 0, 0.45), q(0, 0, -0.45)), [0, 0.55, 1.1]),
    quatTrack("PACUS_RIG_ROOT/spine/left_arm_2.quaternion", values(q(0, 0, -0.28), q(0, 0, 0.28), q(0, 0, -0.28)), [0, 0.55, 1.1]),
    quatTrack("PACUS_RIG_ROOT/spine/right_arm_2.quaternion", values(q(0, 0, 0.28), q(0, 0, -0.28), q(0, 0, 0.28)), [0, 0.55, 1.1]),
  ]);

  // Nado mais vigoroso: amplitude maior + um pouco mais rapido, pra ficar
  // claramente animado em vez de so balancar de leve.
  clips.swim = new THREE.AnimationClip("swim", 1.4, [
    quatTrack("PACUS_RIG_ROOT/tail.quaternion", values(q(0, 0.32, 0), q(0, -0.32, 0), q(0, 0.32, 0)), [0, 0.7, 1.4]),
    quatTrack("PACUS_RIG_ROOT/tail/tail_02.quaternion", values(q(0, -0.4, 0), q(0, 0.4, 0), q(0, -0.4, 0)), [0, 0.7, 1.4]),
    quatTrack("PACUS_RIG_ROOT/tail/tail_02/tail_03.quaternion", values(q(0, 0.5, 0), q(0, -0.5, 0), q(0, 0.5, 0)), [0, 0.7, 1.4]),
    quatTrack("PACUS_RIG_ROOT/spine.quaternion", values(q(0, 0.06, 0), q(0, -0.06, 0), q(0, 0.06, 0)), [0, 0.7, 1.4]),
  ]);

  clips.happy = new THREE.AnimationClip("happy", 0.95, [
    vecTrack("PACUS_RIG_ROOT/root.position", [0, 0.18, 0, 0, 0.32, 0, 0, 0.18, 0, 0, 0.28, 0, 0, 0.18, 0], [0, 0.2, 0.4, 0.6, 0.95]),
    quatTrack("PACUS_RIG_ROOT/head.quaternion", values(q(0, 0, 0), q(-0.18, 0, 0), q(0, 0, 0)), [0, 0.22, 0.5]),
    quatTrack("PACUS_RIG_ROOT/spine/left_arm_1.quaternion", values(q(0, 0, 0), q(0, 0, 0.25), q(0, 0, 0)), [0, 0.48, 0.95]),
    quatTrack("PACUS_RIG_ROOT/spine/right_arm_1.quaternion", values(q(0, 0, 0), q(0, 0, -0.25), q(0, 0, 0)), [0, 0.48, 0.95]),
  ]);

  clips.sleep = new THREE.AnimationClip("sleep", 2.8, [
    quatTrack("PACUS_RIG_ROOT/head.quaternion", values(q(0, 0, 0), q(0.28, 0, 0), q(0, 0, 0)), [0, 1.4, 2.8]),
    quatTrack("PACUS_RIG_ROOT/spine.quaternion", values(q(0, 0, 0), q(-0.05, 0, 0), q(0, 0, 0)), [0, 1.4, 2.8]),
  ]);

  clips.wave = new THREE.AnimationClip("wave", 1.4, [
    quatTrack("PACUS_RIG_ROOT/spine/right_arm_1.quaternion", values(q(0, 0, 0), q(0, 0, -0.65), q(0, 0, 0.65), q(0, 0, 0)), [0, 0.35, 0.7, 1.4]),
    quatTrack("PACUS_RIG_ROOT/head.quaternion", values(q(0, 0, 0), q(0, 0.12, 0), q(0, 0, 0)), [0, 0.7, 1.4]),
  ]);

  return clips;
}

export function prepareLoadedPacusCharacter(character, gltf, pacus = {}) {
  const mixer = new THREE.AnimationMixer(character);
  const actions = Object.fromEntries((gltf?.animations ?? []).map((clip) => {
    const safeName = clip.name.toLowerCase();
    const normalizedName = safeName === "idle" ? "gentle" : safeName;
    return [normalizedName, mixer.clipAction(clip)];
  }));

  const nodes = {};
  character.traverse((object) => {
    if (object.name) nodes[object.name] = object;
  });

  character.userData.mixer = mixer;
  character.userData.actions = actions;
  character.userData.skeleton = nodes.PACUS_Skeleton ?? null;
  character.userData.bones = nodes;
  character.userData.parts = {
    torso: nodes.body_skin,
    headMesh: nodes.head,
    tailFin: nodes.tail_3,
    eggShell: nodes.eggShell,
    bodyMeshes: [],
  };

  character.traverse((object) => {
    if (!object.isMesh || object.name === "eggShell") return;
    character.userData.parts.bodyMeshes.push(object);
    object.castShadow = true;
    object.receiveShadow = true;
  });

  character.userData.asset = "glb";
  applyPacusPalette(character, pacus);
  setPacusStage(character, pacus?.stage ?? "baby");
  return character;
}

export function createPacusCharacter(stage = "baby") {
  const character = createRiggedAxolotl();
  setPacusStage(character, stage);
  character.rotation.y = 0;
  return character;
}

export function setPacusStage(character, stage) {
  const normalized = normalizeStage(stage);
  character.userData.stage = normalized;
  character.userData.baseScale = PACUS_STAGE_SCALE[normalized] ?? 0.68;
  character.scale.setScalar(character.userData.baseScale);
  character.position.y = normalized === "egg" ? -0.42 : -0.08;

  // Tamanho real de referência (cm) do estágio atual — ver docs/pacus-dimensionamento-3d.md.
  // Metadado para a UI (ex.: badge "16 cm"); não altera a escala de cena acima.
  character.userData.sizeCm = getPacusStageSizeCm(normalized);

  // Cabeça escala à parte do corpo (proporção de filhote vs. adulto).
  const headBone = character.userData.bones?.head;
  if (headBone) {
    const headScale = PACUS_HEAD_SCALE[normalized] ?? 1.0;
    headBone.scale.setScalar(headScale);
  }

  const parts = character.userData.parts ?? {};
  const isEgg = normalized === "egg" || normalized === "cracking";
  if (parts.eggShell) parts.eggShell.visible = isEgg;
  if (Array.isArray(parts.bodyMeshes) && parts.bodyMeshes.length) {
    parts.bodyMeshes.forEach((mesh) => { mesh.visible = !isEgg; });
  } else {
    if (parts.torso) parts.torso.parent.visible = !isEgg;
    if (parts.headMesh) parts.headMesh.parent.visible = !isEgg;
    if (parts.tailFin) parts.tailFin.parent.visible = !isEgg;
  }
}

export function playPacusAnimation(character, name, { loop = THREE.LoopRepeat, repetitions = Infinity, fade = 0.22 } = {}) {
  const actions = character?.userData?.actions;
  if (!actions?.[name]) return null;

  Object.entries(actions).forEach(([key, action]) => {
    if (key !== name && action.isRunning()) action.fadeOut(fade);
  });

  const action = actions[name];
  action.reset().setLoop(loop, repetitions).fadeIn(fade).play();
  return action;
}

export function setPacusIdle(character) {
  playPacusAnimation(character, "gentle", { loop: THREE.LoopRepeat });
  playPacusAnimation(character, "swim", { loop: THREE.LoopRepeat, fade: 0.12 });
}

export function disposePacusCharacter(character) {
  character?.traverse((object) => {
    if (object.geometry) object.geometry.dispose();
    if (object.material) {
      if (Array.isArray(object.material)) object.material.forEach((material) => material.dispose());
      else object.material.dispose();
    }
  });
  character?.userData?.mixer?.stopAllAction();
}

export function configureSoftLighting(scene) {
  scene.add(new THREE.HemisphereLight(0xdffcf9, 0x18312f, 1.7));
  const key = new THREE.DirectionalLight(0xffffff, 2.2);
  key.position.set(1.5, 2.7, 2.2);
  key.castShadow = true;
  key.shadow.mapSize.set(512, 512);
  scene.add(key);

  // Luz de preenchimento suave do lado oposto: sem ela o lado escuro do
  // corpo (guelras/bochechas/braços) fica achatado e sem volume.
  const fill = new THREE.DirectionalLight(0xbfe9ff, 0.65);
  fill.position.set(-2.0, 0.6, 1.4);
  scene.add(fill);

  // Leve luz de contorno por tras, pra separar o personagem do fundo do
  // tanque (dá aquele "brilho" de silhueta que ajuda a leitura da forma).
  const rim = new THREE.DirectionalLight(0xfff2da, 0.5);
  rim.position.set(0, 1.2, -2.4);
  scene.add(rim);
}

// Habitat fixo (não varia por estágio — ver HABITAT_DIMENSIONS_CM em
// dimensions.js e a seção 3 do docs/pacus-dimensionamento-3d.md). Os raios
// abaixo são derivados do diâmetro real do tanque (80cm) via
// cmToSceneUnits, em vez de números "no olho": o resultado é
// intencionalmente igual ao valor que já existia aqui antes (1.78
// unidades para o raio da água), então o visual não muda — só passa a
// ter uma régua real por trás.
export function addHabitatDecor(scene) {
  const waterRadius = cmToSceneUnits(HABITAT_DIMENSIONS_CM.tankDiameter / 2);
  const floorRadiusTop = waterRadius * 0.955; // leve afunilamento do piso, como no design original
  const floorRadiusBottom = waterRadius * 1.067;
  const floorThickness = cmToSceneUnits(HABITAT_DIMENSIONS_CM.substrateThickness);

  const floor = new THREE.Mesh(
    new THREE.CylinderGeometry(floorRadiusTop, floorRadiusBottom, floorThickness, 32),
    makeMaterial(0x78b89d, 0.9)
  );
  floor.position.y = -0.72;
  floor.receiveShadow = true;
  scene.add(floor);

  const water = new THREE.Mesh(
    new THREE.CylinderGeometry(waterRadius, waterRadius, 0.05, 32),
    new THREE.MeshStandardMaterial({ color: 0x8bd4cf, transparent: true, opacity: 0.25, roughness: 0.25 })
  );
  water.position.y = -0.62;
  scene.add(water);

  const rockMaterial = makeMaterial(0x9eb7ae, 0.95);
  [-1.1, 0.95].forEach((x, index) => {
    const rock = new THREE.Mesh(new THREE.IcosahedronGeometry(index ? 0.28 : 0.22, 1), rockMaterial);
    rock.scale.y = 0.55;
    rock.position.set(x, -0.54, index ? -0.2 : -0.12);
    rock.castShadow = true;
    scene.add(rock);
  });
}

export { THREE };
