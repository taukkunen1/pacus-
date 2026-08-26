namespace Pacus.Application.DTOs;

public record CreateStoreItemRequest(
    string Title,
    string? Description,
    int Cost,
    string Category,
    string? Icon,
    int? Stock
);

public record RequestRedemptionRequest(string StoreItemId);

public record StoreItemResponse(
    string Id,
    string UserId,
    string Title,
    string? Description,
    int Cost,
    string Category,
    string? Icon,
    bool Active,
    int? Stock,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record RedemptionResponse(
    string Id,
    string UserId,
    string StoreItemId,
    string ItemTitle,
    int Cost,
    string Status,
    string RequestedBy,
    DateTime RequestedAt,
    string? ReviewedBy,
    DateTime? ReviewedAt
);