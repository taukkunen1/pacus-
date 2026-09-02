const CHECK_ICON = `
  <svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="#0F3D3A"
    stroke-width="3"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path d="M20 6 9 17l-5-5"></path>
  </svg>
`;

function pointsLabel(points) {
  const value = Number(points);
  const sign = value > 0 ? "+" : ""; // numero negativo ja vem com "-" sozinho
  return `${sign}${value} PP`;
}

function taskCard(task, canManage) {
  const done =
    task.status === "done" ||
    task.status === 1;

  const type = String(task.type ?? "").toLowerCase();
  const points = Number(task.points);
  const pointsClass = points < 0 ? "task-points--penalty" : "task-points--reward";

  // Escolha real da crianca entre missoes (Teoria da Autodeterminacao -- ver
  // docs/PROPOSITO.md). So aparece quando o template definiu 2-4 Options;
  // cada chip manda a escolha pro backend (PUT /daily-tasks/{id}/option).
  // Nao trava a conclusao da tarefa -- e so um reforco de autonomia, a
  // crianca pode concluir sem escolher nenhuma.
  const options = Array.isArray(task.options) ? task.options : [];

  const optionChips = options.length
    ? `
        <div class="task-options" role="group" aria-label="Escolha uma missão">
          ${options
            .map((option) => {
              const selected = task.selectedOption === option;
              return `
                <button
                  type="button"
                  class="task-option-chip ${selected ? "is-selected" : ""}"
                  data-task-action="select-option"
                  data-option-value="${escapeHtml(option)}"
                  aria-pressed="${selected}"
                >
                  ${selected ? "✓ " : ""}${escapeHtml(option)}
                </button>
              `;
            })
            .join("")}
        </div>
      `
    : "";

  const managementActions = canManage
    ? `
        <button
          type="button"
          class="task-more"
          data-task-action="move-up"
          aria-label="Mover para cima"
        >
          ▲
        </button>

        <button
          type="button"
          class="task-more"
          data-task-action="move-down"
          aria-label="Mover para baixo"
        >
          ▼
        </button>

        <button
          type="button"
          class="task-more"
          data-task-action="edit"
          aria-label="Editar tarefa"
        >
          ✎
        </button>

        <button
          type="button"
          class="task-more"
          data-task-action="delete"
          aria-label="Excluir tarefa"
        >
          ×
        </button>
      `
    : "";

  return `
    <li
      class="task-card task-card--${escapeHtml(type)} ${done ? "is-done" : ""}"
      data-task-id="${escapeHtml(String(task.id))}"
    >
      <button
        type="button"
        class="task-check"
        aria-label="${
          done
            ? "Desmarcar"
            : "Marcar como concluída"
        }"
      >
        ${CHECK_ICON}
      </button>

      <div class="task-info">
        <p class="task-title">
          ${escapeHtml(task.title)}
        </p>

        ${
          task.description
            ? `
              <p class="task-description">
                ${escapeHtml(task.description)}
              </p>
            `
            : ""
        }

        ${
          task.reason
            ? `
              <details class="task-reason">
                <summary class="task-reason__toggle">
                  <span class="task-reason__icon" aria-hidden="true">💡</span>
                  Por que?
                </summary>
                <p class="task-reason__text">
                  <strong>Por que você faz isso?</strong> ${escapeHtml(task.reason)}
                </p>
              </details>
            `
            : ""
        }

        ${optionChips}
      </div>

      <span class="task-points ${pointsClass}">
        ${pointsLabel(points)}
      </span>

      ${managementActions}
    </li>
  `;
}

export function renderTaskSection(
  title,
  tasks,
  options = {}
) {
  const canManage =
    options.canManage === true;

  const type = String(options.type ?? "").toLowerCase();

  const doneCount = tasks.filter(
    (task) =>
      task.status === "done" ||
      task.status === 1
  ).length;

  const body = tasks.length
    ? `
        <ul class="task-list">
          ${tasks
            .map((task) =>
              taskCard(task, canManage)
            )
            .join("")}
        </ul>
      `
    : `
        <p class="task-empty">
          Nenhuma tarefa aqui neste período.
        </p>
      `;

  return `
    <div class="task-section task-section--${escapeHtml(type)}">
      <p class="task-section__title">
        <span class="task-section__dot"></span>
        ${escapeHtml(title)}
        <span class="task-section__count">
          ${doneCount}/${tasks.length}
        </span>
      </p>

      ${body}
    </div>
  `;
}

function escapeHtml(value = "") {
  const div =
    document.createElement("div");

  div.textContent = String(value);

  return div.innerHTML;
}
