import { apiClient } from "../api/api-client.js";
import { renderTank } from "../pacus/habitat.js";

export async function renderPacus(root, navigate) {
  root.innerHTML = `<div class="screen"><div class="container"><p class="task-empty">Carregando PACUS...</p></div></div>`;
  const content = root.querySelector(".container");
  try {
    const pacus = await apiClient("/pacus/me");
    content.innerHTML = `
      <div class="screen-header"><div><p class="eyebrow">MEU COMPANHEIRO</p><h1>${escapeHtml(pacus.name || "Pacus")}</h1></div><button class="btn btn-ghost" id="back">Hoje</button></div>
      ${renderTank()}
      <section class="pacus-stats">
        <div><strong>${escapeHtml(String(pacus.stage ?? "juvenile"))}</strong><span>estágio</span></div>
        <div><strong>${pacus.totalClosedDays ?? 0}</strong><span>dias vividos</span></div>
        <div><strong>${Number(pacus.size ?? 0).toFixed(1)}</strong><span>tamanho</span></div>
      </section>`;
    content.querySelector("#back")?.addEventListener("click", () => navigate("today"));
  } catch (err) { content.innerHTML = `<p class="error-text">Não foi possível carregar o PACUS: ${err.message}</p>`; }
}
function escapeHtml(value = "") { const d = document.createElement("div"); d.textContent = value; return d.innerHTML; }
