using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class ProductCategory : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int ProductCategoryId { get; set; }
    public ProductCategory? Category { get; set; }

    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = "pcs"; // e.g. bottle, box, scoop
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public int LowStockAlertQuantity { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    public ICollection<ProductPriceHistory> PriceHistories { get; set; } = new List<ProductPriceHistory>();
    public ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();
}

public class ProductPriceHistory : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal OldSalePrice { get; set; }
    public decimal NewSalePrice { get; set; }
    public decimal OldCostPrice { get; set; }
    public decimal NewCostPrice { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
}

public class ProductPromotion : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Title { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal PromoDiscountPrice { get; set; } // promotional selling price
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockBalance : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int QuantityOnHand { get; set; }
}

public class StockTransaction : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public StockTransactionType Type { get; set; }
    public int Quantity { get; set; } // Positive for in, negative for out
    public decimal UnitCost { get; set; }
    public decimal UnitSalePrice { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty; // e.g. Sale #101, PO #202, Requisition #303
    public string? Remarks { get; set; }
}

public class StockRequest : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int RequestingBranchId { get; set; }
    public Branch? RequestingBranch { get; set; }

    public int? FulfillingBranchId { get; set; } // Central warehouse or another branch
    public Branch? FulfillingBranch { get; set; }

    public string RequestNumber { get; set; } = string.Empty;
    public StockRequestStatus Status { get; set; } = StockRequestStatus.Pending;
    public string? RequestedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }

    public ICollection<StockRequestItem> Items { get; set; } = new List<StockRequestItem>();
}

public class StockRequestItem : BaseEntity
{
    public int StockRequestId { get; set; }
    public StockRequest? StockRequest { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int RequestedQuantity { get; set; }
    public int ApprovedQuantity { get; set; }
}

public class SalesOrder : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int? MemberId { get; set; } // Null if walk-in guest
    public Member? Member { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? CashierName { get; set; }
    public bool IsAdjustmentPending { get; set; } = false;
    public bool IsVoided { get; set; } = false;
    public string? VoidReason { get; set; }

    public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}

public class SalesOrderItem : BaseEntity
{
    public int SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
