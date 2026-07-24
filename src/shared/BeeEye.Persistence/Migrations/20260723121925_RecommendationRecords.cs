using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeEye.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RuleId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssumptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ImpactSar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    OwnerRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsDemoData = table.Column<bool>(type: "boolean", nullable: false),
                    RulesetVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DatasetVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AnalysisDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ValidUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupersededByRecommendationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recommendations_recommendations_SupersededByRecommendationId",
                        column: x => x.SupersededByRecommendationId,
                        principalTable: "recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_status_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Actor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_status_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recommendation_status_events_recommendations_Recommendation~",
                        column: x => x.RecommendationId,
                        principalTable: "recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_status_events_RecommendationId_AtUtc",
                table: "recommendation_status_events",
                columns: new[] { "RecommendationId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_AnalysisDate",
                table: "recommendations",
                column: "AnalysisDate");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_CurrentStatus_Priority",
                table: "recommendations",
                columns: new[] { "CurrentStatus", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_IdempotencyKey",
                table: "recommendations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_SubjectRef",
                table: "recommendations",
                column: "SubjectRef");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_SupersededByRecommendationId",
                table: "recommendations",
                column: "SupersededByRecommendationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_status_events");

            migrationBuilder.DropTable(
                name: "recommendations");
        }
    }
}
