// Habitat 2D do PACUS: usa sprites reais recortados do pacus-reference.png
// (ver docs/pacus-dimensionamento-3d.md e a conversa que levou a essa
// decisão) em vez do rig 3D em Three.js, que nao tinha como chegar perto do
// acabamento do PNG de referencia sem uma ferramenta de modelagem 3D de
// verdade. Cada estagio tem seu proprio sprite; a "vida"/movimento vem de
// animacoes CSS (boiar, balancar, nadar de um lado a outro) definidas em
// css/pacus/habitat.css.

const STAGE_ORDER = ["egg", "cracking", "hatching", "baby", "young", "adult"];

// cracking reusa o sprite do ovo (ainda nao eclodiu) — soma com a classe
// pacus-tank--cracking, que da o tremelique extra via CSS.
const STAGE_SPRITE = {
  egg: "egg.png",
  cracking: "egg.png",
  hatching: "hatching.png",
  baby: "baby.png",
  young: "young.png",
  adult: "adult.png",
};

const STAGE_LABEL = {
  egg: "Ovo",
  cracking: "Rachando",
  hatching: "Nascendo",
  baby: "Filhote",
  young: "Jovem",
  adult: "Adulto",
};

function normalizeStage(stage) {
  const value = String(stage ?? "baby").trim().toLowerCase();
  if (value.includes("egg") || value.includes("ovo")) return "egg";
  if (value.includes("crack") || value.includes("rach")) return "cracking";
  if (value.includes("hatch") || value.includes("eclos") || value.includes("nasc")) return "hatching";
  if (value.includes("baby") || value.includes("filh")) return "baby";
  if (value.includes("young") || value.includes("jov")) return "young";
  if (value.includes("adult")) return "adult";
  return STAGE_ORDER.includes(value) ? value : "baby";
}

const SPRITE_BASE_URL = new URL("../../assets/pacus/stages/", import.meta.url).href;

export function renderTank(pacus = {}) {
  const stage = normalizeStage(pacus?.stage);
  const label = STAGE_LABEL[stage];
  const spriteUrl = `${SPRITE_BASE_URL}${STAGE_SPRITE[stage]}`;

  return `
    <section class="pacus-tank pacus-tank--${stage}" data-pacus-stage="${stage}" aria-label="Habitat do PACUS">
      <div class="pacus-waterline" aria-hidden="true"></div>
      <div class="pacus-bubbles" aria-hidden="true">
        <span></span><span></span><span></span><span></span>
      </div>

      <div class="pacus-sprite-wrap" data-pacus-sprite-wrap>
        <div class="pacus-sprite-shadow" aria-hidden="true"></div>
        <img
          class="pacus-sprite"
          data-pacus-sprite
          src="${spriteUrl}"
          alt="PACUS - estagio ${label}"
          draggable="false"
        />
      </div>

      <div class="pacus-overlay">
        <span class="pacus-stage-pill">${label}</span>
        <span class="pacus-interaction-hint">Toque no PACUS</span>
      </div>
    </section>
  `;
}

// Liga a interacao de toque (um "pulinho" feliz) ao tanque ja renderizado.
// Retorna uma funcao de cleanup — chame antes do proximo render pra nao
// acumular listeners.
export function mountTankInteraction(root, pacus = {}) {
  const tankEl = root?.querySelector?.(".pacus-tank");
  const spriteEl = tankEl?.querySelector?.("[data-pacus-sprite]");
  if (!tankEl || !spriteEl) return () => {};

  let bounceTimeout = null;

  function onTap() {
    spriteEl.classList.remove("pacus-sprite--bounce");
    // forca reflow pra poder retrigar a mesma animacao em toques seguidos
    void spriteEl.offsetWidth;
    spriteEl.classList.add("pacus-sprite--bounce");
    window.clearTimeout(bounceTimeout);
    bounceTimeout = window.setTimeout(() => {
      spriteEl.classList.remove("pacus-sprite--bounce");
    }, 650);
  }

  tankEl.addEventListener("pointerdown", onTap);

  // Retorna um objeto com .dispose() (mesma forma que o antigo runtime do
  // Three.js tinha) pra nao precisar mudar as telas que ja chamam
  // `pacusRuntime?.dispose()`.
  return {
    dispose() {
      tankEl.removeEventListener("pointerdown", onTap);
      window.clearTimeout(bounceTimeout);
    },
  };
}

// Mantem o nome antigo (mountTank3D) como alias, pra nao precisar tocar em
// todo mundo que ja importa esse nome nas telas — so muda o que ele faz.
export const mountTank3D = mountTankInteraction;
