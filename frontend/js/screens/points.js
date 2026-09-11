import { getPoints, getPointTransactions } from "../api/points-api.js";
import { getWeeklyAutonomyReport } from "../api/autonomy-api.js";
import { formatBrl } from "../utils/format.js";
import { showToast } from "../components/toast.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";

const PAGE_SIZE = 20;

export async function renderPoints(root, navigate) {
  root.innerHTML = `<div class="screen"><div class="container"><p class="task-empty">Carregando Pacus Points...</p></div></div>`;
  const content = root.querySelector(".container");

  let balance;
  let transactions = [];
  let page = 1;
  let totalPages = 1;
  // Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md), item 6:
  // opcional -- se a chamada falhar (ex. sessao antiga sem esse endpoint ainda
  // no ar), a tela dos pontos continua funcionando normalmente sem essa seção.
  let autonomy = null;

  try {
    const [balanceResult, transactionsResult, autonomyResult] = await Promise.all([
      getPoints(),
      getPointTransactions({ page, pageSize: PAGE_SIZE }),
      getWeeklyAutonomyReport().catch(() => null)
    ]);
    balance = balanceResult;
    transactions = transactionsResult.items ?? [];
    totalPages = transactionsResult.totalPages ?? 1;
    autonomy = autonomyResult;
  } catch (err) {
    content.innerHTML = `<p class="error-text">Não foi possível carregar os pontos: ${err.message}</p>`;
    return;
  }

  function draw() {
    content.innerHTML = `
      <div class="screen-header"><div><p class="eyebrow">RECOMPENSAS</p><h1>Pacus Points</h1></div><button class="btn btn-ghost" id="back">Hoje</button></div>
      <section class="points-hero"><strong>${balance.balance}</strong><span>Pacus Points</span><small>${formatBrl(balance.brl)}</small></section>
      ${renderAutonomySection(autonomy)}
      <h2>Movimentações</h2>
      <div class="history-list">${transactions.length ? transactions.map(t => `<div class="history-day"><span><strong>${escapeHtml(t.taskTitle || t.reason || t.type)}</strong><small>${t.date || ""}</small></span><strong>${t.points > 0 ? "+" : ""}${t.points} PP</strong></div>`).join("") : `<p class="task-empty">Nenhuma movimentação.</p>`}</div>
      ${page < totalPages ? `<button class="btn btn-ghost" id="load-more">Carregar mais</button>` : ""}
      ${renderBottomNav("points")}`;
    content.querySelector("#back")?.addEventListener("click", () => navigate("today"));
    attachBottomNav(content, navigate);
    content.querySelector("#load-more")?.addEventListener("click", async (event) => {
      const button = event.currentTarget;
      button.disabled = true;
      try {
        const next = await getPointTransactions({ page: page + 1, pageSize: PAGE_SIZE });
        transactions = transactions.concat(next.items ?? []);
        page += 1;
        totalPages = next.totalPages ?? totalPages;
        draw();
      } catch (err) {
        button.disabled = false;
        showToast(err.message, { error: true });
      }
    });
  }

  draw();
}
function escapeHtml(value = "") { const d = document.createElement("div"); d.textContent = value; return d.innerHTML; }

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md), item 6:
// "feedback de autonomia" -- mostra evolução de independência, não só quantidade
// de tarefas concluídas. Silencioso (retorna string vazia) quando o relatório
// não pôde ser carregado, ou quando ainda não há nenhuma tarefa autodeclarada
// nesta semana (todos os contadores zerados) -- não faz sentido mostrar uma
// seção inteira de zeros pra uma família que ainda não usou o recurso.
function renderAutonomySection(autonomy) {
  if (!autonomy) return "";

  const total =
    autonomy.selfStarted + autonomy.promptedByPacus + autonomy.promptedByAdult;
  if (total === 0) return "";

  const previousTotal =
    autonomy.previousSelfStarted + autonomy.previousPromptedByPacus + autonomy.previousPromptedByAdult;

  // Tendência simples: proporção de "sozinho" sobre o total desta semana vs a
  // anterior -- só mostra a frase quando dá pra comparar (semana anterior teve
  // alguma tarefa autodeclarada também).
  let trendPhrase = "";
  if (previousTotal > 0) {
    const currentRatio = autonomy.selfStarted / total;
    const previousRatio = autonomy.previousSelfStarted / previousTotal;
    if (currentRatio > previousRatio) {
      trendPhrase = "Você está precisando de menos ajuda para cuidar da sua rotina.";
    } else if (currentRatio < previousRatio) {
      trendPhrase = "Essa semana teve mais lembretes que o normal — tudo bem, dias diferentes pedem coisas diferentes.";
    }
  }

  return `
    <section class="autonomy-report">
      <h2>Sua autonomia esta semana</h2>
      <ul class="autonomy-report__list">
        <li><span aria-hidden="true">🟢</span> ${autonomy.selfStarted} tarefa${autonomy.selfStarted === 1 ? "" : "s"} iniciada${autonomy.selfStarted === 1 ? "" : "s"} sozinho</li>
        <li><span aria-hidden="true">🟡</span> ${autonomy.promptedByPacus} iniciada${autonomy.promptedByPacus === 1 ? "" : "s"} após lembrete do PACUS</li>
        <li><span aria-hidden="true">🔴</span> ${autonomy.promptedByAdult} precisou${autonomy.promptedByAdult === 1 ? "" : "ram"} de ajuda de um adulto</li>
      </ul>
      ${trendPhrase ? `<p class="autonomy-report__trend">${escapeHtml(trendPhrase)}</p>` : ""}
    </section>
  `;
}
