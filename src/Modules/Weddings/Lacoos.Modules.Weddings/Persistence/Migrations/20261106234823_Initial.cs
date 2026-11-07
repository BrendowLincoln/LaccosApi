using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lacoos.Modules.Weddings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "weddings");

            migrationBuilder.CreateTable(
                name: "weddings",
                schema: "weddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    wedding_date = table.Column<DateOnly>(type: "date", nullable: false),
                    estimated_guest_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    partner1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    partner2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    venue = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    cover_image = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weddings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wedding_members",
                schema: "weddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    joined_at = table.Column<DateOnly>(type: "date", nullable: false),
                    avatar = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wedding_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_wedding_members_weddings_wedding_id",
                        column: x => x.wedding_id,
                        principalSchema: "weddings",
                        principalTable: "weddings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_invitations",
                schema: "weddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_invitations_members_member_id",
                        column: x => x.member_id,
                        principalSchema: "weddings",
                        principalTable: "wedding_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_member_invitations_member_id",
                schema: "weddings",
                table: "member_invitations",
                column: "member_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_member_invitations_token_hash",
                schema: "weddings",
                table: "member_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wedding_members_user_id",
                schema: "weddings",
                table: "wedding_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_wedding_members_wedding_id_normalized_email",
                schema: "weddings",
                table: "wedding_members",
                columns: new[] { "wedding_id", "normalized_email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wedding_members_wedding_id_user_id",
                schema: "weddings",
                table: "wedding_members",
                columns: new[] { "wedding_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_invitations",
                schema: "weddings");

            migrationBuilder.DropTable(
                name: "wedding_members",
                schema: "weddings");

            migrationBuilder.DropTable(
                name: "weddings",
                schema: "weddings");
        }
    }
}
