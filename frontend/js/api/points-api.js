import { apiClient } from "./api-client.js";

export const getPoints = () => apiClient("/points");

// Paginado (achado #4 da auditoria de API de 2026-09-01, ver
// backend/docs/ESTADO_ATUAL.md): resposta e { items, page, pageSize, totalCount,
// totalPages } em vez de um array solto.
export const getPointTransactions = ({ page, pageSize } = {}) => {
  const params = new URLSearchParams();
  if (page) params.set("page", page);
  if (pageSize) params.set("pageSize", pageSize);
  const query = params.toString();
  return apiClient(`/points/transactions${query ? `?${query}` : ""}`);
};
