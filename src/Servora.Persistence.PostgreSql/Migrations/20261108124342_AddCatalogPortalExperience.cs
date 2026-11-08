using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servora.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogPortalExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingType",
                table: "sc_req_item",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OneTimePrice",
                table: "sc_req_item",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TermMonths",
                table: "sc_req_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeText",
                table: "sc_cat_item",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingType",
                table: "sc_cat_item",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "sc_cat_item",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMarkdown",
                table: "sc_cat_item",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryText",
                table: "sc_cat_item",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailsMarkdown",
                table: "sc_cat_item",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeaturedOrder",
                table: "sc_cat_item",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Highlights",
                table: "sc_cat_item",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "sc_cat_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "sc_cat_item",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OneTimePrice",
                table: "sc_cat_item",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "sc_cat_item",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Teaser",
                table: "sc_cat_item",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeaserText",
                table: "sc_cat_item",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TermMonths",
                table: "sc_cat_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceDelta",
                table: "item_option_new",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                table: "item_option_new",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sc_favorite",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_favorite", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sc_favorite_sc_cat_item_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "sc_cat_item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sc_favorite_sys_user_UserId",
                        column: x => x.UserId,
                        principalTable: "sys_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sc_item_section",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_item_section", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sc_item_section_sc_cat_item_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "sc_cat_item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sc_cat_item_IsFeatured",
                table: "sc_cat_item",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_item_option_new_SectionId",
                table: "item_option_new",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_favorite_CatalogItemId",
                table: "sc_favorite",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sc_favorite_UserId_CatalogItemId",
                table: "sc_favorite",
                columns: new[] { "UserId", "CatalogItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sc_item_section_CatalogItemId_Order",
                table: "sc_item_section",
                columns: new[] { "CatalogItemId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_item_option_new_sc_item_section_SectionId",
                table: "item_option_new",
                column: "SectionId",
                principalTable: "sc_item_section",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_item_option_new_sc_item_section_SectionId",
                table: "item_option_new");

            migrationBuilder.DropTable(
                name: "sc_favorite");

            migrationBuilder.DropTable(
                name: "sc_item_section");

            migrationBuilder.DropIndex(
                name: "IX_sc_cat_item_IsFeatured",
                table: "sc_cat_item");

            migrationBuilder.DropIndex(
                name: "IX_item_option_new_SectionId",
                table: "item_option_new");

            migrationBuilder.DropColumn(
                name: "BillingType",
                table: "sc_req_item");

            migrationBuilder.DropColumn(
                name: "OneTimePrice",
                table: "sc_req_item");

            migrationBuilder.DropColumn(
                name: "TermMonths",
                table: "sc_req_item");

            migrationBuilder.DropColumn(
                name: "BadgeText",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "BillingType",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "DeliveryMarkdown",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "DeliveryText",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "DetailsMarkdown",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "FeaturedOrder",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "Highlights",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "OneTimePrice",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "Teaser",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "TeaserText",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "TermMonths",
                table: "sc_cat_item");

            migrationBuilder.DropColumn(
                name: "PriceDelta",
                table: "item_option_new");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "item_option_new");
        }
    }
}
