using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lacoos.Modules.Checklist.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "checklist");

            migrationBuilder.CreateTable(
                name: "checklist_categories",
                schema: "checklist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checklist_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "checklist_items",
                schema: "checklist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wedding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    priority = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_checklist_items_checklist_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "checklist",
                        principalTable: "checklist_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_checklist_categories_wedding_id",
                schema: "checklist",
                table: "checklist_categories",
                column: "wedding_id");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_items_assigned_to_member_id",
                schema: "checklist",
                table: "checklist_items",
                column: "assigned_to_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_items_category_id",
                schema: "checklist",
                table: "checklist_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_items_wedding_id",
                schema: "checklist",
                table: "checklist_items",
                column: "wedding_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checklist_items",
                schema: "checklist");

            migrationBuilder.DropTable(
                name: "checklist_categories",
                schema: "checklist");
        }
    }
}
