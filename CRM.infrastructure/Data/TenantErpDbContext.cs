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

    // NEW — complaints / feedback recorded against a tenant customer
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();


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
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactNumber).HasMaxLength(50);
            entity.Property(x => x.EmailAddress).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(500);

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

            entity.Property(x => x.ContactPerson)
                .HasMaxLength(200);

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
    }
}