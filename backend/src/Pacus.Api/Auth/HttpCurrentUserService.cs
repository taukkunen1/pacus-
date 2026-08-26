using System.Security.Claims;
using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Api.Auth;

public class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    public ObjectId UserId
    {
        get
        {
            var value =
                Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Principal?.FindFirst("sub")?.Value;

            return value is not null &&
                   ObjectId.TryParse(value, out var id)
                ? id
                : ObjectId.Empty;
        }
    }

    public UserRole Role
    {
        get
        {
            var role =
                Principal?.FindFirst(ClaimTypes.Role)?.Value
                ?? Principal?.FindFirst("role")?.Value;

            return Enum.TryParse<UserRole>(
                role,
                ignoreCase: true,
                out var parsed)
                ? parsed
                : UserRole.Child;
        }
    }

    public ObjectId FamilyId
    {
        get
        {
            var familyId =
                Principal?.FindFirst("familyId")?.Value;

            return familyId is not null &&
                   ObjectId.TryParse(familyId, out var id)
                ? id
                : ObjectId.Empty;
        }
    }
}