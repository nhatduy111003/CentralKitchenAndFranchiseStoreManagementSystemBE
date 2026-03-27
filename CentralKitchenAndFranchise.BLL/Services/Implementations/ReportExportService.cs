using System.Text.Json;
using ClosedXML.Excel;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Requests.Reports;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class ReportExportService : IReportExportService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly AppDbContext _db;
    private readonly IDashboardScopeService _scopeService;
    private readonly IStoreDashboardService _storeDashboardService;
    private readonly IKitchenDashboardService _kitchenDashboardService;
    private readonly IReportsService _reportsService;

    public ReportExportService(
        AppDbContext db,
        IDashboardScopeService scopeService,
        IStoreDashboardService storeDashboardService,
        IKitchenDashboardService kitchenDashboardService,
        IReportsService reportsService)
    {
        _db = db;
        _scopeService = scopeService;
        _storeDashboardService = storeDashboardService;
        _kitchenDashboardService = kitchenDashboardService;
        _reportsService = reportsService;
    }

    /// <summary>Export one monthly XLSX workbook for a single franchise/store scope.</summary>
    public async Task<FileExportPayload> ExportStoreMonthlyAsync(StoreMonthlyExportQuery query, CancellationToken ct = default)
    {
        query ??= new StoreMonthlyExportQuery();

        var month = NormalizeMonth(query.Year, query.Month, query.TimezoneOffsetMinutes);
        var scope = await _scopeService.ResolveFranchiseScopeAsync(query.FranchiseId, ct);

        var overview = await _storeDashboardService.GetOverviewAsync(new StoreDashboardOverviewQuery
        {
            FranchiseId = scope.FranchiseId,
            FromDate = month.FromDate,
            ToDate = month.ToDate,
            TimezoneOffsetMinutes = month.TimezoneOffsetMinutes,
            Limit = 100
        }, ct);

        var inventoryReport = await _reportsService.GetInventoryReportAsync(new InventoryReportQuery
        {
            FranchiseId = scope.FranchiseId,
            FromDate = month.FromDate,
            ToDate = month.ToDate,
            TimezoneOffsetMinutes = month.TimezoneOffsetMinutes
        }, ct);

        var wastageReport = await _reportsService.GetWastageReportAsync(new WastageReportQuery
        {
            FranchiseId = scope.FranchiseId,
            FromDate = month.FromDate,
            ToDate = month.ToDate,
            TimezoneOffsetMinutes = month.TimezoneOffsetMinutes,
            SortBy = "lostValue"
        }, ct);

        var orderRows = await QueryOrderRowsAsync(
            isFranchiseScope: true,
            scopeId: scope.FranchiseId,
            fromDate: month.FromDate,
            toDate: month.ToDate,
            ct);

        var receivingRows = await QueryDeliveryRowsAsync(
            isFranchiseScope: true,
            scopeId: scope.FranchiseId,
            fromDate: month.FromDate,
            toDate: month.ToDate,
            ct);

        var closingBatchRows = await QueryClosingBatchSnapshotRowsAsync(
            isFranchiseScope: true,
            scopeId: scope.FranchiseId,
            toUtcExclusive: month.ToUtcExclusive,
            ct);

        using var workbook = new XLWorkbook();

        BuildStoreOverviewSheet(workbook, scope, month, overview);
        BuildStoreOrdersSheet(workbook, orderRows);
        BuildStoreReceivingsSheet(workbook, receivingRows);
        BuildInventoryReportSheet(workbook, "Inventory Summary", inventoryReport);
        BuildBatchSnapshotSheet(workbook, "Closing Batches", month, closingBatchRows);
        BuildWastageSheet(workbook, wastageReport);
        BuildStoreAlertsSheet(workbook, overview);
        BuildNotesSheet(workbook, overview.Notes, inventoryReport.Notes, wastageReport.Notes);

        return BuildFilePayload(
            workbook,
            $"store_{scope.FranchiseId}_{month.FromDate:yyyy_MM}_monthly_report.xlsx");
    }

    /// <summary>Export one monthly XLSX workbook for a single central-kitchen scope.</summary>
    public async Task<FileExportPayload> ExportKitchenMonthlyAsync(KitchenMonthlyExportQuery query, CancellationToken ct = default)
    {
        query ??= new KitchenMonthlyExportQuery();

        var month = NormalizeMonth(query.Year, query.Month, query.TimezoneOffsetMinutes);
        var scope = await _scopeService.ResolveCentralKitchenScopeAsync(query.CentralKitchenId, ct);

        var overview = await _kitchenDashboardService.GetOverviewAsync(new KitchenDashboardOverviewQuery
        {
            CentralKitchenId = scope.CentralKitchenId,
            FromDate = month.FromDate,
            ToDate = month.ToDate,
            TimezoneOffsetMinutes = month.TimezoneOffsetMinutes,
            Limit = 100
        }, ct);

        var inventoryReport = await _reportsService.GetInventoryReportAsync(new InventoryReportQuery
        {
            CentralKitchenId = scope.CentralKitchenId,
            FromDate = month.FromDate,
            ToDate = month.ToDate,
            TimezoneOffsetMinutes = month.TimezoneOffsetMinutes
        }, ct);

        var wastageReport = await _reportsService.GetWastageReportAsync(new WastageReportQuery
        {
            CentralKitchenId = scope.CentralKitchenId,
            FromDate = month.FromDate,
            ToDate = month.ToDate,
            TimezoneOffsetMinutes = month.TimezoneOffsetMinutes,
            SortBy = "lostValue"
        }, ct);

        var incomingOrderRows = await QueryOrderRowsAsync(
            isFranchiseScope: false,
            scopeId: scope.CentralKitchenId,
            fromDate: month.FromDate,
            toDate: month.ToDate,
            ct);

        var deliveryRows = await QueryDeliveryRowsAsync(
            isFranchiseScope: false,
            scopeId: scope.CentralKitchenId,
            fromDate: month.FromDate,
            toDate: month.ToDate,
            ct);

        var productionPlanRows = await QueryProductionPlanRowsAsync(
            scope.CentralKitchenId,
            month.FromDate,
            month.ToDate,
            ct);

        var productionRunRows = await QueryProductionRunRowsAsync(
            scope.CentralKitchenId,
            month.FromDate,
            month.ToDate,
            ct);

        var closingBatchRows = await QueryClosingBatchSnapshotRowsAsync(
            isFranchiseScope: false,
            scopeId: scope.CentralKitchenId,
            toUtcExclusive: month.ToUtcExclusive,
            ct);

        using var workbook = new XLWorkbook();

        BuildKitchenOverviewSheet(workbook, scope, month, overview);
        BuildKitchenIncomingOrdersSheet(workbook, incomingOrderRows);
        BuildProductionPlansSheet(workbook, productionPlanRows);
        BuildProductionRunsSheet(workbook, productionRunRows);
        BuildKitchenDeliveriesSheet(workbook, deliveryRows);
        BuildInventoryReportSheet(workbook, "Inventory Summary", inventoryReport);
        BuildBatchSnapshotSheet(workbook, "Closing Batches", month, closingBatchRows);
        BuildWastageSheet(workbook, wastageReport);
        BuildKitchenActionsSheet(workbook, overview);
        BuildNotesSheet(workbook, overview.Notes, inventoryReport.Notes, wastageReport.Notes);

        return BuildFilePayload(
            workbook,
            $"kitchen_{scope.CentralKitchenId}_{month.FromDate:yyyy_MM}_monthly_report.xlsx");
    }

    /// <summary>Build the store overview worksheet from dashboard aggregates.</summary>
    private static void BuildStoreOverviewSheet(
        XLWorkbook workbook,
        DashboardFranchiseScope scope,
        MonthlyDateRange month,
        StoreDashboardOverviewResponse overview)
    {
        var ws = workbook.Worksheets.Add("Overview");
        var row = 1;

        WriteTitle(ws, ref row, $"Store Monthly Report - {scope.FranchiseName}");

        WriteTable(ws, ref row, "Scope", new[]
        {
            "Field", "Value"
        }, new[]
        {
            new object?[] { "Month", $"{month.FromDate:yyyy-MM}" },
            new object?[] { "FranchiseId", scope.FranchiseId },
            new object?[] { "FranchiseName", scope.FranchiseName },
            new object?[] { "CentralKitchenId", scope.CentralKitchenId },
            new object?[] { "CentralKitchenName", scope.CentralKitchenName },
            new object?[] { "DateRange", $"{month.FromDate:yyyy-MM-dd} -> {month.ToDate:yyyy-MM-dd}" },
            new object?[] { "TimezoneOffsetMinutes", month.TimezoneOffsetMinutes }
        });

        WriteTable(ws, ref row, "KPI", new[]
        {
            "Metric", "Value"
        }, new[]
        {
            new object?[] { "Total Orders", overview.OrderSummary.Total },
            new object?[] { "Active Orders", overview.OrderSummary.ActiveOrderCount },
            new object?[] { "Delivered Pending Receiving", overview.OrderSummary.DeliveredPendingReceivingCount },
            new object?[] { "Received Orders", overview.OrderSummary.ReceivedCount },
            new object?[] { "Pending Receiving Confirmation", overview.ReceivingSummary.PendingConfirmationCount },
            new object?[] { "Confirmed Receivings", overview.ReceivingSummary.ConfirmedCount },
            new object?[] { "Ingredient Item Count", overview.InventorySummary.IngredientItemCount },
            new object?[] { "Product Item Count", overview.InventorySummary.ProductItemCount },
            new object?[] { "Low Stock Ingredient Count", overview.InventorySummary.LowStockIngredientCount },
            new object?[] { "Near Expiry Ingredient Batch Count", overview.InventorySummary.NearExpiryIngredientBatchCount },
            new object?[] { "Total Ingredient On Hand", overview.InventorySummary.TotalIngredientOnHand },
            new object?[] { "Total Product On Hand", overview.InventorySummary.TotalProductOnHand },
            new object?[] { "Latest Delivered At (UTC)", overview.ReceivingSummary.LatestDeliveredAtUtc },
            new object?[] { "Latest Confirmed At (UTC)", overview.ReceivingSummary.LatestConfirmedAtUtc }
        });

        WriteTable(ws, ref row, "Order Status Breakdown", new[]
        {
            "Status", "Count"
        }, overview.OrderSummary.ByStatus
            .OrderByDescending(x => x.Value)
            .Select(x => new object?[] { x.Key, x.Value }));

        WriteTable(ws, ref row, "Recent Deliveries", new[]
        {
            "DeliveryId", "DeliveryCode", "PlannedDate", "Status", "CreatedAtUtc", "DeliveredAtUtc", "ConfirmedAtUtc", "TotalItems", "TotalQuantity"
        }, overview.RecentDeliveries.Select(x => new object?[]
        {
            x.DeliveryId,
            x.DeliveryCode,
            x.PlannedDate,
            x.Status,
            x.CreatedAt,
            x.DeliveredAt,
            x.ConfirmedAt,
            x.TotalItems,
            x.TotalQuantity
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the kitchen overview worksheet from dashboard aggregates.</summary>
    private static void BuildKitchenOverviewSheet(
        XLWorkbook workbook,
        DashboardCentralKitchenScope scope,
        MonthlyDateRange month,
        KitchenDashboardOverviewResponse overview)
    {
        var ws = workbook.Worksheets.Add("Overview");
        var row = 1;

        WriteTitle(ws, ref row, $"Kitchen Monthly Report - {scope.CentralKitchenName}");

        WriteTable(ws, ref row, "Scope", new[]
        {
            "Field", "Value"
        }, new[]
        {
            new object?[] { "Month", $"{month.FromDate:yyyy-MM}" },
            new object?[] { "CentralKitchenId", scope.CentralKitchenId },
            new object?[] { "CentralKitchenName", scope.CentralKitchenName },
            new object?[] { "DateRange", $"{month.FromDate:yyyy-MM-dd} -> {month.ToDate:yyyy-MM-dd}" },
            new object?[] { "TimezoneOffsetMinutes", month.TimezoneOffsetMinutes },
            new object?[] { "Managed Franchise Count", overview.ManagedFranchiseCount }
        });

        WriteTable(ws, ref row, "KPI", new[]
        {
            "Metric", "Value"
        }, new[]
        {
            new object?[] { "Total Incoming Orders", overview.OrderQueueSummary.Total },
            new object?[] { "Locked Orders", overview.OrderQueueSummary.LockedCount },
            new object?[] { "Received By Kitchen", overview.OrderQueueSummary.ReceivedByKitchenCount },
            new object?[] { "Forwarded To Supply", overview.OrderQueueSummary.ForwardedToSupplyCount },
            new object?[] { "Overdue Order Actions", overview.OrderQueueSummary.OverdueActionCount },
            new object?[] { "Total Production Plans", overview.ProductionPlanSummary.Total },
            new object?[] { "Due Today Open Plans", overview.ProductionPlanSummary.DueTodayOpenCount },
            new object?[] { "Overdue Open Plans", overview.ProductionPlanSummary.OverdueOpenCount },
            new object?[] { "Total Planned Quantity", overview.ProductionPlanSummary.TotalPlannedQuantity },
            new object?[] { "Total Production Runs", overview.ProductionRunSummary.Total },
            new object?[] { "Total Run Quantity", overview.ProductionRunSummary.TotalRunQuantity },
            new object?[] { "Completed Run Quantity", overview.ProductionRunSummary.CompletedQuantity }
        });

        WriteTable(ws, ref row, "Incoming Order Status Breakdown", new[]
        {
            "Status", "Count"
        }, overview.OrderQueueSummary.ByStatus
            .OrderByDescending(x => x.Value)
            .Select(x => new object?[] { x.Key, x.Value }));

        WriteTable(ws, ref row, "Production Plan Status Breakdown", new[]
        {
            "Status", "Count"
        }, overview.ProductionPlanSummary.ByStatus
            .OrderByDescending(x => x.Value)
            .Select(x => new object?[] { x.Key, x.Value }));

        WriteTable(ws, ref row, "Production Run Status Breakdown", new[]
        {
            "Status", "Count"
        }, overview.ProductionRunSummary.ByStatus
            .OrderByDescending(x => x.Value)
            .Select(x => new object?[] { x.Key, x.Value }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the store order detail worksheet.</summary>
    private static void BuildStoreOrdersSheet(XLWorkbook workbook, IReadOnlyCollection<OrderExportRow> rows)
    {
        var ws = workbook.Worksheets.Add("Orders");
        var row = 1;

        WriteTitle(ws, ref row, "Store Orders");

        WriteTable(ws, ref row, "Order Lines", new[]
        {
            "StoreOrderId", "OrderCode", "OrderDate", "Status", "ItemType", "ItemId", "ItemName", "Unit",
            "RequestedQuantity", "ForwardedQuantity", "DroppedQuantity", "HasDrop", "DropReason",
            "CreatedAtUtc", "SubmittedAtUtc", "LockedAtUtc", "CancelledAtUtc", "CancelReason"
        }, rows.Select(x => new object?[]
        {
            x.StoreOrderId,
            x.OrderCode,
            x.OrderDate,
            x.Status,
            x.ItemType,
            x.ItemId,
            x.ItemName,
            x.Unit,
            x.RequestedQuantity,
            x.ForwardedQuantity,
            x.DroppedQuantity,
            x.HasDrop,
            x.DropReason,
            x.CreatedAt,
            x.SubmittedAt,
            x.LockedAt,
            x.CancelledAt,
            x.CancelReason
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the kitchen incoming-order detail worksheet.</summary>
    private static void BuildKitchenIncomingOrdersSheet(XLWorkbook workbook, IReadOnlyCollection<OrderExportRow> rows)
    {
        var ws = workbook.Worksheets.Add("Incoming Orders");
        var row = 1;

        WriteTitle(ws, ref row, "Incoming Store Orders");

        WriteTable(ws, ref row, "Incoming Order Lines", new[]
        {
            "StoreOrderId", "OrderCode", "FranchiseId", "FranchiseName", "OrderDate", "Status",
            "ItemType", "ItemId", "ItemName", "Unit",
            "RequestedQuantity", "ForwardedQuantity", "DroppedQuantity", "HasDrop", "DropReason",
            "CreatedAtUtc", "SubmittedAtUtc", "LockedAtUtc"
        }, rows.Select(x => new object?[]
        {
            x.StoreOrderId,
            x.OrderCode,
            x.FranchiseId,
            x.FranchiseName,
            x.OrderDate,
            x.Status,
            x.ItemType,
            x.ItemId,
            x.ItemName,
            x.Unit,
            x.RequestedQuantity,
            x.ForwardedQuantity,
            x.DroppedQuantity,
            x.HasDrop,
            x.DropReason,
            x.CreatedAt,
            x.SubmittedAt,
            x.LockedAt
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the store receiving worksheet from delivery/receiving lines.</summary>
    private static void BuildStoreReceivingsSheet(XLWorkbook workbook, IReadOnlyCollection<DeliveryExportRow> rows)
    {
        var ws = workbook.Worksheets.Add("Receivings");
        var row = 1;

        WriteTitle(ws, ref row, "Store Receivings");

        WriteTable(ws, ref row, "Receiving Lines", new[]
        {
            "DeliveryId", "DeliveryCode", "StoreOrderId", "OrderCode", "PlanDate",
            "DeliveryStatus", "ReceivingStatus", "DeliveredAtUtc", "ConfirmedAtUtc",
            "ItemType", "ItemId", "ItemName", "Unit",
            "ExpectedQuantity", "DeliveredQuantity", "ReceivedQuantity",
            "DroppedQuantity", "HasDrop", "DropReason"
        }, rows.Select(x => new object?[]
        {
            x.DeliveryId,
            x.DeliveryCode,
            x.StoreOrderId,
            x.OrderCode,
            x.PlanDate,
            x.DeliveryStatus,
            x.ReceivingStatus,
            x.DeliveredAt,
            x.ConfirmedAt,
            x.ItemType,
            x.ItemId,
            x.ItemName,
            x.Unit,
            x.ExpectedQuantity,
            x.DeliveredQuantity,
            x.ReceivedQuantity,
            x.DroppedQuantity,
            x.HasDrop,
            x.DropReason
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the kitchen outbound-delivery worksheet.</summary>
    private static void BuildKitchenDeliveriesSheet(XLWorkbook workbook, IReadOnlyCollection<DeliveryExportRow> rows)
    {
        var ws = workbook.Worksheets.Add("Deliveries");
        var row = 1;

        WriteTitle(ws, ref row, "Kitchen Outbound Deliveries");

        WriteTable(ws, ref row, "Delivery Lines", new[]
        {
            "DeliveryId", "DeliveryCode", "FranchiseId", "FranchiseName",
            "StoreOrderId", "OrderCode", "PlanDate", "DeliveryStatus", "ReceivingStatus",
            "DeliveredAtUtc", "ConfirmedAtUtc",
            "ItemType", "ItemId", "ItemName", "Unit",
            "ExpectedQuantity", "DeliveredQuantity", "ReceivedQuantity",
            "DroppedQuantity", "HasDrop", "DropReason"
        }, rows.Select(x => new object?[]
        {
            x.DeliveryId,
            x.DeliveryCode,
            x.FranchiseId,
            x.FranchiseName,
            x.StoreOrderId,
            x.OrderCode,
            x.PlanDate,
            x.DeliveryStatus,
            x.ReceivingStatus,
            x.DeliveredAt,
            x.ConfirmedAt,
            x.ItemType,
            x.ItemId,
            x.ItemName,
            x.Unit,
            x.ExpectedQuantity,
            x.DeliveredQuantity,
            x.ReceivedQuantity,
            x.DroppedQuantity,
            x.HasDrop,
            x.DropReason
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the shared inventory summary worksheet from inventory report rows.</summary>
    private static void BuildInventoryReportSheet(XLWorkbook workbook, string sheetName, InventoryReportResponse report)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        var row = 1;

        WriteTitle(ws, ref row, sheetName);

        WriteTable(ws, ref row, "Scope", new[]
        {
            "Field", "Value"
        }, new[]
        {
            new object?[] { "FromDate", report.FromDate },
            new object?[] { "ToDate", report.ToDate },
            new object?[] { "ScopeType", report.ScopeType },
            new object?[] { "FranchiseId", report.FranchiseId },
            new object?[] { "FranchiseName", report.FranchiseName },
            new object?[] { "CentralKitchenId", report.CentralKitchenId },
            new object?[] { "CentralKitchenName", report.CentralKitchenName },
            new object?[] { "TimezoneOffsetMinutes", report.TimezoneOffsetMinutes }
        });

        WriteTable(ws, ref row, "Inventory Items", new[]
        {
            "ItemType", "ItemId", "ItemName", "Unit",
            "OpeningQuantity", "InboundQuantity", "OutboundQuantity", "WastedQuantity",
            "AdjustmentQuantity", "ClosingQuantity", "ClosingValue"
        }, report.Items.Select(x => new object?[]
        {
            x.ItemType,
            x.ItemId,
            x.ItemName,
            x.Unit,
            x.OpeningQuantity,
            x.InboundQuantity,
            x.OutboundQuantity,
            x.WastedQuantity,
            x.AdjustmentQuantity,
            x.ClosingQuantity,
            x.ClosingValue
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build one closing-batch snapshot worksheet reconstructed at the selected month end.</summary>
    private static void BuildBatchSnapshotSheet(
        XLWorkbook workbook,
        string sheetName,
        MonthlyDateRange month,
        IReadOnlyCollection<InventoryBatchSnapshotRow> rows)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        var row = 1;

        WriteTitle(ws, ref row, sheetName);

        WriteTable(ws, ref row, "Snapshot Metadata", new[]
        {
            "Field", "Value"
        }, new[]
        {
            new object?[] { "SnapshotAtLocalBusinessEnd", $"{month.ToDate:yyyy-MM-dd} 23:59:59" },
            new object?[] { "SnapshotUtcExclusiveBoundary", month.ToUtcExclusive },
            new object?[] { "Note", "ClosingQuantity is reconstructed from movement history + signed adjustment audit logs up to the month-end boundary." }
        });

        WriteTable(ws, ref row, "Batch Snapshot Rows", new[]
        {
            "ItemType", "ItemId", "ItemName", "Unit", "BatchId", "BatchCode", "CreatedAtUtc", "ExpiredAt",
            "ClosingQuantity"
        }, rows.Select(x => new object?[]
        {
            x.ItemType,
            x.ItemId,
            x.ItemName,
            x.Unit,
            x.BatchId,
            x.BatchCode,
            x.CreatedAt,
            x.ExpiredAt,
            x.ClosingQuantity
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the wastage worksheet from the existing wastage report response.</summary>
    private static void BuildWastageSheet(XLWorkbook workbook, WastageReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Wastage");
        var row = 1;

        WriteTitle(ws, ref row, "Wastage");

        WriteTable(ws, ref row, "Scope", new[]
        {
            "Field", "Value"
        }, new[]
        {
            new object?[] { "FromDate", report.FromDate },
            new object?[] { "ToDate", report.ToDate },
            new object?[] { "ScopeType", report.ScopeType },
            new object?[] { "FranchiseId", report.FranchiseId },
            new object?[] { "FranchiseName", report.FranchiseName },
            new object?[] { "CentralKitchenId", report.CentralKitchenId },
            new object?[] { "CentralKitchenName", report.CentralKitchenName },
            new object?[] { "SortBy", report.SortBy }
        });

        WriteTable(ws, ref row, "Wastage Rows", new[]
        {
            "IngredientId", "IngredientName", "Unit", "WasteReason", "WastedQuantity", "WasteRate", "TotalLostValue"
        }, report.Items.Select(x => new object?[]
        {
            x.IngredientId,
            x.IngredientName,
            x.Unit,
            x.WasteReason,
            x.WastedQuantity,
            x.WasteRate,
            x.TotalLostValue
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the store operational alert worksheet.</summary>
    private static void BuildStoreAlertsSheet(XLWorkbook workbook, StoreDashboardOverviewResponse overview)
    {
        var ws = workbook.Worksheets.Add("Alerts");
        var row = 1;

        WriteTitle(ws, ref row, "Store Alerts");

        WriteTable(ws, ref row, "Low Stock", new[]
        {
            "IngredientId", "IngredientName", "Unit", "OnHandQuantity", "SafetyStock"
        }, overview.LowStockAlerts.Select(x => new object?[]
        {
            x.IngredientId,
            x.IngredientName,
            x.Unit,
            x.OnHandQuantity,
            x.SafetyStock
        }));

        WriteTable(ws, ref row, "Near Expiry", new[]
        {
            "IngredientId", "IngredientName", "Unit", "BatchId", "BatchCode", "Quantity", "ExpiredAt", "DaysToExpire"
        }, overview.NearExpiryAlerts.Select(x => new object?[]
        {
            x.IngredientId,
            x.IngredientName,
            x.Unit,
            x.BatchId,
            x.BatchCode,
            x.Quantity,
            x.ExpiredAt,
            x.DaysToExpire
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the kitchen operational action worksheet.</summary>
    private static void BuildKitchenActionsSheet(XLWorkbook workbook, KitchenDashboardOverviewResponse overview)
    {
        var ws = workbook.Worksheets.Add("Actions");
        var row = 1;

        WriteTitle(ws, ref row, "Kitchen Alerts & Priority Actions");

        WriteTable(ws, ref row, "Low Stock", new[]
        {
            "IngredientId", "IngredientName", "Unit", "OnHandQuantity", "SafetyStock"
        }, overview.LowStockAlerts.Select(x => new object?[]
        {
            x.IngredientId,
            x.IngredientName,
            x.Unit,
            x.OnHandQuantity,
            x.SafetyStock
        }));

        WriteTable(ws, ref row, "Near Expiry", new[]
        {
            "IngredientId", "IngredientName", "Unit", "BatchId", "BatchCode", "Quantity", "ExpiredAt", "DaysToExpire"
        }, overview.NearExpiryAlerts.Select(x => new object?[]
        {
            x.IngredientId,
            x.IngredientName,
            x.Unit,
            x.BatchId,
            x.BatchCode,
            x.Quantity,
            x.ExpiredAt,
            x.DaysToExpire
        }));

        WriteTable(ws, ref row, "Priority Actions", new[]
        {
            "ActionType", "Message", "RelatedId", "RelatedCode", "BusinessDate", "OccurredAtUtc"
        }, overview.PriorityActions.Select(x => new object?[]
        {
            x.ActionType,
            x.Message,
            x.RelatedId,
            x.RelatedCode,
            x.BusinessDate,
            x.OccurredAtUtc
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the production-plan worksheet.</summary>
    private static void BuildProductionPlansSheet(XLWorkbook workbook, IReadOnlyCollection<ProductionPlanExportRow> rows)
    {
        var ws = workbook.Worksheets.Add("Production Plans");
        var row = 1;

        WriteTitle(ws, ref row, "Production Plans");

        WriteTable(ws, ref row, "Plan Rows", new[]
        {
            "ProductionPlanId", "PlanDate", "Status", "ProductId", "ProductName", "Unit", "Quantity", "CreatedAtUtc"
        }, rows.Select(x => new object?[]
        {
            x.ProductionPlanId,
            x.PlanDate,
            x.Status,
            x.ProductId,
            x.ProductName,
            x.Unit,
            x.Quantity,
            x.CreatedAt
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build the production-run worksheet.</summary>
    private static void BuildProductionRunsSheet(XLWorkbook workbook, IReadOnlyCollection<ProductionRunExportRow> rows)
    {
        var ws = workbook.Worksheets.Add("Production Runs");
        var row = 1;

        WriteTitle(ws, ref row, "Production Runs");

        WriteTable(ws, ref row, "Run Rows", new[]
        {
            "ProductionRunId", "RunCode", "ProductionPlanId", "PlanDate", "ProductionDate",
            "Quantity", "Status", "CreatedAtUtc", "CompletedAtUtc"
        }, rows.Select(x => new object?[]
        {
            x.ProductionRunId,
            x.RunCode,
            x.ProductionPlanId,
            x.PlanDate,
            x.ProductionDate,
            x.Quantity,
            x.Status,
            x.CreatedAt,
            x.CompletedAt
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Build a notes worksheet to keep business caveats visible to FE and management.</summary>
    private static void BuildNotesSheet(
        XLWorkbook workbook,
        IEnumerable<string> dashboardNotes,
        IEnumerable<string> inventoryNotes,
        IEnumerable<string> wastageNotes)
    {
        var ws = workbook.Worksheets.Add("Notes");
        var row = 1;

        WriteTitle(ws, ref row, "Notes");

        var notes = dashboardNotes
            .Concat(inventoryNotes)
            .Concat(wastageNotes)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        WriteTable(ws, ref row, "Collected Notes", new[]
        {
            "No", "Note"
        }, notes.Select((x, idx) => new object?[]
        {
            idx + 1,
            x
        }));

        FinalizeSheet(ws);
    }

    /// <summary>Query flattened order/item rows with forwarded/dropped snapshot reconstructed from delivery lines.</summary>
    private async Task<List<OrderExportRow>> QueryOrderRowsAsync(
        bool isFranchiseScope,
        int scopeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        IQueryable<StoreOrder> orderQuery = _db.StoreOrders.AsNoTracking();

        orderQuery = isFranchiseScope
            ? orderQuery.Where(x => x.FranchiseId == scopeId)
            : orderQuery.Where(x => x.Franchise.CentralKitchenId == scopeId);

        var orders = await orderQuery
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .Select(x => new OrderBaseRow
            {
                StoreOrderId = x.StoreOrderId,
                FranchiseId = x.FranchiseId,
                FranchiseName = x.Franchise.Name,
                Status = x.Status,
                OrderDate = x.OrderDate,
                CreatedAt = x.CreatedAt,
                SubmittedAt = x.SubmittedAt,
                LockedAt = x.LockedAt,
                CancelledAt = x.CancelledAt,
                CancelReason = x.CancelReason
            })
            .OrderBy(x => x.OrderDate)
            .ThenBy(x => x.StoreOrderId)
            .ToListAsync(ct);

        if (orders.Count == 0)
            return new List<OrderExportRow>();

        var orderIds = orders.Select(x => x.StoreOrderId).ToList();

        var productLines = await _db.Set<StoreOrderItem>()
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.StoreOrderId))
            .Select(x => new OrderLineBaseRow
            {
                StoreOrderId = x.StoreOrderId,
                ItemType = "PRODUCT",
                ItemId = x.ProductId,
                ItemName = x.Product.Name,
                Unit = x.Product.Unit,
                RequestedQuantity = x.Quantity
            })
            .ToListAsync(ct);

        var ingredientLines = await _db.Set<StoreOrderIngredientItem>()
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.StoreOrderId))
            .Select(x => new OrderLineBaseRow
            {
                StoreOrderId = x.StoreOrderId,
                ItemType = "INGREDIENT",
                ItemId = x.IngredientId,
                ItemName = x.Ingredient.Name,
                Unit = x.Ingredient.Unit,
                RequestedQuantity = x.Quantity
            })
            .ToListAsync(ct);

        var productSnapshotRaw = await _db.Set<DeliveryProductItem>()
            .AsNoTracking()
            .Where(x => x.Delivery.DeliveryPlan.StoreOrderId.HasValue)
            .Where(x => orderIds.Contains(x.Delivery.DeliveryPlan.StoreOrderId!.Value))
            .Select(x => new DeliverySnapshotRaw
            {
                StoreOrderId = x.Delivery.DeliveryPlan.StoreOrderId!.Value,
                ItemType = "PRODUCT",
                ItemId = x.ProductId,
                RequestedQuantity = x.RequestedQuantity,
                ForwardedQuantity = x.Quantity,
                IsDropped = x.IsDropped,
                DropReason = x.DropReason
            })
            .ToListAsync(ct);

        var ingredientSnapshotRaw = await _db.Set<DeliveryIngredientItem>()
            .AsNoTracking()
            .Where(x => x.Delivery.DeliveryPlan.StoreOrderId.HasValue)
            .Where(x => orderIds.Contains(x.Delivery.DeliveryPlan.StoreOrderId!.Value))
            .Select(x => new DeliverySnapshotRaw
            {
                StoreOrderId = x.Delivery.DeliveryPlan.StoreOrderId!.Value,
                ItemType = "INGREDIENT",
                ItemId = x.IngredientId,
                RequestedQuantity = x.RequestedQuantity,
                ForwardedQuantity = x.Quantity,
                IsDropped = x.IsDropped,
                DropReason = x.DropReason
            })
            .ToListAsync(ct);

        var snapshotMap = productSnapshotRaw
            .Concat(ingredientSnapshotRaw)
            .GroupBy(x => BuildSnapshotKey(x.StoreOrderId, x.ItemType, x.ItemId))
            .ToDictionary(
                g => g.Key,
                g => new DeliverySnapshotAggregate
                {
                    ForwardedQuantity = g.Sum(x => x.ForwardedQuantity),
                    DroppedQuantity = Math.Max(g.Sum(x => x.RequestedQuantity) - g.Sum(x => x.ForwardedQuantity), 0m),
                    HasDrop = g.Any(x => x.IsDropped) || g.Sum(x => x.ForwardedQuantity) < g.Sum(x => x.RequestedQuantity),
                    DropReason = string.Join(" | ",
                        g.Select(x => x.DropReason)
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
                });

        var orderMap = orders.ToDictionary(x => x.StoreOrderId);

        var rows = productLines
            .Concat(ingredientLines)
            .Select(line =>
            {
                var order = orderMap[line.StoreOrderId];
                snapshotMap.TryGetValue(BuildSnapshotKey(line.StoreOrderId, line.ItemType, line.ItemId), out var snap);

                return new OrderExportRow
                {
                    StoreOrderId = order.StoreOrderId,
                    OrderCode = BuildOrderCode(order.StoreOrderId),
                    FranchiseId = order.FranchiseId,
                    FranchiseName = order.FranchiseName,
                    Status = order.Status,
                    OrderDate = order.OrderDate,
                    CreatedAt = order.CreatedAt,
                    SubmittedAt = order.SubmittedAt,
                    LockedAt = order.LockedAt,
                    CancelledAt = order.CancelledAt,
                    CancelReason = order.CancelReason,

                    ItemType = line.ItemType,
                    ItemId = line.ItemId,
                    ItemName = line.ItemName,
                    Unit = line.Unit,
                    RequestedQuantity = line.RequestedQuantity,
                    ForwardedQuantity = snap?.ForwardedQuantity ?? 0m,
                    DroppedQuantity = snap?.DroppedQuantity ?? 0m,
                    HasDrop = snap?.HasDrop ?? false,
                    DropReason = string.IsNullOrWhiteSpace(snap?.DropReason) ? null : snap!.DropReason
                };
            })
            .OrderBy(x => x.OrderDate)
            .ThenBy(x => x.StoreOrderId)
            .ThenBy(x => x.ItemType)
            .ThenBy(x => x.ItemName)
            .ToList();

        return rows;
    }

    /// <summary>Query flattened delivery/receiving rows for either store or kitchen scope.</summary>
    private async Task<List<DeliveryExportRow>> QueryDeliveryRowsAsync(
        bool isFranchiseScope,
        int scopeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        IQueryable<DeliveryProductItem> productQuery = _db.Set<DeliveryProductItem>().AsNoTracking();
        IQueryable<DeliveryIngredientItem> ingredientQuery = _db.Set<DeliveryIngredientItem>().AsNoTracking();

        productQuery = isFranchiseScope
            ? productQuery.Where(x => x.Delivery.DeliveryPlan.FranchiseId == scopeId)
            : productQuery.Where(x => x.Delivery.FromCentralKitchenId == scopeId);

        ingredientQuery = isFranchiseScope
            ? ingredientQuery.Where(x => x.Delivery.DeliveryPlan.FranchiseId == scopeId)
            : ingredientQuery.Where(x => x.Delivery.FromCentralKitchenId == scopeId);

        var productRaw = await productQuery
            .Where(x => x.Delivery.DeliveryPlan.PlannedDate >= fromDate && x.Delivery.DeliveryPlan.PlannedDate <= toDate)
            .Select(x => new DeliveryLineRaw
            {
                DeliveryId = x.DeliveryId,
                StoreOrderId = x.Delivery.DeliveryPlan.StoreOrderId,
                FranchiseId = x.Delivery.DeliveryPlan.FranchiseId,
                FranchiseName = x.Delivery.DeliveryPlan.Franchise.Name,
                PlanDate = x.Delivery.DeliveryPlan.PlannedDate,
                DeliveryStatus = x.Delivery.Status,
                DeliveredAt = x.Delivery.DeliveredAt,
                ConfirmedAt = x.Delivery.ReceivingReports
                    .OrderByDescending(r => r.ReceivedAt)
                    .Select(r => (DateTime?)r.ReceivedAt)
                    .FirstOrDefault(),
                HasReceivingReport = x.Delivery.ReceivingReports.Any(),
                ItemType = "PRODUCT",
                ItemId = x.ProductId,
                ItemName = x.Product.Name,
                Unit = x.Product.Unit,
                RequestedQuantity = x.RequestedQuantity > 0 ? x.RequestedQuantity : x.Quantity,
                DeliveredQuantity = x.Quantity,
                IsDropped = x.IsDropped,
                DropReason = x.DropReason
            })
            .ToListAsync(ct);

        var ingredientRaw = await ingredientQuery
            .Where(x => x.Delivery.DeliveryPlan.PlannedDate >= fromDate && x.Delivery.DeliveryPlan.PlannedDate <= toDate)
            .Select(x => new DeliveryLineRaw
            {
                DeliveryId = x.DeliveryId,
                StoreOrderId = x.Delivery.DeliveryPlan.StoreOrderId,
                FranchiseId = x.Delivery.DeliveryPlan.FranchiseId,
                FranchiseName = x.Delivery.DeliveryPlan.Franchise.Name,
                PlanDate = x.Delivery.DeliveryPlan.PlannedDate,
                DeliveryStatus = x.Delivery.Status,
                DeliveredAt = x.Delivery.DeliveredAt,
                ConfirmedAt = x.Delivery.ReceivingReports
                    .OrderByDescending(r => r.ReceivedAt)
                    .Select(r => (DateTime?)r.ReceivedAt)
                    .FirstOrDefault(),
                HasReceivingReport = x.Delivery.ReceivingReports.Any(),
                ItemType = "INGREDIENT",
                ItemId = x.IngredientId,
                ItemName = x.Ingredient.Name,
                Unit = x.Ingredient.Unit,
                RequestedQuantity = x.RequestedQuantity > 0 ? x.RequestedQuantity : x.Quantity,
                DeliveredQuantity = x.Quantity,
                IsDropped = x.IsDropped,
                DropReason = x.DropReason
            })
            .ToListAsync(ct);

        return productRaw
            .Concat(ingredientRaw)
            .Select(x => new DeliveryExportRow
            {
                DeliveryId = x.DeliveryId,
                DeliveryCode = BuildDeliveryCode(x.DeliveryId),
                StoreOrderId = x.StoreOrderId,
                OrderCode = x.StoreOrderId.HasValue ? BuildOrderCode(x.StoreOrderId.Value) : null,
                FranchiseId = x.FranchiseId,
                FranchiseName = x.FranchiseName,
                PlanDate = x.PlanDate,
                DeliveryStatus = x.DeliveryStatus,
                ReceivingStatus = ResolveReceivingStatus(x.DeliveryStatus, x.HasReceivingReport),
                DeliveredAt = x.DeliveredAt,
                ConfirmedAt = x.ConfirmedAt,
                ItemType = x.ItemType,
                ItemId = x.ItemId,
                ItemName = x.ItemName,
                Unit = x.Unit,
                ExpectedQuantity = x.RequestedQuantity,
                DeliveredQuantity = x.DeliveredQuantity,
                ReceivedQuantity = x.HasReceivingReport || string.Equals(x.DeliveryStatus, DeliveryStatus.Confirmed, StringComparison.OrdinalIgnoreCase)
                    ? x.DeliveredQuantity
                    : null,
                DroppedQuantity = Math.Max(x.RequestedQuantity - x.DeliveredQuantity, 0m),
                HasDrop = x.IsDropped || x.DeliveredQuantity < x.RequestedQuantity,
                DropReason = x.DropReason
            })
            .OrderBy(x => x.PlanDate)
            .ThenBy(x => x.DeliveryId)
            .ThenBy(x => x.ItemType)
            .ThenBy(x => x.ItemName)
            .ToList();
    }

    /// <summary>Query flattened production-plan rows for the selected kitchen month.</summary>
    private async Task<List<ProductionPlanExportRow>> QueryProductionPlanRowsAsync(
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        return await _db.Set<ProductionPlanItem>()
            .AsNoTracking()
            .Where(x =>
                x.ProductionPlan.CentralKitchenId == centralKitchenId &&
                x.ProductionPlan.PlanDate >= fromDate &&
                x.ProductionPlan.PlanDate <= toDate)
            .Select(x => new ProductionPlanExportRow
            {
                ProductionPlanId = x.ProductionPlanId,
                PlanDate = x.ProductionPlan.PlanDate,
                Status = x.ProductionPlan.Status.HasValue
                    ? x.ProductionPlan.Status.Value.ToString()
                    : "UNKNOWN",
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Unit = x.Product.Unit,
                Quantity = x.Quantity,
                CreatedAt = x.ProductionPlan.CreatedAt
            })
            .OrderBy(x => x.PlanDate)
            .ThenBy(x => x.ProductionPlanId)
            .ThenBy(x => x.ProductName)
            .ToListAsync(ct);
    }

    /// <summary>Query flattened production-run rows for the selected kitchen month.</summary>
    private async Task<List<ProductionRunExportRow>> QueryProductionRunRowsAsync(
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        return await _db.Set<ProductionRun>()
            .AsNoTracking()
            .Where(x =>
                x.CentralKitchenId == centralKitchenId &&
                x.ProductionDate >= fromDate &&
                x.ProductionDate <= toDate)
            .Select(x => new ProductionRunExportRow
            {
                ProductionRunId = x.ProductionRunId,
                RunCode = x.RunCode,
                ProductionPlanId = x.ProductionPlanId,
                PlanDate = x.ProductionPlan.PlanDate,
                ProductionDate = x.ProductionDate,
                Quantity = x.Quantity,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            })
            .OrderBy(x => x.ProductionDate)
            .ThenBy(x => x.ProductionRunId)
            .ToListAsync(ct);
    }

    /// <summary>Reconstruct closing batch quantities at month-end from movement history and signed adjustments.</summary>
    private async Task<List<InventoryBatchSnapshotRow>> QueryClosingBatchSnapshotRowsAsync(
        bool isFranchiseScope,
        int scopeId,
        DateTime toUtcExclusive,
        CancellationToken ct)
    {
        var ingredientRows = await QueryIngredientBatchSnapshotRowsAsync(isFranchiseScope, scopeId, toUtcExclusive, ct);
        var productRows = await QueryProductBatchSnapshotRowsAsync(isFranchiseScope, scopeId, toUtcExclusive, ct);

        return ingredientRows
            .Concat(productRows)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.ItemName)
            .ThenBy(x => x.BatchCode)
            .ToList();
    }

    /// <summary>Reconstruct ingredient-batch closing quantities at the selected cutoff.</summary>
    private async Task<List<InventoryBatchSnapshotRow>> QueryIngredientBatchSnapshotRowsAsync(
        bool isFranchiseScope,
        int scopeId,
        DateTime toUtcExclusive,
        CancellationToken ct)
    {
        IQueryable<IngredientBatch> query = _db.IngredientBatches.AsNoTracking();

        query = isFranchiseScope
            ? query.Where(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId == scopeId &&
                x.CentralKitchenId == null)
            : query.Where(x =>
                x.Type == InventoryOwnerType.CentralKitchen &&
                x.CentralKitchenId == scopeId &&
                x.FranchiseId == null);

        var batches = await query
            .Select(x => new IngredientBatchSnapshotBase
            {
                BatchId = x.BatchId,
                ItemId = x.IngredientId,
                ItemName = x.Ingredient.Name,
                Unit = x.Ingredient.Unit,
                BatchCode = x.BatchCode,
                CreatedAt = x.CreatedAt,
                ShelfLifeDays = x.Ingredient.ShelfLifeDays
            })
            .ToListAsync(ct);

        if (batches.Count == 0)
            return new List<InventoryBatchSnapshotRow>();

        var batchIds = batches.Select(x => x.BatchId).ToList();

        var movements = await _db.InventoryMovements
            .AsNoTracking()
            .Where(x => batchIds.Contains(x.BatchId) && x.CreatedAt < toUtcExclusive)
            .Select(x => new MovementRow
            {
                BatchId = x.BatchId,
                Type = x.Type,
                Quantity = x.Quantity,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        var adjustmentRows = await QueryAdjustmentAuditRowsAsync(
            isFranchiseScope,
            scopeId,
            nameof(IngredientBatch),
            toUtcExclusive,
            ct);

        var movementMap = movements
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<MovementRow>)g.ToList());

        var adjustmentMap = adjustmentRows
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AdjustmentAuditRow>)g.ToList());

        return batches
            .Select(x =>
            {
                movementMap.TryGetValue(x.BatchId, out var batchMovements);
                adjustmentMap.TryGetValue(x.BatchId, out var batchAdjustments);

                var closing = Round(CalculateNetQuantity(
                    batchMovements ?? Array.Empty<MovementRow>(),
                    batchAdjustments ?? Array.Empty<AdjustmentAuditRow>(),
                    toUtcExclusive));

                return new InventoryBatchSnapshotRow
                {
                    ItemType = "INGREDIENT",
                    ItemId = x.ItemId,
                    ItemName = x.ItemName,
                    Unit = x.Unit,
                    BatchId = x.BatchId,
                    BatchCode = x.BatchCode,
                    CreatedAt = x.CreatedAt,
                    ExpiredAt = x.ShelfLifeDays > 0
                        ? DateOnly.FromDateTime(x.CreatedAt.AddDays(x.ShelfLifeDays))
                        : null,
                    ClosingQuantity = closing
                };
            })
            .Where(x => x.ClosingQuantity != 0m)
            .ToList();
    }

    /// <summary>Reconstruct product-batch closing quantities at the selected cutoff.</summary>
    private async Task<List<InventoryBatchSnapshotRow>> QueryProductBatchSnapshotRowsAsync(
        bool isFranchiseScope,
        int scopeId,
        DateTime toUtcExclusive,
        CancellationToken ct)
    {
        IQueryable<ProductBatch> query = _db.ProductBatches.AsNoTracking();

        query = isFranchiseScope
            ? query.Where(x => x.FranchiseId == scopeId && x.CentralKitchenId == null)
            : query.Where(x => x.CentralKitchenId == scopeId && x.FranchiseId == null);

        var batches = await query
            .Select(x => new ProductBatchSnapshotBase
            {
                BatchId = x.BatchId,
                ItemId = x.ProductId,
                ItemName = x.Product.Name,
                Unit = x.Product.Unit,
                BatchCode = x.BatchCode,
                CreatedAt = x.CreatedAt,
                ShelfLifeDays = x.Product.ShelfLifeDays
            })
            .ToListAsync(ct);

        if (batches.Count == 0)
            return new List<InventoryBatchSnapshotRow>();

        var batchIds = batches.Select(x => x.BatchId).ToList();

        var movements = await _db.ProductMovements
            .AsNoTracking()
            .Where(x => batchIds.Contains(x.BatchId) && x.CreatedAt < toUtcExclusive)
            .Select(x => new MovementRow
            {
                BatchId = x.BatchId,
                Type = x.Type,
                Quantity = x.Quantity,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        var adjustmentRows = await QueryAdjustmentAuditRowsAsync(
            isFranchiseScope,
            scopeId,
            nameof(ProductBatch),
            toUtcExclusive,
            ct);

        var movementMap = movements
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<MovementRow>)g.ToList());

        var adjustmentMap = adjustmentRows
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AdjustmentAuditRow>)g.ToList());

        return batches
            .Select(x =>
            {
                movementMap.TryGetValue(x.BatchId, out var batchMovements);
                adjustmentMap.TryGetValue(x.BatchId, out var batchAdjustments);

                var closing = Round(CalculateNetQuantity(
                    batchMovements ?? Array.Empty<MovementRow>(),
                    batchAdjustments ?? Array.Empty<AdjustmentAuditRow>(),
                    toUtcExclusive));

                return new InventoryBatchSnapshotRow
                {
                    ItemType = "PRODUCT",
                    ItemId = x.ItemId,
                    ItemName = x.ItemName,
                    Unit = x.Unit,
                    BatchId = x.BatchId,
                    BatchCode = x.BatchCode,
                    CreatedAt = x.CreatedAt,
                    ExpiredAt = x.ShelfLifeDays > 0
                        ? DateOnly.FromDateTime(x.CreatedAt.AddDays(x.ShelfLifeDays))
                        : null,
                    ClosingQuantity = closing
                };
            })
            .Where(x => x.ClosingQuantity != 0m)
            .ToList();
    }

    /// <summary>Read signed inventory-adjustment deltas from audit logs for one batch entity type.</summary>
    private async Task<List<AdjustmentAuditRow>> QueryAdjustmentAuditRowsAsync(
        bool isFranchiseScope,
        int scopeId,
        string entityName,
        DateTime toUtcExclusive,
        CancellationToken ct)
    {
        var actions = entityName == nameof(IngredientBatch)
            ? new[] { "INGREDIENT_ADJUST", "CK_INGREDIENT_ADJUST" }
            : new[] { "FRANCHISE_PRODUCT_ADJUST", "CK_PRODUCT_ADJUST" };

        IQueryable<AuditLog> query = _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.EntityName == entityName)
            .Where(x => x.EntityId.HasValue)
            .Where(x => actions.Contains(x.Action))
            .Where(x => x.CreatedAt < toUtcExclusive);

        query = isFranchiseScope
            ? query.Where(x => x.FranchiseId == scopeId)
            : query.Where(x => x.CentralKitchenId == scopeId);

        var logs = await query
            .Select(x => new
            {
                BatchId = x.EntityId!.Value,
                x.CreatedAt,
                x.NewDataJson
            })
            .ToListAsync(ct);

        return logs
            .Select(x => new AdjustmentAuditRow
            {
                BatchId = x.BatchId,
                CreatedAt = x.CreatedAt,
                DeltaQuantity = TryReadSignedAdjustmentDelta(x.NewDataJson)
            })
            .Where(x => x.DeltaQuantity != 0m)
            .ToList();
    }

    /// <summary>Create the final downloadable payload from the in-memory workbook.</summary>
    private static FileExportPayload BuildFilePayload(XLWorkbook workbook, string fileName)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        return new FileExportPayload
        {
            Content = ms.ToArray(),
            ContentType = ExcelContentType,
            FileName = fileName
        };
    }

    /// <summary>Normalize a month input and compute the corresponding local/UTC boundaries.</summary>
    private static MonthlyDateRange NormalizeMonth(int year, int month, int? timezoneOffsetMinutes)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentException("year must be between 2000 and 2100.");

        if (month is < 1 or > 12)
            throw new ArgumentException("month must be between 1 and 12.");

        var tz = timezoneOffsetMinutes ?? 420;
        if (tz is < -14 * 60 or > 14 * 60)
            throw new ArgumentException("timezoneOffsetMinutes must be between -840 and 840.");

        var fromDate = new DateOnly(year, month, 1);
        var nextMonth = fromDate.AddMonths(1);
        var toDate = nextMonth.AddDays(-1);

        var fromUtc = DateTime.SpecifyKind(
            fromDate.ToDateTime(TimeOnly.MinValue).AddMinutes(-tz),
            DateTimeKind.Utc);

        var toUtcExclusive = DateTime.SpecifyKind(
            nextMonth.ToDateTime(TimeOnly.MinValue).AddMinutes(-tz),
            DateTimeKind.Utc);

        return new MonthlyDateRange
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tz,
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive
        };
    }

    /// <summary>Resolve one receiving status string from delivery lifecycle + receiving existence.</summary>
    private static string ResolveReceivingStatus(string deliveryStatus, bool hasReceivingReport)
    {
        if (hasReceivingReport || string.Equals(deliveryStatus, DeliveryStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.ReceivedByStore;

        if (string.Equals(deliveryStatus, DeliveryStatus.Delivered, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.Delivered;

        if (string.Equals(deliveryStatus, DeliveryStatus.Shipped, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.InTransit;

        if (string.Equals(deliveryStatus, DeliveryStatus.Created, StringComparison.OrdinalIgnoreCase))
            return DeliveryStatus.Created;

        if (string.Equals(deliveryStatus, DeliveryStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.Cancelled;

        return deliveryStatus;
    }

    /// <summary>Convert movement history and signed adjustments into one net quantity.</summary>
    private static decimal CalculateNetQuantity(
        IReadOnlyCollection<MovementRow> movements,
        IReadOnlyCollection<AdjustmentAuditRow> adjustments,
        DateTime beforeUtc)
    {
        var movementNet = movements
            .Where(x => x.CreatedAt < beforeUtc)
            .Sum(x => x.Type switch
            {
                var t when string.Equals(t, MovementType.In, StringComparison.OrdinalIgnoreCase) => x.Quantity,
                var t when string.Equals(t, MovementType.Out, StringComparison.OrdinalIgnoreCase) => -x.Quantity,
                var t when string.Equals(t, MovementType.Waste, StringComparison.OrdinalIgnoreCase) => -x.Quantity,
                _ => 0m
            });

        var adjustmentNet = adjustments
            .Where(x => x.CreatedAt < beforeUtc)
            .Sum(x => x.DeltaQuantity);

        return movementNet + adjustmentNet;
    }

    /// <summary>Parse signed adjustment delta from audit NewDataJson payload.</summary>
    private static decimal TryReadSignedAdjustmentDelta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0m;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Movement", out var movement) &&
                movement.ValueKind == JsonValueKind.Object &&
                movement.TryGetProperty("Delta", out var nestedDelta) &&
                nestedDelta.TryGetDecimal(out var nestedValue))
            {
                return nestedValue;
            }

            if (root.TryGetProperty("DeltaQuantity", out var delta) &&
                delta.TryGetDecimal(out var value))
            {
                return value;
            }
        }
        catch
        {
            return 0m;
        }

        return 0m;
    }

    /// <summary>Write one worksheet title block.</summary>
    private static void WriteTitle(IXLWorksheet ws, ref int row, string title)
    {
        ws.Cell(row, 1).Value = title;
        ws.Row(row).Style.Font.Bold = true;
        ws.Row(row).Style.Font.FontSize = 16;
        row += 2;
    }

    /// <summary>Write one simple table with shared styling.</summary>
    private static void WriteTable(
        IXLWorksheet ws,
        ref int row,
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<object?[]> dataRows)
    {
        ws.Cell(row, 1).Value = title;
        ws.Row(row).Style.Font.Bold = true;
        row++;

        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(row, i + 1).Value = headers[i];
        }

        var headerRange = ws.Range(row, 1, row, headers.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        row++;

        var startDataRow = row;
        var hasData = false;

        foreach (var values in dataRows)
        {
            hasData = true;

            for (var i = 0; i < headers.Count; i++)
            {
                var value = i < values.Length ? values[i] : null;
                SetCellValue(ws.Cell(row, i + 1), value);
            }

            row++;
        }

        if (!hasData)
        {
            ws.Cell(row, 1).Value = "(no data)";
            row++;
        }

        var endRow = Math.Max(row - 1, startDataRow);
        var allRange = ws.Range(startDataRow - 1, 1, endRow, headers.Count);
        allRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        allRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        row += 2;
    }

    /// <summary>Set cell value with predictable export formatting.</summary>
    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                break;
            case DateOnly d:
                cell.Value = d.ToString("yyyy-MM-dd");
                break;
            case DateTime dt:
                cell.Value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                break;
            case decimal dec:
                cell.Value = dec;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case double dbl:
                cell.Value = dbl;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case float fl:
                cell.Value = (double)fl;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case bool b:
                cell.Value = b ? "YES" : "NO";
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    /// <summary>Apply final worksheet cosmetics after data was written.</summary>
    private static void FinalizeSheet(IXLWorksheet ws)
    {
        ws.Columns().AdjustToContents();
        ws.Rows().AdjustToContents();
    }

    /// <summary>Build one stable composite key for order+item snapshot aggregation.</summary>
    private static string BuildSnapshotKey(int storeOrderId, string itemType, int itemId)
        => $"{storeOrderId}:{itemType}:{itemId}";

    private static string BuildOrderCode(int storeOrderId) => $"SO-{storeOrderId:D6}";
    private static string BuildDeliveryCode(int deliveryId) => $"DLV-{deliveryId:D6}";
    private static decimal Round(decimal value) => Math.Round(value, 2);

    private sealed class MonthlyDateRange
    {
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public int TimezoneOffsetMinutes { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime ToUtcExclusive { get; set; }
    }

    private sealed class OrderBaseRow
    {
        public int StoreOrderId { get; set; }
        public int FranchiseId { get; set; }
        public string FranchiseName { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateOnly OrderDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }
    }

    private sealed class OrderLineBaseRow
    {
        public int StoreOrderId { get; set; }
        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public decimal RequestedQuantity { get; set; }
    }

    private sealed class DeliverySnapshotRaw
    {
        public int StoreOrderId { get; set; }
        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private sealed class DeliverySnapshotAggregate
    {
        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool HasDrop { get; set; }
        public string? DropReason { get; set; }
    }

    private sealed class OrderExportRow
    {
        public int StoreOrderId { get; set; }
        public string OrderCode { get; set; } = default!;
        public int FranchiseId { get; set; }
        public string FranchiseName { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateOnly OrderDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }

        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public decimal RequestedQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool HasDrop { get; set; }
        public string? DropReason { get; set; }
    }

    private sealed class DeliveryLineRaw
    {
        public int DeliveryId { get; set; }
        public int? StoreOrderId { get; set; }
        public int FranchiseId { get; set; }
        public string FranchiseName { get; set; } = default!;
        public DateOnly PlanDate { get; set; }
        public string DeliveryStatus { get; set; } = default!;
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public bool HasReceivingReport { get; set; }

        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public decimal RequestedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private sealed class DeliveryExportRow
    {
        public int DeliveryId { get; set; }
        public string DeliveryCode { get; set; } = default!;
        public int? StoreOrderId { get; set; }
        public string? OrderCode { get; set; }
        public int FranchiseId { get; set; }
        public string FranchiseName { get; set; } = default!;
        public DateOnly PlanDate { get; set; }
        public string DeliveryStatus { get; set; } = default!;
        public string ReceivingStatus { get; set; } = default!;
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public decimal ExpectedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal? ReceivedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool HasDrop { get; set; }
        public string? DropReason { get; set; }
    }

    private sealed class IngredientBatchSnapshotBase
    {
        public int BatchId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public string BatchCode { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public int ShelfLifeDays { get; set; }
    }

    private sealed class ProductBatchSnapshotBase
    {
        public int BatchId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public string BatchCode { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public int ShelfLifeDays { get; set; }
    }

    private sealed class InventoryBatchSnapshotRow
    {
        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateOnly? ExpiredAt { get; set; }
        public decimal ClosingQuantity { get; set; }
    }

    private sealed class MovementRow
    {
        public int BatchId { get; set; }
        public string Type { get; set; } = default!;
        public decimal Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class AdjustmentAuditRow
    {
        public int BatchId { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal DeltaQuantity { get; set; }
    }

    private sealed class ProductionPlanExportRow
    {
        public int ProductionPlanId { get; set; }
        public DateOnly PlanDate { get; set; }
        public string Status { get; set; } = default!;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public decimal Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class ProductionRunExportRow
    {
        public int ProductionRunId { get; set; }
        public string RunCode { get; set; } = default!;
        public int ProductionPlanId { get; set; }
        public DateOnly PlanDate { get; set; }
        public DateOnly ProductionDate { get; set; }
        public decimal Quantity { get; set; }
        public string Status { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}