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
