// Renderiza o tanque (elemento de assinatura). A aparencia muda por estagio:
// egg/cracking/hatching mostram um ovo (com rachaduras progressivas); baby/young/adult
// mostram o corpo nadando, crescendo de tamanho a cada estagio (ver habitat.css).
// A cor do PACUS nasce com ele (ver ./color.js) e so fica mais intensa com o tempo.
import { getPacusColorStyle } from "./color.js";

const STAGE_ORDER = ["egg", "cracking", "hatching", "baby", "young", "adult"];

const STAGE_CAPTIONS = {
  egg: "Um ovo bem quietinho no fundo do tanque...",
  cracking: "Alguma coisa esta se mexendo ai dentro!",
  hatching: "O ovo esta rachando... quase la!",
  baby: "Pacus acabou de nascer, ainda pequenininho.",
  young: "Pacus esta crescendo bem por aqui 👀",
  adult: "Pacus esta por aqui em algum lugar 👀",
};

function normalizeStage(stage) {
  const value = String(stage ?? "egg").toLowerCase();
  return STAGE_ORDER.includes(value) ? value : "egg";
}

export function renderTank(pacus, stageOverride) {
  const normalizedStage = normalizeStage(stageOverride ?? pacus?.stage);
  const colorStyle = getPacusColorStyle(pacus, normalizedStage);
  const isEggPhase = normalizedStage === "egg" || normalizedStage === "cracking" || normalizedStage === "hatching";

  const creature = isEggPhase
    ? `<div class="pacus-egg"></div>`
    : `
      <div class="pacus-body">
        <svg class="pacus-critter" viewBox="0 0 128 64" xmlns="http://www.w3.org/2000/svg">
          <!-- cauda em forma de remo, achatada -->
          <path class="pacus-critter__tail" d="M4 32 C 8 16, 28 16, 36 32 C 28 48, 8 48, 4 32 Z"></path>

          <!-- corpo, ja afunilando pra cabeca larga do lado direito -->
          <path class="pacus-critter__body" d="M22 32 C 22 8, 58 4, 80 12 C 96 18, 100 46, 80 52 C 58 60, 22 56, 22 32 Z"></path>

          <!-- cabeca larga e arredondada, marca registrada do axolote -->
          <ellipse class="pacus-critter__head" cx="90" cy="32" rx="21" ry="20"></ellipse>

          <!-- perninhas atarracadas -->
          <ellipse class="pacus-critter__leg" cx="38" cy="13" rx="7" ry="4.5" transform="rotate(-18 38 13)"></ellipse>
          <ellipse class="pacus-critter__leg" cx="38" cy="51" rx="7" ry="4.5" transform="rotate(18 38 51)"></ellipse>
          <ellipse class="pacus-critter__leg" cx="74" cy="10" rx="7.5" ry="5" transform="rotate(-14 74 10)"></ellipse>
          <ellipse class="pacus-critter__leg" cx="74" cy="54" rx="7.5" ry="5" transform="rotate(14 74 54)"></ellipse>

          <!-- guelras externas em leque, de um lado da cabeca -->
          <g class="pacus-critter__gill pacus-critter__gill--1">
            <path d="M96 18 C 104 10, 110 6, 114 -2"></path>
            <circle cx="114" cy="-2" r="3"></circle>
            <circle cx="107" cy="3" r="2.4"></circle>
            <circle cx="101" cy="9" r="2"></circle>
          </g>
          <g class="pacus-critter__gill pacus-critter__gill--2">
            <path d="M100 16 C 110 11, 117 7, 122 0"></path>
            <circle cx="122" cy="0" r="3"></circle>
            <circle cx="114" cy="4" r="2.4"></circle>
            <circle cx="107" cy="9" r="2"></circle>
          </g>
          <g class="pacus-critter__gill pacus-critter__gill--3">
            <path d="M101 22 C 111 20, 118 17, 124 12"></path>
            <circle cx="124" cy="12" r="3"></circle>
            <circle cx="116" cy="15" r="2.4"></circle>
            <circle cx="109" cy="18" r="2"></circle>
          </g>

          <!-- rostinho: olhos e sorriso -->
          <circle class="pacus-critter__eye" cx="87" cy="21" r="2.3"></circle>
          <circle class="pacus-critter__eye" cx="97" cy="20" r="2.3"></circle>
          <path class="pacus-critter__mouth" d="M85 36 Q 92 41 100 35"></path>
        </svg>
      </div>
    `;

  return `
    <div class="pacus-tank pacus-tank--${normalizedStage}" style="${colorStyle}" aria-hidden="true">
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>

      <div class="tank-cave">
        <div class="tank-cave__opening"></div>
      </div>
      <div class="tank-driftwood"></div>

      ${creature}

      <div class="tank-plant tank-plant--left">
        <span class="tank-plant__blade"></span>
        <span class="tank-plant__blade"></span>
        <span class="tank-plant__blade"></span>
      </div>
      <div class="tank-plant tank-plant--right">
        <span class="tank-plant__blade"></span>
        <span class="tank-plant__blade"></span>
        <span class="tank-plant__blade"></span>
      </div>

      <div class="tank-rock tank-rock--1"></div>
      <div class="tank-rock tank-rock--2"></div>
      <div class="tank-floor"></div>
      <span class="tank-caption">${STAGE_CAPTIONS[normalizedStage]}</span>
    </div>
  `;
}
