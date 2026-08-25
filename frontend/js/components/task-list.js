const CHECK_ICON = `<svg viewBox="0 0 24 24" fill="none" stroke="#0F3D3A" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg>`;

function taskCard(task) {
  const done = task.status === "done" || task.status === 1; // API pode devolver enum como string ou int
  return `
    <li class="task-card ${done ? "is-done" : ""}" data-task-id="${task.id}">
      <button type="button" class="task-check" aria-label="${done ? "Desmarcar" : "Marcar como concluida"}">
        ${CHECK_ICON}
      </button>
      <div class="task-info">
        <p class="task-title">${escapeHtml(task.title)}</p>
      </div>
      <span class="task-points">+${task.points} PP</span>
      <button type="button" class="task-more" data-task-action="edit" aria-label="Editar tarefa">✎</button><button type="button" class="task-more" data-task-action="delete" aria-label="Excluir tarefa">×</button>
    </li>
  `;
}

export function renderTaskSection(title, tasks) {
  const doneCount = tasks.filter((t) => t.status === "done" || t.status === 1).length;
  const body = tasks.length
    ? `<ul class="task-list">${tasks.map(taskCard).join("")}</ul>`
    : `<p class="task-empty">Nenhuma tarefa aqui neste periodo.</p>`;

  return `
    <div class="task-section">
      <p class="task-section__title">
        ${title} <span class="task-section__count">${doneCount}/${tasks.length}</span>
      </p>
      ${body}
    </div>
  `;
}

function escapeHtml(str) {
  const div = document.createElement("div");
  div.textContent = str;
  return div.innerHTML;
}
