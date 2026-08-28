import { apiClient } from "./api-client.js";

export function getTodayRoutine() {
  return apiClient("/daily-routines/today");
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
