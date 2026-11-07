using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lacoos.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    payment_method = table.Column<string>(type: "text", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    budget_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_installments",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_installments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_installments_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "payments",
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "installment_transactions",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    paid_at = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_installment_transactions_payment_installments_installment_id",
                        column: x => x.installment_id,
                        principalSchema: "payments",
                        principalTable: "payment_installments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_receipts",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_key = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_receipts_installment_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalSchema: "payments",
                        principalTable: "installment_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_receipts_payment_installments_installment_id",
                        column: x => x.installment_id,
                        principalSchema: "payments",
                        principalTable: "payment_installments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_installment_transactions_installment_id",
                schema: "payments",
                table: "installment_transactions",
                column: "installment_id");

            migrationBuilder.CreateIndex(
                name: "ix_installment_transactions_wedding_id",
                schema: "payments",
                table: "installment_transactions",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_installments_payment_id_installment_number",
                schema: "payments",
                table: "payment_installments",
                columns: new[] { "payment_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_installments_wedding_id",
                schema: "payments",
                table: "payment_installments",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_receipts_installment_id",
                schema: "payments",
                table: "payment_receipts",
                column: "installment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_receipts_transaction_id",
                schema: "payments",
                table: "payment_receipts",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_receipts_wedding_id",
                schema: "payments",
                table: "payment_receipts",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_budget_item_id",
                schema: "payments",
                table: "payments",
                column: "budget_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_supplier_id",
                schema: "payments",
                table: "payments",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_wedding_id",
                schema: "payments",
                table: "payments",
                column: "wedding_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_receipts",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "installment_transactions",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "payment_installments",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "payments");
        }
    }
}
