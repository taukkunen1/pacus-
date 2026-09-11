using Pacus.Domain.Entities;

namespace Pacus.Application.DTOs;

// Revisao de API (2026-09-11, achado #5): HabitatController.Update recebia a
// entidade de dominio Habitat crua como corpo da requisicao ([FromBody] Habitat).
// O controller ja reconstruia os campos sensiveis no servidor (Id, FamilyId,
// CreatedAt), entao o risco pratico era baixo, mas misturava a camada de API com
// a de dominio e deixava Theme sem nenhuma validacao. Este DTO expõe só os campos
// que o cliente de fato define.
public record UpdateHabitatRequest(
    HabitatElements? Elements,
    HabitatBounds? Bounds,
    string? Theme
);

