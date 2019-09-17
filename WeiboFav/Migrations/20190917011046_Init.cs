using Microsoft.EntityFrameworkCore.Migrations;

namespace WeiboFav.Migrations
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeiboInfo",
                columns: table => new
                {
                    WeiboInfoId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<string>(nullable: false),
                    RawHtml = table.Column<string>(nullable: false),
                    VideoUrl = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeiboInfo", x => x.WeiboInfoId);
                });

            migrationBuilder.CreateTable(
                name: "Img",
                columns: table => new
                {
                    ImgId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImgUrl = table.Column<string>(nullable: false),
                    ImgPath = table.Column<string>(nullable: true),
                    WeiboInfoId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Img", x => x.ImgId);
                    table.ForeignKey(
                        name: "FK_Img_WeiboInfo_WeiboInfoId",
                        column: x => x.WeiboInfoId,
                        principalTable: "WeiboInfo",
                        principalColumn: "WeiboInfoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Img_WeiboInfoId",
                table: "Img",
                column: "WeiboInfoId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Img");

            migrationBuilder.DropTable(
                name: "WeiboInfo");
        }
    }
}
