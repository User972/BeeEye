using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeEye.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManagementDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Route = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseStatus = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: false),
                    PrincipalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "management_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OpenedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ModificationJson = table.Column<string>(type: "jsonb", nullable: true),
                    ImplementedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImplementedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_management_decisions_recommendations_RecommendationId",
                        column: x => x.RecommendationId,
                        principalTable: "recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "action_outcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RealisedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MeasuredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_outcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_action_outcomes_management_decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "management_decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_steps_management_decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "management_decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_action_outcomes_DecisionId",
                table: "action_outcomes",
                column: "DecisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_steps_DecisionId_StepNumber",
                table: "approval_steps",
                columns: new[] { "DecisionId", "StepNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_ExpiresAtUtc",
                table: "idempotency_records",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_Key",
                table: "idempotency_records",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_management_decisions_IdempotencyKey",
                table: "management_decisions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_management_decisions_Outcome_OpenedAtUtc",
                table: "management_decisions",
                columns: new[] { "Outcome", "OpenedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_management_decisions_open_per_recommendation",
                table: "management_decisions",
                column: "RecommendationId",
                unique: true,
                filter: "\"Outcome\" = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_outcomes");

            migrationBuilder.DropTable(
                name: "approval_steps");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "management_decisions");
        }
    }
}
