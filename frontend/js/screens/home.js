import { getTodayRoutine, completeTask, reopenTask, getPointsBalance } from "../api/pacus-api.js";
import { renderTank } from "../pacus/habitat.js";
import { renderTaskSection } from "../components/task-list.js";
import { formatOperationalDate } from "../utils/date.js";
import { formatBrl, periodLabel, typeLabel } from "../utils/format.js";
import { showToast } from "../components/toast.js";

const PERIODS = ["morning", "afternoon", "evening"];
const TYPES = ["mandatory", "expected", "challenge"];

export async function renderHome(root, navigate = () => {}) {
  root.innerHTML = `<div class="screen"><div class="container" id="home-content"><p class="task-empty">Carregando sua rotina...</p></div></div>`;
  const content = root.querySelector("#home-content");

  let routine;
  let balance = { balance: 0, brl: 0 };
  let activePeriod = currentPeriodGuess();

  try {
    [routine, balance] = await Promise.all([getTodayRoutine(), getPointsBalance()]);
  } catch (err) {
    content.innerHTML = `<p class="error-text">Nao foi possivel carregar a rotina de hoje: ${err.message}</p>`;
    return;
  }

  function totalTasks() {
    return routine.tasks.filter((t) => !t.deletedAt).length;
  }
  function doneTasks() {
    return routine.tasks.filter((t) => t.status === "done").length;
  }

  function tasksFor(period, type) {
    return routine.tasks.filter((t) => !t.deletedAt && t.period === period && t.type === type);
  }

  function draw() {
    const total = totalTasks();
    const done = doneTasks();
    const pct = total === 0 ? 0 : Math.round((done / total) * 100);
    const circumference = 2 * Math.PI * 22;
    const offset = circumference - (pct / 100) * circumference;

    content.innerHTML = `
      <div class="progress-header">
        <div>
          <p class="progress-header__date">${formatOperationalDate(routine.date)}</p>
          <h1 class="progress-header__title">Hoje</h1>
        </div>
        <div class="progress-ring">
          <svg width="56" height="56" viewBox="0 0 56 56">
            <circle class="progress-ring__track" cx="28" cy="28" r="22"></circle>
            <circle class="progress-ring__value" cx="28" cy="28" r="22"
              stroke-dasharray="${circumference}" stroke-dashoffset="${offset}"></circle>
          </svg>
          <span class="progress-ring__label">${pct}%</span>
        </div>
      </div>

      ${renderTank()}

      <div class="period-tabs" role="tablist">
        ${PERIODS.map(
          (p) => `<button type="button" class="period-tab ${p === activePeriod ? "is-active" : ""}" data-period="${p}" role="tab">${periodLabel(p)}</button>`
        ).join("")}
      </div>

      <div id="task-sections">
        ${TYPES.map((type) => renderTaskSection(typeLabel(type), tasksFor(activePeriod, type))).join("")}
      </div>

      <div class="task-actions-bar">
        <button class="btn btn-primary" id="add-task">+ Nova tarefa</button>
        <button class="btn btn-ghost" id="reorder-help">↕ Reordenar</button>
      </div>

      <div class="points-footer">
        <div>
          <p class="points-footer__balance">${balance.balance} PP</p>
          <p class="points-footer__brl">${formatBrl(balance.brl)}</p>
        </div>
      </div>
      <nav class="bottom-nav" aria-label="Navegação principal">
        <button data-nav="today" class="is-active">Hoje</button>
        <button data-nav="history">Histórico</button>
        <button data-nav="points">Pontos</button>
        <button data-nav="pacus">PACUS</button>
      </nav>
    `;

    attachHandlers();
  }

  function attachHandlers() {
    content.querySelectorAll(".period-tab").forEach((btn) => {
      btn.addEventListener("click", () => {
        activePeriod = btn.dataset.period;
        draw();
      });
    });

    content.querySelectorAll("[data-nav]").forEach((btn) => {
      btn.addEventListener("click", () => { location.hash = btn.dataset.nav; navigate(btn.dataset.nav); });
    });

    content.querySelector("#add-task")?.addEventListener("click", async () => {
      const title = window.prompt("Nome da nova tarefa:");
      if (!title?.trim()) return;
      const pointsRaw = window.prompt("Pacus Points (1, 2 ou 3):", "1");
      const points = Number(pointsRaw);
      if (![1, 2, 3].includes(points)) { showToast("A tarefa deve valer 1, 2 ou 3 Pacus Points.", { error: true }); return; }
      try {
        routine = await (await import("../api/tasks-api.js")).createDailyTask({ title: title.trim(), description: null, type: "challenge", period: activePeriod, points });
        draw();
      } catch (err) { showToast(err.message, { error: true }); }
    });

    content.querySelectorAll("[data-task-action=edit]").forEach((btn) => {
      btn.addEventListener("click", async () => {
        const card = btn.closest(".task-card");
        const task = routine.tasks.find(t => t.id === card?.dataset.taskId);
        if (!task) return;
        const title = window.prompt("Nome da tarefa:", task.title);
        if (!title?.trim()) return;
        const points = Number(window.prompt("Pacus Points (1, 2 ou 3):", String(task.points)));
        if (![1, 2, 3].includes(points)) { showToast("A tarefa deve valer 1, 2 ou 3 Pacus Points.", { error: true }); return; }
        try {
          routine = await (await import("../api/tasks-api.js")).updateDailyTask(task.id, { title: title.trim(), description: task.description ?? null, type: task.type, period: task.period, points });
          balance = await getPointsBalance();
          draw();
        } catch (err) { showToast(err.message, { error: true }); }
      });
    });

    content.querySelectorAll("[data-task-action=delete]").forEach((btn) => {
      btn.addEventListener("click", async () => {
        if (!window.confirm("Remover esta tarefa de hoje?")) return;
        const taskId = btn.closest(".task-card")?.dataset.taskId;
        try {
          routine = await (await import("../api/tasks-api.js")).deleteDailyTask(taskId);
          balance = await getPointsBalance();
          draw();
        } catch (err) { showToast(err.message, { error: true }); }
      });
    });

    content.querySelectorAll(".task-check").forEach((btn) => {
      btn.addEventListener("click", async () => {
        const card = btn.closest(".task-card");
        const taskId = card.dataset.taskId;
        const task = routine.tasks.find((t) => t.id === taskId);
        if (!task) return;

        const willComplete = task.status !== "done";
        // Atualizacao otimista — a rotina real (com Stats recalculado) volta da API
        // e substitui isso quando a chamada terminar, com toggle livre nao ha rollback delicado.
        task.status = willComplete ? "done" : "pending";
        draw();

        try {
          const updated = willComplete ? await completeTask(taskId) : await reopenTask(taskId);
          routine = updated;
          balance = await getPointsBalance();
          draw();
        } catch (err) {
          showToast(err.message, { error: true });
          task.status = willComplete ? "pending" : "done"; // desfaz a atualizacao otimista
          draw();
        }
      });
    });
  }

  draw();
}

function currentPeriodGuess() {
  const hour = new Date().getHours();
  if (hour < 12) return "morning";
  if (hour < 18) return "afternoon";
  return "evening";
}
