using CRM.domain.Entities;
using CRM.Domain.Entities;   // add this — Company, CompanyDatabase, Device live here
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Data;

public class MasterErpDbContext : IdentityDbContext
{
    public MasterErpDbContext(DbContextOptions<MasterErpDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyDatabase> CompanyDatabases => Set<CompanyDatabase>();
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ServiceStatusLog> ServiceStatusLogs => Set<ServiceStatusLog>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<CustomerSubscription> CustomerSubscriptions => Set<CustomerSubscription>();
    public DbSet<BillingTransaction> BillingTransactions => Set<BillingTransaction>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();
    public DbSet<TermsCondition> TermsConditions => Set<TermsCondition>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Company>(entity =>
        {
            entity.HasKey(x => x.CompanyId);

            entity.Property(x => x.CompanyCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.CompanyName)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(x => x.CompanyCode)
                .IsUnique();
        });

        builder.Entity<CompanyDatabase>(entity =>
        {
            entity.HasKey(x => x.CompanyDatabaseId);

            entity.Property(x => x.ServerName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.DatabaseName)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Device ----
        builder.Entity<Device>(entity =>
        {
            entity.HasKey(x => x.DeviceId);

            entity.Property(x => x.DeviceCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.DeviceName)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.CompanyId, x.DeviceCode })
                .IsUnique();
        });

        // ---- CRM entities ----

        builder.Entity<Role>(entity =>
        {
            entity.HasKey(x => x.RoleId);
            entity.Property(x => x.RoleName).HasMaxLength(50).IsRequired();
        });

        builder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();

            entity.HasOne(x => x.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.CustomerId);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        });

        builder.Entity<Service>(entity =>
        {
            entity.HasKey(x => x.ServiceId);
            entity.Property(x => x.ServiceName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Price).HasColumnType("decimal(10,2)");
        });

        builder.Entity<ServiceRequest>(entity =>
        {
            entity.HasKey(x => x.RequestId);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);

            // FK → Users (both are same-DB, keep these)
            entity.HasOne(x => x.AssignedStaff)
                .WithMany(u => u.AssignedRequests)
                .HasForeignKey(x => x.AssignedStaffId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);   // ← optional relationship

            entity.HasOne(x => x.CreatedByUser)
                .WithMany(u => u.CreatedRequests)
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ServiceStatusLog>(entity =>
        {
            entity.HasKey(x => x.LogId);

            entity.HasOne(x => x.ServiceRequest)
                .WithMany(r => r.StatusLogs)
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.UpdatedByUser)
                .WithMany(u => u.StatusUpdates)
                .HasForeignKey(x => x.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FollowUp>(entity =>
        {
            entity.HasKey(x => x.FollowUpId);
            entity.Property(x => x.Type).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ContactMethod).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(200);
            entity.Property(x => x.DiscountOffer).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();

            // No FK to Customer — tenant side reference only
        });

        builder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(x => x.PlanId);
            entity.Property(x => x.PlanName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Price).HasColumnType("decimal(10,2)");
        });

        builder.Entity<CustomerSubscription>(entity =>
        {
            entity.HasKey(x => x.SubscriptionId);

            entity.HasOne(x => x.Customer)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Plan)
                .WithMany(p => p.CustomerSubscriptions)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ManagedByUser)
                .WithMany(u => u.ManagedSubscriptions)
                .HasForeignKey(x => x.ManagedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BillingTransaction>(entity =>
        {
            entity.HasKey(x => x.TransactionId);
            entity.Property(x => x.Amount).HasColumnType("decimal(10,2)");

            entity.HasOne(x => x.CustomerSubscription)
                .WithMany(s => s.BillingTransactions)
                .HasForeignKey(x => x.CustomerSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ServiceRequest)
                .WithMany(r => r.BillingTransactions)
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BackupLog>(entity =>
        {
            entity.HasKey(x => x.BackupId);

            entity.HasOne(x => x.PerformedByUser)
                .WithMany(u => u.BackupsPerformed)
                .HasForeignKey(x => x.PerformedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TermsCondition>(entity =>
        {
            entity.HasKey(x => x.TermsId);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany(u => u.TermsPublished)
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}