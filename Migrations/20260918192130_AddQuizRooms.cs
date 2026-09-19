using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduSathi.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuizRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatorUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuizRoomDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuizRoomId = table.Column<int>(type: "int", nullable: false),
                    UploadedDocumentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizRoomDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizRoomDocuments_QuizRooms_QuizRoomId",
                        column: x => x.QuizRoomId,
                        principalTable: "QuizRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizRoomDocuments_UploadedDocuments_UploadedDocumentId",
                        column: x => x.UploadedDocumentId,
                        principalTable: "UploadedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizRoomParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuizRoomId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsHost = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    TotalQuestions = table.Column<int>(type: "int", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizRoomParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizRoomParticipants_QuizRooms_QuizRoomId",
                        column: x => x.QuizRoomId,
                        principalTable: "QuizRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizRoomDocuments_QuizRoomId",
                table: "QuizRoomDocuments",
                column: "QuizRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizRoomDocuments_UploadedDocumentId",
                table: "QuizRoomDocuments",
                column: "UploadedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizRoomParticipants_QuizRoomId",
                table: "QuizRoomParticipants",
                column: "QuizRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizRooms_Code",
                table: "QuizRooms",
                column: "Code",
                unique: true,
                filter: "[Code] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizRoomDocuments");

            migrationBuilder.DropTable(
                name: "QuizRoomParticipants");

            migrationBuilder.DropTable(
                name: "QuizRooms");
        }
    }
}
