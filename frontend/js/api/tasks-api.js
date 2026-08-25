import { apiClient } from "./api-client.js";

export const createDailyTask = (payload) => apiClient("/daily-tasks", {
  method: "POST", body: JSON.stringify(payload)
});

export const updateDailyTask = (id, payload) => apiClient(`/daily-tasks/${id}`, {
  method: "PUT", body: JSON.stringify(payload)
});

export const deleteDailyTask = (id) => apiClient(`/daily-tasks/${id}`, { method: "DELETE" });

export const reorderDailyTasks = (orderedTaskIds) => apiClient("/daily-routines/today/order", {
  method: "PUT", body: JSON.stringify(orderedTaskIds)
});
