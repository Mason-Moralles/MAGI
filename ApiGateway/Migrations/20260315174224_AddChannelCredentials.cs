using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAGI.ApiGateway.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelNetworks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelNetworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Link = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NetworkId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PublishMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApiId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApiHash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BotToken = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SessionFile = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DelayBetweenPosts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Hashtag = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Person = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Posted = table.Column<int>(type: "INTEGER", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PostTime = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostedImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Person = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PostedAt = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostedImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Time = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Days = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsoKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Date = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Time = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    File = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Person = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSlots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_NetworkId",
                table: "Channels",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadRecords_SourceUrl",
                table: "DownloadRecords",
                column: "SourceUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FileName",
                table: "Images",
                column: "FileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostedImages_FileName",
                table: "PostedImages",
                column: "FileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostingRules_ChannelId",
                table: "PostingRules",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlots_IsoKey",
                table: "ScheduleSlots",
                column: "IsoKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelNetworks");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "DownloadRecords");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "PostedImages");

            migrationBuilder.DropTable(
                name: "PostingRules");

            migrationBuilder.DropTable(
                name: "ScheduleSlots");
        }
    }
}
