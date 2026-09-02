namespace Pacus.Application.DTOs;

// Usado pela tela de login da crianca: so nome + id, nunca PIN ou dado sensivel —
// e por isso que pode ser cacheado no frontend (localStorage) sem risco.
public record ChildProfileDto(string Id, string Name);

public record UpdateChildPinRequest(string NewPin);

public record UpdateTimezoneRequest(string Timezone);
