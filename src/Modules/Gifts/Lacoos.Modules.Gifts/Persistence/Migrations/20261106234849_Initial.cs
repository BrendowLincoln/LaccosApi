using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lacoos.Modules.Gifts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gifts");

            migrationBuilder.CreateTable(
                name: "gift_categories",
                schema: "gifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gift_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gifts",
                schema: "gifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimated_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    desired_quantity = table.Column<int>(type: "integer", nullable: false),
                    external_link = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gifts", x => x.id);
                    table.ForeignKey(
                        name: "fk_gifts_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "gifts",
                        principalTable: "gift_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gift_reservations",
                schema: "gifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    guest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    person_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reserved_at = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gift_reservations", x => x.id);
                    table.ForeignKey(
                        name: "fk_gift_reservations_gifts_gift_id",
                        column: x => x.gift_id,
                        principalSchema: "gifts",
                        principalTable: "gifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gift_receipts",
                schema: "gifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    guest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    person_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_at = table.Column<DateOnly>(type: "date", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gift_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_gift_receipts_gifts_gift_id",
                        column: x => x.gift_id,
                        principalSchema: "gifts",
                        principalTable: "gifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_gift_receipts_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "gifts",
                        principalTable: "gift_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gift_categories_wedding_id",
                schema: "gifts",
                table: "gift_categories",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_receipts_gift_id",
                schema: "gifts",
                table: "gift_receipts",
                column: "gift_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_receipts_guest_id",
                schema: "gifts",
                table: "gift_receipts",
                column: "guest_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_receipts_reservation_id",
                schema: "gifts",
                table: "gift_receipts",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_receipts_wedding_id",
                schema: "gifts",
                table: "gift_receipts",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_reservations_gift_id",
                schema: "gifts",
                table: "gift_reservations",
                column: "gift_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_reservations_guest_id",
                schema: "gifts",
                table: "gift_reservations",
                column: "guest_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_reservations_wedding_id",
                schema: "gifts",
                table: "gift_reservations",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_gifts_category_id",
                schema: "gifts",
                table: "gifts",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_gifts_wedding_id",
                schema: "gifts",
                table: "gifts",
                column: "wedding_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gift_receipts",
                schema: "gifts");

            migrationBuilder.DropTable(
                name: "gift_reservations",
                schema: "gifts");

            migrationBuilder.DropTable(
                name: "gifts",
                schema: "gifts");

            migrationBuilder.DropTable(
                name: "gift_categories",
                schema: "gifts");
        }
    }
}
