import { mountPacus3D } from "./renderer.js";

export function renderTank(pacus = {}) {
  const stage = String(pacus.stage ?? "baby");
  const label = stageLabel(stage);
  return `
    <section class="pacus-tank pacus-tank--3d" data-pacus-stage="${escapeHtml(stage)}" aria-label="Habitat 3D do PACUS">
      <div class="pacus-3d-canvas" data-pacus-3d></div>
      <div class="pacus-waterline" aria-hidden="true"></div>
      <div class="pacus-bubbles" aria-hidden="true">
        <span></span><span></span><span></span><span></span>
      </div>
      <div class="pacus-overlay">
        <span class="pacus-stage-pill">${label}</span>
        <span class="pacus-interaction-hint">Toque no PACUS</span>
      </div>
    </section>
  `;
}

export function mountTank3D(root, pacus = {}) {
  const host = root?.querySelector?.("[data-pacus-3d]");
  if (!host) return null;
  return mountPacus3D(host, pacus);
}

function stageLabel(stage) {
  const value = stage.toLowerCase();
  if (value.includes("egg")) return "Ovo";
  if (value.includes("crack")) return "Rachando";
  if (value.includes("hatch")) return "Nascendo";
  if (value.includes("baby")) return "Filhote";
  if (value.includes("young")) return "Jovem";
  if (value.includes("adult")) return "Adulto";
  return "Filhote";
}

function escapeHtml(value = "") {
  const div = document.createElement("div");
  div.textContent = value;
  return div.innerHTML;
}
