using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeEye.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceObject = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ChassisNo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Variant = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Colour = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Interior = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DateOfPurchase = table.Column<DateOnly>(type: "date", nullable: false),
                    DateOfManufacture = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HoldingCostPerDay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_facts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Variant = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Colour = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Interior = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UnitsSold = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DiscountApplied = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPct = table.Column<int>(type: "integer", nullable: false),
                    IsRamadan = table.Column<bool>(type: "boolean", nullable: false),
                    DateOfManufacture = table.Column<DateOnly>(type: "date", nullable: false),
                    RowHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IngestionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_facts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_batches_SourceObject_Checksum",
                table: "ingestion_batches",
                columns: new[] { "SourceObject", "Checksum" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Location_Model_Variant",
                table: "inventory_items",
                columns: new[] { "Location", "Model", "Variant" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_StockId",
                table: "inventory_items",
                column: "StockId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_facts_Model_Variant_Location_SaleMonth",
                table: "sales_facts",
                columns: new[] { "Model", "Variant", "Location", "SaleMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_facts_RowHash",
                table: "sales_facts",
                column: "RowHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_facts_SaleMonth",
                table: "sales_facts",
                column: "SaleMonth");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_batches");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropTable(
                name: "sales_facts");
        }
    }
}
