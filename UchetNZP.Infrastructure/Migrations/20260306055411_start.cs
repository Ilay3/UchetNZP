using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class start : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ts = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    Succeeded = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabelNumberCounters",
                columns: table => new
                {
                    RootNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NextSuffix = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelNumberCounters", x => x.RootNumber);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    NewQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PreviousBalance = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    NewBalance = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    PreviousLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousLabelAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    NewLabelAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransferAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromOpNumber = table.Column<int>(type: "integer", nullable: false),
                    ToSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToOpNumber = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    TransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBalanceBefore = table.Column<decimal>(type: "numeric", nullable: false),
                    FromBalanceAfter = table.Column<decimal>(type: "numeric", nullable: false),
                    ToBalanceBefore = table.Column<decimal>(type: "numeric", nullable: false),
                    ToBalanceAfter = table.Column<decimal>(type: "numeric", nullable: false),
                    IsWarehouseTransfer = table.Column<bool>(type: "boolean", nullable: false),
                    WipLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabelNumber = table.Column<string>(type: "text", nullable: true),
                    LabelQuantityBefore = table.Column<decimal>(type: "numeric", nullable: true),
                    LabelQuantityAfter = table.Column<decimal>(type: "numeric", nullable: true),
                    ResidualWipLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResidualLabelNumber = table.Column<string>(type: "text", nullable: true),
                    ResidualLabelQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    ScrapQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ScrapType = table.Column<int>(type: "integer", nullable: true),
                    ScrapComment = table.Column<string>(type: "text", nullable: true),
                    IsReverted = table.Column<bool>(type: "boolean", nullable: false),
                    RevertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WipLabelLedger",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FromLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromOpNumber = table.Column<int>(type: "integer", nullable: true),
                    ToSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToOpNumber = table.Column<int>(type: "integer", nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ScrapQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    RefEntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RefEntityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipLabelLedger", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "ImportJobItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportJobItems_ImportJobs_ImportJobId",
                        column: x => x.ImportJobId,
                        principalTable: "ImportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WipLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LabelYear = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentOpNumber = table.Column<int>(type: "integer", nullable: true),
                    RootLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    RootNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Suffix = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true, defaultValueSql: "'\\x'::bytea")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipLabels", x => x.Id);
                    table.CheckConstraint("CK_WipLabels_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_WipLabels_Remaining_NonNegative", "\"RemainingQuantity\" >= 0");
                    table.CheckConstraint("CK_WipLabels_Remaining_NotGreaterThanQuantity", "\"RemainingQuantity\" <= \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_WipLabels_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormHours = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartRoutes_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartRoutes_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartRoutes_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WipBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipBalances_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipBalances_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WipLaunches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromOpNumber = table.Column<int>(type: "integer", nullable: false),
                    LaunchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SumHoursToFinish = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipLaunches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipLaunches_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipLaunches_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferAuditOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferAuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartRouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric", nullable: false),
                    QuantityChange = table.Column<decimal>(type: "numeric", nullable: false),
                    IsWarehouse = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferAuditOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferAuditOperations_TransferAudits_TransferAuditId",
                        column: x => x.TransferAuditId,
                        principalTable: "TransferAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabelMerges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InputLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelMerges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelMerges_WipLabels_InputLabelId",
                        column: x => x.InputLabelId,
                        principalTable: "WipLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabelMerges_WipLabels_OutputLabelId",
                        column: x => x.OutputLabelId,
                        principalTable: "WipLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WipReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    WipLabelId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipReceipts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipReceipts_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipReceipts_WipLabels_WipLabelId",
                        column: x => x.WipLabelId,
                        principalTable: "WipLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WipTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromOpNumber = table.Column<int>(type: "integer", nullable: false),
                    ToSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToOpNumber = table.Column<int>(type: "integer", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    WipLabelId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipTransfers_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipTransfers_WipLabels_WipLabelId",
                        column: x => x.WipLabelId,
                        principalTable: "WipLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WipBalanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WipBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    NewQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipBalanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipBalanceAdjustments_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WipBalanceAdjustments_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WipBalanceAdjustments_WipBalances_WipBalanceId",
                        column: x => x.WipBalanceId,
                        principalTable: "WipBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WipLaunchOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WipLaunchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    PartRouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    NormHours = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipLaunchOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipLaunchOperations_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WipLaunchOperations_PartRoutes_PartRouteId",
                        column: x => x.PartRouteId,
                        principalTable: "PartRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WipLaunchOperations_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WipLaunchOperations_WipLaunches_WipLaunchId",
                        column: x => x.WipLaunchId,
                        principalTable: "WipLaunches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferLabelUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ScrapQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    RemainingBefore = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CreatedToLabelId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferLabelUsages", x => x.Id);
                    table.CheckConstraint("CK_TransferLabelUsages_Consumption_WithinRemaining", "(\"Qty\" + \"ScrapQty\") <= \"RemainingBefore\"");
                    table.CheckConstraint("CK_TransferLabelUsages_Qty_NonNegative", "\"Qty\" >= 0");
                    table.CheckConstraint("CK_TransferLabelUsages_RemainingBefore_NonNegative", "\"RemainingBefore\" >= 0");
                    table.CheckConstraint("CK_TransferLabelUsages_ScrapQty_NonNegative", "\"ScrapQty\" >= 0");
                    table.ForeignKey(
                        name: "FK_TransferLabelUsages_WipLabels_CreatedToLabelId",
                        column: x => x.CreatedToLabelId,
                        principalTable: "WipLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferLabelUsages_WipLabels_FromLabelId",
                        column: x => x.FromLabelId,
                        principalTable: "WipLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferLabelUsages_WipTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WipTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseItems_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WarehouseItems_WipTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WipTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WipScraps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ScrapType = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipScraps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipScraps_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipScraps_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipScraps_WipTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WipTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WipTransferOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WipTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    PartRouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityChange = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipTransferOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_PartRoutes_PartRouteId",
                        column: x => x.PartRouteId,
                        principalTable: "PartRoutes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_WipTransfers_WipTransferId",
                        column: x => x.WipTransferId,
                        principalTable: "WipTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseLabelItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WipLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseLabelItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseLabelItems_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WarehouseLabelItems_WipLabels_WipLabelId",
                        column: x => x.WipLabelId,
                        principalTable: "WipLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobItems_ImportJobId_RowIndex",
                table: "ImportJobItems",
                columns: new[] { "ImportJobId", "RowIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelMerges_InputLabelId",
                table: "LabelMerges",
                column: "InputLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelMerges_InputLabelId_OutputLabelId",
                table: "LabelMerges",
                columns: new[] { "InputLabelId", "OutputLabelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelMerges_OutputLabelId",
                table: "LabelMerges",
                column: "OutputLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_Code",
                table: "Operations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartRoutes_OperationId",
                table: "PartRoutes",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_PartRoutes_PartId_OpNumber",
                table: "PartRoutes",
                columns: new[] { "PartId", "OpNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartRoutes_SectionId",
                table: "PartRoutes",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Code",
                table: "Parts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_Code",
                table: "Sections",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferAuditOperations_TransferAuditId",
                table: "TransferAuditOperations",
                column: "TransferAuditId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferLabelUsages_CreatedToLabelId",
                table: "TransferLabelUsages",
                column: "CreatedToLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferLabelUsages_FromLabelId",
                table: "TransferLabelUsages",
                column: "FromLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferLabelUsages_TransferId",
                table: "TransferLabelUsages",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_PartId",
                table: "WarehouseItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_TransferId",
                table: "WarehouseItems",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLabelItems_WarehouseItemId",
                table: "WarehouseLabelItems",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLabelItems_WipLabelId",
                table: "WarehouseLabelItems",
                column: "WipLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WipBalanceAdjustments_PartId",
                table: "WipBalanceAdjustments",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WipBalanceAdjustments_SectionId",
                table: "WipBalanceAdjustments",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipBalanceAdjustments_WipBalanceId",
                table: "WipBalanceAdjustments",
                column: "WipBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WipBalances_PartId_SectionId_OpNumber",
                table: "WipBalances",
                columns: new[] { "PartId", "SectionId", "OpNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipBalances_SectionId",
                table: "WipBalances",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_EventTime",
                table: "WipLabelLedger",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_FromLabelId",
                table: "WipLabelLedger",
                column: "FromLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_ToLabelId",
                table: "WipLabelLedger",
                column: "ToLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_TransactionId",
                table: "WipLabelLedger",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Number_LabelYear",
                table: "WipLabels",
                columns: new[] { "Number", "LabelYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_PartId",
                table: "WipLabels",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_RootLabelId",
                table: "WipLabels",
                column: "RootLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_RootNumber_Suffix",
                table: "WipLabels",
                columns: new[] { "RootNumber", "Suffix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Status_CurrentSectionId_CurrentOpNumber",
                table: "WipLabels",
                columns: new[] { "Status", "CurrentSectionId", "CurrentOpNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WipLaunches_PartId_SectionId_LaunchDate",
                table: "WipLaunches",
                columns: new[] { "PartId", "SectionId", "LaunchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WipLaunches_SectionId",
                table: "WipLaunches",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLaunchOperations_OperationId",
                table: "WipLaunchOperations",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLaunchOperations_PartRouteId",
                table: "WipLaunchOperations",
                column: "PartRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLaunchOperations_SectionId",
                table: "WipLaunchOperations",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLaunchOperations_WipLaunchId_OpNumber",
                table: "WipLaunchOperations",
                columns: new[] { "WipLaunchId", "OpNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipReceipts_PartId_SectionId_OpNumber_ReceiptDate",
                table: "WipReceipts",
                columns: new[] { "PartId", "SectionId", "OpNumber", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WipReceipts_SectionId",
                table: "WipReceipts",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipReceipts_WipLabelId",
                table: "WipReceipts",
                column: "WipLabelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipScraps_PartId_OpNumber",
                table: "WipScraps",
                columns: new[] { "PartId", "OpNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WipScraps_SectionId",
                table: "WipScraps",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipScraps_TransferId",
                table: "WipScraps",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_OperationId",
                table: "WipTransferOperations",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_PartRouteId",
                table: "WipTransferOperations",
                column: "PartRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_SectionId",
                table: "WipTransferOperations",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_WipTransferId",
                table: "WipTransferOperations",
                column: "WipTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransfers_PartId",
                table: "WipTransfers",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransfers_WipLabelId",
                table: "WipTransfers",
                column: "WipLabelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportJobItems");

            migrationBuilder.DropTable(
                name: "LabelMerges");

            migrationBuilder.DropTable(
                name: "LabelNumberCounters");

            migrationBuilder.DropTable(
                name: "ReceiptAudits");

            migrationBuilder.DropTable(
                name: "TransferAuditOperations");

            migrationBuilder.DropTable(
                name: "TransferLabelUsages");

            migrationBuilder.DropTable(
                name: "WarehouseLabelItems");

            migrationBuilder.DropTable(
                name: "WipBalanceAdjustments");

            migrationBuilder.DropTable(
                name: "WipLabelLedger");

            migrationBuilder.DropTable(
                name: "WipLaunchOperations");

            migrationBuilder.DropTable(
                name: "WipReceipts");

            migrationBuilder.DropTable(
                name: "WipScraps");

            migrationBuilder.DropTable(
                name: "WipTransferOperations");

            migrationBuilder.DropTable(
                name: "ImportJobs");

            migrationBuilder.DropTable(
                name: "TransferAudits");

            migrationBuilder.DropTable(
                name: "WarehouseItems");

            migrationBuilder.DropTable(
                name: "WipBalances");

            migrationBuilder.DropTable(
                name: "WipLaunches");

            migrationBuilder.DropTable(
                name: "PartRoutes");

            migrationBuilder.DropTable(
                name: "WipTransfers");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "WipLabels");

            migrationBuilder.DropTable(
                name: "Parts");
        }
    }
}
