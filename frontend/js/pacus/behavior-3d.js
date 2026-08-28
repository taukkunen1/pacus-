import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.185.1/build/three.module.js";
import { playPacusAnimation } from "./character.js";

export function configureBehavior(character) {
  character.userData.behavior = {
    mood: "calm",
    lastInteractionAt: 0,
  };
  return character.userData.behavior;
}

export function reactToInteraction(character, kind = "tap") {
  const behavior = character.userData.behavior ?? configureBehavior(character);
  behavior.lastInteractionAt = Date.now();
  behavior.mood = kind === "task" ? "happy" : "curious";
  playPacusAnimation(character, kind === "task" ? "happy" : "wave", {
    loop: THREE.LoopOnce,
    repetitions: 1,
    fade: 0.1,
  });
}

export function setMood(character, mood = "calm") {
  const behavior = character.userData.behavior ?? configureBehavior(character);
  behavior.mood = mood;
  const animationByMood = {
    calm: "gentle",
    happy: "happy",
    tired: "sleep",
    curious: "wave",
  };
  playPacusAnimation(character, animationByMood[mood] ?? "gentle");
}
