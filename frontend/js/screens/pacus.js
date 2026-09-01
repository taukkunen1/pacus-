import { apiClient } from "../api/api-client.js";
import { renderTank } from "../pacus/habitat.js";
import {
  getTasks,
  createTask,
  updateTask,
  deleteTask,
  activateTask
} from "../api/tasks-api.js";
import { periodLabel, typeLabel } from "../utils/format.js";
import { showToast } from "../components/toast.js";
import { isValidPoints, POINTS_HELP_TEXT } from "../utils/validation.js";
import { promptTextarea } from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";

const PERIODS = ["morning", "afternoon", "evening"];
const TYPES = ["mandatory", "expected", "challenge"];

// Rotulo em portugues + nome em ingles do enum DayOfWeek do backend (Enum.TryParse
// so aceita "Monday", "Tuesday" etc). So segunda a sexta: recurrence
// "weekday_rotation" e so pra dias uteis (ver TaskTemplateService no backend).
const WEEKDAYS = [
  { key: "Monday", label: "Segunda-feira" },
  { key: "Tuesday", label: "Terça-feira" },
  { key: "Wednesday", label: "Quarta-feira" },
  { key: "Thursday", label: "Quinta-feira" },
  { key: "Friday", label: "Sexta-feira" }
];

// Pergunta o titulo + descricao de cada dia util pra uma tarefa "rotativa"
// (atividade diferente de segunda a sexta, ex.: Momento Criativo). Retorna
// null se a pessoa cancelar em qualquer dia (o chamador deve abortar a
// criacao inteira nesse caso, pra nao criar uma rotina pela metade).
async function promptWeekdayVariants() {
  const variants = [];

  for (const { key, label } of WEEKDAYS) {
    const dayTitle = window.prompt(
      `Atividade de ${label} (título curto):`
    );

    if (!dayTitle?.trim()) {
      return null;
    }

    // Descricao e opcional aqui -- cancelar o modal (ex.: Esc) so significa
    // "sem descricao pra esse dia", nao aborta a criacao da tarefa inteira
    // (diferente do titulo, que e obrigatorio).
    const dayDescription = await promptTextarea({
      title: `${label} — detalhes`,
      label: "Descrição (opcional) — um item por linha",
      value: "",
      placeholder: "Como funciona, o que a criança precisa fazer..."
    });

    variants.push({
      dayOfWeek: key,
      title: dayTitle.trim(),
      description: dayDescription?.trim() || null
    });
  }

  return variants;
}

// Abreviacao em portugues (o que a pessoa digita) -> nome em ingles do enum
// DayOfWeek do backend (o que a API espera). Cobre a semana inteira -- usado
// so pela recorrencia "custom" (dias especificos, ex.: "Ingles" so terca e
// quarta, "Escoteiro" so sabado), diferente de WEEKDAYS acima (so uteis, so
// pra "atividade diferente por dia").
const DAY_ABBR = [
  { abbr: "seg", key: "Monday" },
  { abbr: "ter", key: "Tuesday" },
  { abbr: "qua", key: "Wednesday" },
  { abbr: "qui", key: "Thursday" },
  { abbr: "sex", key: "Friday" },
  { abbr: "sab", key: "Saturday" },
  { abbr: "dom", key: "Sunday" }
];

// "seg, Qua ,sáb" -> ["Monday", "Wednesday", "Saturday"]. Retorna null se
// algum token nao bater com nenhuma abreviacao (o chamador deve tratar como
// entrada invalida) ou se a lista ficar vazia.
function parseCustomDaysInput(raw) {
  if (!raw?.trim()) return null;

  const tokens = raw
    .split(",")
    .map((token) =>
      token
        .trim()
        .toLowerCase()
        .normalize("NFD")
        .replace(/[̀-ͯ]/g, "") // remove acento: "sáb" -> "sab"
        .slice(0, 3)
    )
    .filter(Boolean);

  if (!tokens.length) return null;

  const days = [];
  for (const token of tokens) {
    const match = DAY_ABBR.find((d) => d.abbr === token);
    if (!match) return null;
    if (!days.includes(match.key)) days.push(match.key);
  }

  return days;
}

// Pergunta quando a tarefa deve aparecer e retorna { recurrence, variants?,
// customDays? } pronto pra mandar no payload de createTask/updateTask (ou
// null se a pessoa cancelar em qualquer etapa -- o chamador deve abortar).
// `existingTask` (opcional, usado na edicao) pre-seleciona a opcao e os dias
// que a tarefa ja tinha configurados, pra "aceitar o padrao" bastar apertar
// Enter na maioria dos casos.
async function promptRecurrenceChoice(existingTask) {
  const currentRecurrence = existingTask?.recurrence ?? "daily";
  const defaultChoice =
    {
      daily: "1",
      weekday: "2",
      weekend: "3",
      custom: "4",
      weekday_rotation: "5"
    }[currentRecurrence] ?? "1";

  const choice = window.prompt(
    "Quando essa tarefa deve aparecer?\n" +
      "1) Todos os dias\n" +
      "2) Dias úteis (segunda a sexta)\n" +
      "3) Fim de semana (sábado e domingo)\n" +
      "4) Dias específicos (você escolhe quais)\n" +
      "5) Atividade diferente a cada dia útil (ex.: Momento Criativo)",
    defaultChoice
  );

  if (choice === null) return null;

  switch (choice.trim()) {
    case "2":
      return { recurrence: "weekday" };

    case "3":
      return { recurrence: "weekend" };

    case "4": {
      const currentDaysHint = (existingTask?.customDays ?? [])
        .map((day) => DAY_ABBR.find((d) => d.key.toLowerCase() === String(day).toLowerCase())?.abbr)
        .filter(Boolean)
        .join(",");

      const raw = window.prompt(
        "Quais dias? Separe por vírgula: seg,ter,qua,qui,sex,sab,dom",
        currentDaysHint
      );

      if (raw === null) return null;

      const customDays = parseCustomDaysInput(raw);
      if (!customDays) {
        showToast(
          "Dias inválidos. Use seg,ter,qua,qui,sex,sab,dom separados por vírgula.",
          { error: true }
        );
        return null;
      }

      return { recurrence: "custom", customDays };
    }

    case "5": {
      // Nao pre-preenche as atividades de cada dia mesmo editando uma tarefa
      // rotativa existente -- limitacao aceitavel do formato "um prompt por
      // vez"; a pessoa precisa redigitar as 5 atividades pra manter a rotacao.
      const variants = await promptWeekdayVariants();
      if (!variants) return null;
      return { recurrence: "weekday_rotation", variants };
    }

    default:
      return { recurrence: "daily" };
  }
}

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
  let tasks = [];

  try {
    tasks = await getTasks();
  } catch (err) {
    content.innerHTML = `
      <div class="screen-header">
        <div>
          <p class="eyebrow">PACUS</p>
          <h1>Meu companheiro</h1>
        </div>

        <button class="btn btn-ghost" id="back">
          Hoje
        </button>
      </div>

      <p class="error-text">
        Nao foi possivel carregar as tarefas permanentes:
        ${escapeHtml(err.message)}
      </p>
    `;

    content.querySelector("#back")
      ?.addEventListener(
        "click",
        () => navigate("today")
      );

    return;
  }

  try {
    pacus = await apiClient("/pacus/me");
  } catch (err) {
    console.warn(
      "PACUS nao encontrado. A tela de tarefas permanentes continuara disponivel.",
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

      <section class="task-management">
        <div class="screen-header">
          <div>
            <p class="eyebrow">ROTINA</p>
            <h2>Tarefas permanentes</h2>
          </div>

          <button
            class="btn btn-primary"
            id="add-permanent-task"
          >
            + Nova tarefa
          </button>
        </div>

        <div id="permanent-task-list">
          ${renderPermanentTasks()}
        </div>
      </section>

      ${renderBottomNav("pacus")}
    `;

    content
      .querySelector("#back")
      ?.addEventListener(
        "click",
        () => navigate("today")
      );

    attachBottomNav(content, navigate);

    content
      .querySelector("#add-permanent-task")
      ?.addEventListener(
        "click",
        createPermanentTask
      );

    content
      .querySelectorAll(
        "[data-task-action=edit]"
      )
      .forEach((button) => {
        button.addEventListener(
          "click",
          () =>
            editPermanentTask(
              button.dataset.taskId
            )
        );
      });

    content
      .querySelectorAll(
        "[data-task-action=delete]"
      )
      .forEach((button) => {
        button.addEventListener(
          "click",
          () =>
            deletePermanentTask(
              button.dataset.taskId
            )
        );
      });

    content
      .querySelectorAll(
        "[data-task-action=activate]"
      )
      .forEach((button) => {
        button.addEventListener(
          "click",
          () =>
            reactivatePermanentTask(
              button.dataset.taskId
            )
        );
      });
  }

  function renderPermanentTasks() {
    if (!tasks.length) {
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

    return tasks
      .map((task) => {
        const active = task.active !== false;

        return `
          <article
            class="task-card"
          >
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
                <span>
                  ${typeLabel(task.type)}
                </span>

                <span>
                  ${periodLabel(task.period)}
                </span>

                <span>
                  ${task.points} PP
                </span>

                <span>
                  ${active ? "Ativa" : "Inativa"}
                </span>

                ${recurrenceBadge(task)}
              </div>
            </div>

            <div class="task-actions">
              <button
                class="btn btn-ghost"
                data-task-action="edit"
                data-task-id="${escapeHtml(String(task.id))}"
              >
                Editar
              </button>

              ${
                active
                  ? `
                    <button
                      class="btn btn-ghost"
                      data-task-action="delete"
                      data-task-id="${escapeHtml(String(task.id))}"
                    >
                      Desativar
                    </button>
                  `
                  : `
                    <button
                      class="btn btn-primary"
                      data-task-action="activate"
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

  async function createPermanentTask() {
    const title = window.prompt(
      "Nome da tarefa permanente:"
    );

    if (!title?.trim()) {
      return;
    }

    const descriptionRaw = await promptTextarea({
      title: "Descrição da tarefa",
      label: "Descrição (opcional) — um item por linha",
      value: "",
      placeholder: "Ex.: 48 ÷ 6 = ___\n72 ÷ 8 = ___"
    });

    const type = window
      .prompt(
        "Tipo: mandatory, expected ou challenge",
        "challenge"
      )
      ?.trim()
      .toLowerCase();

    if (!TYPES.includes(type)) {
      showToast(
        "Tipo invalido. Use mandatory, expected ou challenge.",
        { error: true }
      );

      return;
    }

    const period = window
      .prompt(
        "Periodo: morning, afternoon ou evening",
        "morning"
      )
      ?.trim()
      .toLowerCase();

    if (!PERIODS.includes(period)) {
      showToast(
        "Periodo invalido. Use morning, afternoon ou evening.",
        { error: true }
      );

      return;
    }

    const points = Number(
      window.prompt(
        POINTS_HELP_TEXT,
        "1"
      )
    );

    if (!isValidPoints(points)) {
      showToast(
        `Valor invalido. ${POINTS_HELP_TEXT}.`,
        { error: true }
      );

      return;
    }

    // Em quais dias essa tarefa aparece -- sem isso toda tarefa permanente
    // aparecia igual em todos os dias, sem excecao.
    const recurrenceChoice = await promptRecurrenceChoice();
    if (!recurrenceChoice) {
      return;
    }

    try {
      const created = await createTask({
        title: title.trim(),
        description:
          descriptionRaw?.trim() || null,
        type,
        period,
        points,
        ...recurrenceChoice
      });

      tasks = [created, ...tasks];

      showToast("Tarefa permanente criada.");

      draw();
    } catch (err) {
      showToast(
        err.message,
        { error: true }
      );
    }
  }

  async function editPermanentTask(id) {
    const task = tasks.find(
      (item) =>
        String(item.id) === String(id)
    );

    if (!task) {
      return;
    }

    const title = window.prompt(
      "Nome da tarefa:",
      task.title
    );

    if (!title?.trim()) {
      return;
    }

    const descriptionRaw = await promptTextarea({
      title: "Descrição da tarefa",
      label: "Descrição (opcional) — um item por linha",
      value: task.description ?? "",
      placeholder: "Ex.: 48 ÷ 6 = ___\n72 ÷ 8 = ___"
    });

    const type = window
      .prompt(
        "Tipo: mandatory, expected ou challenge",
        task.type
      )
      ?.trim()
      .toLowerCase();

    if (!TYPES.includes(type)) {
      showToast(
        "Tipo invalido. Use mandatory, expected ou challenge.",
        { error: true }
      );

      return;
    }

    const period = window
      .prompt(
        "Periodo: morning, afternoon ou evening",
        task.period
      )
      ?.trim()
      .toLowerCase();

    if (!PERIODS.includes(period)) {
      showToast(
        "Periodo invalido. Use morning, afternoon ou evening.",
        { error: true }
      );

      return;
    }

    const points = Number(
      window.prompt(
        POINTS_HELP_TEXT,
        String(task.points)
      )
    );

    if (!isValidPoints(points)) {
      showToast(
        `Valor invalido. ${POINTS_HELP_TEXT}.`,
        { error: true }
      );

      return;
    }

    // Pre-seleciona a opcao (e os dias, se for "custom") que a tarefa ja tinha,
    // pra bastar apertar Enter na maioria dos casos -- sem isso o backend reseta
    // a recorrencia pro padrao ("daily") sempre que o payload de edicao nao manda
    // esses campos, apagando silenciosamente uma configuracao como "Inglês" so
    // terca e quarta so por editar o titulo.
    const recurrenceChoice = await promptRecurrenceChoice(task);
    if (!recurrenceChoice) {
      return;
    }

    try {
      const updated = await updateTask(
        id,
        {
          title: title.trim(),
          description:
            descriptionRaw?.trim() || null,
          type,
          period,
          points,
          ...recurrenceChoice
        }
      );

      tasks = tasks.map(
        (item) =>
          String(item.id) === String(id)
            ? updated
            : item
      );

      showToast(
        "Tarefa permanente atualizada."
      );

      draw();
    } catch (err) {
      showToast(
        err.message,
        { error: true }
      );
    }
  }

  async function deletePermanentTask(id) {
    const task = tasks.find(
      (item) =>
        String(item.id) === String(id)
    );

    if (!task) {
      return;
    }

    if (
      !window.confirm(
        `Desativar a tarefa "${task.title}"?`
      )
    ) {
      return;
    }

    try {
      await deleteTask(id);

      tasks = tasks.filter(
        (item) =>
          String(item.id) !== String(id)
      );

      showToast(
        "Tarefa permanente desativada."
      );

      draw();
    } catch (err) {
      showToast(
        err.message,
        { error: true }
      );
    }
  }

  async function reactivatePermanentTask(id) {
    try {
      await activateTask(id);

      tasks = await getTasks();

      showToast(
        "Tarefa permanente ativada."
      );

      draw();
    } catch (err) {
      showToast(
        err.message,
        { error: true }
      );
    }
  }
}

function escapeHtml(value = "") {
  const div =
    document.createElement("div");

  div.textContent = value;

  return div.innerHTML;
}