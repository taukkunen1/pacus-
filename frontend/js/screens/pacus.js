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

                ${
                  task.recurrence === "weekday_rotation"
                    ? `<span>🔁 1 atividade/dia útil</span>`
                    : ""
                }
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

    // Tarefa "rotativa" (ex.: Momento Criativo): uma atividade diferente de
    // segunda a sexta, some no fim de semana. Sem isso toda tarefa permanente
    // aparece igual em todos os dias.
    const isRotation = window.confirm(
      "Essa tarefa deve ter uma atividade diferente a cada dia útil (segunda a sexta)?\n\n" +
        "OK = Sim, uma atividade por dia (não aparece no fim de semana)\n" +
        "Cancelar = Não, a mesma tarefa todo dia"
    );

    let recurrence;
    let variants;

    if (isRotation) {
      variants = await promptWeekdayVariants();
      if (!variants) {
        return;
      }
      recurrence = "weekday_rotation";
    }

    try {
      const created = await createTask({
        title: title.trim(),
        description:
          descriptionRaw?.trim() || null,
        type,
        period,
        points,
        ...(recurrence ? { recurrence, variants } : {})
      });

      tasks = [created, ...tasks];

      showToast(
        isRotation
          ? "Tarefa permanente criada — uma atividade diferente por dia útil."
          : "Tarefa permanente criada."
      );

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
          // O backend reseta recorrencia pro padrao ("daily", sem variantes) se
          // esses campos nao vierem no payload -- sem isso, editar titulo/tipo/etc
          // de uma tarefa "rotativa" (Momento Criativo e afins) apagava a rotacao
          // por dia da semana sem a pessoa pedir. Edicao da propria rotacao ainda
          // nao tem UI; por enquanto isso so preserva o que ja estava configurado.
          recurrence: task.recurrence,
          variants: task.variants
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