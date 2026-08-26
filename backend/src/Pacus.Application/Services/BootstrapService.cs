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
    private readonly IPasswordHasher _passwordHasher;

    public BootstrapService(
        IUserRepository userRepository,
        IPacusRepository pacusRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _pacusRepository = pacusRepository;
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

        var adult = new User
        {
            Id = ObjectId.GenerateNewId(),
            Role = UserRole.Adult,
            Name = request.AdultName,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.AdultPassword),
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

        return new BootstrapResponse(
            adult.Id.ToString(),
            child.Id.ToString(),
            familyId.ToString(),
            pacus.Id.ToString(),
            "Familia PACUS criada com sucesso."
        );
    }
}

