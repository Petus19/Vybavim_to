using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vybavim.Modules.Projects.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.CreateTable(
                name: "offers",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    freelancer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    delivery_days = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_offers_freelancer_id",
                schema: "projects",
                table: "offers",
                column: "freelancer_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_listing_id_freelancer_id",
                schema: "projects",
                table: "offers",
                columns: new[] { "listing_id", "freelancer_id" },
                unique: true,
                filter: "status IN ('Pending', 'Accepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offers",
                schema: "projects");
        }
    }
}
