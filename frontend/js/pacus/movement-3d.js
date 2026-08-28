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
  // Camera mais proxima agora (ver renderer.js) — amplitude horizontal um
  // pouco menor pra nao sair do enquadramento, mas com mais vida no
  // giro/flutuacao vertical, que e o que mais "le" como nado animado.
  const sway = state.roam ? Math.sin(state.phase) * 0.5 : 0;
  character.position.x = sway;
  character.position.z = Math.cos(state.phase * 0.55) * 0.14;
  character.position.y += Math.sin(elapsed * 1.6) * 0.006;
  character.rotation.y = Math.sin(state.phase * 0.5) * 0.42;
  character.rotation.z = Math.sin(state.phase * 0.7 + 1) * 0.05;
}

export function stopMovement(character) {
  if (character?.userData?.movementState) character.userData.movementState.roam = false;
}
