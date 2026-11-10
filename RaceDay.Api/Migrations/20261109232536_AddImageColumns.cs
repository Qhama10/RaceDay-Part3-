using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Raceday_API__prog_part_2_.Migrations
{
    /// <inheritdoc />
    public partial class AddImageColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "profile_picture_url",
                table: "Participants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profile_picture_url",
                table: "Organisers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "banner_url",
                table: "Events",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "profile_picture_url",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "profile_picture_url",
                table: "Organisers");

            migrationBuilder.DropColumn(
                name: "banner_url",
                table: "Events");
        }
    }
}
