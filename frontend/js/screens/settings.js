import { apiClient } from "../api/api-client.js";
import { showToast } from "../components/toast.js";
import { promptInput } from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";
import { appState } from "../state/app-state.js";
import {
  getFamilyChildren,
  updateChildPin,
  getFamilyTimezone,
  updateFamilyTimezone,
  generateRecoveryCode,
  getFamilyCode
} from "../api/family-api.js";

// Painel do adulto (fuso horario, codigo de recuperacao, PIN da crianca,
// calendario de crescimento do PACUS) -- morava dentro da aba PACUS
// (screens/pacus.js), misturado com a tela do bichinho de estimacao. Virou
// aba propria "Config" (ver components/bottom-nav.js), visivel so pro adulto
// -- a aba PACUS agora e so o bichinho e o historico de estagios dele.
export async function renderSettings(root, navigate) {
  const isAdult = (appState.user?.role ?? "").toLowerCase() === "adult";

  // A aba "Config" nem aparece na barra pra quem nao e adulto (ver
  // components/bottom-nav.js), mas o hash #settings pode ser digitado direto
  // na URL -- entao a tela tambem se protege sozinha, igual as outras acoes
  // de adulto do app.
  if (!isAdult) {
    navigate("today");
    return;
  }

  root.innerHTML = `
    <div class="screen">
      <div class="container">
        <p class="task-empty">Carregando configurações...</p>
      </div>
    </div>
  `;

  const content = root.querySelector(".container");

  let growthStages = [];
  let familyTimezone = "";
  let familyCode = "";

  try {
    [growthStages, familyTimezone, familyCode] = await Promise.all([
      apiClient("/settings/growth-stages"),
      getFamilyTimezone().then((res) => res?.timezone ?? ""),
      getFamilyCode().then((res) => res?.familyCode ?? "")
    ]);
  } catch (err) {
    console.warn("Nao foi possivel carregar configuracoes da familia.", err);
  }

  draw();

  function draw() {
    content.innerHTML = `
      <div class="screen-header">
        <div>
          <p class="eyebrow">PAINEL DO ADULTO</p>
          <h1>Configurações da família</h1>
        </div>
      </div>

      <section class="task-management">
        <div class="task-card">
          <div class="task-card__content">
            <strong class="task-title">Código da família</strong>
            <span class="task-description">
              ${familyCode ? `<span class="register-code register-code--inline">${escapeHtml(familyCode)}</span>` : "não disponível"} — a criança digita este código para logar em um novo aparelho, no lugar de colar um id.
            </span>
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

      ${renderBottomNav("settings")}
    `;

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
  const div = document.createElement("div");
  div.textContent = value;
  return div.innerHTML;
}
