using CRM.domain.Entities;
using CRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Data;

public class TenantErpDbContext : DbContext
{
    public TenantErpDbContext(DbContextOptions<TenantErpDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<TenantCustomer> TenantCustomers => Set<TenantCustomer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<BillingTransaction> BillingTransactions => Set<BillingTransaction>();
    public DbSet<ServiceStatusLog> ServiceStatusLogs => Set<ServiceStatusLog>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<TenantBranch> Branches => Set<TenantBranch>();

    public int CompanyId { get; set; }
    public CRM.Infrastructure.Services.ICloudSyncService? CloudSyncService { get; set; }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.ProductId);
            entity.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Category).HasMaxLength(50);

            // Archive extension
            entity.Property(x => x.ArchivedBy).HasMaxLength(200);
            entity.HasIndex(x => x.IsArchived);

            entity.HasIndex(x => x.ProductCode).IsUnique();
        });

        builder.Entity<TenantCustomer>(entity =>
        {
            entity.HasKey(x => x.TenantCustomerId);
            entity.Property(x => x.CustomerCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Street).HasMaxLength(200);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.Province).HasMaxLength(100);
            entity.Property(x => x.CustomerName)
                .HasMaxLength(200)
                .HasComputedColumnSql("(ltrim(rtrim(concat([FirstName],' ',[LastName]))))", stored: false)
                .ValueGeneratedOnAddOrUpdate();
            entity.Property(x => x.Address)
                .HasMaxLength(500)
                .HasComputedColumnSql("(case when [Street] IS NOT NULL AND [City] IS NOT NULL then concat([Street],', ',[City],case when [Province] IS NOT NULL then concat(', ',[Province]) else '' end) when [Street] IS NOT NULL then [Street] when [City] IS NOT NULL then [City]  end)", stored: false)
                .ValueGeneratedOnAddOrUpdate();
            entity.Property(x => x.ContactNumber).HasMaxLength(50);
            entity.Property(x => x.EmailAddress).HasMaxLength(200);

            entity.Property(x => x.PlateNumber).HasMaxLength(50);
            entity.Property(x => x.VehicleMake).HasMaxLength(100);
            entity.Property(x => x.VehicleModel).HasMaxLength(100);
            entity.Property(x => x.VehicleColor).HasMaxLength(50);
            entity.Property(x => x.VehicleType).HasMaxLength(50);
            entity.Property(x => x.Source).HasMaxLength(50);

            // Archive extension
            entity.Property(x => x.ArchivedBy).HasMaxLength(200);
            entity.HasIndex(x => x.IsArchived);

            entity.HasIndex(x => x.CustomerCode).IsUnique();
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.HasKey(x => x.SupplierId);

            entity.Property(x => x.SupplierCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.SupplierName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.ContactFirstName)
                .HasMaxLength(100);

            entity.Property(x => x.ContactLastName)
                .HasMaxLength(100);

            entity.Property(x => x.ContactPerson)
                .HasMaxLength(200)
                .HasComputedColumnSql("(ltrim(rtrim(concat(isnull([ContactFirstName],''),' ',isnull([ContactLastName],'')))))", stored: false)
                .ValueGeneratedOnAddOrUpdate();

            entity.Property(x => x.ContactNumber)
                .HasMaxLength(50);

            entity.Property(x => x.EmailAddress)
                .HasMaxLength(200);

            entity.Property(x => x.Address)
                .HasMaxLength(500);

            entity.HasIndex(x => x.SupplierCode)
                .IsUnique();
        });

        builder.Entity<Inventory>(entity =>
        {
            entity.HasKey(x => x.InventoryId);

            entity.Property(x => x.QuantityOnHand)
                .HasPrecision(18, 2);

            entity.Property(x => x.ReorderLevel)
                .HasPrecision(18, 2);

            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // NEW — customer interactions (complaints & feedback)
        builder.Entity<CustomerInteraction>(entity =>
        {
            entity.HasKey(x => x.InteractionId);

            entity.Property(x => x.Kind)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Severity)
                .HasMaxLength(20);

            entity.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Details)
                .HasMaxLength(2000);

            entity.Property(x => x.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.RecordedBy)
                .HasMaxLength(200);

            // Index for the common query: fetch all interactions for one customer
            entity.HasIndex(x => x.CustomerId);

            // NOTE: no FK constraint to TenantCustomer on purpose.
            // Interactions can outlive a customer deletion if you ever
            // add soft-delete — the CustomerId is just a logical link.
        });

        builder.Entity<ServiceRequest>(entity =>
        {
            entity.HasKey(x => x.RequestId);
            entity.Ignore(x => x.AssignedStaff);
            entity.Ignore(x => x.CreatedByUser);

            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ArchivedBy).HasMaxLength(200);

            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.ServiceId);
            entity.HasIndex(x => x.IsArchived);

            entity.HasMany(x => x.StatusLogs)
                .WithOne(x => x.ServiceRequest)
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.BillingTransactions)
                .WithOne(x => x.ServiceRequest)
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ServiceStatusLog>(entity =>
        {
            entity.HasKey(x => x.LogId);
            entity.Ignore(x => x.UpdatedByUser);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.Notes).IsRequired();
        });

        builder.Entity<BillingTransaction>(entity =>
        {
            entity.HasKey(x => x.TransactionId);
            entity.Ignore(x => x.CustomerSubscription);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentStatus).IsRequired();
        });

        builder.Entity<TenantBranch>(entity =>
        {
            entity.ToTable("Branches");
            entity.HasKey(x => x.BranchId);
            entity.Property(x => x.BranchCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.BranchName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.Province).HasMaxLength(100);
            entity.Property(x => x.ContactNumber).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.ArchivedBy).HasMaxLength(200);
            entity.HasIndex(x => x.IsArchived);
            entity.HasIndex(x => x.BranchCode);
        });

        builder.Entity<ServiceRequest>(entity =>
        {
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TenantCustomer>(entity =>
        {
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<FollowUp>(entity =>
        {
            entity.HasKey(x => x.FollowUpId);
            entity.Property(x => x.Type).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ContactMethod).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(200);
            entity.Property(x => x.DiscountOffer).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ArchivedBy).HasMaxLength(200);
            entity.Property(x => x.ApprovalStatus).IsRequired();
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.IsArchived);

            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<BillingTransaction>(entity =>
        {
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pendingSyncs = new List<(string TableName, string Operation, object Entity, Func<string> GetKey)>();

        if (CloudSyncService != null && CompanyId > 0)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                {
                    string op = entry.State switch
                    {
                        EntityState.Added => "INSERT",
                        EntityState.Modified => "UPDATE",
                        EntityState.Deleted => "DELETE",
                        _ => "UPDATE"
                    };

                    switch (entry.Entity)
                    {
                        case TenantCustomer c:
                            pendingSyncs.Add(("TenantCustomers", op, c, () => c.TenantCustomerId.ToString()));
                            break;
                        case Product p:
                            pendingSyncs.Add(("Products", op, p, () => p.ProductId.ToString()));
                            break;
                        case Supplier s:
                            pendingSyncs.Add(("Suppliers", op, s, () => s.SupplierId.ToString()));
                            break;
                        case Inventory inv:
                            pendingSyncs.Add(("Inventories", op, inv, () => inv.InventoryId.ToString()));
                            break;
                        case CustomerInteraction ci:
                            pendingSyncs.Add(("CustomerInteractions", op, ci, () => ci.InteractionId.ToString()));
                            break;
                        case ServiceRequest sr:
                            pendingSyncs.Add(("ServiceRequests", op, sr, () => sr.RequestId.ToString()));
                            break;
                        case BillingTransaction bt:
                            pendingSyncs.Add(("BillingTransactions", op, bt, () => bt.TransactionId.ToString()));
                            break;
                        case ServiceStatusLog sl:
                            pendingSyncs.Add(("ServiceStatusLogs", op, sl, () => sl.LogId.ToString()));
                            break;
                        case FollowUp fu:
                            pendingSyncs.Add(("FollowUps", op, fu, () => fu.FollowUpId.ToString()));
                            break;
                        case TenantBranch br:
                            pendingSyncs.Add(("Branches", op, br, () => br.BranchId.ToString()));
                            break;
                    }
                }
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        if (CloudSyncService != null && CompanyId > 0 && pendingSyncs.Count > 0)
        {
            foreach (var item in pendingSyncs)
            {
                try
                {
                    string key = item.GetKey();
                    await CloudSyncService.QueueAndSyncRecordAsync(
                        item.TableName,
                        CompanyId,
                        key,
                        item.Operation,
                        item.Entity,
                        cancellationToken);
                }
                catch
                {
                    // Local save already succeeded; do not disrupt caller
                }
            }
        }

        return result;
    }
}