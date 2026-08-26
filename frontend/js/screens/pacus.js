import { getPacus } from "../api/pacus-api.js";
import { renderTank, mountPacusBehavior } from "../pacus/pacus.js";
import {
  getTasks,
  createTask,
  updateTask,
  deleteTask,
  activateTask
} from "../api/tasks-api.js";
import { periodLabel, typeLabel } from "../utils/format.js";
import { showToast } from "../components/toast.js";

const PERIODS = ["morning", "afternoon", "evening"];
const TYPES = ["mandatory", "expected", "challenge"];

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
  let cleanupPacus = () => {};

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
    pacus = await getPacus();
  } catch (err) {
    console.warn(
      "PACUS nao encontrado. A tela de tarefas permanentes continuara disponivel.",
      err
    );
  }

  draw();

  function draw() {
    cleanupPacus();

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
    `;

    cleanupPacus = mountPacusBehavior(
      content,
      pacus?.stage
    );

    content
      .querySelector("#back")
      ?.addEventListener(
        "click",
        () => navigate("today")
      );

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

    const descriptionRaw = window.prompt(
      "Descricao da tarefa (opcional):",
      ""
    );

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
        "Pacus Points (1, 2 ou 3):",
        "1"
      )
    );

    if (![1, 2, 3].includes(points)) {
      showToast(
        "A tarefa deve valer 1, 2 ou 3 Pacus Points.",
        { error: true }
      );

      return;
    }

    try {
      const created = await createTask({
        title: title.trim(),
        description:
          descriptionRaw?.trim() || null,
        type,
        period,
        points
      });

      tasks = [created, ...tasks];

      showToast(
        "Tarefa permanente criada."
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

    const descriptionRaw = window.prompt(
      "Descricao da tarefa (opcional):",
      task.description ?? ""
    );

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
        "Pacus Points (1, 2 ou 3):",
        String(task.points)
      )
    );

    if (![1, 2, 3].includes(points)) {
      showToast(
        "A tarefa deve valer 1, 2 ou 3 Pacus Points.",
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
          points
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