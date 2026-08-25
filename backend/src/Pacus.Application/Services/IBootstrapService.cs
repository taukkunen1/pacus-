using Pacus.Application.DTOs;

namespace Pacus.Application.Services;

public interface IBootstrapService
{
    Task<BootstrapResponse> CreateInitialFamilyAsync(BootstrapRequest request);
}