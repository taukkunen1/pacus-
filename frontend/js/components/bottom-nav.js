// Barra de navegação inferior compartilhada entre as telas. Antes so existia
// dentro de home.js (a tela "Hoje"), entao as outras telas (Histórico, Pontos,
// PACUS, Loja) so tinham um botao "Hoje" no cabecalho pra voltar -- sem jeito
// de ir direto de uma tela pra outra sem passar por "Hoje" no meio. Esse
// componente centraliza a lista de abas num lugar so, pra toda tela poder
// incluir a mesma barra (com a aba certa marcada como ativa) e nao precisar
// duplicar/dessincronizar a lista de novo.
import { appState } from "../state/app-state.js";

const NAV_ITEMS = [
  { key: "today", label: "Hoje" },
  { key: "history", label: "Histórico" },
  { key: "points", label: "Pontos" },
  { key: "pacus", label: "PACUS" },
  { key: "store", label: "Loja" },
  // So aparece pro adulto (ver screens/settings.js) -- o painel de
  // configuracoes da familia que morava dentro da aba PACUS ganhou aba
  // propria, e a crianca nem precisa ver essa opcao na barra.
  { key: "settings", label: "Config", adultOnly: true }
];

// `badges` e opcional: { [navKey]: count }. So aparece um numerozinho no canto
// da aba quando count > 0 -- ex. tarefas de hoje ainda pendentes (crianca) ou
// resgates aguardando aprovacao (adulto). Cada tela calcula os proprios numeros
// com os dados que ja carregou (nao faz nenhuma chamada nova aqui).
export function renderBottomNav(activeKey, badges = {}) {
  const isAdult = (appState.user?.role ?? "").toLowerCase() === "adult";
  const items = NAV_ITEMS.filter((item) => !item.adultOnly || isAdult);

  return `
    <nav class="bottom-nav" aria-label="Navegação principal">
      ${items.map((item) => {
        const count = Number(badges[item.key]) || 0;
        return `
          <button
            data-nav="${item.key}"
            class="${item.key === activeKey ? "is-active" : ""}"
            type="button"
          >
            ${item.label}
            ${count > 0 ? `<span class="nav-badge">${count > 99 ? "99+" : count}</span>` : ""}
          </button>
        `;
      }).join("")}
    </nav>
  `;
}

// Liga os cliques da barra dentro de `container` (chamar depois de inserir o
// HTML de renderBottomNav no DOM). `navigate` e a mesma funcao de roteamento
// que app.js ja passa pra cada tela.
export function attachBottomNav(container, navigate) {
  container.querySelectorAll("[data-nav]").forEach((button) => {
    button.addEventListener("click", () => {
      location.hash = button.dataset.nav;
      navigate(button.dataset.nav);
    });
  });
}
