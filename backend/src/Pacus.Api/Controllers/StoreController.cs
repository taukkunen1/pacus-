using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/store")]
public class StoreController : ControllerBase
{
    private readonly IStoreRepository _storeRepository;
    private readonly IStoreService _storeService;
    private readonly ICurrentUserService _currentUser;

    public StoreController(
        IStoreRepository storeRepository,
        IStoreService storeService,
        ICurrentUserService currentUser)
    {
        _storeRepository = storeRepository;
        _storeService = storeService;
        _currentUser = currentUser;
    }

    // Ambos os papeis podem visualizar a loja.
    [HttpGet("items")]
    public async Task<IActionResult> GetItems()
    {
        var items = await _storeRepository.GetActiveItemsAsync(_currentUser.FamilyId);

        return Ok(items.Select(ToStoreItemResponse));
    }

    // Somente adulto cria itens.
    [RequireRole(UserRole.Adult)]
    [HttpPost("items")]
    public async Task<IActionResult> CreateItem(
        [FromBody] CreateStoreItemRequest request)
    {
        var item = await _storeService.CreateItemAsync(
            _currentUser.FamilyId,
            _currentUser.UserId,
            request);

        return CreatedAtAction(
            nameof(GetItems),
            new { },
            ToStoreItemResponse(item));
    }

    // A crianca solicita o resgate.
    [HttpPost("redemptions")]
    public async Task<IActionResult> RequestRedemption(
        [FromBody] RequestRedemptionRequest request)
    {
        try
        {
            if (!ObjectId.TryParse(request.StoreItemId, out var storeItemId))
            {
                return BadRequest(new
                {
                    error = "Id do item da loja invalido."
                });
            }

            var redemption = await _storeService.RequestRedemptionAsync(
                _currentUser.FamilyId,
                _currentUser.UserId,
                storeItemId);

            return Ok(ToRedemptionResponse(redemption));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }

    // Somente adulto aprova.
    [RequireRole(UserRole.Adult)]
    [HttpPut("redemptions/{id}/approve")]
    public async Task<IActionResult> ApproveRedemption(string id)
    {
        try
        {
            var redemption = await _storeService.ApproveRedemptionAsync(
                _currentUser.FamilyId,
                id,
                _currentUser.UserId);

            return Ok(ToRedemptionResponse(redemption));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }

    // Somente adulto rejeita.
    [RequireRole(UserRole.Adult)]
    [HttpPut("redemptions/{id}/reject")]
    public async Task<IActionResult> RejectRedemption(string id)
    {
        try
        {
            var redemption = await _storeService.RejectRedemptionAsync(
                _currentUser.FamilyId,
                id,
                _currentUser.UserId);

            return Ok(ToRedemptionResponse(redemption));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }

    private static StoreItemResponse ToStoreItemResponse(StoreItem item)
    {
        return new StoreItemResponse(
            item.Id.ToString(),
            item.FamilyId.ToString(),
            item.Title,
            item.Description,
            item.Cost,
            item.Category,
            item.Icon,
            item.Active,
            item.Stock,
            item.CreatedBy.ToString(),
            item.CreatedAt,
            item.UpdatedAt);
    }

    private static RedemptionResponse ToRedemptionResponse(Redemption redemption)
    {
        return new RedemptionResponse(
            redemption.Id.ToString(),
            redemption.FamilyId.ToString(),
            redemption.StoreItemId.ToString(),
            redemption.ItemTitle,
            redemption.Cost,
            redemption.Status.ToString(),
            redemption.RequestedBy.ToString(),
            redemption.RequestedAt,
            redemption.ReviewedBy?.ToString(),
            redemption.ReviewedAt);
    }
}