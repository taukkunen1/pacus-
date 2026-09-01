import { apiClient } from "./api-client.js";

export const getFamilyChildren = () => apiClient("/family/children");

export const updateChildPin = (childId, newPin) =>
  apiClient(`/family/children/${childId}/pin`, {
    method: "PUT",
    body: JSON.stringify({ newPin }),
  });

export const getFamilyTimezone = () => apiClient("/family/timezone");

export const updateFamilyTimezone = (timezone) =>
  apiClient("/family/timezone", {
    method: "PUT",
    body: JSON.stringify({ timezone }),
  });

export const generateRecoveryCode = () =>
  apiClient("/family/recovery-code", { method: "POST" });
