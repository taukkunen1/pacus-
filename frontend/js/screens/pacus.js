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
import { appState } from "../state/app-state.js";
import {
  getFamilyChildren,
  updateChildPin,
  getFamilyTimezone,
  updateFamilyTimezone,
  generateRecoveryCode
} from "../api/family-api.js";

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

// Pergunta o titulo + descricao + pontos de cada dia util pra uma tarefa
// "rotativa" (atividade diferente de segunda a sexta, ex.: Momento Criativo).
// `defaultPoints` e o valor ja escolhido pra tarefa como um todo (perguntado
// antes, na tela principal) -- serve de sugestao pra cada dia, mas cada
// missao pode valer diferente (ex.: uma que exige supervisao de um adulto
// vale mais que uma rapida e sozinha). Retorna null se a pessoa cancelar em
// qualquer dia (o chamador deve abortar a criacao inteira nesse caso, pra
// nao criar uma rotina pela metade).
async function promptWeekdayVariants(defaultPoints) {
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

    const pointsRaw = window.prompt(
      `Pontos de ${label}:\n${POINTS_HELP_TEXT}`,
      String(defaultPoints)
    );

    if (pointsRaw === null) {
      return null;
    }

    const dayPoints = Number(pointsRaw);
    if (!isValidPoints(dayPoints)) {
      showToast(
        `Pontos inválidos em ${label}. ${POINTS_HELP_TEXT}.`,
        { error: true }
      );
      return null;
    }

    variants.push({
      dayOfWeek: key,
      title: dayTitle.trim(),
      description: dayDescription?.trim() || null,
      points: dayPoints
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
// Enter na maioria dos casos. `basePoints` e o valor de pontos ja escolhido
// pra tarefa nesta mesma tela (usado como sugestao pra opcao 5, atividade
// diferente por dia).
async function promptRecurrenceChoice(existingTask, basePoints) {
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
      const variants = await promptWeekdayVariants(basePoints);
      if (!variants) return null;
      return { recurrence: "weekday_rotation", variants };
    }

    default:
      return { recurrence: "daily" };
  }
}

// Pergunta as opcoes de escolha (2-4) que a crianca vai poder selecionar
// antes de concluir a tarefa (Teoria da Autodeterminacao -- docs/PROPOSITO.md).
// Mesma logica de frontend/js/screens/home.js (duplicada aqui em vez de
// compartilhada -- os dois arquivos ja duplicam varios outros prompts, e nao
// ha um modulo comum de "prompts de tarefa" ainda). Retorna null se a pessoa
// cancelar depois de comecar a preencher (o chamador deve abortar).
function promptForOptions(currentOptions) {
  const hasCurrent = Array.isArray(currentOptions) && currentOptions.length > 0;

  const wantsOptions = window.confirm(
    hasCurrent
      ? "Esta tarefa tem opções pra escolher. Editar as opções?\n\nOK = Sim\nCancelar = Manter como está"
      : "Adicionar opções pra criança escolher entre missões (ex.: torre de copos / ponte de papel)?\n\nOK = Sim\nCancelar = Não, tarefa comum"
  );

  if (!wantsOptions) {
    return hasCurrent ? currentOptions : [];
  }

  const options = [];
  for (let i = 1; i <= 4; i++) {
    const suggestion = currentOptions?.[i - 1] ?? "";
    const raw = window.prompt(
      `Opção ${i}${i > 2 ? " (deixe em branco pra parar)" : ""}:`,
      suggestion
    );

    if (raw === null) return null;

    const trimmed = raw.trim();
    if (!trimmed) {
      if (i <= 2) {
        showToast(
          "Uma tarefa com opções precisa de pelo menos 2.",
          { error: true }
        );
        return null;
      }
      break;
    }

    options.push(trimmed);
  }

  return options;
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
    const timezone = window.prompt(
      "Fuso horário da família (formato IANA, ex.: America/Sao_Paulo):",
      familyTimezone || "America/Sao_Paulo"
    );
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
      const choice = Number(window.prompt(`Qual criança?\n${names}`, "1"));
      const chosen = children[choice - 1];
      if (!chosen) return;
      childId = chosen.id;
    }

    const newPin = window.prompt("Novo PIN (4 dígitos):");
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
    const stage = window
      .prompt(`Estágio: ${stageNames.join(", ")}`, "adult")
      ?.trim()
      .toLowerCase();

    if (!stageNames.includes(stage)) {
      showToast(`Estágio inválido. Use um de: ${stageNames.join(", ")}.`, { error: true });
      return;
    }

    const date = window.prompt("A partir de qual data (AAAA-MM-DD)?", "");
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
    const recurrenceChoice = await promptRecurrenceChoice(null, points);
    if (!recurrenceChoice) {
      return;
    }

    const options = promptForOptions(null);
    if (options === null) {
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
        options,
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
    const recurrenceChoice = await promptRecurrenceChoice(task, points);
    if (!recurrenceChoice) {
      return;
    }

    const options = promptForOptions(task.options);
    if (options === null) {
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
          options,
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