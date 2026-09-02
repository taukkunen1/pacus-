import { apiClient } from "./api-client.js";

export const getStoreItems = () => apiClient("/store/items");

// Painel de gerenciamento do adulto -- inclui itens desativados.
export const getAllStoreItems = () => apiClient("/store/items/all");

export const createStoreItem = (payload) =>
  apiClient("/store/items", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const updateStoreItem = (id, payload) =>
  apiClient(`/store/items/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const setStoreItemActive = (id, active) =>
  apiClient(`/store/items/${id}/active`, {
    method: "PUT",
    body: JSON.stringify({ active }),
  });

export const requestRedemption = (storeItemId) =>
  apiClient("/store/redemptions", {
    method: "POST",
    body: JSON.stringify({ storeItemId }),
  });

export const getPendingRedemptions = () =>
  apiClient("/store/redemptions/pending");

export const approveRedemption = (id) =>
  apiClient(`/store/redemptions/${id}/approve`, { method: "PUT" });

export const rejectRedemption = (id) =>
  apiClient(`/store/redemptions/${id}/reject`, { method: "PUT" });
