using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceRbac.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePermissionModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resource",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "Resource",
                table: "Permissions");
        }
    }
}
