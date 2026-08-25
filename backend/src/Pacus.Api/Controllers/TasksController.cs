using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

// Gerencia task_templates — as REGRAS PERMANENTES da rotina.
// Isso e diferente de criar uma tarefa so para o dia atual (autonomia da crianca,
// tratada em DailyTasksController); aqui e "Adulto: criar/editar/excluir tarefas
// permanentes" — administrativo por definicao da spec.
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
        var templates = await _taskTemplateRepository.GetActiveByUserAsync(_currentUser.FamilyId);
        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
        try
        {
            var template = await _taskTemplateService.CreateAsync(_currentUser.FamilyId, _currentUser.UserId, request);
            return CreatedAtAction(nameof(GetAll), new { }, template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateTaskRequest request)
    {
        try
        {
            var template = await _taskTemplateService.UpdateAsync(_currentUser.FamilyId, id, request);
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
        await _taskTemplateRepository.SoftDeleteAsync(ObjectId.Parse(id));
        return NoContent();
    }

    // "Replicar em outro dia": toda tarefa criada ad-hoc pela crianca (ou adulto) via
    // DailyTasksController ja nasce com um TaskTemplate inativo por baixo, com os mesmos
    // dados. Ativa-lo e o que a faz passar a ser gerada todos os dias — sem redigitar nada.
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> Activate(string id)
    {
        await _taskTemplateRepository.ActivateAsync(ObjectId.Parse(id));
        return NoContent();
    }
}
