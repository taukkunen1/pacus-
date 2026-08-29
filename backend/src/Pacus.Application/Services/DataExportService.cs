using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;

using PacusEntity = Pacus.Domain.Entities.Pacus;

namespace Pacus.Application.Services;

// Reune os dados das 12 collections da familia num unico objeto (LGPD, item
// B2 -- portabilidade de dados). Le direto dos repositorios "GetAllByFamilyAsync"
// (sem os filtros de "so ativo"/"so recente" que a UI normal usa), pra garantir
// que a exportacao e realmente completa -- ver docs/DATA_MAP.md.
public class DataExportService : IDataExportService
{
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

    public DataExportService(
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

    public async Task<FamilyDataExport> ExportFamilyDataAsync(ObjectId familyId)
    {
        var users = await _userRepository.GetByFamilyAsync(familyId);
        var pacus = await _pacusRepository.GetByFamilyIdAsync(familyId);
        var habitat = await _habitatRepository.GetByFamilyIdAsync(familyId);
        var settings = await _settingsRepository.GetByUserIdAsync(familyId);
        var dailyRoutines = await _dailyRoutineRepository.GetAllByFamilyAsync(familyId);
        var taskTemplates = await _taskTemplateRepository.GetAllByFamilyAsync(familyId);
        var pointTransactions = await _pointTransactionRepository.GetAllByFamilyAsync(familyId);
        var pacusGrowthLogs = await _pacusGrowthRepository.GetAllByFamilyAsync(familyId);
        var taskEvents = await _taskEventRepository.GetAllByFamilyAsync(familyId);
        var storeItems = await _storeRepository.GetAllItemsByFamilyAsync(familyId);
        var redemptions = await _storeRepository.GetAllRedemptionsByFamilyAsync(familyId);
        var auditLogs = await _auditLogRepository.GetAllByFamilyAsync(familyId);

        var members = users
            .Select(u => new FamilyMemberExport(
                u.Id.ToString(),
                u.Role.ToString(),
                u.Name,
                u.Email,
                u.Timezone,
                u.CreatedAt,
                u.UpdatedAt))
            .ToList();

        return new FamilyDataExport(
            DateTime.UtcNow,
            familyId.ToString(),
            members,
            pacus,
            habitat,
            settings,
            dailyRoutines,
            taskTemplates,
            pointTransactions,
            pacusGrowthLogs,
            taskEvents,
            storeItems,
            redemptions,
            auditLogs);
    }
}
