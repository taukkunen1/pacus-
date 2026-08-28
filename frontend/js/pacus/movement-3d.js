export function configureMovement(character, options = {}) {
  const state = {
    speed: options.speed ?? 0.55,
    direction: 1,
    phase: Math.random() * Math.PI * 2,
    roam: options.roam ?? true,
  };
  character.userData.movementState = state;
  return state;
}

export function updateMovement(character, delta, elapsed) {
  const state = character?.userData?.movementState;
  if (!state) return;

  state.phase += delta * state.speed;
  const sway = state.roam ? Math.sin(state.phase) * 0.82 : 0;
  character.position.x = sway;
  character.position.z = Math.cos(state.phase * 0.55) * 0.18;
  character.position.y += Math.sin(elapsed * 2.0) * 0.0009;
  character.rotation.y = Math.sin(state.phase * 0.55) * 0.28;
}

export function stopMovement(character) {
  if (character?.userData?.movementState) character.userData.movementState.roam = false;
}
