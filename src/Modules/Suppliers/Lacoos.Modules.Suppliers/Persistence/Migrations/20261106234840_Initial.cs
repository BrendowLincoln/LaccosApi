using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lacoos.Modules.Suppliers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "suppliers");

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    website = table.Column<string>(type: "text", nullable: true),
                    social_media = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    negotiation_status = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    cover_image = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_contracts",
                schema: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    signed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    file_key = table.Column<string>(type: "text", nullable: true),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    file_content_type = table.Column<string>(type: "text", nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_contracts_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "suppliers",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_services",
                schema: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    quoted_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    contracted_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_services", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_services_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "suppliers",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_contracts_supplier_id",
                schema: "suppliers",
                table: "supplier_contracts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_contracts_wedding_id",
                schema: "suppliers",
                table: "supplier_contracts",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_services_supplier_id",
                schema: "suppliers",
                table: "supplier_services",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_services_wedding_id",
                schema: "suppliers",
                table: "supplier_services",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_wedding_id",
                schema: "suppliers",
                table: "suppliers",
                column: "wedding_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_contracts",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "supplier_services",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "suppliers");
        }
    }
}
