using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

// Exportacao de dados da familia (LGPD, item B2 -- portabilidade de dados,
// art. 18, V). Restrito ao adulto: a criança nao tem como saber o que fazer
// com um arquivo de exportacao, e o dado exportado inclui informacao dos
// dois papeis da familia.
[ApiController]
[Authorize]
[RequireRole(UserRole.Adult)]
[Route("api/v1/export")]
public class ExportController : ControllerBase
{
    private readonly IDataExportService _dataExportService;
    private readonly ICurrentUserService _currentUser;

    public ExportController(
        IDataExportService dataExportService,
        ICurrentUserService currentUser)
    {
        _dataExportService = dataExportService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        var data = await _dataExportService.ExportFamilyDataAsync(_currentUser.FamilyId);

        var json = JsonSerializer.Serialize(
            data,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new ObjectIdJsonConverter() },
                WriteIndented = true,
            });

        var bytes = Encoding.UTF8.GetBytes(json);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var fileName = $"pacus-dados-{timestamp}.json";

        return File(bytes, "application/json", fileName);
    }
}
