using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.DTOs;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Events;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly GymDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly ICacheService _cache;
    private readonly IPaymentNotificationService _notificationService;

    public InventoryService(
        GymDbContext db, 
        ICurrentUserService currentUser, 
        IAuditLogService auditLog, 
        ICacheService cache,
        IPaymentNotificationService? notificationService = null)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _cache = cache;
        _notificationService = notificationService ?? new NullPaymentNotificationService();
    }

    public async Task<List<ProductDto>> GetProductsAsync()
    {
        var products = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.StockBalances)
                .ThenInclude(s => s.Branch)
            .Where(p => p.IsActive)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var activePromos = await _db.ProductPromotions
            .Where(p => p.IsActive && p.ValidFrom <= now && p.ValidTo >= now)
            .ToListAsync();

        int? currentBranchId = _currentUser.BranchId;

        return products.Select(p =>
        {
            var promo = activePromos.FirstOrDefault(pr => pr.ProductId == p.Id);
            int totalStock = p.StockBalances.Sum(s => s.QuantityOnHand);
            int branchStock = currentBranchId.HasValue 
                ? p.StockBalances.Where(s => s.BranchId == currentBranchId.Value).Sum(s => s.QuantityOnHand)
                : totalStock;

            var branchBreakdown = p.StockBalances
                .Where(s => s.QuantityOnHand > 0)
                .Select(s => new BranchStockDetailDto
                {
                    BranchId = s.BranchId ?? 0,
                    BranchName = s.Branch?.BranchName ?? "Main Branch",
                    QuantityOnHand = s.QuantityOnHand
                })
                .OrderBy(b => b.BranchName)
                .ToList();

            return new ProductDto
            {
                Id = p.Id,
                TenantId = p.TenantId,
                ProductCategoryId = p.ProductCategoryId,
                CategoryName = p.Category?.CategoryName ?? "General",
                Barcode = p.Barcode,
                ProductName = p.ProductName,
                UnitOfMeasure = p.UnitOfMeasure,
                CostPrice = p.CostPrice,
                SalePrice = p.SalePrice,
                PromoPrice = promo?.PromoDiscountPrice,
                TotalStock = totalStock,
                BranchStock = branchStock,
                LowStockAlertQuantity = p.LowStockAlertQuantity,
                BranchStocks = branchBreakdown
            };
        }).ToList();
    }

    public async Task<ProductDto> CreateProductAsync(ProductDto dto)
    {
        int tenantId = _currentUser.TenantId ?? dto.TenantId;
        if (tenantId <= 0)
        {
            var firstTenant = await _db.Tenants.FirstOrDefaultAsync();
            tenantId = firstTenant?.Id ?? 1;
        }

        var product = new Product
        {
            TenantId = tenantId,
            ProductCategoryId = dto.ProductCategoryId > 0 ? dto.ProductCategoryId : 1,
            Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? $"P-{DateTime.UtcNow.Ticks % 1000000}" : dto.Barcode,
            ProductName = dto.ProductName,
            UnitOfMeasure = dto.UnitOfMeasure,
            CostPrice = dto.CostPrice,
            SalePrice = dto.SalePrice,
            LowStockAlertQuantity = dto.LowStockAlertQuantity > 0 ? dto.LowStockAlertQuantity : 5,
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // Initialize stock balance for branch if provided
        int? branchId = _currentUser.BranchId;
        if (branchId.HasValue)
        {
            _db.StockBalances.Add(new StockBalance
            {
                TenantId = tenantId,
                BranchId = branchId.Value,
                ProductId = product.Id,
                QuantityOnHand = dto.BranchStock
            });

            _db.StockTransactions.Add(new StockTransaction
            {
                TenantId = tenantId,
                BranchId = branchId.Value,
                ProductId = product.Id,
                Type = StockTransactionType.InitialOpening,
                Quantity = dto.BranchStock,
                UnitCost = dto.CostPrice,
                UnitSalePrice = dto.SalePrice,
                ReferenceNumber = "INIT-STOCK",
                Remarks = "Initial stock opening"
            });

            await _db.SaveChangesAsync();
        }

        dto.Id = product.Id;
        return dto;
    }

    public async Task UpdateProductPriceAsync(int productId, decimal newSalePrice, decimal newCostPrice, string reason)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return;

        var priceHistory = new ProductPriceHistory
        {
            TenantId = product.TenantId,
            ProductId = product.Id,
            OldSalePrice = product.SalePrice,
            NewSalePrice = newSalePrice,
            OldCostPrice = product.CostPrice,
            NewCostPrice = newCostPrice,
            Reason = reason,
            ChangedBy = _currentUser.Username ?? "Admin"
        };

        _db.ProductPriceHistories.Add(priceHistory);

        var oldSale = product.SalePrice;
        var oldCost = product.CostPrice;

        product.SalePrice = newSalePrice;
        product.CostPrice = newCostPrice;
        product.UpdatedAt = DateTime.UtcNow;

        product.AddDomainEvent(new ProductPriceChangedEvent(product.Id, product.TenantId, oldSale, newSalePrice, oldCost, newCostPrice, reason));

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: product.TenantId,
            branchId: _currentUser.BranchId,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "Admin",
            userRole: _currentUser.Role?.ToString() ?? "Admin",
            actionType: "PRICE_CHANGE",
            module: "Inventory",
            entityName: "Product",
            entityId: product.Id.ToString(),
            details: $"Updated pricing for product '{product.ProductName}': Sale {oldSale:N0} -> {newSalePrice:N0} MMK, Cost {oldCost:N0} -> {newCostPrice:N0} MMK. Reason: {reason}",
            oldValues: $"Sale: {oldSale:N0}, Cost: {oldCost:N0}",
            newValues: $"Sale: {newSalePrice:N0}, Cost: {newCostPrice:N0}"
        );
    }

    public async Task CreatePromotionAsync(string title, int productId, decimal promoPrice, DateTime validFrom, DateTime validTo)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return;

        var promo = new ProductPromotion
        {
            TenantId = product.TenantId,
            Title = title,
            ProductId = productId,
            PromoDiscountPrice = promoPrice,
            ValidFrom = validFrom,
            ValidTo = validTo,
            IsActive = true
        };

        _db.ProductPromotions.Add(promo);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: product.TenantId,
            branchId: _currentUser.BranchId,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "Admin",
            userRole: _currentUser.Role?.ToString() ?? "Admin",
            actionType: "PROMOTION",
            module: "Inventory",
            entityName: "ProductPromotion",
            entityId: promo.Id.ToString(),
            details: $"Created promotion '{title}' for '{product.ProductName}' with promo price {promoPrice:N0} MMK (Valid {validFrom:dd MMM} - {validTo:dd MMM}).",
            newValues: $"PromoPrice: {promoPrice:N0} MMK"
        );
    }

    public async Task<List<StockBalance>> GetStockBalancesAsync(int? branchId = null)
    {
        var query = _db.StockBalances
            .Include(s => s.Product)
            .Include(s => s.Branch)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<StockRequest> CreateStockRequestAsync(int requestingBranchId, int? fulfillingBranchId, List<(int productId, int qty)> items, string requestedBy)
    {
        int tenantId = _currentUser.TenantId ?? 1;

        var count = await _db.StockRequests.CountAsync();
        var request = new StockRequest
        {
            TenantId = tenantId,
            RequestingBranchId = requestingBranchId,
            FulfillingBranchId = fulfillingBranchId,
            RequestNumber = $"REQ-{(count + 1):D5}",
            Status = StockRequestStatus.Pending,
            RequestedBy = requestedBy,
            Items = items.Select(i => new StockRequestItem
            {
                ProductId = i.productId,
                RequestedQuantity = i.qty,
                ApprovedQuantity = i.qty
            }).ToList()
        };

        _db.StockRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<List<StockRequest>> GetStockRequestsAsync()
    {
        return await _db.StockRequests
            .Include(r => r.RequestingBranch)
            .Include(r => r.FulfillingBranch)
            .Include(r => r.Items)
                .ThenInclude(i => i.Product)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task ApproveStockRequestAsync(int requestId, string approvedBy, Dictionary<int, int>? adjustedApprovedQuantities = null, string? notes = null)
    {
        var req = await _db.StockRequests
            .Include(r => r.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req == null) return;

        // Apply manual HQ quantity adjustments (allocation per branch)
        if (adjustedApprovedQuantities != null)
        {
            foreach (var item in req.Items)
            {
                if (adjustedApprovedQuantities.TryGetValue(item.Id, out var qty) || adjustedApprovedQuantities.TryGetValue(item.ProductId, out qty))
                {
                    item.ApprovedQuantity = Math.Max(0, qty);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            req.Notes = string.IsNullOrWhiteSpace(req.Notes)
                ? notes
                : $"{req.Notes} | HQ Note: {notes}";
        }

        req.Status = StockRequestStatus.InTransit;
        req.ApprovedBy = approvedBy;
        req.UpdatedAt = DateTime.UtcNow;

        int sourceBranchId = req.FulfillingBranchId ?? 1;

        // Deduct approved quantities from source/fulfilling branch (HQ Central Warehouse) upon dispatch
        foreach (var item in req.Items)
        {
            if (item.ApprovedQuantity <= 0) continue;

            var fulStock = await _db.StockBalances
                .FirstOrDefaultAsync(s => s.BranchId == sourceBranchId && s.ProductId == item.ProductId);

            if (fulStock != null)
            {
                fulStock.QuantityOnHand = Math.Max(0, fulStock.QuantityOnHand - item.ApprovedQuantity);
            }

            var prod = await _db.Products.FindAsync(item.ProductId);
            _db.StockTransactions.Add(new StockTransaction
            {
                TenantId = req.TenantId,
                BranchId = sourceBranchId,
                ProductId = item.ProductId,
                Type = StockTransactionType.TransferOut,
                Quantity = -item.ApprovedQuantity,
                UnitCost = prod?.CostPrice ?? 0,
                UnitSalePrice = prod?.SalePrice ?? 0,
                ReferenceNumber = req.RequestNumber,
                Remarks = $"Dispatched for Branch Requisition #{req.RequestNumber} to Branch #{req.RequestingBranchId}"
            });
        }

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: req.TenantId,
            branchId: sourceBranchId,
            userId: _currentUser.UserId,
            username: approvedBy,
            userRole: _currentUser.Role?.ToString() ?? "Admin",
            actionType: "STOCK_APPROVAL",
            module: "Inventory",
            entityName: "StockRequest",
            entityId: req.Id.ToString(),
            details: $"Approved & dispatched stock transfer request #{req.RequestNumber} ({req.Items.Count} line items, {req.Items.Sum(i => i.ApprovedQuantity)} total pcs approved) from Branch #{sourceBranchId} to Branch #{req.RequestingBranchId} by {approvedBy}.",
            newValues: "Status: InTransit"
        );
    }

    public async Task<StockRequest> DispatchStockFromHqAsync(int sourceBranchId, int destBranchId, List<(int productId, int qty)> items, string dispatchedBy, string? notes)
    {
        int tenantId = _currentUser.TenantId ?? 1;

        var count = await _db.StockRequests.CountAsync();
        var request = new StockRequest
        {
            TenantId = tenantId,
            RequestingBranchId = destBranchId,
            FulfillingBranchId = sourceBranchId,
            RequestNumber = $"DST-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D3}",
            Status = StockRequestStatus.InTransit,
            RequestedBy = dispatchedBy,
            ApprovedBy = dispatchedBy,
            Notes = notes,
            Items = items.Select(i => new StockRequestItem
            {
                ProductId = i.productId,
                RequestedQuantity = i.qty,
                ApprovedQuantity = i.qty
            }).ToList()
        };

        _db.StockRequests.Add(request);

        // Deduct stock from source (HQ) immediately upon dispatch
        foreach (var item in items)
        {
            var srcStock = await _db.StockBalances
                .FirstOrDefaultAsync(s => s.BranchId == sourceBranchId && s.ProductId == item.productId);

            if (srcStock != null)
            {
                srcStock.QuantityOnHand = Math.Max(0, srcStock.QuantityOnHand - item.qty);
            }

            var prod = await _db.Products.FindAsync(item.productId);
            _db.StockTransactions.Add(new StockTransaction
            {
                TenantId = tenantId,
                BranchId = sourceBranchId,
                ProductId = item.productId,
                Type = StockTransactionType.TransferOut,
                Quantity = -item.qty,
                UnitCost = prod?.CostPrice ?? 0,
                UnitSalePrice = prod?.SalePrice ?? 0,
                ReferenceNumber = request.RequestNumber,
                Remarks = $"Dispatched to Branch #{destBranchId}: {notes}"
            });
        }

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: tenantId,
            branchId: sourceBranchId,
            userId: _currentUser.UserId,
            username: dispatchedBy,
            userRole: _currentUser.Role?.ToString() ?? "Admin",
            actionType: "STOCK_DISPATCH",
            module: "Inventory",
            entityName: "StockRequest",
            entityId: request.Id.ToString(),
            details: $"Dispatched {items.Sum(i => i.qty)} items from HQ #{sourceBranchId} to Branch #{destBranchId}. Tracking: {request.RequestNumber}",
            newValues: $"Status: InTransit"
        );

        return request;
    }

    public async Task ConfirmStockReceiptAsync(int requestId, string receivedBy)
    {
        var req = await _db.StockRequests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req == null || req.Status == StockRequestStatus.Received) return;

        req.Status = StockRequestStatus.Received;
        req.UpdatedAt = DateTime.UtcNow;

        // Add stock to destination branch
        foreach (var item in req.Items)
        {
            var destStock = await _db.StockBalances
                .FirstOrDefaultAsync(s => s.BranchId == req.RequestingBranchId && s.ProductId == item.ProductId);

            if (destStock != null)
            {
                destStock.QuantityOnHand += item.ApprovedQuantity;
            }
            else
            {
                _db.StockBalances.Add(new StockBalance
                {
                    TenantId = req.TenantId,
                    BranchId = req.RequestingBranchId,
                    ProductId = item.ProductId,
                    QuantityOnHand = item.ApprovedQuantity
                });
            }

            var prod = await _db.Products.FindAsync(item.ProductId);
            _db.StockTransactions.Add(new StockTransaction
            {
                TenantId = req.TenantId,
                BranchId = req.RequestingBranchId,
                ProductId = item.ProductId,
                Type = StockTransactionType.TransferIn,
                Quantity = item.ApprovedQuantity,
                UnitCost = prod?.CostPrice ?? 0,
                UnitSalePrice = prod?.SalePrice ?? 0,
                ReferenceNumber = req.RequestNumber,
                Remarks = $"Received from HQ #{req.FulfillingBranchId} by {receivedBy}"
            });
        }

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: req.TenantId,
            branchId: req.RequestingBranchId,
            userId: _currentUser.UserId,
            username: receivedBy,
            userRole: _currentUser.Role?.ToString() ?? "Staff",
            actionType: "STOCK_RECEIVED",
            module: "Inventory",
            entityName: "StockRequest",
            entityId: req.Id.ToString(),
            details: $"Branch #{req.RequestingBranchId} confirmed receipt of dispatch #{req.RequestNumber} by {receivedBy}.",
            newValues: $"Status: Received"
        );
    }

    public async Task<SalesOrder> ProcessSalesOrderAsync(int? memberId, int branchId, List<(int productId, int qty, decimal price)> items, PaymentMethod method, string cashier)
    {
        int tenantId = _currentUser.TenantId ?? 1;

        var count = await _db.SalesOrders.CountAsync();
        var order = new SalesOrder
        {
            TenantId = tenantId,
            BranchId = branchId,
            MemberId = memberId,
            InvoiceNumber = $"INV-{(count + 1):D6}",
            PaymentMethod = method,
            CashierName = cashier,
            CreatedAt = DateTime.UtcNow
        };

        decimal subtotal = 0;

        foreach (var item in items)
        {
            var product = await _db.Products.FindAsync(item.productId);
            decimal cost = product?.CostPrice ?? 0;
            decimal lineTotal = item.qty * item.price;
            subtotal += lineTotal;

            order.Items.Add(new SalesOrderItem
            {
                ProductId = item.productId,
                Quantity = item.qty,
                UnitCost = cost,
                UnitPrice = item.price,
                LineTotal = lineTotal
            });

            // Deduct from stock balance
            var stock = await _db.StockBalances
                .FirstOrDefaultAsync(s => s.BranchId == branchId && s.ProductId == item.productId);

            if (stock != null)
            {
                stock.QuantityOnHand = Math.Max(0, stock.QuantityOnHand - item.qty);
                if (product != null && stock.QuantityOnHand <= product.LowStockAlertQuantity)
                {
                    stock.AddDomainEvent(new LowStockAlertEvent(product.Id, tenantId, branchId, product.ProductName, stock.QuantityOnHand, product.LowStockAlertQuantity));
                }
            }

            // Record transaction ledger
            _db.StockTransactions.Add(new StockTransaction
            {
                TenantId = tenantId,
                BranchId = branchId,
                ProductId = item.productId,
                Type = StockTransactionType.SaleOut,
                Quantity = -item.qty,
                UnitCost = cost,
                UnitSalePrice = item.price,
                ReferenceNumber = order.InvoiceNumber,
                Remarks = $"Sold to {(memberId.HasValue ? $"Member #{memberId}" : "Walk-in")}"
            });
        }

        order.SubTotal = subtotal;
        order.TotalAmount = subtotal;

        _db.SalesOrders.Add(order);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: tenantId,
            branchId: branchId,
            userId: _currentUser.UserId,
            username: cashier,
            userRole: _currentUser.Role?.ToString() ?? "Cashier",
            actionType: "POS_SALE",
            module: "Inventory",
            entityName: "SalesOrder",
            entityId: order.InvoiceNumber,
            details: $"Completed POS retail sale {order.InvoiceNumber} for {order.TotalAmount:N0} MMK ({items.Count} items) paid via {method} by Cashier '{cashier}'.",
            newValues: $"Total: {order.TotalAmount:N0} MMK, Method: {method}"
        );

        // Real-Time Notification to Admin
        var branch = await _db.Branches.FindAsync(branchId);
        string branchName = branch?.BranchName ?? "Main Branch";
        string customerName = "Walk-in Customer";
        if (memberId.HasValue)
        {
            var member = await _db.Members.FindAsync(memberId.Value);
            if (member != null) customerName = member.FullName;
        }

        await _notificationService.NotifyPaymentAsync(new PaymentNotificationDto
        {
            TenantId = tenantId,
            BranchId = branchId,
            BranchName = branchName,
            TransactionType = "STOCK_SALE",
            ReferenceNumber = order.InvoiceNumber,
            CustomerName = customerName,
            ItemDescription = $"POS Retail Sale ({items.Count} items)",
            Amount = order.TotalAmount,
            PaymentMethod = method.ToString(),
            CashierOrStaff = cashier,
            Timestamp = DateTime.UtcNow
        });

        return order;
    }

    public async Task<List<StockPnLDto>> GetStockPnLAsync()
    {
        var salesItems = await _db.SalesOrderItems
            .Include(si => si.Product)
            .ToListAsync();

        var stockBalances = await _db.StockBalances
            .Include(sb => sb.Branch)
            .ToListAsync();

        var grouped = salesItems.GroupBy(si => si.ProductId);
        var result = new List<StockPnLDto>();

        foreach (var g in grouped)
        {
            var product = g.First().Product;
            int soldCount = g.Sum(x => x.Quantity);
            decimal revenue = g.Sum(x => x.LineTotal);
            decimal cogs = g.Sum(x => x.Quantity * x.UnitCost);
            var prodBalances = stockBalances.Where(sb => sb.ProductId == g.Key).ToList();
            int currentStock = prodBalances.Sum(sb => sb.QuantityOnHand);

            var branchBreakdown = prodBalances
                .Where(sb => sb.QuantityOnHand > 0)
                .Select(sb => new BranchStockDetailDto
                {
                    BranchId = sb.BranchId ?? 0,
                    BranchName = sb.Branch?.BranchName ?? "Main Branch",
                    QuantityOnHand = sb.QuantityOnHand
                })
                .OrderBy(b => b.BranchName)
                .ToList();

            result.Add(new StockPnLDto
            {
                ProductId = g.Key,
                ProductName = product?.ProductName ?? $"Product #{g.Key}",
                UnitsSold = soldCount,
                TotalSalesRevenue = revenue,
                TotalCostOfGoodsSold = cogs,
                CurrentStock = currentStock,
                BranchStocks = branchBreakdown
            });
        }

        // Include products that haven't sold yet
        var soldProductIds = result.Select(r => r.ProductId).ToHashSet();
        var unsoldProducts = await _db.Products.Where(p => !soldProductIds.Contains(p.Id)).ToListAsync();

        foreach (var p in unsoldProducts)
        {
            var prodBalances = stockBalances.Where(sb => sb.ProductId == p.Id).ToList();
            int currentStock = prodBalances.Sum(sb => sb.QuantityOnHand);

            var branchBreakdown = prodBalances
                .Where(sb => sb.QuantityOnHand > 0)
                .Select(sb => new BranchStockDetailDto
                {
                    BranchId = sb.BranchId ?? 0,
                    BranchName = sb.Branch?.BranchName ?? "Main Branch",
                    QuantityOnHand = sb.QuantityOnHand
                })
                .OrderBy(b => b.BranchName)
                .ToList();

            result.Add(new StockPnLDto
            {
                ProductId = p.Id,
                ProductName = p.ProductName,
                UnitsSold = 0,
                TotalSalesRevenue = 0,
                TotalCostOfGoodsSold = 0,
                CurrentStock = currentStock,
                BranchStocks = branchBreakdown
            });
        }

        return result.OrderByDescending(r => r.GrossProfit).ToList();
    }
}
