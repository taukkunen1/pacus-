import {
  getTodayRoutine,
  completeTask,
  reopenTask,
  getPointsBalance,
  getPacus
} from "../api/pacus-api.js";

import {
  createDailyTask,
  createTask,
  updateDailyTask,
  deleteDailyTask
} from "../api/tasks-api.js";

import { renderTank } from "../pacus/habitat.js";
import { renderTaskSection } from "../components/task-list.js";
import { formatOperationalDate } from "../utils/date.js";
import {
  formatBrl,
  periodLabel,
  typeLabel
} from "../utils/format.js";
import { showToast } from "../components/toast.js";
import { appState } from "../state/app-state.js";

const PERIODS = [
  "morning",
  "afternoon",
  "evening"
];

const TYPES = [
  "mandatory",
  "expected",
  "challenge"
];

const TYPE_PROMPT_LABELS = "1) Obrigatoria\n2) Deve fazer\n3) Desafio";
const TYPE_PROMPT_MAP = { "1": "mandatory", "2": "expected", "3": "challenge" };
const TYPE_PROMPT_DEFAULT = { mandatory: "1", expected: "2", challenge: "3" };

// window.prompt so aceita texto — pede o numero do tipo e traduz pro valor
// que a API espera. Retorna null se a pessoa cancelar (o chamador deve abortar).
function promptForType(currentType) {
  const suggested = TYPE_PROMPT_DEFAULT[currentType] ?? "1";
  const answer = window.prompt(
    `Tipo da tarefa:\n${TYPE_PROMPT_LABELS}`,
    suggested
  );

  if (answer === null) return null;

  return TYPE_PROMPT_MAP[answer.trim()] ?? currentType;
}

export async function renderHome(
  root,
  navigate = () => {}
) {
  const role = appState.user?.role ?? "";

  const isAdult =
    role.toLowerCase() === "adult";

  root.innerHTML = `
    <div class="screen">
      <div
        class="container"
        id="home-content"
      >
        <p class="task-empty">
          Carregando sua rotina...
        </p>
      </div>
    </div>
  `;

  const content =
    root.querySelector("#home-content");

  let routine;

  let balance = {
    balance: 0,
    brl: 0
  };

  let pacus = null;

  let activePeriod =
    currentPeriodGuess();

  try {
    [routine, balance] =
      await Promise.all([
        getTodayRoutine(),
        getPointsBalance()
      ]);
  } catch (err) {
    content.innerHTML = `
      <p class="error-text">
        Não foi possível carregar
        a rotina de hoje:
        ${escapeHtml(err.message)}
      </p>
    `;

    return;
  }

  try {
    pacus = await getPacus();
  } catch (err) {
    console.warn("PACUS nao encontrado. A tela de hoje continuara sem o estagio do PACUS.", err);
  }

  function totalTasks() {
    return routine.tasks.filter(
      (task) => !task.deletedAt
    ).length;
  }

  function doneTasks() {
    return routine.tasks.filter(
      (task) =>
        task.status === "done"
    ).length;
  }

  function tasksFor(period, type) {
    return routine.tasks.filter(
      (task) =>
        !task.deletedAt &&
        task.period === period &&
        task.type === type
    );
  }

  function draw() {
    const total = totalTasks();
    const done = doneTasks();

    const pct =
      total === 0
        ? 0
        : Math.round(
            (done / total) * 100
          );

    const circumference =
      2 * Math.PI * 22;

    const offset =
      circumference -
      (pct / 100) *
        circumference;

    content.innerHTML = `
      <div class="progress-header">
        <div>
          <p class="progress-header__date">
            ${formatOperationalDate(
              routine.date
            )}
          </p>

          <h1 class="progress-header__title">
            Hoje
          </h1>
        </div>

        <div class="progress-ring">
          <svg
            width="56"
            height="56"
            viewBox="0 0 56 56"
            aria-label="${pct}% concluído"
          >
            <circle
              class="progress-ring__track"
              cx="28"
              cy="28"
              r="22"
            ></circle>

            <circle
              class="progress-ring__value"
              cx="28"
              cy="28"
              r="22"
              stroke-dasharray="${circumference}"
              stroke-dashoffset="${offset}"
            ></circle>
          </svg>

          <span class="progress-ring__label">
            ${pct}%
          </span>
        </div>
      </div>

      ${renderTank(pacus?.stage)}

      <div
        class="period-tabs"
        role="tablist"
        aria-label="Período do dia"
      >
        ${PERIODS.map(
          (period) => `
            <button
              type="button"
              class="period-tab ${
                period === activePeriod
                  ? "is-active"
                  : ""
              }"
              data-period="${period}"
              role="tab"
              aria-selected="${
                period === activePeriod
              }"
            >
              ${periodLabel(period)}
            </button>
          `
        ).join("")}
      </div>

      <div id="task-sections">
        ${TYPES.map(
          (type) =>
            renderTaskSection(
              typeLabel(type),
              tasksFor(
                activePeriod,
                type
              ),
              {
                canManage: isAdult
              }
            )
        ).join("")}
      </div>

      ${
        isAdult
          ? `
              <div
                class="task-actions-bar"
              >
                <button
                  class="btn btn-primary"
                  id="add-task"
                  type="button"
                >
                  + Nova tarefa
                </button>

                <button
                  class="btn btn-ghost"
                  id="reorder-help"
                  type="button"
                >
                  ↕ Reordenar
                </button>
              </div>
            `
          : ""
      }

      <div class="points-footer">
        <div>
          <p
            class="points-footer__balance"
          >
            ${balance.balance} PP
          </p>

          <p
            class="points-footer__brl"
          >
            ${formatBrl(balance.brl)}
          </p>
        </div>
      </div>

      <nav
        class="bottom-nav"
        aria-label="Navegação principal"
      >
        <button
          data-nav="today"
          class="is-active"
          type="button"
        >
          Hoje
        </button>

        <button
          data-nav="history"
          type="button"
        >
          Histórico
        </button>

        <button
          data-nav="points"
          type="button"
        >
          Pontos
        </button>

        <button
          data-nav="pacus"
          type="button"
        >
          PACUS
        </button>
      </nav>
    `;

    attachHandlers();
  }

  function attachHandlers() {
    content
      .querySelectorAll(".period-tab")
      .forEach((button) => {
        button.addEventListener(
          "click",
          () => {
            activePeriod =
              button.dataset.period;

            draw();
          }
        );
      });

    content
      .querySelectorAll("[data-nav]")
      .forEach((button) => {
        button.addEventListener(
          "click",
          () => {
            location.hash =
              button.dataset.nav;

            navigate(
              button.dataset.nav
            );
          }
        );
      });

    if (isAdult) {
      content
        .querySelector("#add-task")
        ?.addEventListener(
          "click",
          async () => {
            const title =
              window.prompt(
                "Nome da nova tarefa:"
              );

            if (!title?.trim()) {
              return;
            }

            const pointsRaw =
              window.prompt(
                "Pacus Points:",
                "1"
              );

            const points =
              Number(pointsRaw);

            if (
              !Number.isInteger(points) ||
              points <= 0
            ) {
              showToast(
                "A tarefa deve ter um valor de pontos maior que zero.",
                { error: true }
              );

              return;
            }

            const type = promptForType("challenge");

            if (!type) {
              return;
            }

            const permanent =
              window.confirm(
                "Esta tarefa deve se repetir nos próximos dias?\n\n" +
                  "OK = Sim, tarefa permanente\n" +
                  "Cancelar = Não, somente hoje"
              );

            const payload = {
              title: title.trim(),
              description: null,
              type,
              period: activePeriod,
              points
            };

            try {
              if (permanent) {
                await createTask(
                  payload
                );

                showToast(
                  "Tarefa permanente criada."
                );

                routine =
                  await getTodayRoutine();
              } else {
                routine =
                  await createDailyTask(
                    payload
                  );

                showToast(
                  "Tarefa adicionada somente para hoje."
                );
              }

              draw();
            } catch (err) {
              showToast(
                err.message,
                { error: true }
              );
            }
          }
        );

      content
        .querySelectorAll(
          "[data-task-action=edit]"
        )
        .forEach((button) => {
          button.addEventListener(
            "click",
            async () => {
              const card =
                button.closest(
                  ".task-card"
                );

              const task =
                routine.tasks.find(
                  (item) =>
                    String(item.id) ===
                    String(
                      card?.dataset.taskId
                    )
                );

              if (!task) {
                return;
              }

              const title =
                window.prompt(
                  "Nome da tarefa:",
                  task.title
                );

              if (!title?.trim()) {
                return;
              }

              const points =
                Number(
                  window.prompt(
                    "Pacus Points:",
                    String(
                      task.points
                    )
                  )
                );

              if (
                !Number.isInteger(points) ||
                points <= 0
              ) {
                showToast(
                  "A tarefa deve ter um valor de pontos maior que zero.",
                  { error: true }
                );

                return;
              }

              const type = promptForType(task.type);

              if (!type) {
                return;
              }

              try {
                routine =
                  await updateDailyTask(
                    task.id,
                    {
                      title:
                        title.trim(),
                      description:
                        task.description ??
                        null,
                      type,
                      period:
                        task.period,
                      points
                    }
                  );

                balance =
                  await getPointsBalance();

                draw();
              } catch (err) {
                showToast(
                  err.message,
                  { error: true }
                );
              }
            }
          );
        });

      content
        .querySelectorAll(
          "[data-task-action=delete]"
        )
        .forEach((button) => {
          button.addEventListener(
            "click",
            async () => {
              const confirmed =
                window.confirm(
                  "Remover esta tarefa de hoje?"
                );

              if (!confirmed) {
                return;
              }

              const taskId =
                button
                  .closest(
                    ".task-card"
                  )
                  ?.dataset.taskId;

              if (!taskId) {
                return;
              }

              try {
                routine =
                  await deleteDailyTask(
                    taskId
                  );

                balance =
                  await getPointsBalance();

                draw();
              } catch (err) {
                showToast(
                  err.message,
                  { error: true }
                );
              }
            }
          );
        });
    }

    content
      .querySelectorAll(".task-check")
      .forEach((button) => {
        button.addEventListener(
          "click",
          async () => {
            const card =
              button.closest(
                ".task-card"
              );

            const taskId =
              card?.dataset.taskId;

            if (!taskId) {
              return;
            }

            const task =
              routine.tasks.find(
                (item) =>
                  String(item.id) ===
                  String(taskId)
              );

            if (!task) {
              return;
            }

            const willComplete =
              task.status !== "done";

            task.status =
              willComplete
                ? "done"
                : "pending";

            draw();

            try {
              const updated =
                willComplete
                  ? await completeTask(
                      taskId
                    )
                  : await reopenTask(
                      taskId
                    );

              routine = updated;

              balance =
                await getPointsBalance();

              draw();
            } catch (err) {
              showToast(
                err.message,
                { error: true }
              );

              task.status =
                willComplete
                  ? "pending"
                  : "done";

              draw();
            }
          }
        );
      });
  }

  draw();
}

function currentPeriodGuess() {
  const hour =
    new Date().getHours();

  if (hour < 12) {
    return "morning";
  }

  if (hour < 18) {
    return "afternoon";
  }

  return "evening";
}

function escapeHtml(value = "") {
  const div =
    document.createElement("div");

  div.textContent = String(value);

  return div.innerHTML;
}
