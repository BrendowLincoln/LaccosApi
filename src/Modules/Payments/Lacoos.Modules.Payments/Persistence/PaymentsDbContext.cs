using Lacoos.Modules.Payments.Domain;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Payments.Persistence;

internal sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "payments";
    public const string InstallmentsTable = "payments.payment_installments";

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentInstallment> Installments => Set<PaymentInstallment>();

    public DbSet<InstallmentTransaction> Transactions => Set<InstallmentTransaction>();

    public DbSet<PaymentReceipt> Receipts => Set<PaymentReceipt>();
}

internal sealed class PaymentsDbContextFactory() : ModuleDesignTimeFactory<PaymentsDbContext>(PaymentsDbContext.SchemaName);

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Ignore(p => p.PaidAmount);
        builder.Ignore(p => p.RemainingAmount);
        builder.Property(p => p.Description).HasMaxLength(300);
        builder.HasIndex(p => p.SupplierId);
        builder.HasIndex(p => p.BudgetItemId);
        builder.HasMany(p => p.Installments).WithOne().HasForeignKey(i => i.PaymentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PaymentInstallmentConfiguration : IEntityTypeConfiguration<PaymentInstallment>
{
    public void Configure(EntityTypeBuilder<PaymentInstallment> builder)
    {
        builder.ToTable("payment_installments");
        builder.Ignore(i => i.PaidAmount);
        builder.Ignore(i => i.RemainingAmount);
        builder.Ignore(i => i.LatestSettlement);
        builder.HasIndex(i => new { i.PaymentId, i.InstallmentNumber }).IsUnique();
        builder.HasMany(i => i.Transactions).WithOne().HasForeignKey(t => t.InstallmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Receipts).WithOne().HasForeignKey(r => r.InstallmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InstallmentTransactionConfiguration : IEntityTypeConfiguration<InstallmentTransaction>
{
    public void Configure(EntityTypeBuilder<InstallmentTransaction> builder)
    {
        builder.ToTable("installment_transactions");
    }
}

internal sealed class PaymentReceiptConfiguration : IEntityTypeConfiguration<PaymentReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentReceipt> builder)
    {
        builder.ToTable("payment_receipts");
        builder.Property(r => r.FileName).HasMaxLength(200);
        builder.HasOne<InstallmentTransaction>().WithMany().HasForeignKey(r => r.TransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
