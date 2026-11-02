namespace GymManagement.Domain.Enums;

public enum TenantStatus
{
    Active = 1,
    Suspended = 2,
    Pending = 3
}

public enum InvoicePaymentStatus
{
    Unpaid = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Cancelled = 4
}

public enum UserRole
{
    SuperAdmin = 1,
    TenantAdmin = 2,
    BranchManager = 3,
    FrontDeskStaff = 4
}

public enum MemberStatus
{
    Active = 1,
    Expired = 2,
    Frozen = 3,
    Pending = 4
}

public enum CheckInStatus
{
    Accepted = 1,
    Denied = 2
}

public enum CheckInMethod
{
    ManualKioskScan = 1,
    GeofenceAuto = 2,
    BleBeacon = 3
}

public enum StockTransactionType
{
    InitialOpening = 1,
    PurchaseIn = 2,
    SaleOut = 3,
    TransferOut = 4,
    TransferIn = 5,
    Adjustment = 6,
    DamageLost = 7
}

public enum StockRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    InTransit = 4,
    Received = 5
}

public enum PaymentMethod
{
    Cash = 1,
    CreditCard = 2,
    KBZPay = 3,
    WavePay = 4,
    BankTransfer = 5
}

public enum RevenueTransactionType
{
    MemberFee = 1,
    StockSale = 2
}

public enum AdjustmentActionType
{
    VoidRefund = 1,
    AmountCorrection = 2,
    PaymentMethodCorrection = 3
}

public enum AdjustmentApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

