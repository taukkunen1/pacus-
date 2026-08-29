using MongoDB.Bson;
using Pacus.Application.DTOs;

namespace Pacus.Application.Interfaces;

public interface IDataExportService
{
    Task<FamilyDataExport> ExportFamilyDataAsync(ObjectId familyId);
}
