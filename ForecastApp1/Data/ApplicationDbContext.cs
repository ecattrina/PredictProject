using ForecastApp1.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractCondition> ContractConditions => Set<ContractCondition>();
    public DbSet<BusinessCalendarDay> BusinessCalendarDays => Set<BusinessCalendarDay>();
    public DbSet<IncomingDebtSnapshot> IncomingDebtSnapshots => Set<IncomingDebtSnapshot>();
    public DbSet<ActualShipment> ActualShipments => Set<ActualShipment>();
    public DbSet<SupplierOrder> SupplierOrders => Set<SupplierOrder>();
    public DbSet<ActualPayment> ActualPayments => Set<ActualPayment>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<StagingSupplier> StagingSuppliers => Set<StagingSupplier>();
    public DbSet<StagingContract> StagingContracts => Set<StagingContract>();
    public DbSet<StagingContractCondition> StagingContractConditions => Set<StagingContractCondition>();
    public DbSet<StagingIncomingDebt> StagingIncomingDebts => Set<StagingIncomingDebt>();
    public DbSet<StagingActualShipment> StagingActualShipments => Set<StagingActualShipment>();
    public DbSet<StagingSupplierOrder> StagingSupplierOrders => Set<StagingSupplierOrder>();
    public DbSet<PaymentCalculationRun> PaymentCalculationRuns => Set<PaymentCalculationRun>();
    public DbSet<PaymentScheduleItem> PaymentScheduleItems => Set<PaymentScheduleItem>();
    public DbSet<PaymentScheduleAllocation> PaymentScheduleAllocations => Set<PaymentScheduleAllocation>();
    public DbSet<UncoveredDebtItem> UncoveredDebtItems => Set<UncoveredDebtItem>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(x => x.Id);
            e.Property(x => x.SupplierCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(512).IsRequired();
            e.HasIndex(x => new { x.SupplierCode, x.DeletedAt }).IsUnique();
        });

        modelBuilder.Entity<Contract>(e =>
        {
            e.ToTable("contracts");
            e.HasKey(x => x.Id);
            e.Property(x => x.InternalContractNumber).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.SupplierId, x.InternalContractNumber, x.DeletedAt }).IsUnique();
            e.HasOne(x => x.Supplier).WithMany(s => s.Contracts).HasForeignKey(x => x.SupplierId);
        });

        modelBuilder.Entity<ContractCondition>(e =>
        {
            e.ToTable("contract_conditions");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Contract).WithMany(c => c.Conditions).HasForeignKey(x => x.ContractId);
            e.HasOne(x => x.SourceImportBatch).WithMany().HasForeignKey(x => x.SourceImportBatchId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BusinessCalendarDay>(e =>
        {
            e.ToTable("business_calendar_days");
            e.HasKey(x => new { x.CalendarCode, x.CalendarDate });
            e.Property(x => x.CalendarCode).HasMaxLength(32);
        });

        modelBuilder.Entity<ImportBatch>(e =>
        {
            e.ToTable("import_batches");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId);
        });

        modelBuilder.Entity<ImportRow>(e =>
        {
            e.ToTable("import_rows");
            e.HasKey(x => x.Id);
            e.Property(x => x.RawJson).HasColumnType("TEXT");
            e.HasOne(x => x.Batch).WithMany(b => b.Rows).HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportError>(e =>
        {
            e.ToTable("import_errors");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Batch).WithMany(b => b.Errors).HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Row).WithMany().HasForeignKey(x => x.ImportRowId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StagingSupplier>(e =>
        {
            e.ToTable("staging_suppliers");
            e.HasKey(x => x.Id);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StagingContract>(e =>
        {
            e.ToTable("staging_contracts");
            e.HasKey(x => x.Id);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StagingContractCondition>(e =>
        {
            e.ToTable("staging_contract_conditions");
            e.HasKey(x => x.Id);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StagingIncomingDebt>(e =>
        {
            e.ToTable("staging_incoming_debts");
            e.HasKey(x => x.Id);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StagingActualShipment>(e =>
        {
            e.ToTable("staging_actual_shipments");
            e.HasKey(x => x.Id);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StagingSupplierOrder>(e =>
        {
            e.ToTable("staging_supplier_orders");
            e.HasKey(x => x.Id);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IncomingDebtSnapshot>(e =>
        {
            e.ToTable("incoming_debt_snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.CurrencyCode).HasMaxLength(3);
            e.HasIndex(x => new { x.SupplierId, x.ContractId, x.DebtDate, x.DeletedAt }).IsUnique();
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
            e.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
            e.HasOne(x => x.SourceImportBatch).WithMany().HasForeignKey(x => x.SourceImportBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActualShipment>(e =>
        {
            e.ToTable("actual_shipments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SupplierId, x.ContractId, x.ShipmentDocNumber, x.ShipmentDocDate, x.OrderNumber, x.DeletedAt }).IsUnique();
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
            e.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
            e.HasOne(x => x.SourceImportBatch).WithMany().HasForeignKey(x => x.SourceImportBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplierOrder>(e =>
        {
            e.ToTable("supplier_orders");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SupplierId, x.ContractId, x.OrderNumber, x.DeletedAt }).IsUnique();
            e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            e.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.SourceImportBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActualPayment>(e =>
        {
            e.ToTable("actual_payments");
            e.HasKey(x => x.Id);
            e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            e.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.SourceImportBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentCalculationRun>(e =>
        {
            e.ToTable("payment_calculation_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.RunCode).HasConversion<string>().HasMaxLength(36);
            e.HasIndex(x => x.RunCode).IsUnique();
            e.HasOne(x => x.StartedByUser).WithMany().HasForeignKey(x => x.StartedByUserId);
            e.HasOne(x => x.FilterSupplier).WithMany().HasForeignKey(x => x.FilterSupplierId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FilterContract).WithMany().HasForeignKey(x => x.FilterContractId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PaymentScheduleItem>(e =>
        {
            e.ToTable("payment_schedule_items");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.CalculationRun).WithMany(r => r.ScheduleItems).HasForeignKey(x => x.CalculationRunId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
            e.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
            e.HasOne(x => x.IncomingDebtSnapshot).WithMany().HasForeignKey(x => x.IncomingDebtSnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ActualShipment).WithMany().HasForeignKey(x => x.ActualShipmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentScheduleAllocation>(e =>
        {
            e.ToTable("payment_schedule_allocations");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.ScheduleItem).WithMany(s => s.Allocations).HasForeignKey(x => x.ScheduleItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentCalculationRun>().WithMany().HasForeignKey(x => x.CalculationRunId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IncomingDebtSnapshot>().WithMany().HasForeignKey(x => x.IncomingDebtSnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ActualShipment>().WithMany().HasForeignKey(x => x.ActualShipmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ContractCondition).WithMany().HasForeignKey(x => x.ContractConditionId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UncoveredDebtItem>(e =>
        {
            e.ToTable("uncovered_debt_items");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.CalculationRun).WithMany(r => r.UncoveredDebts).HasForeignKey(x => x.CalculationRunId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.IncomingDebtSnapshot).WithMany().HasForeignKey(x => x.IncomingDebtSnapshotId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLogEntry>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<PaymentCalculationRun>().WithMany().HasForeignKey(x => x.CalculationRunId).OnDelete(DeleteBehavior.SetNull);
        });

        base.OnModelCreating(modelBuilder);
    }
}
