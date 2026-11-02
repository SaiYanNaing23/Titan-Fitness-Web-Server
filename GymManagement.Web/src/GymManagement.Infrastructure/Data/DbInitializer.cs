using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(GymDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // Ensure newly added columns exist in existing SQLite database
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberPromotions ADD COLUMN Description TEXT;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Branches ADD COLUMN Latitude REAL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Branches ADD COLUMN Longitude REAL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Branches ADD COLUMN GeofenceRadiusMeters INTEGER NOT NULL DEFAULT 50;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberCheckIns ADD COLUMN Method INTEGER NOT NULL DEFAULT 1;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberCheckIns ADD COLUMN Latitude REAL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberCheckIns ADD COLUMN Longitude REAL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberCheckIns ADD COLUMN DistanceMeters REAL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberSubscriptions ADD COLUMN IsAdjustmentPending INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberSubscriptions ADD COLUMN IsVoided INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberSubscriptions ADD COLUMN VoidReason TEXT;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE SalesOrders ADD COLUMN IsAdjustmentPending INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE SalesOrders ADD COLUMN IsVoided INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE SalesOrders ADD COLUMN VoidReason TEXT;"); } catch { }

        // Create FinancialAdjustmentRequests table if not existing in SQLite
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS FinancialAdjustmentRequests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TenantId INTEGER NOT NULL,
                    BranchId INTEGER,
                    TransactionType INTEGER NOT NULL,
                    ReferenceId INTEGER NOT NULL,
                    ReferenceNumber TEXT NOT NULL,
                    CustomerOrMemberName TEXT NOT NULL,
                    OriginalAmount REAL NOT NULL,
                    ProposedAmount REAL NOT NULL,
                    OriginalPaymentMethod INTEGER NOT NULL,
                    ProposedPaymentMethod INTEGER,
                    ActionType INTEGER NOT NULL,
                    Reason TEXT NOT NULL,
                    RequestedBy TEXT NOT NULL,
                    RequestedDate TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    ReviewedBy TEXT,
                    ReviewedDate TEXT,
                    ReviewNotes TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    DeletedAt TEXT,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS IX_FinancialAdjustmentRequests_TenantId_Status ON FinancialAdjustmentRequests (TenantId, Status);
            ");
        }
        catch { }



        // Populate sample Geofence coordinates for existing branches if not set
        try
        {
            var branchesToUpdate = await db.Branches.Where(b => b.Latitude == null).ToListAsync();
            foreach (var b in branchesToUpdate)
            {
                if (b.BranchCode == "TT-DT")
                {
                    b.Latitude = 16.7794;
                    b.Longitude = 96.1557;
                    b.GeofenceRadiusMeters = 60;
                }
                else if (b.BranchCode == "TT-HL")
                {
                    b.Latitude = 16.8276;
                    b.Longitude = 96.1306;
                    b.GeofenceRadiusMeters = 60;
                }
                else
                {
                    b.Latitude = 16.8143;
                    b.Longitude = 96.1668;
                    b.GeofenceRadiusMeters = 60;
                }
            }
            if (branchesToUpdate.Any())
            {
                await db.SaveChangesAsync();
            }
        }
        catch { }

        if (await db.Tenants.AnyAsync())
        {
            // Seed initial promotions if none exist yet in existing database
            if (!await db.MemberPromotions.AnyAsync())
            {
                var firstTenant = await db.Tenants.FirstOrDefaultAsync();
                if (firstTenant != null)
                {
                    var currentTime = DateTime.UtcNow;
                    db.MemberPromotions.AddRange(
                        new MemberPromotion
                        {
                            TenantId = firstTenant.Id,
                            PromotionTitle = "Summer Kickstart 15% OFF",
                            Description = "Special 15% discount on all multi-month fitness plans",
                            DiscountPercent = 15m,
                            DiscountAmount = 0m,
                            ValidFrom = currentTime.AddDays(-7),
                            ValidTo = currentTime.AddDays(7),
                            IsActive = true
                        },
                        new MemberPromotion
                        {
                            TenantId = firstTenant.Id,
                            PromotionTitle = "New Year Flash Sale (Expired)",
                            Description = "Limited time discount that ended earlier this month",
                            DiscountPercent = 25m,
                            DiscountAmount = 0m,
                            ValidFrom = currentTime.AddDays(-30),
                            ValidTo = currentTime.AddDays(-2),
                            IsActive = true
                        },
                        new MemberPromotion
                        {
                            TenantId = firstTenant.Id,
                            PromotionTitle = "VIP Member MMK 10,000 Rebate",
                            Description = "Instant 10,000 MMK discount off annual packages",
                            DiscountPercent = 0m,
                            DiscountAmount = 10000m,
                            ValidFrom = currentTime.AddDays(-3),
                            ValidTo = currentTime.AddDays(14),
                            IsActive = true
                        }
                    );
                    await db.SaveChangesAsync();
                }
            }
            await SeedDailyRevenueIfEmptyAsync(db);
            return; // Already seeded
        }

        // 1. Seed Tenant 1: "Titan Fitness Center" (Multi-branch chain)
        var tenant1 = new Tenant
        {
            CompanyName = "Titan Fitness Club",
            Code = "TITAN",
            ContactPerson = "U Kyaw Myint",
            ContactPhone = "09450011223",
            ContactEmail = "info@titanfitness.com",
            RatePerActiveMember = 1500m,
            Status = TenantStatus.Active,
            Subscription = new TenantSubscription
            {
                MaxBranches = 5,
                MaxStaffAccounts = 25,
                InventoryModuleEnabled = true,
                MultiBranchAccessEnabled = true
            }
        };

        // 2. Seed Tenant 2: "Flex Gym & Health" (Single branch)
        var tenant2 = new Tenant
        {
            CompanyName = "Flex Health & Gym",
            Code = "FLEX",
            ContactPerson = "Daw Hla Hla",
            ContactPhone = "09790033445",
            ContactEmail = "contact@flexgym.com",
            RatePerActiveMember = 1200m,
            Status = TenantStatus.Active,
            Subscription = new TenantSubscription
            {
                MaxBranches = 2,
                MaxStaffAccounts = 10,
                InventoryModuleEnabled = true,
                MultiBranchAccessEnabled = false
            }
        };

        db.Tenants.AddRange(tenant1, tenant2);
        await db.SaveChangesAsync();

        // 3. Branches for Tenant 1
        var branchDowntown = new Branch
        {
            TenantId = tenant1.Id,
            BranchName = "Titan Downtown (HQ)",
            BranchCode = "TT-DT",
            Address = "No. 45, Bogyoke Road, Pabedan, Yangon",
            PhoneNumber = "09450011224",
            Latitude = 16.7794,
            Longitude = 96.1557,
            GeofenceRadiusMeters = 60,
            IsActive = true
        };

        var branchHledan = new Branch
        {
            TenantId = tenant1.Id,
            BranchName = "Titan Hledan Center",
            BranchCode = "TT-HL",
            Address = "Seikkantha Street, Kamayut, Yangon",
            PhoneNumber = "09450011225",
            Latitude = 16.8276,
            Longitude = 96.1306,
            GeofenceRadiusMeters = 60,
            IsActive = true
        };

        // Branch for Tenant 2
        var branchFlexMain = new Branch
        {
            TenantId = tenant2.Id,
            BranchName = "Flex Main Branch",
            BranchCode = "FX-01",
            Address = "Sayar San Road, Bahan, Yangon",
            PhoneNumber = "09790033446",
            Latitude = 16.8143,
            Longitude = 96.1668,
            GeofenceRadiusMeters = 60,
            IsActive = true
        };

        db.Branches.AddRange(branchDowntown, branchHledan, branchFlexMain);
        await db.SaveChangesAsync();

        // 4. Staff Users
        var users = new List<StaffUser>
        {
            // Super Admin
            new() {
                TenantId = tenant1.Id,
                Username = "superadmin",
                FullName = "Platform Super Admin",
                Email = "dev@gymplatform.com",
                Phone = "09977889900",
                Role = UserRole.SuperAdmin,
                IsActive = true,
                PasswordHash = "admin123"
            },
            // Tenant 1 Admin
            new() {
                TenantId = tenant1.Id,
                BranchId = branchDowntown.Id,
                Username = "titanadmin",
                FullName = "Ko Aung Zaw (General Manager)",
                Email = "aungzaw@titanfitness.com",
                Phone = "09450011226",
                Role = UserRole.TenantAdmin,
                IsActive = true,
                PasswordHash = "admin123"
            },
            // Downtown Receptionist
            new() {
                TenantId = tenant1.Id,
                BranchId = branchDowntown.Id,
                Username = "reception_dt",
                FullName = "Ma Su Su (Front Desk)",
                Email = "susu@titanfitness.com",
                Phone = "09450011227",
                Role = UserRole.FrontDeskStaff,
                IsActive = true,
                PasswordHash = "admin123"
            },
            // Hledan Receptionist
            new() {
                TenantId = tenant1.Id,
                BranchId = branchHledan.Id,
                Username = "reception_hl",
                FullName = "Ko Min Thein (Front Desk)",
                Email = "minthein@titanfitness.com",
                Phone = "09450011228",
                Role = UserRole.FrontDeskStaff,
                IsActive = true,
                PasswordHash = "admin123"
            }
        };

        db.StaffUsers.AddRange(users);
        await db.SaveChangesAsync();

        // 5. Membership Plans for Tenant 1
        var plan1Month = new MembershipPlan
        {
            TenantId = tenant1.Id,
            PlanName = "1-Month Standard Pass",
            DurationDays = 30,
            BasePrice = 65000m,
            IsMultiBranchAllowed = true,
            IsActive = true,
            Description = "Full gym and locker access for 30 days"
        };

        var plan3Months = new MembershipPlan
        {
            TenantId = tenant1.Id,
            PlanName = "3-Months Fitness Pack",
            DurationDays = 90,
            BasePrice = 175000m,
            IsMultiBranchAllowed = true,
            IsActive = true,
            Description = "Popular 90-day pack with free towel service"
        };

        var planVIPAnnual = new MembershipPlan
        {
            TenantId = tenant1.Id,
            PlanName = "1-Year VIP Platinum All-Access",
            DurationDays = 365,
            BasePrice = 600000m,
            IsMultiBranchAllowed = true,
            IsActive = true,
            Description = "All-branch unlimited access with complimentary shaker and gym bag"
        };

        db.MembershipPlans.AddRange(plan1Month, plan3Months, planVIPAnnual);
        await db.SaveChangesAsync();

        // 5b. Seed Member Promotions
        var now = DateTime.UtcNow;
        db.MemberPromotions.AddRange(
            new MemberPromotion
            {
                TenantId = tenant1.Id,
                PromotionTitle = "Summer Kickstart 15% OFF",
                Description = "Special 15% discount on all multi-month fitness plans",
                DiscountPercent = 15m,
                DiscountAmount = 0m,
                ValidFrom = now.AddDays(-7),
                ValidTo = now.AddDays(7),
                IsActive = true
            },
            new MemberPromotion
            {
                TenantId = tenant1.Id,
                PromotionTitle = "New Year Flash Sale (Expired)",
                Description = "Limited time discount that ended earlier this month",
                DiscountPercent = 25m,
                DiscountAmount = 0m,
                ValidFrom = now.AddDays(-30),
                ValidTo = now.AddDays(-2),
                IsActive = true
            },
            new MemberPromotion
            {
                TenantId = tenant1.Id,
                PromotionTitle = "VIP Member MMK 10,000 Rebate",
                Description = "Instant 10,000 MMK discount off annual packages",
                DiscountPercent = 0m,
                DiscountAmount = 10000m,
                ValidFrom = now.AddDays(-3),
                ValidTo = now.AddDays(14),
                IsActive = true
            }
        );
        await db.SaveChangesAsync();

        // 6. Seed Members for Tenant 1

        var member1 = new Member
        {
            TenantId = tenant1.Id,
            BranchId = branchDowntown.Id,
            MemberCode = "MBR-00101",
            FullName = "Ko Thant Zin",
            PhoneNumber = "095123456",
            Gender = "Male",
            PhotoUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=150&q=80",
            EmergencyContactName = "Daw Tin Tin",
            EmergencyContactPhone = "095111222",
            Status = MemberStatus.Active
        };

        var member2 = new Member
        {
            TenantId = tenant1.Id,
            BranchId = branchDowntown.Id,
            MemberCode = "MBR-00102",
            FullName = "Ma Htet Htet",
            PhoneNumber = "097812345",
            Gender = "Female",
            PhotoUrl = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=150&q=80",
            EmergencyContactName = "U Ba Aye",
            EmergencyContactPhone = "097833344",
            Status = MemberStatus.Active
        };

        var member3Overdue = new Member
        {
            TenantId = tenant1.Id,
            BranchId = branchHledan.Id,
            MemberCode = "MBR-00103",
            FullName = "Ko Myo Min",
            PhoneNumber = "099612345",
            Gender = "Male",
            PhotoUrl = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=150&q=80",
            EmergencyContactName = "Ko Zaw",
            EmergencyContactPhone = "099699988",
            Status = MemberStatus.Expired
        };

        db.Members.AddRange(member1, member2, member3Overdue);
        await db.SaveChangesAsync();

        // 7. Seed Subscriptions
        // Member 1: Active with 25 days remaining
        db.MemberSubscriptions.Add(new MemberSubscription
        {
            TenantId = tenant1.Id,
            BranchId = branchDowntown.Id,
            MemberId = member1.Id,
            MembershipPlanId = plan1Month.Id,
            StartDate = now.Date.AddDays(-5),
            EndDate = now.Date.AddDays(25),
            OriginalPrice = 65000m,
            DiscountApplied = 0,
            FinalAmountPaid = 65000m,
            PaymentMethod = PaymentMethod.Cash,
            Status = MemberStatus.Active
        });

        // Member 2: Expiring in 2 days (Trigger Due Alert!)
        db.MemberSubscriptions.Add(new MemberSubscription
        {
            TenantId = tenant1.Id,
            BranchId = branchDowntown.Id,
            MemberId = member2.Id,
            MembershipPlanId = plan1Month.Id,
            StartDate = now.Date.AddDays(-28),
            EndDate = now.Date.AddDays(2),
            OriginalPrice = 65000m,
            DiscountApplied = 5000m,
            FinalAmountPaid = 60000m,
            PaymentMethod = PaymentMethod.KBZPay,
            Status = MemberStatus.Active
        });

        // Member 3: Expired 4 days ago
        db.MemberSubscriptions.Add(new MemberSubscription
        {
            TenantId = tenant1.Id,
            BranchId = branchHledan.Id,
            MemberId = member3Overdue.Id,
            MembershipPlanId = plan1Month.Id,
            StartDate = now.Date.AddDays(-34),
            EndDate = now.Date.AddDays(-4),
            OriginalPrice = 65000m,
            DiscountApplied = 0,
            FinalAmountPaid = 65000m,
            PaymentMethod = PaymentMethod.WavePay,
            Status = MemberStatus.Expired
        });

        // 8. Seed Product Categories & Products
        var catSupplements = new ProductCategory { TenantId = tenant1.Id, CategoryName = "Supplements", IsActive = true };
        var catDrinks = new ProductCategory { TenantId = tenant1.Id, CategoryName = "Drinks & Energy", IsActive = true };
        var catGear = new ProductCategory { TenantId = tenant1.Id, CategoryName = "Gear & Accessories", IsActive = true };

        db.ProductCategories.AddRange(catSupplements, catDrinks, catGear);
        await db.SaveChangesAsync();

        var prodWhey = new Product
        {
            TenantId = tenant1.Id,
            ProductCategoryId = catSupplements.Id,
            Barcode = "8850123001",
            ProductName = "Optimum Nutrition Gold Standard Whey 5lbs",
            UnitOfMeasure = "tub",
            CostPrice = 240000m,
            SalePrice = 295000m,
            LowStockAlertQuantity = 3,
            IsActive = true
        };

        var prodCreatine = new Product
        {
            TenantId = tenant1.Id,
            ProductCategoryId = catSupplements.Id,
            Barcode = "8850123002",
            ProductName = "Muscletech Platinum Creatine 400g",
            UnitOfMeasure = "tub",
            CostPrice = 75000m,
            SalePrice = 98000m,
            LowStockAlertQuantity = 5,
            IsActive = true
        };

        var prodDrink = new Product
        {
            TenantId = tenant1.Id,
            ProductCategoryId = catDrinks.Id,
            Barcode = "8850123003",
            ProductName = "Celsius Live Fit Sparkling Orange 355ml",
            UnitOfMeasure = "can",
            CostPrice = 6500m,
            SalePrice = 8500m,
            LowStockAlertQuantity = 10,
            IsActive = true
        };

        var prodWater = new Product
        {
            TenantId = tenant1.Id,
            ProductCategoryId = catDrinks.Id,
            Barcode = "8850123004",
            ProductName = "Purified Mineral Water 1L",
            UnitOfMeasure = "bottle",
            CostPrice = 800m,
            SalePrice = 1500m,
            LowStockAlertQuantity = 20,
            IsActive = true
        };

        var prodShaker = new Product
        {
            TenantId = tenant1.Id,
            ProductCategoryId = catGear.Id,
            Barcode = "8850123005",
            ProductName = "Titan Matte Black Shaker Bottle 700ml",
            UnitOfMeasure = "pcs",
            CostPrice = 9000m,
            SalePrice = 16000m,
            LowStockAlertQuantity = 5,
            IsActive = true
        };

        db.Products.AddRange(prodWhey, prodCreatine, prodDrink, prodWater, prodShaker);
        await db.SaveChangesAsync();

        // 9. Stock Balances
        db.StockBalances.AddRange(
            new StockBalance { TenantId = tenant1.Id, BranchId = branchDowntown.Id, ProductId = prodWhey.Id, QuantityOnHand = 8 },
            new StockBalance { TenantId = tenant1.Id, BranchId = branchDowntown.Id, ProductId = prodCreatine.Id, QuantityOnHand = 12 },
            new StockBalance { TenantId = tenant1.Id, BranchId = branchDowntown.Id, ProductId = prodDrink.Id, QuantityOnHand = 36 },
            new StockBalance { TenantId = tenant1.Id, BranchId = branchDowntown.Id, ProductId = prodWater.Id, QuantityOnHand = 85 },
            new StockBalance { TenantId = tenant1.Id, BranchId = branchDowntown.Id, ProductId = prodShaker.Id, QuantityOnHand = 15 },
            new StockBalance { TenantId = tenant1.Id, BranchId = branchHledan.Id, ProductId = prodWhey.Id, QuantityOnHand = 3 },
            new StockBalance { TenantId = tenant1.Id, BranchId = branchHledan.Id, ProductId = prodDrink.Id, QuantityOnHand = 18 }
        );

        // 10. Sample Today Check-in for Member 1
        db.MemberCheckIns.Add(new MemberCheckIn
        {
            TenantId = tenant1.Id,
            BranchId = branchDowntown.Id,
            MemberId = member1.Id,
            CheckInTime = now.AddMinutes(-45),
            Status = CheckInStatus.Accepted
        });

        // 11. Initial Monthly Invoices for Tenant 1 and 2
        db.TenantMonthlyInvoices.Add(new TenantMonthlyInvoice
        {
            TenantId = tenant1.Id,
            BillingYear = now.Year,
            BillingMonth = now.Month == 1 ? 12 : now.Month - 1,
            ActiveMemberCount = 142,
            RatePerMember = 1500m,
            TotalAmount = 142 * 1500m,
            PaymentStatus = InvoicePaymentStatus.Paid,
            PaidAt = now.AddDays(-10),
            PaymentReference = "KBZ-BANK-TR-881920",
            Notes = "Monthly postpaid settlement"
        });

        // 12. Seed Realistic Audit Trail Logs
        db.AuditLogs.AddRange(
            new AuditLog
            {
                TenantId = tenant1.Id,
                BranchId = branchDowntown.Id,
                UserId = 1,
                Username = "admin_titan",
                UserRole = "TenantAdmin",
                ActionType = "PRICE_CHANGE",
                Module = "Inventory",
                EntityName = "Product",
                EntityId = prodWhey.Id.ToString(),
                Details = "Updated pricing for 'Optimum Nutrition Gold Standard Whey 5lb': Sale 185,000 -> 195,000 MMK, Cost 140,000 -> 150,000 MMK. Reason: Supplier exchange rate increase.",
                OldValues = "Sale: 185,000 MMK, Cost: 140,000 MMK",
                NewValues = "Sale: 195,000 MMK, Cost: 150,000 MMK",
                Timestamp = now.AddDays(-2).AddHours(3)
            },
            new AuditLog
            {
                TenantId = tenant1.Id,
                BranchId = branchDowntown.Id,
                UserId = 2,
                Username = "staff_downtown",
                UserRole = "BranchStaff",
                ActionType = "FEE_COLLECTION",
                Module = "Membership",
                EntityName = "MemberSubscription",
                EntityId = "1",
                Details = "Collected fee for member 'Aung Kyaw Thu' - Plan: '6-Month Gold Pass', Amount: 380,000 MMK (KBZPay). Valid 01 Oct 2026 to 01 Apr 2027.",
                NewValues = "Paid: 380,000 MMK, Method: KBZPay",
                Timestamp = now.AddDays(-3).AddHours(5)
            },
            new AuditLog
            {
                TenantId = tenant1.Id,
                BranchId = branchDowntown.Id,
                UserId = 2,
                Username = "staff_downtown",
                UserRole = "BranchStaff",
                ActionType = "ACCESS_DENIED",
                Module = "AccessControl",
                EntityName = "Member",
                EntityId = member3Overdue.Id.ToString(),
                Details = "Access Denied for 'Ko Myo Min' (Code: MBR-00103) at Downtown Turnstile Kiosk. Reason: Membership Expired or Inactive.",
                OldValues = "Expired/Inactive",
                Timestamp = now.AddHours(-2)
            },
            new AuditLog
            {
                TenantId = tenant1.Id,
                BranchId = branchDowntown.Id,
                UserId = 2,
                Username = "staff_downtown",
                UserRole = "BranchStaff",
                ActionType = "POS_SALE",
                Module = "Inventory",
                EntityName = "SalesOrder",
                EntityId = "INV-000001",
                Details = "Completed POS retail sale INV-000001 for 18,500 MMK (2 items: Cellucor C4, Mineral Water) paid via Cash.",
                NewValues = "Total: 18,500 MMK, Method: Cash",
                Timestamp = now.AddHours(-1)
            },
            new AuditLog
            {
                TenantId = tenant1.Id,
                BranchId = null,
                UserId = 1,
                Username = "superadmin",
                UserRole = "SuperAdmin",
                ActionType = "SETTLED",
                Module = "Billing",
                EntityName = "TenantMonthlyInvoice",
                EntityId = "1",
                Details = "Settled postpaid invoice #1 for Titan Fitness Club (213,000 MMK, 142 Active Members @ 1,500 MMK). Ref: KBZ-BANK-TR-881920.",
                NewValues = "Paid: 213,000 MMK, Ref: KBZ-BANK-TR-881920",
                Timestamp = now.AddDays(-10)
            }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedDailyRevenueIfEmptyAsync(GymDbContext db)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync();
        if (tenant == null) return;

        var branches = await db.Branches.Where(b => b.TenantId == tenant.Id).ToListAsync();
        if (!branches.Any()) return;

        var dtBranch = branches.FirstOrDefault(b => b.BranchCode == "TT-DT") ?? branches.First();
        var hlBranch = branches.FirstOrDefault(b => b.BranchCode == "TT-HL") ?? branches.Last();

        var products = await db.Products.Where(p => p.TenantId == tenant.Id).ToListAsync();
        var members = await db.Members.Where(m => m.TenantId == tenant.Id).ToListAsync();
        var plans = await db.MembershipPlans.Where(p => p.TenantId == tenant.Id).ToListAsync();

        var today = DateTime.UtcNow.Date;

        // 1. Seed today subscriptions if none exist for today
        bool hasTodaySubs = await db.MemberSubscriptions.AnyAsync(s => s.CreatedAt >= today || s.StartDate == today);
        if (!hasTodaySubs && members.Any() && plans.Any())
        {
            var plan1M = plans.FirstOrDefault(p => p.DurationDays <= 30) ?? plans.First();
            var plan3M = plans.FirstOrDefault(p => p.DurationDays > 30) ?? plans.Last();

            var sub1 = new MemberSubscription
            {
                TenantId = tenant.Id,
                BranchId = dtBranch.Id,
                MemberId = members[0].Id,
                MembershipPlanId = plan1M.Id,
                StartDate = today,
                EndDate = today.AddDays(plan1M.DurationDays),
                OriginalPrice = plan1M.BasePrice,
                DiscountApplied = 0,
                FinalAmountPaid = plan1M.BasePrice,
                PaymentMethod = PaymentMethod.Cash,
                Status = MemberStatus.Active,
                CreatedAt = today.AddHours(9).AddMinutes(15)
            };

            var sub2 = new MemberSubscription
            {
                TenantId = tenant.Id,
                BranchId = hlBranch.Id,
                MemberId = members.Count > 1 ? members[1].Id : members[0].Id,
                MembershipPlanId = plan3M.Id,
                StartDate = today,
                EndDate = today.AddDays(plan3M.DurationDays),
                OriginalPrice = plan3M.BasePrice,
                DiscountApplied = 10000m,
                FinalAmountPaid = Math.Max(0, plan3M.BasePrice - 10000m),
                PaymentMethod = PaymentMethod.KBZPay,
                Status = MemberStatus.Active,
                CreatedAt = today.AddHours(11).AddMinutes(30)
            };

            db.MemberSubscriptions.AddRange(sub1, sub2);
            await db.SaveChangesAsync();
        }

        // 2. Seed today SalesOrders if none exist
        if (!await db.SalesOrders.AnyAsync() && products.Any())
        {
            var whey = products.FirstOrDefault(p => p.ProductName.Contains("Whey")) ?? products[0];
            var drink = products.FirstOrDefault(p => p.ProductName.Contains("Celsius") || p.ProductName.Contains("Drink")) ?? products[0];
            var water = products.FirstOrDefault(p => p.ProductName.Contains("Water")) ?? products[0];
            var shaker = products.FirstOrDefault(p => p.ProductName.Contains("Shaker")) ?? products[0];

            // Order 1: Downtown Branch (Drinks & Water - Cash)
            var order1 = new SalesOrder
            {
                TenantId = tenant.Id,
                BranchId = dtBranch.Id,
                MemberId = members.FirstOrDefault()?.Id,
                InvoiceNumber = "INV-000101",
                PaymentMethod = PaymentMethod.Cash,
                CashierName = "Ma Su Su (Front Desk)",
                SubTotal = (drink.SalePrice * 2) + water.SalePrice,
                DiscountAmount = 0,
                TotalAmount = (drink.SalePrice * 2) + water.SalePrice,
                CreatedAt = today.AddHours(8).AddMinutes(45),
                Items = new List<SalesOrderItem>
                {
                    new SalesOrderItem { ProductId = drink.Id, Quantity = 2, UnitPrice = drink.SalePrice, UnitCost = drink.CostPrice, LineTotal = drink.SalePrice * 2 },
                    new SalesOrderItem { ProductId = water.Id, Quantity = 1, UnitPrice = water.SalePrice, UnitCost = water.CostPrice, LineTotal = water.SalePrice }
                }
            };

            // Order 2: Downtown Branch (Whey + Shaker - KBZPay)
            var order2 = new SalesOrder
            {
                TenantId = tenant.Id,
                BranchId = dtBranch.Id,
                MemberId = members.Count > 1 ? members[1].Id : null,
                InvoiceNumber = "INV-000102",
                PaymentMethod = PaymentMethod.KBZPay,
                CashierName = "Ma Su Su (Front Desk)",
                SubTotal = whey.SalePrice + shaker.SalePrice,
                DiscountAmount = 5000m,
                TotalAmount = (whey.SalePrice + shaker.SalePrice) - 5000m,
                CreatedAt = today.AddHours(10).AddMinutes(20),
                Items = new List<SalesOrderItem>
                {
                    new SalesOrderItem { ProductId = whey.Id, Quantity = 1, UnitPrice = whey.SalePrice, UnitCost = whey.CostPrice, LineTotal = whey.SalePrice },
                    new SalesOrderItem { ProductId = shaker.Id, Quantity = 1, UnitPrice = shaker.SalePrice, UnitCost = shaker.CostPrice, LineTotal = shaker.SalePrice }
                }
            };

            // Order 3: Hledan Branch (Water & Drink - WavePay)
            var order3 = new SalesOrder
            {
                TenantId = tenant.Id,
                BranchId = hlBranch.Id,
                MemberId = null,
                InvoiceNumber = "INV-000103",
                PaymentMethod = PaymentMethod.WavePay,
                CashierName = "Ko Min Min (Counter Staff)",
                SubTotal = (water.SalePrice * 3) + drink.SalePrice,
                DiscountAmount = 0,
                TotalAmount = (water.SalePrice * 3) + drink.SalePrice,
                CreatedAt = today.AddHours(12).AddMinutes(10),
                Items = new List<SalesOrderItem>
                {
                    new SalesOrderItem { ProductId = water.Id, Quantity = 3, UnitPrice = water.SalePrice, UnitCost = water.CostPrice, LineTotal = water.SalePrice * 3 },
                    new SalesOrderItem { ProductId = drink.Id, Quantity = 1, UnitPrice = drink.SalePrice, UnitCost = drink.CostPrice, LineTotal = drink.SalePrice }
                }
            };

            // Order 4: Downtown Branch (Duplicate scan error - Pending HQ Void Approval)
            var order4 = new SalesOrder
            {
                TenantId = tenant.Id,
                BranchId = dtBranch.Id,
                MemberId = null,
                InvoiceNumber = "INV-000104",
                PaymentMethod = PaymentMethod.Cash,
                CashierName = "Ma Su Su (Front Desk)",
                SubTotal = drink.SalePrice * 2,
                DiscountAmount = 0,
                TotalAmount = drink.SalePrice * 2,
                CreatedAt = today.AddHours(13).AddMinutes(5),
                IsAdjustmentPending = true,
                Items = new List<SalesOrderItem>
                {
                    new SalesOrderItem { ProductId = drink.Id, Quantity = 2, UnitPrice = drink.SalePrice, UnitCost = drink.CostPrice, LineTotal = drink.SalePrice * 2 }
                }
            };

            db.SalesOrders.AddRange(order1, order2, order3, order4);
            await db.SaveChangesAsync();

            // Seed Sample Pending Approval Request for Order 4
            db.FinancialAdjustmentRequests.Add(new FinancialAdjustmentRequest
            {
                TenantId = tenant.Id,
                BranchId = dtBranch.Id,
                TransactionType = RevenueTransactionType.StockSale,
                ReferenceId = order4.Id,
                ReferenceNumber = order4.InvoiceNumber,
                CustomerOrMemberName = "Walk-in Guest",
                OriginalAmount = order4.TotalAmount,
                ProposedAmount = 0,
                OriginalPaymentMethod = PaymentMethod.Cash,
                ActionType = AdjustmentActionType.VoidRefund,
                Reason = "Duplicate scan error at counter: Barcode was scanned twice by mistake. Customer only purchased 1 can.",
                RequestedBy = "reception_dt",
                RequestedDate = today.AddHours(13).AddMinutes(15),
                Status = AdjustmentApprovalStatus.Pending
            });

            await db.SaveChangesAsync();
        }
    }
}

