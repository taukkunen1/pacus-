import { apiClient } from "./api-client.js";

// O backend ja tenta se resolver sozinho quando duas requisicoes concorrentes
// colidem ao carregar a rotina de hoje (ex.: duas abas/dispositivos da familia
// abrindo a tela "Hoje" ao mesmo tempo -- ver retry em DailyRoutinesController.
// GetToday), mas isso nao cobre uma corrida entre ESTA aba e outra: duas
// chamadas emitidas de lugares diferentes no mesmo cliente (ou uma requisicao
// duplicada por uma reconexao apos o cold start lento do Render) ainda podem
// aparecer como um 409 aqui. Repetir uma vez a chamada, so pra este endpoint
// de leitura, evita jogar esse erro de corrida passageira na tela de quem so
// queria abrir o app.
export async function getTodayRoutine() {
  try {
    return await apiClient("/daily-routines/today");
  } catch (err) {
    if (err.status === 409) {
      return apiClient("/daily-routines/today");
    }
    throw err;
  }
}

export function completeTask(taskId) {
  return apiClient(`/daily-tasks/${taskId}/complete`, { method: "POST" });
}

export function reopenTask(taskId) {
  return apiClient(`/daily-tasks/${taskId}/reopen`, { method: "POST" });
}

export function createTask({ title, description, type, period, points }) {
  return apiClient("/daily-tasks", {
    method: "POST",
    body: JSON.stringify({ title, description, type, period, points }),
  });
}

export function getPointsBalance() {
  return apiClient("/points");
}

export function getPacus() {
  return apiClient("/pacus/me");
}

export function pauseGameTimer() {
  return apiClient("/daily-routines/today/game-timer/pause", { method: "PUT" });
}

export function resumeGameTimer() {
  return apiClient("/daily-routines/today/game-timer/resume", { method: "PUT" });
}

// deltaMinutes: positivo soma tempo, negativo remove (ex: -60 pra tirar 1h).
export function adjustGameTimer(deltaMinutes) {
  return apiClient("/daily-routines/today/game-timer/adjust", {
    method: "PUT",
    body: JSON.stringify({ deltaMinutes }),
  });
}

// Vinculo (relatedness -- ver docs/PROPOSITO.md e pacus/habitat.js). Restrito a adulto
// no backend; icon deve ser uma chave de REACTION_ICONS (heart/clap/star/hug).
export function setDailyReaction(icon, message) {
  return apiClient("/daily-routines/today/reaction", {
    method: "PUT",
    body: JSON.stringify({ icon, message }),
  });
}
