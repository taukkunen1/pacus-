import { apiClient } from "./api-client.js";

export function getHistory({ date, from, to } = {}) {
  const params = new URLSearchParams();
  if (date) params.set("date", date);
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  const query = params.toString();
  return apiClient(`/history${query ? `?${query}` : ""}`);
}
