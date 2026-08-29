using Pacus.Domain.Entities;

using PacusEntity = Pacus.Domain.Entities.Pacus;

namespace Pacus.Application.DTOs;

// Exportacao completa dos dados da familia (LGPD, item B2 -- portabilidade de
// dados, art. 18, V). Espelha exatamente as 12 collections listadas em
// docs/DATA_MAP.md. Nao inclui passwordHash/pinHash (nao sao "dados do
// titular" no sentido de portabilidade -- sao segredos de autenticacao; ver
// FamilyMemberExport abaixo).
public record FamilyDataExport(
    DateTime ExportedAt,
    string FamilyId,
    List<FamilyMemberExport> Members,
    PacusEntity? Pacus,
    Habitat? Habitat,
    Settings? Settings,
    List<DailyRoutine> DailyRoutines,
    List<TaskTemplate> TaskTemplates,
    List<PointTransaction> PointTransactions,
    List<PacusGrowthLog> PacusGrowthLogs,
    List<TaskEvent> TaskEvents,
    List<StoreItem> StoreItems,
    List<Redemption> Redemptions,
    List<AuditLog> AuditLogs
);

// Projecao de User sem passwordHash/pinHash -- exportar um hash de senha nao
// da ao titular nenhum dado util e e um segredo de seguranca, nao um dado
// pessoal no sentido de portabilidade (ver docs/DATA_MAP.md, secao 1).
public record FamilyMemberExport(
    string Id,
    string Role,
    string Name,
    string? Email,
    string Timezone,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
