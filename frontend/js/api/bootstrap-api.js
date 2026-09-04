import { apiClient } from "./api-client.js";

// Cria a familia inicial (1 adulto + 1 crianca) -- ver Pacus.Application.Services.BootstrapService.
// Devolve, entre outras coisas, o codigo de recuperacao do adulto e o codigo curto
// da familia (ver User.FamilyCode), cada um em texto puro so nesta resposta.
export const createFamily = (payload) =>
  apiClient("/bootstrap", {
    method: "POST",
    body: JSON.stringify(payload),
  });
