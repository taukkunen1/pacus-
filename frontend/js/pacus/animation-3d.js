import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.185.1/build/three.module.js";
import { playPacusAnimation } from "./character.js";

export const PACUS_ANIMATIONS = Object.freeze([
  "gentle",
  "swim",
  "walk",
  "happy",
  "sleep",
  "wave",
]);

export function playAnimation(character, name, options = {}) {
  return playPacusAnimation(character, name, options);
}

export function playOneShot(character, name) {
  return playPacusAnimation(character, name, {
    loop: THREE.LoopOnce,
    repetitions: 1,
    fade: 0.12,
  });
}

export function configureAnimationState(character) {
  character.userData.animationState = { current: "swim", lastInteraction: 0 };
  playAnimation(character, "swim");
  return character.userData.animationState;
}
