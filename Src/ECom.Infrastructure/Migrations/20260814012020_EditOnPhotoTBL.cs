using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EditOnPhotoTBL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblPhotos_TblProducts_ProductId",
                table: "tblPhotos");

            migrationBuilder.DropIndex(
                name: "IX_tblPhotos_ProductId",
                table: "tblPhotos");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "tblPhotos");

            migrationBuilder.AddColumn<int>(
                name: "TblProductsId",
                table: "tblPhotos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tblPhotos_TblProductsId",
                table: "tblPhotos",
                column: "TblProductsId");

            migrationBuilder.AddForeignKey(
                name: "FK_tblPhotos_TblProducts_TblProductsId",
                table: "tblPhotos",
                column: "TblProductsId",
                principalTable: "TblProducts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblPhotos_TblProducts_TblProductsId",
                table: "tblPhotos");

            migrationBuilder.DropIndex(
                name: "IX_tblPhotos_TblProductsId",
                table: "tblPhotos");

            migrationBuilder.DropColumn(
                name: "TblProductsId",
                table: "tblPhotos");

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "tblPhotos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_tblPhotos_ProductId",
                table: "tblPhotos",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_tblPhotos_TblProducts_ProductId",
                table: "tblPhotos",
                column: "ProductId",
                principalTable: "TblProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
