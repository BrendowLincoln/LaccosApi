using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lacoos.Modules.Budget.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "budget");

            migrationBuilder.CreateTable(
                name: "budget_categories",
                schema: "budget",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    planned_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "budget_settings",
                schema: "budget",
                columns: table => new
                {
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_settings", x => x.wedding_id);
                });

            migrationBuilder.CreateTable(
                name: "budget_items",
                schema: "budget",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    planned_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    contracted_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_budget_items_budget_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "budget",
                        principalTable: "budget_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_categories_wedding_id",
                schema: "budget",
                table: "budget_categories",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_items_category_id",
                schema: "budget",
                table: "budget_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_items_supplier_id",
                schema: "budget",
                table: "budget_items",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_items_wedding_id",
                schema: "budget",
                table: "budget_items",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_settings_wedding_id",
                schema: "budget",
                table: "budget_settings",
                column: "wedding_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_items",
                schema: "budget");

            migrationBuilder.DropTable(
                name: "budget_settings",
                schema: "budget");

            migrationBuilder.DropTable(
                name: "budget_categories",
                schema: "budget");
        }
    }
}
