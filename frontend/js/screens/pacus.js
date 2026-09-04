import { apiClient } from "../api/api-client.js";
import { renderTank } from "../pacus/habitat.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";

// O painel do adulto (fuso horario, codigo de recuperacao, PIN da crianca,
// calendario de crescimento) morava aqui misturado com a tela do bichinho.
// Virou aba propria "Config" (ver screens/settings.js e
// components/bottom-nav.js) -- esta tela agora e so o PACUS em si: estagio,
// estatisticas e historico de estagios anteriores.
export async function renderPacus(root, navigate) {
  root.innerHTML = `
    <div class="screen">
      <div class="container">
        <p class="task-empty">Carregando PACUS...</p>
      </div>
    </div>
  `;

  const content = root.querySelector(".container");

  let pacus = null;

  try {
    pacus = await apiClient("/pacus/me");
  } catch (err) {
    console.warn(
      "PACUS nao encontrado. A tela continuara disponivel sem o estagio do PACUS.",
      err
    );
  }

  draw();

  function draw() {
    content.innerHTML = `
      <div class="screen-header">
        <div>
          <p class="eyebrow">MEU COMPANHEIRO</p>
          <h1>${escapeHtml(pacus?.name || "Pacus")}</h1>
        </div>

        <button class="btn btn-ghost" id="back">
          Hoje
        </button>
      </div>

      ${renderTank(pacus)}

      <section class="pacus-stats">
        <div>
          <strong>
            ${escapeHtml(String(pacus?.stage ?? "juvenile"))}
          </strong>
          <span>estágio</span>
        </div>

        <div>
          <strong>
            ${pacus?.totalClosedDays ?? 0}
          </strong>
          <span>dias vividos</span>
        </div>

        <div>
          <strong>
            ${Number(pacus?.size ?? 0).toFixed(1)}
          </strong>
          <span>tamanho</span>
        </div>
      </section>

      ${
        pacus
          ? ""
          : `
            <div class="error-text">
              O registro do PACUS desta família não foi encontrado.
              O gerenciamento das tarefas continua disponível.
            </div>
          `
      }

      ${pacus?.stageHistory?.length ? `
        <section class="task-management">
          <div class="screen-header">
            <div>
              <p class="eyebrow">HISTÓRICO</p>
              <h2>Estágios anteriores</h2>
            </div>
          </div>

          <div id="stage-history-list">
            ${renderStageHistory(pacus.stageHistory)}
          </div>
        </section>
      ` : ""}

      ${renderBottomNav("pacus")}
    `;

    content
      .querySelector("#back")
      ?.addEventListener(
        "click",
        () => navigate("today")
      );

    attachBottomNav(content, navigate);
  }

  function renderStageHistory(history) {
    return [...history]
      .sort((a, b) => new Date(b.reachedAt) - new Date(a.reachedAt))
      .map(
        (entry) => `
          <div class="task-card">
            <div class="task-card__content">
              <strong class="task-title">${stageLabel(entry.stage)}</strong>
              <span class="task-description">alcançado em ${new Date(entry.reachedAt).toLocaleDateString("pt-BR")}</span>
            </div>
          </div>
        `
      )
      .join("");
  }

  function stageLabel(stage) {
    const labels = {
      egg: "Ovo",
      cracking: "Rachando",
      hatching: "Eclodindo",
      baby: "Filhote",
      young: "Jovem",
      adult: "Adulto"
    };
    return labels[String(stage).toLowerCase()] ?? escapeHtml(String(stage));
  }
}

function escapeHtml(value = "") {
  const div =
    document.createElement("div");

  div.textContent = value;

  return div.innerHTML;
}
