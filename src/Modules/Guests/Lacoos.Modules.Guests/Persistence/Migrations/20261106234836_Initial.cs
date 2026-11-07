using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lacoos.Modules.Guests.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "guests");

            migrationBuilder.CreateTable(
                name: "guest_groups",
                schema: "guests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guest_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guests",
                schema: "guests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_primary_guest = table.Column<bool>(type: "boolean", nullable: false),
                    max_companions = table.Column<int>(type: "integer", nullable: false),
                    invitation_status = table.Column<string>(type: "text", nullable: false),
                    attendance_status = table.Column<string>(type: "text", nullable: false),
                    dietary_restrictions = table.Column<string>(type: "text", nullable: true),
                    accessibility_needs = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guests", x => x.id);
                    table.ForeignKey(
                        name: "fk_guests_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "guests",
                        principalTable: "guest_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_guest_groups_wedding_id",
                schema: "guests",
                table: "guest_groups",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_guests_group_id",
                schema: "guests",
                table: "guests",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_guests_wedding_id",
                schema: "guests",
                table: "guests",
                column: "wedding_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guests",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "guest_groups",
                schema: "guests");
        }
    }
}
