using GymManagement.Domain.Common;

namespace GymManagement.Domain.Events;

public record MemberEnrolledEvent(
    int MemberId, 
    int TenantId, 
    int? BranchId, 
    string FullName, 
    string MemberCode
) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ProductPriceChangedEvent(
    int ProductId, 
    int TenantId, 
    decimal OldSalePrice, 
    decimal NewSalePrice, 
    decimal OldCostPrice, 
    decimal NewCostPrice, 
    string Reason
) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record LowStockAlertEvent(
    int ProductId, 
    int TenantId, 
    int BranchId, 
    string ProductName, 
    int CurrentStock, 
    int AlertThreshold
) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record TenantInvoiceSettledEvent(
    int InvoiceId, 
    int TenantId, 
    decimal Amount, 
    string PaymentReference
) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record TurnstileAccessDeniedEvent(
    int MemberId, 
    int TenantId, 
    int BranchId, 
    string Reason
) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
