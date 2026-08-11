using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProductIds",
                table: "tblCategories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.InsertData(
                table: "tblCategories",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "IsDeleted", "Name", "ProductIds", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, null, 0, "Electronic devices and gadgets", false, "Electronics", null, null, 0 });

            migrationBuilder.InsertData(
                table: "TblProducts",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "IsDeleted", "Name", "Price", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, null, 0, "Description for Product 1", false, "Product 1", 10.99m, null, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TblProducts",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "tblCategories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "ProductIds",
                table: "tblCategories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
