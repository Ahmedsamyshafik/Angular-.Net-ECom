using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDiscountAndPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblPhotos_TblProducts_ProductId",
                table: "tblPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_tblPhotos_tblCategories_CategoryId",
                table: "tblPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_tblProductDiscounts_TblProducts_productId",
                table: "tblProductDiscounts");

            migrationBuilder.DropForeignKey(
                name: "FK_tblProductDiscounts_tblDiscounds_DiscountId",
                table: "tblProductDiscounts");

            migrationBuilder.DropForeignKey(
                name: "FK_TblProducts_tblCategories_CategoryId",
                table: "TblProducts");

            migrationBuilder.DropTable(
                name: "tblDiscounds");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tblProductDiscounts",
                table: "tblProductDiscounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tblPhotos",
                table: "tblPhotos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tblCategories",
                table: "tblCategories");

            migrationBuilder.RenameTable(
                name: "tblProductDiscounts",
                newName: "TblProductDiscounts");

            migrationBuilder.RenameTable(
                name: "tblPhotos",
                newName: "TblPhotos");

            migrationBuilder.RenameTable(
                name: "tblCategories",
                newName: "TblCategories");

            migrationBuilder.RenameColumn(
                name: "productId",
                table: "TblProductDiscounts",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_tblProductDiscounts_productId",
                table: "TblProductDiscounts",
                newName: "IX_TblProductDiscounts_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_tblProductDiscounts_DiscountId",
                table: "TblProductDiscounts",
                newName: "IX_TblProductDiscounts_DiscountId");

            migrationBuilder.RenameIndex(
                name: "IX_tblPhotos_ProductId",
                table: "TblPhotos",
                newName: "IX_TblPhotos_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_tblPhotos_CategoryId",
                table: "TblPhotos",
                newName: "IX_TblPhotos_CategoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TblProductDiscounts",
                table: "TblProductDiscounts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TblPhotos",
                table: "TblPhotos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TblCategories",
                table: "TblCategories",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "TblDiscounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TblDiscounts", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_TblPhotos_TblCategories_CategoryId",
                table: "TblPhotos",
                column: "CategoryId",
                principalTable: "TblCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TblPhotos_TblProducts_ProductId",
                table: "TblPhotos",
                column: "ProductId",
                principalTable: "TblProducts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TblProductDiscounts_TblDiscounts_DiscountId",
                table: "TblProductDiscounts",
                column: "DiscountId",
                principalTable: "TblDiscounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TblProductDiscounts_TblProducts_ProductId",
                table: "TblProductDiscounts",
                column: "ProductId",
                principalTable: "TblProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TblProducts_TblCategories_CategoryId",
                table: "TblProducts",
                column: "CategoryId",
                principalTable: "TblCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TblPhotos_TblCategories_CategoryId",
                table: "TblPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_TblPhotos_TblProducts_ProductId",
                table: "TblPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_TblProductDiscounts_TblDiscounts_DiscountId",
                table: "TblProductDiscounts");

            migrationBuilder.DropForeignKey(
                name: "FK_TblProductDiscounts_TblProducts_ProductId",
                table: "TblProductDiscounts");

            migrationBuilder.DropForeignKey(
                name: "FK_TblProducts_TblCategories_CategoryId",
                table: "TblProducts");

            migrationBuilder.DropTable(
                name: "TblDiscounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TblProductDiscounts",
                table: "TblProductDiscounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TblPhotos",
                table: "TblPhotos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TblCategories",
                table: "TblCategories");

            migrationBuilder.RenameTable(
                name: "TblProductDiscounts",
                newName: "tblProductDiscounts");

            migrationBuilder.RenameTable(
                name: "TblPhotos",
                newName: "tblPhotos");

            migrationBuilder.RenameTable(
                name: "TblCategories",
                newName: "tblCategories");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "tblProductDiscounts",
                newName: "productId");

            migrationBuilder.RenameIndex(
                name: "IX_TblProductDiscounts_ProductId",
                table: "tblProductDiscounts",
                newName: "IX_tblProductDiscounts_productId");

            migrationBuilder.RenameIndex(
                name: "IX_TblProductDiscounts_DiscountId",
                table: "tblProductDiscounts",
                newName: "IX_tblProductDiscounts_DiscountId");

            migrationBuilder.RenameIndex(
                name: "IX_TblPhotos_ProductId",
                table: "tblPhotos",
                newName: "IX_tblPhotos_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_TblPhotos_CategoryId",
                table: "tblPhotos",
                newName: "IX_tblPhotos_CategoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tblProductDiscounts",
                table: "tblProductDiscounts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tblPhotos",
                table: "tblPhotos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tblCategories",
                table: "tblCategories",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "tblDiscounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblDiscounds", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_tblPhotos_TblProducts_ProductId",
                table: "tblPhotos",
                column: "ProductId",
                principalTable: "TblProducts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tblPhotos_tblCategories_CategoryId",
                table: "tblPhotos",
                column: "CategoryId",
                principalTable: "tblCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tblProductDiscounts_TblProducts_productId",
                table: "tblProductDiscounts",
                column: "productId",
                principalTable: "TblProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tblProductDiscounts_tblDiscounds_DiscountId",
                table: "tblProductDiscounts",
                column: "DiscountId",
                principalTable: "tblDiscounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TblProducts_tblCategories_CategoryId",
                table: "TblProducts",
                column: "CategoryId",
                principalTable: "tblCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
