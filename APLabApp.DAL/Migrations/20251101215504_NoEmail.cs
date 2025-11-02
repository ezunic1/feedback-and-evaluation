using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APLabApp.DAL.Migrations
{
    /// <inheritdoc />
    public partial class NoEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "users",
                type: "text",
                nullable: true);
        }
    }
}
