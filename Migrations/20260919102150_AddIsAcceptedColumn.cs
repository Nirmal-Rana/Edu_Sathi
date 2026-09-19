using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduSathi.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAcceptedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAccepted",
                table: "CustomRooms",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAccepted",
                table: "CustomRooms");
        }
    }
}
