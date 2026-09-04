import { apiClient } from "../api/api-client.js";
import { renderTank } from "../pacus/habitat.js";
import { showToast } from "../components/toast.js";
import { promptInput } from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";
import { appState } from "../state/app-state.js";
import {
  getFamilyChildren,
  updateChildPin,
  getFamilyTimezone,
  updateFamilyTimezone,
  generateRecoveryCode
} from "../api/family-api.js";

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
  let growthStages = [];
  let familyTimezone = "";

  try {
    pacus = await apiClient("/pacus/me");
  } catch (err) {
    console.warn(
      "PACUS nao encontrado.",
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

      ${renderBottomNav("pacus")}
    `;

    content
      .querySelector("#back")
      ?.addEventListener(
        "click",
        () => navigate("today")
      );

    attachBottomNav(content, navigate);

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
}

function escapeHtml(value = "") {
  const div =
    document.createElement("div");

  div.textContent = value;

  return div.innerHTML;
}
