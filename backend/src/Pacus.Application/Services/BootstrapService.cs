using System.Text.RegularExpressions;
using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using PacusEntity = Pacus.Domain.Entities.Pacus;
using Pacus.Application.Exceptions;

namespace Pacus.Application.Services;

public class BootstrapService : IBootstrapService
{
    private const string ChildDataConsentVersion = "2026-09-04";
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private const int MaxFamilyCodeAttempts = 10;

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
        // Este endpoint e anonimo (qualquer um pode chamar pra criar uma familia
        // nova) e, ate agora, nao validava nada -- um POST com campos vazios ou
        // mal formados criava uma familia quebrada silenciosamente. Auditoria de
        // seguranca, achado adicional junto do sistema de codigo de familia.
        ValidateRequest(request);

        var email = request.AdultEmail.Trim().ToLowerInvariant();

        var existingAdult =
            await _userRepository.GetByEmailAsync(email);

        if (existingAdult is not null)
        {
            throw new ConflictException(
                "Ja existe um usuario adulto com este email.");
        }

        var now = DateTime.UtcNow;
        var familyId = ObjectId.GenerateNewId();

        // Recovery code do "esqueci minha senha" (ver AuthService.ResetAdultPasswordAsync) --
        // so aparece em texto puro aqui, uma unica vez, na resposta deste endpoint. O frontend
        // deve orientar o adulto a guardar em lugar seguro antes de sair da tela.
        var recoveryCode = AuthService.GenerateRecoveryCode();

        // Codigo curto da familia (ver User.FamilyCode) -- gerado com checagem de
        // unicidade porque, ao contrario do email, nao ha indice unico do banco
        // garantindo isso sozinho (o espaco de codigos e pequeno o bastante pra
        // colisao ser um evento real, nao so teorico).
        var familyCode = await GenerateUniqueFamilyCodeAsync();

        var adult = new User
        {
            Id = ObjectId.GenerateNewId(),
            Role = UserRole.Adult,
            Name = request.AdultName,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.AdultPassword),
            RecoveryCodeHash = _passwordHasher.Hash(recoveryCode),
            ChildDataConsentAt = now,
            ChildDataConsentVersion = ChildDataConsentVersion,
            Timezone = "America/Sao_Paulo",
            FamilyCode = familyCode,
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
            FamilyCode = familyCode,
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
            recoveryCode,
            familyCode
        );
    }

    private static void ValidateRequest(BootstrapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AdultName))
            throw new ValidationException("Nome do adulto e obrigatorio.");

        if (string.IsNullOrWhiteSpace(request.ChildName))
            throw new ValidationException("Nome da crianca e obrigatorio.");

        if (string.IsNullOrWhiteSpace(request.AdultEmail) || !EmailPattern.IsMatch(request.AdultEmail.Trim()))
            throw new ValidationException("Email invalido.");

        if (string.IsNullOrEmpty(request.AdultPassword) || request.AdultPassword.Length < 8)
            throw new ValidationException("A senha deve ter pelo menos 8 caracteres.");

        if (!Regex.IsMatch(request.ChildPin ?? string.Empty, "^[0-9]{4}$"))
            throw new ValidationException("O PIN da crianca deve ter exatamente 4 digitos numericos.");

        if (!request.ResponsibleConsent)
            throw new ValidationException("O aceite do responsavel pelo tratamento dos dados da crianca e obrigatorio.");
    }

    private async Task<string> GenerateUniqueFamilyCodeAsync()
    {
        for (var attempt = 0; attempt < MaxFamilyCodeAttempts; attempt++)
        {
            var candidate = AuthService.GenerateFamilyCode();
            var existing = await _userRepository.GetByFamilyCodeAsync(candidate);
            if (existing.Count == 0) return candidate;
        }

        // Espaco de codigos e grande (33^6), entao chegar aqui na pratica so
        // acontece se o repositorio estiver com problema -- mas preferimos falhar
        // alto (ConflictException -> 409) a devolver um codigo que colide.
        throw new ConflictException("Nao foi possivel gerar um codigo de familia unico. Tente novamente.");
    }
}

