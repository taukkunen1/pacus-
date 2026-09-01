import { apiClient } from "./api-client.js";

// Sem "date", a resposta e paginada (achado #4 da auditoria de API de 2026-09-01,
// backend/docs/ESTADO_ATUAL.md): { items, page, pageSize, totalCount, totalPages }
// em vez de um array solto. Com "date", continua devolvendo o dia unico direto.
export function getHistory({ date, from, to, page, pageSize } = {}) {
  const params = new URLSearchParams();
  if (date) params.set("date", date);
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  if (page) params.set("page", page);
  if (pageSize) params.set("pageSize", pageSize);
  const query = params.toString();
  return apiClient(`/history${query ? `?${query}` : ""}`);
}
