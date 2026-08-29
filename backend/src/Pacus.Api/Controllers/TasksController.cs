using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[RequireRole(UserRole.Adult)]
[Route("api/v1/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskTemplateRepository _taskTemplateRepository;
    private readonly ITaskTemplateService _taskTemplateService;
    private readonly ICurrentUserService _currentUser;

    public TasksController(
        ITaskTemplateRepository taskTemplateRepository,
        ITaskTemplateService taskTemplateService,
        ICurrentUserService currentUser)
    {
        _taskTemplateRepository = taskTemplateRepository;
        _taskTemplateService = taskTemplateService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await _taskTemplateRepository
            .GetActiveByUserAsync(_currentUser.FamilyId);

        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request)
    {
        try
        {
            var template = await _taskTemplateService.CreateAsync(
                _currentUser.FamilyId,
                _currentUser.UserId,
                request);

            return CreatedAtAction(
                nameof(GetAll),
                new { },
                template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] CreateTaskRequest request)
    {
        try
        {
            var template = await _taskTemplateService.UpdateAsync(
                _currentUser.FamilyId,
                id,
                request);

            return Ok(template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            // Passa pelo Service (verifica posse via FamilyId) em vez de chamar o
            // repositorio direto — a versao anterior fazia SoftDeleteAsync(templateId)
            // sem checar de quem era o template, permitindo excluir tarefas de
            // qualquer familia so sabendo o id.
            await _taskTemplateService.DeleteAsync(
                _currentUser.FamilyId,
                id,
                _currentUser.UserId,
                _currentUser.Role.ToString());

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> Activate(string id)
    {
        try
        {
            await _taskTemplateService.ActivateAsync(
                _currentUser.FamilyId,
                id);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}