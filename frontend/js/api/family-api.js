import { apiClient } from "./api-client.js";

export const getFamilyChildren = () => apiClient("/family/children");
