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
import { promptPermanentTaskForm, promptInput } from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";
import { appState } from "../state/app-state.js";
import {
  getFamilyChildren,
  updateChildPin,
  getFamilyTimezone,
  updateFamilyTimezone,
  generateRecoveryCode
} from "../api/family-api.js";

// Abreviacao em portugues -> nome em ingles do enum DayOfWeek do backend
// (Enum.TryParse so aceita "Monday", "Tuesday" etc). Cobre a semana inteira --
// usado so pelo selo de recorrencia customizada na lista de tarefas
// permanentes (ver recurrenceBadge abaixo); a escolha dos dias em si agora e
// feita com checkboxes no painel de components/modal.js promptPermanentTaskForm.
const DAY_ABBR = [
  { abbr: "seg", key: "Monday" },
  { abbr: "ter", key: "Tuesday" },
  { abbr: "qua", key: "Wednesday" },
  { abbr: "qui", key: "Thursday" },
  { abbr: "sex", key: "Friday" },
  { abbr: "sab", key: "Saturday" },
  { abbr: "dom", key: "Sunday" }
];

export async function renderPacus(root, navigate) {
  root.innerHTML = `
    <div class="screen">
      <div class="container">
        <p class="task-empty">Carregando PACUS...</p>
      </div>
    </div>
  `;

  const content = root.querySelector(".container");

  const isAdult = (appState.user?.role ?? "").toLowerCase() === "adult";

  let pacus = null;
  let tasks = [];
  let growthStages = [];
  let familyTimezone = "";

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

  if (isAdult) {
    try {
      [growthStages, familyTimezone] = await Promise.all([
        apiClient("/settings/growth-stages"),
        getFamilyTimezone().then((res) => res?.timezone ?? "")
      ]);
    } catch (err) {
      console.warn("Nao foi possivel carregar configuracoes da familia.", err);
    }
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

      ${pacus?.stageHistory?.length ? `
        <section class="task-management">
          <div class="screen-header">
            <div>
              <p class="eyebrow">HISTÓRICO</p>
              <h2>Estágios anteriores</h2>
            </div>
          </div>

          <div id="stage-history-list">
            ${renderStageHistory(pacus.stageHistory)}
          </div>
        </section>
      ` : ""}

      ${isAdult ? `
        <section class="task-management">
          <div class="screen-header">
            <div>
              <p class="eyebrow">PAINEL DO ADULTO</p>
              <h2>Configurações da família</h2>
            </div>
          </div>

          <div class="task-card">
            <div class="task-card__content">
              <strong class="task-title">Fuso horário</strong>
              <span class="task-description">
                ${escapeHtml(familyTimezone || "não configurado")} — usado para saber quando o dia operacional começa/termina.
              </span>
            </div>
            <div class="task-actions">
              <button class="btn btn-ghost" id="change-timezone">Alterar</button>
            </div>
          </div>

          <div class="task-card">
            <div class="task-card__content">
              <strong class="task-title">Código de recuperação de senha</strong>
              <span class="task-description">Gera um novo código para usar em "esqueci minha senha" (invalida o anterior).</span>
            </div>
            <div class="task-actions">
              <button class="btn btn-ghost" id="generate-recovery-code">Gerar novo código</button>
            </div>
          </div>

          <div class="task-card">
            <div class="task-card__content">
              <strong class="task-title">PIN da criança</strong>
              <span class="task-description">Redefine o PIN de login de uma das crianças da família.</span>
            </div>
            <div class="task-actions">
              <button class="btn btn-ghost" id="change-child-pin">Trocar PIN</button>
            </div>
          </div>

          <div class="task-card">
            <div class="task-card__content">
              <strong class="task-title">Calendário de crescimento do PACUS</strong>
              <span class="task-description">
                ${
                  growthStages.length
                    ? growthStages.map((s) => `${stageLabel(s.stage)}: ${s.date}`).join(" · ")
                    : "Nenhum estágio configurado — o PACUS mantém o estágio atual até você definir um calendário."
                }
              </span>
            </div>
            <div class="task-actions">
              <button class="btn btn-ghost" id="add-growth-stage">+ Estágio</button>
              ${growthStages.length ? `<button class="btn btn-ghost" id="clear-growth-stages">Limpar</button>` : ""}
            </div>
          </div>
        </section>
      ` : ""}

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

    content.querySelector("#change-timezone")?.addEventListener("click", changeTimezone);
    content.querySelector("#change-child-pin")?.addEventListener("click", changeChildPin);
    content.querySelector("#generate-recovery-code")?.addEventListener("click", handleGenerateRecoveryCode);
    content.querySelector("#add-growth-stage")?.addEventListener("click", addGrowthStage);
    content.querySelector("#clear-growth-stages")?.addEventListener("click", clearGrowthStages);
  }

  async function changeTimezone() {
    const timezone = await promptInput({
      title: "Fuso horário da família",
      label: "Fuso horário (formato IANA)",
      value: familyTimezone || "America/Sao_Paulo",
      placeholder: "America/Sao_Paulo",
      hint: "Ex.: America/Sao_Paulo, America/Manaus, America/Fortaleza."
    });
    if (!timezone?.trim()) return;

    try {
      const result = await updateFamilyTimezone(timezone.trim());
      familyTimezone = result?.timezone ?? timezone.trim();
      showToast("Fuso horário atualizado.");
      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function handleGenerateRecoveryCode() {
    if (!window.confirm("Gerar um novo código de recuperação? O código anterior deixará de funcionar.")) return;

    try {
      const result = await generateRecoveryCode();
      window.alert(`Seu novo código de recuperação é:\n\n${result.recoveryCode}\n\nGuarde em lugar seguro — ele só aparece esta vez.`);
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function changeChildPin() {
    let children = [];
    try {
      children = await getFamilyChildren();
    } catch (err) {
      showToast(err.message, { error: true });
      return;
    }

    if (!children.length) {
      showToast("Nenhuma criança cadastrada nesta família.", { error: true });
      return;
    }

    let childId = children[0].id;
    if (children.length > 1) {
      const names = children.map((c, i) => `${i + 1}) ${c.name}`).join("\n");
      const choice = Number(
        await promptInput({
          title: "Trocar PIN",
          label: "Qual criança? (digite o número)",
          value: "1",
          hint: names
        })
      );
      const chosen = children[choice - 1];
      if (!chosen) return;
      childId = chosen.id;
    }

    const newPin = await promptInput({
      title: "Trocar PIN",
      label: "Novo PIN (4 dígitos)",
      placeholder: "0000",
      type: "text"
    });
    if (!newPin?.trim()) return;

    if (!/^[0-9]{4}$/.test(newPin.trim())) {
      showToast("O PIN deve ter exatamente 4 dígitos numéricos.", { error: true });
      return;
    }

    try {
      await updateChildPin(childId, newPin.trim());
      showToast("PIN atualizado.");
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function addGrowthStage() {
    const stageNames = ["egg", "cracking", "hatching", "baby", "young", "adult"];
    const stageLabels = {
      egg: "Ovo",
      cracking: "Rachando",
      hatching: "Eclodindo",
      baby: "Filhote",
      young: "Jovem",
      adult: "Adulto"
    };
    const stage = (
      await promptInput({
        title: "Novo estágio de crescimento",
        label: "Estágio",
        value: "adult",
        hint: `Opções: ${stageNames.map((s) => stageLabels[s]).join(", ")}.`
      })
    )
      ?.trim()
      .toLowerCase();

    if (!stageNames.includes(stage)) {
      showToast(`Estágio inválido. Use um de: ${stageNames.map((s) => stageLabels[s]).join(", ")}.`, { error: true });
      return;
    }

    const date = await promptInput({
      title: "Novo estágio de crescimento",
      label: "A partir de qual data?",
      type: "date"
    });
    if (!date?.trim() || !/^\d{4}-\d{2}-\d{2}$/.test(date.trim())) {
      showToast("Data inválida. Use o formato AAAA-MM-DD.", { error: true });
      return;
    }

    const updated = [...growthStages.filter((s) => s.date !== date.trim()), { stage, date: date.trim() }];

    try {
      growthStages = await apiClient("/settings/growth-stages", {
        method: "PUT",
        body: JSON.stringify({ stages: updated })
      });
      showToast("Calendário de crescimento atualizado.");
      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function clearGrowthStages() {
    if (!window.confirm("Remover todo o calendário de estágios configurado?")) return;

    try {
      growthStages = await apiClient("/settings/growth-stages", {
        method: "PUT",
        body: JSON.stringify({ stages: [] })
      });
      showToast("Calendário de crescimento limpo.");
      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  function renderStageHistory(history) {
    return [...history]
      .sort((a, b) => new Date(b.reachedAt) - new Date(a.reachedAt))
      .map(
        (entry) => `
          <div class="task-card">
            <div class="task-card__content">
              <strong class="task-title">${stageLabel(entry.stage)}</strong>
              <span class="task-description">alcançado em ${new Date(entry.reachedAt).toLocaleDateString("pt-BR")}</span>
            </div>
          </div>
        `
      )
      .join("");
  }

  function stageLabel(stage) {
    const labels = {
      egg: "Ovo",
      cracking: "Rachando",
      hatching: "Eclodindo",
      baby: "Filhote",
      young: "Jovem",
      adult: "Adulto"
    };
    return labels[String(stage).toLowerCase()] ?? escapeHtml(String(stage));
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

  // Painel unico (components/modal.js promptPermanentTaskForm), mesmo padrao
  // do editor de tarefas do dia: Tipo e Periodo como grupos de botoes,
  // recorrencia com os blocos de dias/variantes revelados so quando fazem
  // sentido, Opcoes e Motivos como listas editaveis -- tudo isso substitui a
  // antiga cadeia de window.prompt/window.confirm (que pedia, por exemplo,
  // digitar "mandatory, expected ou challenge" por extenso).
  async function createPermanentTask() {
    const result = await promptPermanentTaskForm({
      title: "Nova tarefa permanente",
      values: { type: "challenge", points: 1, period: "morning" },
      confirmLabel: "Adicionar"
    });

    if (!result) return;

    try {
      const created = await createTask(result);

      tasks = [created, ...tasks];

      showToast("Tarefa permanente criada.");

      draw();
    } catch (err) {
      showToast(err.message, { error: true });
    }
  }

  async function editPermanentTask(id) {
    const task = tasks.find((item) => String(item.id) === String(id));
    if (!task) return;

    const result = await promptPermanentTaskForm({
      title: "Editar tarefa",
      values: task,
      confirmLabel: "Salvar"
    });

    if (!result) return;

    try {
      const updated = await updateTask(id, result);

      tasks = tasks.map((item) => (String(item.id) === String(id) ? updated : item));

      showToast("Tarefa permanente atualizada.");

      draw();
    } catch (err) {
      showToast(err.message, { error: true });
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
