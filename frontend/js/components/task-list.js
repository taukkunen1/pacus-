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

function taskCard(task, canManage) {
  const done =
    task.status === "done" ||
    task.status === 1;

  const managementActions = canManage
    ? `
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
      class="task-card ${done ? "is-done" : ""}"
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

      <span class="task-points">
        +${escapeHtml(String(task.points))} PP
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
    <div class="task-section">
      <p class="task-section__title">
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
