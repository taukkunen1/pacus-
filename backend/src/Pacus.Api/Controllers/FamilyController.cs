using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

// Autorizado para qualquer papel (adulto ou crianca) — so devolve nome + id,
// nunca PIN/senha, entao nao ha problema em cachear isso no frontend.
[ApiController]
[Authorize]
[Route("api/v1/family")]
public class FamilyController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IFamilyTimezoneService _familyTimezoneService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUser;

    public FamilyController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IFamilyTimezoneService familyTimezoneService,
        IAuditLogRepository auditLogRepository,
        ICurrentUserService currentUser)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _familyTimezoneService = familyTimezoneService;
        _auditLogRepository = auditLogRepository;
        _currentUser = currentUser;
    }

    // Usado pela tela de login para trocar o campo "cole o id do perfil" por uma
    // lista com o nome de cada crianca da familia.
    [HttpGet("children")]
    public async Task<IActionResult> GetChildren()
    {
        var children = await _userRepository.GetByFamilyAndRoleAsync(_currentUser.FamilyId, UserRole.Child);

        var result = children
            .Select(c => new ChildProfileDto(c.Id.ToString(), c.Name))
            .ToList();

        return Ok(result);
    }

    // So o adulto troca o PIN da crianca -- ate aqui, o PIN so podia ser definido uma
    // vez, no bootstrap. Nao exige o PIN antigo (o adulto ja e o responsavel logado).
    [RequireRole(UserRole.Adult)]
    [HttpPut("children/{id}/pin")]
    public async Task<IActionResult> UpdateChildPin(string id, [FromBody] UpdateChildPinRequest request)
    {
        if (!Regex.IsMatch(request.NewPin ?? string.Empty, "^[0-9]{4}$"))
            return BadRequest(new { error = "O PIN deve ter exatamente 4 digitos numericos." });

        if (!ObjectId.TryParse(id, out var childId))
            return BadRequest(new { error = "Id de crianca invalido." });

        var child = await _userRepository.GetByIdAsync(childId);
        if (child is null || child.FamilyId != _currentUser.FamilyId || child.Role != UserRole.Child)
            return BadRequest(new { error = "Crianca nao encontrada." });

        child.PinHash = _passwordHasher.Hash(request.NewPin!);
        child.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(child);

        // Log de auditoria (mesmo padrao das outras acoes administrativas sensiveis --
        // checklist de seguranca, item A5): troca de PIN muda quem consegue logar como a crianca.
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = _currentUser.FamilyId,
            Action = "child.pin_changed",
            EntityType = "User",
            EntityId = child.Id.ToString(),
            Details = $"PIN de '{child.Name}' redefinido pelo responsavel.",
            ActorId = _currentUser.UserId,
            ActorRole = UserRole.Adult,
            CreatedAt = DateTime.UtcNow,
        });

        return NoContent();
    }

    // Fuso horario real da familia -- antes disso o dia operacional sempre calculava em
    // America/Sao_Paulo fixo, mesmo com este campo salvo (e nunca lido) desde o bootstrap.
    [HttpGet("timezone")]
    public async Task<IActionResult> GetTimezone()
    {
        var timezone = await _familyTimezoneService.GetTimezoneAsync(_currentUser.FamilyId);
        return Ok(new { timezone });
    }

    // So o adulto altera. Aplica a todos os membros da familia (adulto + crianca(s)) --
    // o valor e tratado como um unico fuso "da familia", nao por pessoa.
    [RequireRole(UserRole.Adult)]
    [HttpPut("timezone")]
    public async Task<IActionResult> UpdateTimezone([FromBody] UpdateTimezoneRequest request)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(request.Timezone);
        }
        catch (Exception)
        {
            return BadRequest(new { error = "Fuso horario invalido (use um id IANA, ex.: America/Sao_Paulo)." });
        }

        var members = await _userRepository.GetByFamilyAsync(_currentUser.FamilyId);
        foreach (var member in members)
        {
            member.Timezone = request.Timezone;
            member.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(member);
        }

        return Ok(new { timezone = request.Timezone });
    }

    // Gera (ou re-gera) o codigo de recuperacao de senha do proprio adulto logado --
    // cobre tanto quem nunca teve um (contas criadas antes deste recurso existir,
    // RecoveryCodeHash nulo) quanto quem quer trocar o codigo atual por seguranca.
    // Devolve o codigo em texto puro so nesta resposta; depois so o hash fica salvo.
    [RequireRole(UserRole.Adult)]
    [HttpPost("recovery-code")]
    public async Task<IActionResult> GenerateRecoveryCode()
    {
        var user = await _userRepository.GetByIdAsync(_currentUser.UserId);
        if (user is null) return NotFound();

        var recoveryCode = Application.Services.AuthService.GenerateRecoveryCode();
        user.RecoveryCodeHash = _passwordHasher.Hash(recoveryCode);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        return Ok(new { recoveryCode });
    }
}
