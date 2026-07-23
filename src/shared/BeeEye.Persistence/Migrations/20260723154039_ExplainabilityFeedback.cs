using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeEye.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExplainabilityFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "explainability_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Verdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_explainability_feedback", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_explainability_feedback_IdempotencyKey",
                table: "explainability_feedback",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_explainability_feedback_SubjectKind_SubjectRef_SubmittedAtU~",
                table: "explainability_feedback",
                columns: new[] { "SubjectKind", "SubjectRef", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "explainability_feedback");
        }
    }
}
