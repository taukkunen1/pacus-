// Renderiza o tanque (elemento de assinatura). O visual muda por estagio —
// ver frontend/js/pacus/growth.js para a lista de estagios e
// frontend/css/pacus/habitat.css para as animacoes de cada um.
import { getStageInfo } from "./growth.js";

export function renderTank(pacus) {
  const stage = pacus?.stage ?? "egg";
  const info = getStageInfo(stage);
  const caption = info.isEgg
    ? info.caption
    : pacus?.name
    ? `${escapeHtml(pacus.name)} esta por aqui em algum lugar 👀`
    : info.caption;

  return `
    <div class="pacus-tank" data-stage="${stage}" aria-hidden="true">
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      ${info.isEgg ? renderEgg(stage) : renderCreature(stage)}
      <div class="tank-rock tank-rock--1"></div>
      <div class="tank-rock tank-rock--2"></div>
      <div class="tank-floor"></div>
      <span class="tank-caption">${caption}</span>
    </div>
  `;
}

function renderEgg(stage) {
  return `
    <div class="pacus-egg pacus-egg--${stage}">
      <div class="pacus-egg__shell"></div>
      <div class="pacus-egg__crack pacus-egg__crack--1"></div>
      <div class="pacus-egg__crack pacus-egg__crack--2"></div>
      <div class="pacus-egg__peek"></div>
    </div>
  `;
}

function renderCreature(stage) {
  return `
    <div class="pacus-body pacus-body--${stage}">
      <div class="pacus-body__gill"></div>
      <div class="pacus-body__gill"></div>
      <div class="pacus-body__gill"></div>
      <div class="pacus-body__torso"></div>
    </div>
  `;
}

function escapeHtml(value = "") {
  const div = document.createElement("div");
  div.textContent = String(value);
  return div.innerHTML;
}
