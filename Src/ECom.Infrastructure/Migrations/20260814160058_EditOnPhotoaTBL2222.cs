using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EditOnPhotoaTBL2222 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblPhotos_TblProducts_TblProductsId",
                table: "tblPhotos");

            migrationBuilder.RenameColumn(
                name: "TblProductsId",
                table: "tblPhotos",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_tblPhotos_TblProductsId",
                table: "tblPhotos",
                newName: "IX_tblPhotos_ProductId");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "tblPhotos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tblPhotos_CategoryId",
                table: "tblPhotos",
                column: "CategoryId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblPhotos_TblProducts_ProductId",
                table: "tblPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_tblPhotos_tblCategories_CategoryId",
                table: "tblPhotos");

            migrationBuilder.DropIndex(
                name: "IX_tblPhotos_CategoryId",
                table: "tblPhotos");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "tblPhotos");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "tblPhotos",
                newName: "TblProductsId");

            migrationBuilder.RenameIndex(
                name: "IX_tblPhotos_ProductId",
                table: "tblPhotos",
                newName: "IX_tblPhotos_TblProductsId");

            migrationBuilder.AddForeignKey(
                name: "FK_tblPhotos_TblProducts_TblProductsId",
                table: "tblPhotos",
                column: "TblProductsId",
                principalTable: "TblProducts",
                principalColumn: "Id");
        }
    }
}
