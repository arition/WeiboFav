using Microsoft.EntityFrameworkCore.Migrations;

namespace WeiboFav.Migrations
{
    public partial class AddWeiboUrl : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "WeiboInfo",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Url",
                table: "WeiboInfo");
        }
    }
}
