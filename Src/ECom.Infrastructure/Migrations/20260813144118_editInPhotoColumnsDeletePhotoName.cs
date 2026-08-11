using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class editInPhotoColumnsDeletePhotoName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoName",
                table: "tblPhotos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoName",
                table: "tblPhotos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
