using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyAddressIntoTblShippingAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old owned table keyed each shipping address by its order (TblOrdersId).
            // Add OrderId first and copy the old key over so existing orders keep their
            // shipping address, then drop the obsolete owner key.
            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "tblShippingAddresses",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [tblShippingAddresses] SET [OrderId] = [TblOrdersId];");

            migrationBuilder.DropForeignKey(
                name: "FK_tblShippingAddresses_tblOrders_TblOrdersId",
                table: "tblShippingAddresses");

            migrationBuilder.DropTable(
                name: "Address");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tblShippingAddresses",
                table: "tblShippingAddresses");

            migrationBuilder.DropColumn(
                name: "TblOrdersId",
                table: "tblShippingAddresses");

            // SQL Server cannot add IDENTITY to an existing column, so the column is
            // dropped and recreated. The old Id values were all 0 (it was not the key),
            // so no data is lost; the order link was already copied to OrderId above.
            migrationBuilder.DropColumn(
                name: "Id",
                table: "tblShippingAddresses");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "tblShippingAddresses",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<string>(
                name: "AppUserId",
                table: "tblShippingAddresses",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_tblShippingAddresses",
                table: "tblShippingAddresses",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_tblShippingAddresses_AppUserId",
                table: "tblShippingAddresses",
                column: "AppUserId",
                unique: true,
                filter: "[AppUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tblShippingAddresses_OrderId",
                table: "tblShippingAddresses",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_tblShippingAddresses_AspNetUsers_AppUserId",
                table: "tblShippingAddresses",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tblShippingAddresses_tblOrders_OrderId",
                table: "tblShippingAddresses",
                column: "OrderId",
                principalTable: "tblOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tblShippingAddresses_AspNetUsers_AppUserId",
                table: "tblShippingAddresses");

            migrationBuilder.DropForeignKey(
                name: "FK_tblShippingAddresses_tblOrders_OrderId",
                table: "tblShippingAddresses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tblShippingAddresses",
                table: "tblShippingAddresses");

            migrationBuilder.DropIndex(
                name: "IX_tblShippingAddresses_AppUserId",
                table: "tblShippingAddresses");

            migrationBuilder.DropIndex(
                name: "IX_tblShippingAddresses_OrderId",
                table: "tblShippingAddresses");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "tblShippingAddresses");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "tblShippingAddresses");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "tblShippingAddresses");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "tblShippingAddresses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TblOrdersId",
                table: "tblShippingAddresses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_tblShippingAddresses",
                table: "tblShippingAddresses",
                column: "TblOrdersId");

            migrationBuilder.CreateTable(
                name: "Address",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Street = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Address", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Address_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Address_AppUserId",
                table: "Address",
                column: "AppUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tblShippingAddresses_tblOrders_TblOrdersId",
                table: "tblShippingAddresses",
                column: "TblOrdersId",
                principalTable: "tblOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
