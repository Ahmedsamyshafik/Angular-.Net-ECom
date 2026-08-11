using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscoundTblForProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Entity",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "tblDiscounds");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "tblDiscounds",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "tblDiscounds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "tblDiscounds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedBy",
                table: "tblDiscounds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "tblDiscounds",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "tblDiscounds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "tblDiscounds",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "tblDiscounds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "tblDiscounds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "tblProductDiscounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productId = table.Column<int>(type: "int", nullable: false),
                    DiscountId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblProductDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tblProductDiscounts_TblProducts_productId",
                        column: x => x.productId,
                        principalTable: "TblProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tblProductDiscounts_tblDiscounds_DiscountId",
                        column: x => x.DiscountId,
                        principalTable: "tblDiscounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tblProductDiscounts_DiscountId",
                table: "tblProductDiscounts",
                column: "DiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_tblProductDiscounts_productId",
                table: "tblProductDiscounts",
                column: "productId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tblProductDiscounts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "tblDiscounds");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "tblDiscounds");

            migrationBuilder.AddColumn<string>(
                name: "Entity",
                table: "tblDiscounds",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "tblDiscounds",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
