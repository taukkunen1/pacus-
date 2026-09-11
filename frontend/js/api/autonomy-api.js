import { apiClient } from "./api-client.js";

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md).

// initiative: "selfStarted" | "promptedByPacus" | "promptedByAdult" (ver
// Pacus.Domain.Enums.TaskInitiativeLevel -- os enums do backend viram string
// camelCase na API, ver Program.cs/JsonStringEnumConverter).
export const setTaskInitiative = (id, initiative) =>
  apiClient(`/daily-tasks/${id}/initiative`, {
    method: "PUT",
    body: JSON.stringify({ initiative }),
  });

// reason: "sleepy" | "preferredOtherActivity" | "noTime" | "notInTheMood" |
// "disliked" | "forgot" | "other" (ver Pacus.Domain.Enums.TaskSkipReason).
// note so e usado quando reason === "other".
export const setTaskSkipReason = (id, reason, note = null) =>
  apiClient(`/daily-tasks/${id}/skip-reason`, {
    method: "PUT",
    body: JSON.stringify({ reason, note }),
  });

// items: [{ taskId, approxLabel? }], na ordem escolhida pela crianca.
export const setEveningPlan = (items) =>
  apiClient("/daily-routines/today/evening-plan", {
    method: "PUT",
    body: JSON.stringify({ items }),
  });

export const getWeeklyAutonomyReport = () =>
  apiClient("/autonomy/weekly");

