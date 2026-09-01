using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using PacusEntity = Pacus.Domain.Entities.Pacus;

namespace Pacus.Application.Services;

public class BootstrapService : IBootstrapService
{
    private readonly IUserRepository _userRepository;
    private readonly IPacusRepository _pacusRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IPasswordHasher _passwordHasher;

    public BootstrapService(
        IUserRepository userRepository,
        IPacusRepository pacusRepository,
        IStoreRepository storeRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _pacusRepository = pacusRepository;
        _storeRepository = storeRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<BootstrapResponse> CreateInitialFamilyAsync(
        BootstrapRequest request)
    {
        var email = request.AdultEmail.Trim().ToLowerInvariant();

        var existingAdult =
            await _userRepository.GetByEmailAsync(email);

        if (existingAdult is not null)
        {
            throw new InvalidOperationException(
                "Ja existe um usuario adulto com este email.");
        }

        var now = DateTime.UtcNow;
        var familyId = ObjectId.GenerateNewId();

        // Recovery code do "esqueci minha senha" (ver AuthService.ResetAdultPasswordAsync) --
        // so aparece em texto puro aqui, uma unica vez, na resposta deste endpoint. O frontend
        // deve orientar o adulto a guardar em lugar seguro antes de sair da tela.
        var recoveryCode = AuthService.GenerateRecoveryCode();

        var adult = new User
        {
            Id = ObjectId.GenerateNewId(),
            Role = UserRole.Adult,
            Name = request.AdultName,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.AdultPassword),
            RecoveryCodeHash = _passwordHasher.Hash(recoveryCode),
            Timezone = "America/Sao_Paulo",
            FamilyId = familyId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var child = new User
        {
            Id = ObjectId.GenerateNewId(),
            Role = UserRole.Child,
            Name = request.ChildName,
            PinHash = _passwordHasher.Hash(request.ChildPin),
            Timezone = "America/Sao_Paulo",
            FamilyId = familyId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _userRepository.CreateAsync(adult);
        await _userRepository.CreateAsync(child);

        var pacus = new PacusEntity
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Name = "Pacus",
            Species = "axolotl",
            BirthDate = now,
            Stage = PacusStage.Egg,
            Size = 1,
            TotalClosedDays = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _pacusRepository.CreateAsync(pacus);

        // Item padrao da loja de Pacus Points para toda familia nova -- pedido do dono do
        // produto: "1 hora de tela = 100 pontos", 1 resgate por dia, credita 60min no game
        // timer do dia ao ser aprovado (StoreService.ApproveRedemptionAsync). O adulto pode
        // editar/desativar depois pela tela de loja como qualquer outro item.
        var screenTimeItem = new StoreItem
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Title = "1 hora de tela",
            Description = "Resgata 1 hora extra de tempo de tela para hoje.",
            Cost = 100,
            Category = "screen_time",
            Icon = "🎮",
            Active = true,
            Stock = null,
            DailyLimit = 1,
            ScreenTimeMinutes = 60,
            CreatedBy = adult.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _storeRepository.CreateItemAsync(screenTimeItem);

        return new BootstrapResponse(
            adult.Id.ToString(),
            child.Id.ToString(),
            familyId.ToString(),
            pacus.Id.ToString(),
            "Familia PACUS criada com sucesso.",
            recoveryCode
        );
    }
}

