using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vybavim.Modules.Listings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "budget_amount",
                schema: "listings",
                table: "listings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "budget_type",
                schema: "listings",
                table: "listings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string[]>(
                name: "deliverables",
                schema: "listings",
                table: "listings",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "engagement",
                schema: "listings",
                table: "listings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "estimated_duration",
                schema: "listings",
                table: "listings",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "invoice_required",
                schema: "listings",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "level",
                schema: "listings",
                table: "listings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "remote",
                schema: "listings",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string[]>(
                name: "requirements",
                schema: "listings",
                table: "listings",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "size",
                schema: "listings",
                table: "listings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string[]>(
                name: "skills",
                schema: "listings",
                table: "listings",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "listings",
                table: "categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                schema: "listings",
                table: "categories",
                columns: new[] { "id", "description", "name", "parent_id", "slug", "sort_order" },
                values: new object[,]
                {
                    { new Guid("019a0b2c-1000-7000-8000-000000000001"), "Weby, e-shopy, mobilné appky", "Web a aplikácie", null, "web", 1 },
                    { new Guid("019a0b2c-1000-7000-8000-000000000002"), "Logá, vizuály, tlačoviny", "Grafika a logo", null, "grafika", 2 },
                    { new Guid("019a0b2c-1000-7000-8000-000000000003"), "Siete, počítače, servery", "IT podpora", null, "it-podpora", 3 },
                    { new Guid("019a0b2c-1000-7000-8000-000000000004"), "Reklamy, sociálne siete, SEO", "Online marketing", null, "marketing", 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "listings",
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("019a0b2c-1000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "listings",
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("019a0b2c-1000-7000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "listings",
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("019a0b2c-1000-7000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "listings",
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("019a0b2c-1000-7000-8000-000000000004"));

            migrationBuilder.DropColumn(
                name: "budget_type",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "deliverables",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "engagement",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "estimated_duration",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "invoice_required",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "level",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "remote",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "requirements",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "size",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "skills",
                schema: "listings",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "listings",
                table: "categories");

            migrationBuilder.AlterColumn<decimal>(
                name: "budget_amount",
                schema: "listings",
                table: "listings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
