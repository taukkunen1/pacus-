// Ponto de entrada do modulo PACUS: junta habitat (markup), movement e
// behavior (comportamento) num unico ciclo mount/cleanup para as telas.
//
// Uso tipico dentro de uma tela:
//   let cleanupPacus = () => {};
//   function draw() {
//     cleanupPacus();
//     content.innerHTML = `... ${renderTank(pacus)} ...`;
//     cleanupPacus = mountPacusBehavior(content, pacus?.stage);
//   }
import { renderTank } from "./habitat.js";
import { attachMovement } from "./movement.js";
import { attachBehavior } from "./behavior.js";

export { renderTank };

// Liga natacao/comportamento ao `.pacus-tank` que ja esta dentro de
// `rootEl` (renderizado via renderTank()). Retorna uma funcao de cleanup —
// chame-a sempre antes do proximo render, para nao acumular timers.
export function mountPacusBehavior(rootEl, stage = "egg") {
  const tankEl = rootEl?.querySelector?.(".pacus-tank");
  if (!tankEl) return () => {};

  const cleanupMovement = attachMovement(tankEl, stage);
  const cleanupBehavior = attachBehavior(tankEl, stage);

  return () => {
    cleanupMovement();
    cleanupBehavior();
  };
}
