using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Application.Interfaces;

// Abstrai "quem esta fazendo a requisicao agora". Implementado na camada Api (le do
// HttpContext/JWT), mas a interface mora aqui para que Application/Services dependam
// so da abstracao, nunca de ASP.NET diretamente.
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    ObjectId UserId { get; }
    UserRole Role { get; }
    ObjectId FamilyId { get; }
}
