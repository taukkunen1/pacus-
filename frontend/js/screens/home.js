import {
  getTodayRoutine,
  completeTask,
  reopenTask,
  getPointsBalance,
  getPacus,
  consumeGameTimer,
  adjustGameTimer,
  setDailyReaction
} from "../api/pacus-api.js";

import {
  createDailyTask,
  createTask,
  updateDailyTask,
  deleteDailyTask,
  reorderDailyTasks,
  selectTaskOption,
  getTasks,
  updateTask,
  deleteTask,
  activateTask
} from "../api/tasks-api.js";

import { getPendingRedemptions } from "../api/store-api.js";
import {
  setTaskInitiative,
  setTaskSkipReason,
  setEveningPlan
} from "../api/autonomy-api.js";
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
import {
  promptTaskForm,
  promptPermanentTaskForm,
  showMessageModal,
  promptReactionForm,
  promptChoiceForm,
  promptEveningPlanForm
} from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";
import { withSlowLoadHint, SLOW_LOAD_MESSAGE } from "../utils/slow-load-hint.js";

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md), item 4:
// "Como você começou essa tarefa?" -- autodeclaração da própria criança, sem
// nenhuma tentativa de detectar isso automaticamente (o app não tem push).
const INITIATIVE_OPTIONS = [
  { value: "selfStarted", label: "Percebi sozinho e comecei", emoji: "🟢" },
  { value: "promptedByPacus", label: "O PACUS me ajudou a lembrar", emoji: "🟡" },
  { value: "promptedByAdult", label: "Um adulto me lembrou", emoji: "🔴" }
];

// Item 5: "O que aconteceu?" -- nunca usado pra punir, só pra entender.
const SKIP_REASON_OPTIONS = [
  { value: "sleepy", label: "Estava com sono", emoji: "😴" },
  { value: "preferredOtherActivity", label: "Preferi outra atividade", emoji: "🎮" },
  { value: "noTime", label: "Não deu tempo", emoji: "⏰" },
  { value: "notInTheMood", label: "Não estava com vontade", emoji: "😐" },
  { value: "disliked", label: "Não gostei", emoji: "📚" },
  { value: "forgot", label: "Esqueci", emoji: "🤷" },
  { value: "other", label: "Outro", emoji: "✏️" }
];

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

// Abreviacao em portugues -> nome em ingles do enum DayOfWeek do backend
// (Enum.TryParse so aceita "Monday", "Tuesday" etc). Cobre a semana inteira --
// usado so pelo selo de recorrencia customizada na lista de tarefas
// permanentes (ver recurrenceBadge abaixo); a escolha dos dias em si e feita
// com checkboxes no painel de components/modal.js promptPermanentTaskForm.
// Movido de screens/pacus.js pra ca junto com o resto da gestao de tarefas
// permanentes (ver comentario na secao "ROTINA" dentro de draw()).
const DAY_ABBR = [
  { abbr: "seg", key: "Monday" },
  { abbr: "ter", key: "Tuesday" },
  { abbr: "qua", key: "Wednesday" },
  { abbr: "qui", key: "Thursday" },
  { abbr: "sex", key: "Friday" },
  { abbr: "sab", key: "Saturday" },
  { abbr: "dom", key: "Sunday" }
];

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
  let completingGameSession = false;
  let duckAudioContext = null;

  function gameSessionStorageKey() {
    return `pacus-game-session:${routine?.date ?? "today"}`;
  }

  function getAvailableGameMinutes() {
    return Math.max(0, (routine?.gameTimerMinutes ?? 120) + (routine?.gameTimerExtraMinutes ?? 0));
  }

  function formatGameMinutes(minutes) {
    const safe = Math.max(0, Math.floor(minutes));
    const hours = Math.floor(safe / 60);
    const mins = safe % 60;
    if (hours && mins) return `${hours}h ${String(mins).padStart(2, "0")}min`;
    if (hours) return `${hours}h`;
    return `${mins}min`;
  }

  function readGameSession() {
    try {
      const raw = localStorage.getItem(gameSessionStorageKey());
      if (!raw) return null;
      const value = JSON.parse(raw);
      if (
        value?.date !== routine?.date ||
        !Number.isFinite(value?.minutes) ||
        !Number.isFinite(value?.startedAt) ||
        !Number.isFinite(value?.endAt)
      ) {
        localStorage.removeItem(gameSessionStorageKey());
        return null;
      }
      return value;
    } catch {
      localStorage.removeItem(gameSessionStorageKey());
      return null;
    }
  }

  function saveGameSession(minutes) {
    const startedAt = Date.now();
    const session = {
      date: routine.date,
      minutes,
      startedAt,
      endAt: startedAt + minutes * 60 * 1000
    };
    localStorage.setItem(gameSessionStorageKey(), JSON.stringify(session));
    return session;
  }

  function clearGameSession() {
    localStorage.removeItem(gameSessionStorageKey());
  }

  function primeDuckAudio() {
    const AudioContextClass = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextClass) return;
    duckAudioContext ??= new AudioContextClass();
    if (duckAudioContext.state === "suspended") duckAudioContext.resume().catch(() => {});
  }

  function playDuckQuack() {
    primeDuckAudio();
    const ctx = duckAudioContext;
    if (!ctx || ctx.state !== "running") return;

    const now = ctx.currentTime;
    [0, 0.28].forEach((delay, index) => {
      const oscillator = ctx.createOscillator();
      const gain = ctx.createGain();
      const filter = ctx.createBiquadFilter();
      oscillator.type = "sawtooth";
      oscillator.frequency.setValueAtTime(index === 0 ? 310 : 280, now + delay);
      oscillator.frequency.exponentialRampToValueAtTime(125, now + delay + 0.18);
      filter.type = "lowpass";
      filter.frequency.setValueAtTime(900, now + delay);
      gain.gain.setValueAtTime(0.0001, now + delay);
      gain.gain.exponentialRampToValueAtTime(0.22, now + delay + 0.015);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + delay + 0.22);
      oscillator.connect(filter);
      filter.connect(gain);
      gain.connect(ctx.destination);
      oscillator.start(now + delay);
      oscillator.stop(now + delay + 0.24);
    });
  }

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
  // so uma contagem leve, sem mudar nada na tela de hoje em si. Aproveita a mesma
  // chamada (Promise.all) pra buscar as tarefas permanentes tambem, ja que as duas
  // so servem pro adulto -- ver secao "ROTINA" em draw() e docs/ESTADO_ATUAL.md
  // (gestao de tarefas permanentes migrou de screens/pacus.js pra ca: la a lista
  // aparecia pra qualquer papel, sem checar isAdult, o que deixava uma crianca
  // logada editar/desativar tarefa permanente pela aba PACUS).
  let pendingRedemptionsCount = 0;
  let permanentTasks = [];
  if (isAdult) {
    try {
      const [pending, tasks] = await Promise.all([
        getPendingRedemptions(),
        getTasks()
      ]);
      pendingRedemptionsCount = pending?.length ?? 0;
      permanentTasks = tasks;
    } catch (err) {
      console.warn("Nao foi possivel carregar resgates pendentes ou tarefas permanentes.", err);
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
  // Item 2 da spec de autonomia: "Primeiro → Depois" -- nunca trava nem tira
  // pontos, só um lembrete gentil de organização quando ainda sobra alguma
  // tarefa com meta mínima (ex.: leitura) não feita, mesmo com o tempo de tela
  // já liberado. A criança continua com autonomia total sobre como usar as
  // 2h -- isso não impede o botão "jogar" logo abaixo, só aparece acima dele.
  function renderMinimumGoalNudge() {
    const pending = routine.tasks.find(
      (task) =>
        task.status !== "done" &&
        !task.deletedAt &&
        task.minimumGoalLabel
    );

    if (!pending) return "";

    return `
      <p class="minimum-goal-nudge">
        <span aria-hidden="true">🎯</span>
        Primeiro <strong>${escapeHtml(pending.minimumGoalLabel)}</strong> de "${escapeHtml(pending.title)}". Depois é só aproveitar seu tempo de tela.
      </p>
    `;
  }

  function renderGameTimer() {
    if (!routine?.gameTimerEnabled) return "";

    const availableMinutes = getAvailableGameMinutes();
    const session = readGameSession();

    if (session) {
      const afterSession = Math.max(0, availableMinutes - session.minutes);
      return `
        ${renderMinimumGoalNudge()}
        <section class="game-timer game-timer--session" id="game-timer-container" aria-live="polite">
          <p class="game-timer__eyebrow">Tempo desta sessão</p>
          <div class="game-timer__countdown" id="game-session-countdown">--:--</div>
          <p class="game-timer__session-note">
            Você escolheu <strong>${formatGameMinutes(session.minutes)}</strong>.
            Quando terminar, ainda terá <strong>${formatGameMinutes(afterSession)}</strong> hoje.
          </p>
          <div class="game-timer__bar" aria-hidden="true">
            <div class="game-timer__bar-fill" id="game-timer-bar-fill"></div>
          </div>
          <p class="game-timer__balance-small">Saldo de hoje: ${formatGameMinutes(availableMinutes)}</p>
        </section>
      `;
    }

    const presets = [15, 30, 45, 60]
      .filter((minutes) => minutes <= availableMinutes);

    return `
      ${renderMinimumGoalNudge()}
      <section class="game-timer game-timer--wallet" id="game-timer-container">
        <p class="game-timer__eyebrow">Tempo disponível hoje</p>
        <div class="game-timer__balance">${formatGameMinutes(availableMinutes)}</div>
        ${availableMinutes > 0 ? `
          <p class="game-timer__question">Quanto tempo você quer usar agora?</p>
          <div class="game-timer__presets">
            ${presets.map((minutes) => `
              <button type="button" class="game-timer__preset" data-session-minutes="${minutes}">
                ${minutes === 60 ? "1 hora" : `${minutes} min`}
              </button>
            `).join("")}
            ${presets.length === 0 ? `
              <button type="button" class="game-timer__preset" data-session-minutes="${availableMinutes}">
                Usar ${formatGameMinutes(availableMinutes)}
              </button>
            ` : ""}
          </div>
          <div class="game-timer__custom">
            <input id="game-timer-custom-minutes" type="number" min="1" max="${availableMinutes}" inputmode="numeric" placeholder="Outro tempo" aria-label="Outro tempo em minutos">
            <button type="button" class="game-timer__btn game-timer__btn--start" id="game-timer-custom-start">Começar</button>
          </div>
        ` : `
          <p class="game-timer__finished">Seu tempo de tela de hoje acabou. 🦆</p>
        `}

        ${isAdult ? `
          <div class="game-timer__adult">
            <span>Adulto: adicionar tempo</span>
            <div class="game-timer__adult-actions">
              <button type="button" class="game-timer__btn" data-add-game-minutes="15">+15 min</button>
              <button type="button" class="game-timer__btn" data-add-game-minutes="30">+30 min</button>
              <button type="button" class="game-timer__btn" data-add-game-minutes="60">+1 hora</button>
            </div>
          </div>
        ` : ""}
      </section>
    `;
  }

  async function finishGameSession(session) {
    if (completingGameSession) return;
    completingGameSession = true;

    try {
      routine = await consumeGameTimer(session.minutes);
      clearGameSession();
      playDuckQuack();
      const remaining = getAvailableGameMinutes();
      showMessageModal({
        title: "🦆 Quá quá! Seu tempo terminou",
        body: remaining > 0
          ? `Sessão concluída. Você ainda tem ${formatGameMinutes(remaining)} disponível hoje.`
          : "Sessão concluída. O tempo de tela de hoje acabou."
      });
      draw();
    } catch (err) {
      showToast(`Não foi possível concluir a sessão: ${err.message}`, { error: true });
    } finally {
      completingGameSession = false;
    }
  }

  function startGameTimerCountdown() {
    if (gameTimerIntervalId) {
      clearInterval(gameTimerIntervalId);
      gameTimerIntervalId = null;
    }

    const session = readGameSession();
    if (!session) return;

    const tick = async () => {
      const countdown = content.querySelector("#game-session-countdown");
      if (!countdown) {
        clearInterval(gameTimerIntervalId);
        gameTimerIntervalId = null;
        return;
      }

      const remainingMs = session.endAt - Date.now();
      if (remainingMs <= 0) {
        countdown.textContent = "00:00";
        clearInterval(gameTimerIntervalId);
        gameTimerIntervalId = null;
        await finishGameSession(session);
        return;
      }

      const totalSeconds = Math.ceil(remainingMs / 1000);
      const minutes = Math.floor(totalSeconds / 60);
      const seconds = totalSeconds % 60;
      countdown.textContent = `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;

      const durationMs = session.minutes * 60 * 1000;
      const pct = Math.max(0, Math.min(100, (remainingMs / durationMs) * 100));
      const barFill = content.querySelector("#game-timer-bar-fill");
      const container = content.querySelector("#game-timer-container");
      if (barFill) barFill.style.width = `${pct}%`;
      if (container) {
        container.classList.toggle("game-timer--warning", pct <= 33 && pct > 15);
        container.classList.toggle("game-timer--critical", pct <= 15);
      }
    };

    tick();
    gameTimerIntervalId = setInterval(tick, 1000);
  }

  function startGameSession(minutes) {
    const available = getAvailableGameMinutes();
    const safeMinutes = Math.floor(Number(minutes));
    if (!Number.isFinite(safeMinutes) || safeMinutes <= 0 || safeMinutes > available) {
      showToast("Escolha um tempo que caiba no saldo de hoje.", { error: true });
      return;
    }

    primeDuckAudio();
    saveGameSession(safeMinutes);
    draw();
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
                period: activePeriod,
                currentPeriod: currentPeriodGuess()
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

        <button
          class="btn btn-ghost"
          id="evening-plan"
          type="button"
        >
          🌙 Planejar minha noite
        </button>

        <button
          class="btn btn-ghost"
          id="whats-next"
          type="button"
        >
          ❓ E agora?
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

      ${isAdult ? `
        <section class="task-management">
          <div class="screen-header">
            <div>
              <p class="eyebrow">ROTINA</p>
              <h2>Tarefas permanentes</h2>
            </div>

            <button
              class="btn btn-primary"
              id="add-permanent-task"
              type="button"
            >
              + Nova tarefa
            </button>
          </div>

          <div id="permanent-task-list">
            ${renderPermanentTasks()}
          </div>
        </section>
      ` : ""}

      ${renderBottomNav("today", {
        today: !isAdult ? Math.max(0, totalTasks() - doneTasks()) : 0,
        store: isAdult ? pendingRedemptionsCount : 0
      })}
    `;

    attachHandlers();
    startGameTimerCountdown();
  }

  // Movido de screens/pacus.js (aba PACUS) pra ca: a lista la nao checava
  // isAdult nenhuma vez, entao uma crianca logada via os mesmos botoes de
  // editar/desativar tarefa permanente que o adulto via. Aqui a secao inteira
  // (template acima) so renderiza quando isAdult, e os event listeners
  // (attachHandlers) so sao ligados quando os botoes existem no DOM -- ou
  // seja, ja saem protegidos de graca.
  function renderPermanentTasks() {
    if (!permanentTasks.length) {
      return `
        <div class="task-card">
          <div class="task-card__content">
            <strong class="task-title">
              Nenhuma tarefa permanente
            </strong>

            <span class="task-description">
              Crie uma tarefa para ela aparecer automaticamente nas próximas rotinas.
            </span>
          </div>
        </div>
      `;
    }

    return permanentTasks
      .map((task) => {
        const active = task.active !== false;

        return `
          <article class="task-card">
            <div class="task-card__content">
              <strong class="task-title">
                ${escapeHtml(task.title)}
              </strong>

              ${
                task.description
                  ? `
                    <span class="task-description">
                      ${escapeHtml(task.description)}
                    </span>
                  `
                  : ""
              }

              <div class="task-meta">
                <span>${typeLabel(task.type)}</span>
                <span>${periodLabel(task.period)}</span>
                <span>${task.points} PP</span>
                <span>${active ? "Ativa" : "Inativa"}</span>
                ${recurrenceBadge(task)}
              </div>
            </div>

            <div class="task-actions">
              <button
                class="btn btn-ghost"
                data-permanent-task-action="edit"
                data-task-id="${escapeHtml(String(task.id))}"
              >
                Editar
              </button>

              ${
                active
                  ? `
                    <button
                      class="btn btn-ghost"
                      data-permanent-task-action="delete"
                      data-task-id="${escapeHtml(String(task.id))}"
                    >
                      Desativar
                    </button>
                  `
                  : `
                    <button
                      class="btn btn-primary"
                      data-permanent-task-action="activate"
                      data-task-id="${escapeHtml(String(task.id))}"
                    >
                      Ativar
                    </button>
                  `
              }
            </div>
          </article>
        `;
      })
      .join("");
  }

  // Selo curto na lista de tarefas permanentes mostrando em quais dias a
  // tarefa aparece, quando nao e todo dia (o caso mais comum nao precisa de
  // selo nenhum).
  function recurrenceBadge(task) {
    if (task.recurrence === "weekday") return `<span>📅 dias úteis</span>`;
    if (task.recurrence === "weekend") return `<span>📅 fim de semana</span>`;
    if (task.recurrence === "weekday_rotation") return `<span>🔁 1 atividade/dia útil</span>`;

    if (task.recurrence === "custom" && (task.customDays ?? []).length) {
      const labels = task.customDays
        .map((day) => DAY_ABBR.find((d) => d.key.toLowerCase() === String(day).toLowerCase())?.abbr)
        .filter(Boolean)
        .join(", ");
      return `<span>📅 ${escapeHtml(labels)}</span>`;
    }

    return "";
  }

  // Painel unico (components/modal.js promptPermanentTaskForm), mesmo padrao
  // do editor de tarefas do dia: Tipo e Periodo como grupos de botoes,
  // recorrencia com os blocos de dias/variantes revelados so quando fazem
  // sentido, Opcoes e Motivos como listas editaveis.
  async function createPermanentTask() {
    const result = await promptPermanentTaskForm({
      title: "Nova tarefa permanente",
      values: { type: "challenge", points: 1, period: activePeriod },
      confirmLabel: "Adicionar"
    });

    if (!result) return;

    try {
      const created = await createTask(result);

      permanentTasks = [created, ...permanentTasks];

      showToast("Tarefa permanente criada.");

      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function editPermanentTask(id) {
    const task = permanentTasks.find((item) => String(item.id) === String(id));
    if (!task) return;

    const result = await promptPermanentTaskForm({
      title: "Editar tarefa",
      values: task,
      confirmLabel: "Salvar"
    });

    if (!result) return;

    try {
      const updated = await updateTask(id, result);

      permanentTasks = permanentTasks.map((item) => (String(item.id) === String(id) ? updated : item));

      showToast("Tarefa permanente atualizada.");

      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function deletePermanentTask(id) {
    const task = permanentTasks.find((item) => String(item.id) === String(id));
    if (!task) return;

    if (!window.confirm(`Desativar a tarefa "${task.title}"?`)) return;

    try {
      await deleteTask(id);

      permanentTasks = permanentTasks.filter((item) => String(item.id) !== String(id));

      showToast("Tarefa permanente desativada.");

      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function reactivatePermanentTask(id) {
    try {
      await activateTask(id);

      permanentTasks = await getTasks();

      showToast("Tarefa permanente ativada.");

      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
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
    content
      .querySelectorAll("[data-session-minutes]")
      .forEach((button) => {
        button.addEventListener("click", () => startGameSession(button.dataset.sessionMinutes));
      });

    content
      .querySelector("#game-timer-custom-start")
      ?.addEventListener("click", () => {
        const input = content.querySelector("#game-timer-custom-minutes");
        startGameSession(input?.value);
      });

    content
      .querySelectorAll("[data-add-game-minutes]")
      .forEach((button) => {
        button.addEventListener("click", () => {
          handleGameTimerAction(() => adjustGameTimer(Number(button.dataset.addGameMinutes)));
        });
      });

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
      .querySelector("#add-permanent-task")
      ?.addEventListener("click", createPermanentTask);

    content
      .querySelectorAll('[data-permanent-task-action="edit"]')
      .forEach((button) => {
        button.addEventListener("click", () =>
          editPermanentTask(button.dataset.taskId)
        );
      });

    content
      .querySelectorAll('[data-permanent-task-action="delete"]')
      .forEach((button) => {
        button.addEventListener("click", () =>
          deletePermanentTask(button.dataset.taskId)
        );
      });

    content
      .querySelectorAll('[data-permanent-task-action="activate"]')
      .forEach((button) => {
        button.addEventListener("click", () =>
          reactivatePermanentTask(button.dataset.taskId)
        );
      });

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
        // Painel unico (ver components/modal.js promptReactionForm), mesmo padrao
        // dos outros editores do site: icones como grupo de botoes visiveis (nada
        // de menu numerado em window.prompt) + campo de mensagem opcional, tudo
        // numa tela so. Antes disso eram dois window.prompt encadeados.
        const choice = await promptReactionForm({ current: routine.reaction });
        if (!choice) return;

        try {
          routine = await setDailyReaction(choice.icon, choice.message);
          showToast("Mensagem salva — o Pacus vai guardar isso com carinho hoje.");
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
            values: { type: "challenge", points: 1, period: activePeriod },
            showPermanentToggle: isAdult,
            confirmLabel: "Adicionar"
          });

          if (!result) {
            return;
          }

          const { permanent, ...payload } = result;

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

    // Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md), item 1:
    // "como você quer organizar sua noite?" -- a criança monta a ordem/momento das
    // tarefas restantes (pendentes) da tarde/noite. Não trava nada: é só o
    // combinado que ela mesma escolheu.
    content
      .querySelector("#evening-plan")
      ?.addEventListener("click", async () => {
        const remaining = routine.tasks.filter(
          (task) =>
            task.status !== "done" &&
            !task.deletedAt &&
            (task.period === "afternoon" || task.period === "evening")
        );

        if (!remaining.length) {
          showToast("Você já cuidou de tudo da tarde e da noite. 🎉");
          return;
        }

        const plan = await promptEveningPlanForm({
          tasks: remaining,
          initialPlan: routine.eveningPlan ?? []
        });

        if (!plan) return;

        try {
          routine = await setEveningPlan(plan);
          showToast("Combinado! Sua noite está planejada.");
          draw();
        } catch (err) {
          showToast(err.message, { error: true });
        }
      });

    // Item 7: "E agora?" -- sugere a próxima tarefa pendente (do combinado da
    // noite, se houver um, ou da ordem normal da rotina), incentivando a criança
    // a consultar a própria rotina em vez de esperar alguém falar.
    content
      .querySelector("#whats-next")
      ?.addEventListener("click", () => {
        const plannedIds = (routine.eveningPlan ?? [])
          .slice()
          .sort((a, b) => a.order - b.order)
          .map((item) => item.taskId);

        const pending = routine.tasks.filter(
          (task) => task.status !== "done" && !task.deletedAt
        );

        const next =
          plannedIds
            .map((id) => pending.find((task) => String(task.id) === String(id)))
            .find(Boolean) ??
          pending.sort((a, b) => a.order - b.order)[0];

        showToast(
          next
            ? `E agora: ${next.title}`
            : "Você já cuidou de tudo por aqui. 🐟✨"
        );
      });

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
            // tarefa — inclui o tipo (obrigatoria/deve fazer/desafio) e o
            // periodo (manha/tarde/noite) como grupos de botoes visiveis,
            // que antes ficavam escondidos (o periodo nem aparecia). O
            // toggle "tornar permanente" (mesma regra do formulario de
            // criacao: so adulto) so faz sentido aqui quando a tarefa ainda
            // e avulsa de hoje -- uma tarefa que ja veio de um template
            // (task.taskTemplateId preenchido) ja e permanente, entao o
            // toggle fica escondido pra nao sugerir uma acao redundante.
            const result = await promptTaskForm({
              title: "Editar tarefa",
              values: task,
              showPermanentToggle: isAdult && !task.taskTemplateId,
              confirmLabel: "Salvar"
            });

            if (!result) {
              return;
            }

            const { permanent, ...fields } = result;

            try {
              if (permanent) {
                // Promove a tarefa avulsa de hoje a tarefa permanente: cria
                // o template com os dados editados e remove a copia avulsa,
                // porque GET /daily-routines/today ja injeta automaticamente
                // a ocorrencia de hoje de qualquer template ativo -- manter
                // as duas deixaria a tarefa duplicada na lista de hoje.
                // Observacao: se a tarefa avulsa ja estava concluida hoje,
                // a ocorrencia gerada pelo template novo comeca pendente de
                // novo (o progresso de hoje nao e copiado).
                await createTask(fields);
                await deleteDailyTask(task.id);

                routine = await getTodayRoutine();

                showToast("Tarefa promovida a permanente.");
              } else {
                routine =
                  await updateDailyTask(
                    task.id,
                    fields
                  );
              }

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

    // Item 5 da spec de autonomia: "O que aconteceu?" -- nunca usado pra punir,
    // só pra entender por que a tarefa ficou pra trás (ver docs/ESTADO_ATUAL.md).
    content
      .querySelectorAll("[data-task-action=skip-reason]")
      .forEach((button) => {
        button.addEventListener("click", async () => {
          const card = button.closest(".task-card");
          const taskId = card?.dataset.taskId;
          if (!taskId) return;

          const answer = await promptChoiceForm({
            title: "O que aconteceu?",
            options: SKIP_REASON_OPTIONS,
            noteOptionValue: "other",
            notePlaceholder: "Conte com suas palavras...",
            confirmLabel: "Contar"
          });

          if (!answer) return;

          try {
            routine = await setTaskSkipReason(taskId, answer.value, answer.note);
            draw();
          } catch (err) {
            showToast(err.message, { error: true });
          }
        });
      });

    // Item 4 da spec de autonomia: pergunta "Como você começou essa tarefa?"
    // depois de concluir, sem travar nada -- fechar sem responder é uma opção
    // válida (ver promptChoiceForm/skipLabel). Concede um pequeno bônus de
    // pontos quando a resposta não depende de um adulto (ver
    // DailyRoutineService.SetTaskInitiativeAsync).
    async function askInitiative(taskId) {
      const answer = await promptChoiceForm({
        title: "Como você começou essa tarefa?",
        options: INITIATIVE_OPTIONS,
        confirmLabel: "Contar"
      });

      if (!answer) return;

      try {
        routine = await setTaskInitiative(taskId, answer.value);
        balance = await getPointsBalance();
        draw();
      } catch (err) {
        showToast(err.message, { error: true });
      }
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

                // Item 4 da spec de autonomia: "Como você começou essa tarefa?"
                // -- convite, nunca obrigatório (a criança pode fechar sem
                // responder), e só perguntado uma vez por tarefa. Disparado
                // depois do draw() acima pra não atrasar a sensação de "marquei
                // e pronto" da conclusão em si (ver DailyRoutineService.
                // SetTaskInitiativeAsync no backend).
                if (justCompleted && !justCompleted.initiative) {
                  askInitiative(justCompleted.id);
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
