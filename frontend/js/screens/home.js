import {
  getTodayRoutine,
  completeTask,
  reopenTask,
  getPointsBalance,
  getPacus,
  pauseGameTimer,
  resumeGameTimer,
  adjustGameTimer,
  setDailyReaction
} from "../api/pacus-api.js";

import {
  createDailyTask,
  createTask,
  updateDailyTask,
  deleteDailyTask,
  reorderDailyTasks,
  selectTaskOption
} from "../api/tasks-api.js";

import { getPendingRedemptions } from "../api/store-api.js";
import { renderTank, REACTION_ICONS } from "../pacus/habitat.js";
import { renderTaskSection } from "../components/task-list.js";
import { formatOperationalDate } from "../utils/date.js";
import {
  formatBrl,
  periodLabel,
  typeLabel
} from "../utils/format.js";
import { showToast } from "../components/toast.js";
import { pickEffortMessage } from "../utils/effort-messages.js";
import { appState } from "../state/app-state.js";
import { promptTaskForm, showMessageModal } from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";
import { withSlowLoadHint, SLOW_LOAD_MESSAGE } from "../utils/slow-load-hint.js";

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

const REACTION_ORDER = ["heart", "clap", "star", "hug"];

// Fluxo do adulto pra reagir ao dia (relatedness -- ver docs/PROPOSITO.md e
// pacus/habitat.js): escolhe um icone (numero) e recebe a frase padrao daquele
// icone pra editar ou manter -- baixo atrito, mas ainda pessoal.
// Retorna null se cancelar em qualquer etapa (o chamador deve abortar).
async function promptForReactionChoice(currentReaction) {
  const menuLines = REACTION_ORDER
    .map((key, i) => `${i + 1}) ${REACTION_ICONS[key].emoji} ${REACTION_ICONS[key].label}`)
    .join("\n");

  const currentIndex = currentReaction
    ? REACTION_ORDER.indexOf(currentReaction.icon)
    : -1;

  const choice = window.prompt(
    `Como foi o dia da criança hoje?\n${menuLines}`,
    String(currentIndex >= 0 ? currentIndex + 1 : 1)
  );

  if (choice === null) return null;

  const icon = REACTION_ORDER[Number(choice.trim()) - 1] ?? REACTION_ORDER[0];

  const suggestion =
    currentReaction?.icon === icon
      ? currentReaction.message ?? REACTION_ICONS[icon].defaultMessage
      : REACTION_ICONS[icon].defaultMessage;

  const message = window.prompt(
    "Quer personalizar a mensagem? (opcional — deixe como está, edite, ou apague tudo pra deixar só o ícone)",
    suggestion
  );

  if (message === null) return null;

  return { icon, message: message.trim() || null };
}

function formatGameTimerRemaining(remainingMs) {
  const totalSeconds = Math.max(0, Math.floor(remainingMs / 1000));
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = totalSeconds % 60;
  const pad = (n) => String(n).padStart(2, "0");
  return h > 0
    ? `${h}h ${pad(m)}m ${pad(s)}s restantes`
    : `${pad(m)}m ${pad(s)}s restantes`;
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

  let gameTimerIntervalId = null;

  try {
    [routine, balance] =
      await withSlowLoadHint(
        Promise.all([
          getTodayRoutine(),
          getPointsBalance()
        ]),
        () => {
          const loadingEl = content.querySelector("p.task-empty");
          if (loadingEl) loadingEl.textContent = SLOW_LOAD_MESSAGE;
        }
      );
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

  // Numerozinho na aba "Loja" avisando o adulto de resgate(s) aguardando aprovacao --
  // so uma contagem leve, sem mudar nada na tela de hoje em si.
  let pendingRedemptionsCount = 0;
  if (isAdult) {
    try {
      const pending = await getPendingRedemptions();
      pendingRedemptionsCount = pending?.length ?? 0;
    } catch (err) {
      console.warn("Nao foi possivel carregar resgates pendentes.", err);
    }
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

  // Move uma tarefa uma posicao pra cima/baixo DENTRO da propria secao
  // (mesmo periodo + tipo), preservando a ordem relativa de todo o resto.
  // Retorna null quando a tarefa ja esta na ponta da secao (nada a fazer).
  function computeReorderedIds(taskId, direction) {
    const active = routine.tasks
      .filter((t) => !t.deletedAt)
      .slice()
      .sort((a, b) => a.order - b.order);

    const idx = active.findIndex(
      (t) => String(t.id) === String(taskId)
    );
    if (idx === -1) return null;

    const task = active[idx];
    const sameSection = (t) =>
      t.period === task.period && t.type === task.type;

    let swapWith = -1;
    if (direction === "up") {
      for (let i = idx - 1; i >= 0; i--) {
        if (sameSection(active[i])) { swapWith = i; break; }
      }
    } else {
      for (let i = idx + 1; i < active.length; i++) {
        if (sameSection(active[i])) { swapWith = i; break; }
      }
    }

    if (swapWith === -1) return null;

    const reordered = active.slice();
    const tmp = reordered[idx];
    reordered[idx] = reordered[swapWith];
    reordered[swapWith] = tmp;

    return reordered.map((t) => t.id);
  }

  // Antes (2026-09-02): mostrava um cadeado -- "Termine as tarefas da manhã
  // pra liberar Xh de jogo hoje" -- linguagem de "cumpra a obrigação pra
  // desbloquear a recompensa", o oposto do que docs/PROPOSITO.md pede (nunca
  // tratar tarefa como preço de admissão pro lazer). Pedido do dono do
  // produto: trocar por "você já tem esse tempo disponível" + progresso das
  // tarefas da manhã, sem framing de cadeado/bloqueio. Escopo do pedido é só
  // as tarefas da manhã (mesmo criterio que já decide quando o timer destrava
  // de verdade, ver SyncGameTimerAsync no backend) -- não conta o dia inteiro.
  function renderGameTimer() {
    if (!routine?.gameTimerEnabled) return "";

    if (!routine.gameTimerUnlockedAt) {
      const hours = Math.round((routine.gameTimerMinutes ?? 120) / 60);
      const morningTasks = routine.tasks.filter(
        (task) => !task.deletedAt && task.period === "morning"
      );
      const morningDone = morningTasks.filter(
        (task) => task.status === "done"
      ).length;

      return `
        <div class="game-timer game-timer--pending">
          <div class="game-timer__pending-line">
            Hoje você tem até ${hours}h de jogo disponíveis.
          </div>
          ${
            morningTasks.length > 0
              ? `
                <div class="game-timer__pending-line game-timer__pending-line--progress">
                  Você já cuidou de ${morningDone} de ${morningTasks.length} tarefas da manhã.
                </div>
              `
              : ""
          }
        </div>
      `;
    }

    const isPaused = Boolean(routine.gameTimerPausedAt);

    return `
      <div class="game-timer game-timer--unlocked ${isPaused ? "game-timer--paused" : ""}">
        <span class="game-timer__icon">${isPaused ? "⏸️" : "🎮"}</span>
        <span id="game-timer-remaining">calculando...</span>
        <div class="game-timer__controls">
          ${isAdult ? `
            <button type="button" class="game-timer__btn" id="game-timer-minus-5" title="Remover 5 minutos">−5</button>
          ` : ""}
          <button type="button" class="game-timer__btn game-timer__btn--toggle" id="game-timer-toggle" title="${isPaused ? "Despausar" : "Pausar"}">
            ${isPaused ? "▶️ Despausar" : "⏸️ Pausar"}
          </button>
          ${isAdult ? `
            <button type="button" class="game-timer__btn" id="game-timer-plus-5" title="Adicionar 5 minutos">+5</button>
          ` : ""}
        </div>
      </div>
    `;
  }

  // Quanto do tempo total ja foi "consumido" desde que liberou, descontando
  // pausas (passadas + a atual, se ainda estiver pausado agora).
  function computeGameTimerElapsedMs(now = Date.now()) {
    const unlockedAt = new Date(routine.gameTimerUnlockedAt).getTime();
    const pausedMs = routine.gameTimerPausedMs ?? 0;
    const currentPauseMs = routine.gameTimerPausedAt
      ? now - new Date(routine.gameTimerPausedAt).getTime()
      : 0;
    return now - unlockedAt - pausedMs - currentPauseMs;
  }

  function startGameTimerCountdown() {
    if (gameTimerIntervalId) {
      clearInterval(gameTimerIntervalId);
      gameTimerIntervalId = null;
    }

    if (!routine?.gameTimerUnlockedAt) return;

    const totalMinutes = (routine.gameTimerMinutes ?? 120) + (routine.gameTimerExtraMinutes ?? 0);
    const durationMs = Math.max(0, totalMinutes) * 60 * 1000;
    const isPaused = Boolean(routine.gameTimerPausedAt);

    const tick = () => {
      const el = content.querySelector("#game-timer-remaining");
      if (!el) {
        clearInterval(gameTimerIntervalId);
        gameTimerIntervalId = null;
        return;
      }

      const remainingMs = durationMs - computeGameTimerElapsedMs();
      if (remainingMs <= 0) {
        el.textContent = "Tempo de jogo de hoje já acabou. Até amanhã!";
        clearInterval(gameTimerIntervalId);
        gameTimerIntervalId = null;
        return;
      }

      el.textContent = isPaused
        ? `${formatGameTimerRemaining(remainingMs)} (pausado)`
        : formatGameTimerRemaining(remainingMs);
    };

    tick();
    // Enquanto pausado o valor nao muda sozinho, mas mantem o intervalo
    // rodando mesmo assim — mais simples que ligar/desligar, e o custo e
    // irrelevante (so recalcula o mesmo texto a cada segundo).
    gameTimerIntervalId = setInterval(tick, 1000);
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

      ${renderTank(pacus, undefined, { reaction: routine.reaction, isAdult })}

      ${renderGameTimer()}

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
                canManage: true,
                type,
                period: activePeriod
              }
            )
        ).join("")}
      </div>

      <div class="task-actions-bar">
        <button
          class="btn btn-primary"
          id="add-task"
          type="button"
        >
          + Nova tarefa
        </button>
      </div>

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

      ${renderBottomNav("today", {
        today: !isAdult ? Math.max(0, totalTasks() - doneTasks()) : 0,
        store: isAdult ? pendingRedemptionsCount : 0
      })}
    `;

    attachHandlers();
    startGameTimerCountdown();
  }

  async function handleGameTimerAction(action) {
    try {
      routine = await action();
      draw();
    } catch (err) {
      showToast(
        `Não foi possível atualizar o tempo de jogo: ${escapeHtml(err.message)}`,
        { error: true }
      );
    }
  }

  function attachHandlers() {
    const toggleBtn = content.querySelector("#game-timer-toggle");
    if (toggleBtn) {
      toggleBtn.addEventListener("click", () => {
        const isPaused = Boolean(routine.gameTimerPausedAt);
        handleGameTimerAction(isPaused ? resumeGameTimer : pauseGameTimer);
      });
    }

    const plusBtn = content.querySelector("#game-timer-plus-5");
    if (plusBtn) {
      plusBtn.addEventListener("click", () => {
        handleGameTimerAction(() => adjustGameTimer(5));
      });
    }

    const minusBtn = content.querySelector("#game-timer-minus-5");
    if (minusBtn) {
      minusBtn.addEventListener("click", () => {
        handleGameTimerAction(() => adjustGameTimer(-5));
      });
    }

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

    attachBottomNav(content, navigate);

    content
      .querySelectorAll('[data-action="view-reaction"]')
      .forEach((el) => {
        const reveal = () => {
          const reaction = routine.reaction;
          if (!reaction) return;

          const icon = REACTION_ICONS[reaction.icon];
          showMessageModal({
            title: `${icon?.emoji ?? "💬"} Mensagem de hoje`,
            body: reaction.message || icon?.defaultMessage || "Alguém pensou em você hoje!"
          });
        };

        el.addEventListener("click", reveal);
        // role="button" (ver pacus/habitat.js) nao dispara "click" sozinho no teclado
        // como um <button> de verdade -- sem isso, Tab+Enter nao revelava a mensagem.
        el.addEventListener("keydown", (event) => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            reveal();
          }
        });
      });

    content
      .querySelector('[data-action="set-reaction"]')
      ?.addEventListener("click", async () => {
        const choice = await promptForReactionChoice(routine.reaction);
        if (!choice) return;

        try {
          routine = await setDailyReaction(choice.icon, choice.message);
          showToast("Reação registrada — o Pacus vai carregar isso com ele hoje.");
          draw();
        } catch (err) {
          showToast(err.message, { error: true });
        }
      });

    content
      .querySelector("#add-task")
      ?.addEventListener(
        "click",
        async () => {
          // Painel unico (ver components/modal.js promptTaskForm) — nome,
          // descricao, pontos, tipo e opcoes aparecem juntos numa tela so, em
          // vez da fila antiga de prompts um atras do outro. So o adulto pode
          // transformar a tarefa em permanente (mexe nas regras da familia) —
          // o backend tambem bloqueia isso pra crianca, por isso o toggle so
          // aparece no formulario quando isAdult.
          const result = await promptTaskForm({
            title: "Nova tarefa",
            values: { type: "challenge", points: 1 },
            showPermanentToggle: isAdult,
            confirmLabel: "Adicionar"
          });

          if (!result) {
            return;
          }

          const { permanent, ...payload } = {
            ...result,
            period: activePeriod
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

            // Mesmo painel unico da criacao (ver components/modal.js
            // promptTaskForm), ja preenchido com os valores atuais da
            // tarefa — inclui o tipo (obrigatoria/deve fazer/desafio) como
            // grupo de botoes visivel, que antes ficava escondido dentro de
            // mais um prompt generico na fila.
            const result = await promptTaskForm({
              title: "Editar tarefa",
              values: task,
              confirmLabel: "Salvar"
            });

            if (!result) {
              return;
            }

            const { permanent, ...fields } = result;

            try {
              routine =
                await updateDailyTask(
                  task.id,
                  {
                    ...fields,
                    period: task.period
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

    content
      .querySelectorAll(
        "[data-task-action=move-up]"
      )
      .forEach((button) => {
        button.addEventListener(
          "click",
          async () => {
            const taskId =
              button
                .closest(".task-card")
                ?.dataset.taskId;

            if (!taskId) return;

            const orderedIds =
              computeReorderedIds(taskId, "up");

            if (!orderedIds) return;

            try {
              routine =
                await reorderDailyTasks(orderedIds);

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
        "[data-task-action=move-down]"
      )
      .forEach((button) => {
        button.addEventListener(
          "click",
          async () => {
            const taskId =
              button
                .closest(".task-card")
                ?.dataset.taskId;

            if (!taskId) return;

            const orderedIds =
              computeReorderedIds(taskId, "down");

            if (!orderedIds) return;

            try {
              routine =
                await reorderDailyTasks(orderedIds);

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
        "[data-task-action=select-option]"
      )
      .forEach((button) => {
        button.addEventListener(
          "click",
          async () => {
            const card =
              button.closest(".task-card");

            const taskId = card?.dataset.taskId;
            if (!taskId) return;

            const task = routine.tasks.find(
              (item) => String(item.id) === String(taskId)
            );
            if (!task) return;

            const chosen = button.dataset.optionValue;
            // Clicar de novo na mesma opcao ja escolhida desmarca -- da pra
            // criança mudar de ideia sem precisar de outra opcao "neutra".
            const nextValue =
              task.selectedOption === chosen ? null : chosen;

            try {
              routine = await selectTaskOption(taskId, nextValue);
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

              // Reforço de ESFORÇO (nao de resultado/traço), so ao concluir -- ver
              // utils/effort-messages.js e docs/PROPOSITO.md. Reabrir a tarefa nao
              // mostra frase nenhuma, so o toggle silencioso de sempre.
              if (willComplete) {
                const justCompleted = routine.tasks.find(
                  (item) => String(item.id) === String(taskId)
                );

                if (justCompleted) {
                  const message = pickEffortMessage(
                    justCompleted,
                    routine.tasks
                  );

                  showToast(`${message} +${justCompleted.points} PP`);
                }
              }

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
