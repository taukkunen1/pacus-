// Renderiza o tanque (elemento de assinatura). A aparencia muda por estagio:
// egg/cracking/hatching mostram um ovo (com rachaduras progressivas); baby/young/adult
// mostram o corpo nadando, crescendo de tamanho a cada estagio (ver habitat.css).

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

export function renderTank(stage) {
  const normalizedStage = normalizeStage(stage);
  const isEggPhase = normalizedStage === "egg" || normalizedStage === "cracking" || normalizedStage === "hatching";

  const creature = isEggPhase
    ? `<div class="pacus-egg"></div>`
    : `
      <div class="pacus-body">
        <div class="pacus-body__gill"></div>
        <div class="pacus-body__gill"></div>
        <div class="pacus-body__gill"></div>
        <div class="pacus-body__torso"></div>
      </div>
    `;

  return `
    <div class="pacus-tank pacus-tank--${normalizedStage}" aria-hidden="true">
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      ${creature}
      <div class="tank-rock tank-rock--1"></div>
      <div class="tank-rock tank-rock--2"></div>
      <div class="tank-floor"></div>
      <span class="tank-caption">${STAGE_CAPTIONS[normalizedStage]}</span>
    </div>
  `;
}
