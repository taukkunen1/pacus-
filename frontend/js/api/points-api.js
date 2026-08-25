import { apiClient } from "./api-client.js";

export const getPoints = () => apiClient("/points");
export const getPointTransactions = () => apiClient("/points/transactions");
