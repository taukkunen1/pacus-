import { apiClient } from "./api-client.js";

export const getStoreItems = () => apiClient("/store/items");

export const createStoreItem = (payload) =>
  apiClient("/store/items", {
    method: "POST",
    body: JSON.stringify(payload),
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
