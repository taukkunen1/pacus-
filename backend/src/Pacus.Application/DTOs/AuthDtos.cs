namespace Pacus.Application.DTOs;

public record AdultLoginRequest(string Email, string Password);

// userId identifica qual perfil de crianca dentro da familia (pode evoluir para uma tela
// de selecao de perfil antes de pedir o PIN, mas o contrato ja fica pronto para isso).
public record ChildLoginRequest(string UserId, string Pin);

public record AuthResponse(string Token, string UserId, string Role, string Name, DateTime ExpiresAt);
