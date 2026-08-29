using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

// Exclusao de conta (checklist de seguranca e LGPD, item B3): apaga todos os dados
// da familia, seguindo a estrategia por collection definida no mapa de dados (B1,
// ver docs/DATA_MAP.md, "Resumo -- retencao e exclusao por collection"). Para 11 das
// 12 collections, hard delete. audit_logs e a excecao: preservado por 12 meses, mas
// anonimizado (perde o vinculo com a pessoa) -- legitimo interesse em manter um
// historico de responsabilizacao por acoes administrativas sensiveis (art. 7, IX),
// equilibrado com a minimizacao de dados exigida pela LGPD.
public class AccountDeletionService : IAccountDeletionService
{
    private static readonly TimeSpan AuditLogRetentionAfterDeletion = TimeSpan.FromDays(365);

    private readonly IUserRepository _userRepository;
    private readonly IPacusRepository _pacusRepository;
    private readonly IHabitatRepository _habitatRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IDailyRoutineRepository _dailyRoutineRepository;
    private readonly ITaskTemplateRepository _taskTemplateRepository;
    private readonly IPointTransactionRepository _pointTransactionRepository;
    private readonly IPacusGrowthRepository _pacusGrowthRepository;
    private readonly ITaskEventRepository _taskEventRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AccountDeletionService(
        IUserRepository userRepository,
        IPacusRepository pacusRepository,
        IHabitatRepository habitatRepository,
        ISettingsRepository settingsRepository,
        IDailyRoutineRepository dailyRoutineRepository,
        ITaskTemplateRepository taskTemplateRepository,
        IPointTransactionRepository pointTransactionRepository,
        IPacusGrowthRepository pacusGrowthRepository,
        ITaskEventRepository taskEventRepository,
        IStoreRepository storeRepository,
        IAuditLogRepository auditLogRepository)
    {
        _userRepository = userRepository;
        _pacusRepository = pacusRepository;
        _habitatRepository = habitatRepository;
        _settingsRepository = settingsRepository;
        _dailyRoutineRepository = dailyRoutineRepository;
        _taskTemplateRepository = taskTemplateRepository;
        _pointTransactionRepository = pointTransactionRepository;
        _pacusGrowthRepository = pacusGrowthRepository;
        _taskEventRepository = taskEventRepository;
        _storeRepository = storeRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task DeleteAccountAsync(ObjectId familyId, ObjectId requestedBy)
    {
        var now = DateTime.UtcNow;

        // Registra a propria exclusao antes de apagar qualquer coisa -- e a ultima
        // entrada de auditoria da familia, e sera anonimizada como todas as outras
        // logo em seguida (nunca fica com o ActorId exposto).
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Action = "account.deleted",
            EntityType = "Family",
            EntityId = familyId.ToString(),
            Details = "Conta excluida a pedido do titular (LGPD, art. 18, VI).",
            ActorId = requestedBy,
            ActorRole = UserRole.Adult,
            CreatedAt = now,
        });

        // audit_logs: excecao da regra -- anonimiza em vez de apagar (ver comentario
        // da classe). As outras 11 collections seguem hard delete direto.
        await _auditLogRepository.AnonymizeByFamilyAsync(familyId, now.Add(AuditLogRetentionAfterDeletion));

        await _dailyRoutineRepository.DeleteAllByFamilyAsync(familyId);
        await _taskTemplateRepository.DeleteAllByFamilyAsync(familyId);
        await _pointTransactionRepository.DeleteAllByFamilyAsync(familyId);
        await _pacusGrowthRepository.DeleteAllByFamilyAsync(familyId);
        await _taskEventRepository.DeleteAllByFamilyAsync(familyId);
        await _storeRepository.DeleteAllRedemptionsByFamilyAsync(familyId);
        await _storeRepository.DeleteAllItemsByFamilyAsync(familyId);
        await _settingsRepository.DeleteByFamilyIdAsync(familyId);
        await _habitatRepository.DeleteByFamilyIdAsync(familyId);
        await _pacusRepository.DeleteByFamilyIdAsync(familyId);

        // Users por ultimo: e o que o restante da familia (FamilyId) referencia, e e o
        // que autentica -- apagar antes cortaria o acesso no meio da operacao.
        await _userRepository.DeleteAllByFamilyAsync(familyId);
    }
}
