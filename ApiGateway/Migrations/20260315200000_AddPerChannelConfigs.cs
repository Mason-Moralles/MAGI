using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAGI.ApiGateway.Migrations
{
    /// <inheritdoc />
    public partial class AddPerChannelConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ArtsRootPath на таблице Channels
            migrationBuilder.AddColumn<string>(
                name: "ArtsRootPath",
                table: "Channels",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            // Таблица конфигурации парсера (per-channel)
            migrationBuilder.CreateTable(
                name: "ChannelParserConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Hashtags = table.Column<string>(type: "TEXT", nullable: false),
                    NegativeHashtags = table.Column<string>(type: "TEXT", nullable: false),
                    ImagesPerHashtag = table.Column<int>(type: "INTEGER", nullable: false),
                    ScrollDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageLoadDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Sources = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelParserConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelParserConfigs_ChannelId",
                table: "ChannelParserConfigs",
                column: "ChannelId",
                unique: true);

            // Таблица конфигурации теггера (per-channel)
            migrationBuilder.CreateTable(
                name: "ChannelTaggerConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RenameTemplate = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Separator = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    OnlyNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelTaggerConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelTaggerConfigs_ChannelId",
                table: "ChannelTaggerConfigs",
                column: "ChannelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelTaggerConfigs");

            migrationBuilder.DropTable(
                name: "ChannelParserConfigs");

            migrationBuilder.DropColumn(
                name: "ArtsRootPath",
                table: "Channels");
        }
    }
}
