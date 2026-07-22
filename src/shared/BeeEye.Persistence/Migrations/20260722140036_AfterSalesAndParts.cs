using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeEye.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AfterSalesAndParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "part_compatibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_part_compatibilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "part_supersessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OldPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_part_supersessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "part_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vin = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ServiceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_part_usages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    CurrentStock = table.Column<int>(type: "integer", nullable: false),
                    InboundStock = table.Column<int>(type: "integer", nullable: false),
                    SupersededByPartId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "service_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Vin = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Variant = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MonthsSinceSale = table.Column<int>(type: "integer", nullable: false),
                    MileageKm = table.Column<int>(type: "integer", nullable: false),
                    MileageBand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LaborHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Vin = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Variant = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Colour = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SaleMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    SaleYear = table.Column<int>(type: "integer", nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_sales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_part_compatibilities_Model",
                table: "part_compatibilities",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_part_compatibilities_PartId_Model",
                table: "part_compatibilities",
                columns: new[] { "PartId", "Model" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_part_supersessions_NewPartId",
                table: "part_supersessions",
                column: "NewPartId");

            migrationBuilder.CreateIndex(
                name: "IX_part_supersessions_OldPartId",
                table: "part_supersessions",
                column: "OldPartId");

            migrationBuilder.CreateIndex(
                name: "IX_part_usages_Model",
                table: "part_usages",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_part_usages_PartId_UsageDate",
                table: "part_usages",
                columns: new[] { "PartId", "UsageDate" });

            migrationBuilder.CreateIndex(
                name: "IX_parts_Category",
                table: "parts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_parts_PartNumber",
                table: "parts",
                column: "PartNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_events_Model_ServiceDate",
                table: "service_events",
                columns: new[] { "Model", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_service_events_Vin",
                table: "service_events",
                column: "Vin");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_sales_Location",
                table: "vehicle_sales",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_sales_Model_SaleMonth",
                table: "vehicle_sales",
                columns: new[] { "Model", "SaleMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_sales_Vin",
                table: "vehicle_sales",
                column: "Vin",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_compatibilities");

            migrationBuilder.DropTable(
                name: "part_supersessions");

            migrationBuilder.DropTable(
                name: "part_usages");

            migrationBuilder.DropTable(
                name: "parts");

            migrationBuilder.DropTable(
                name: "service_events");

            migrationBuilder.DropTable(
                name: "vehicle_sales");
        }
    }
}
