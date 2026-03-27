namespace CentralKitchenAndFranchise.DTO.Requests.Reports;

public class KitchenMonthlyExportQuery
{
    // Admin/Manager phải truyền centralKitchenId; KitchenStaff/SupplyCoordinator có thể bỏ trống để lấy scope assigned.
    public int? CentralKitchenId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    // Vietnam business timezone mặc định.
    public int? TimezoneOffsetMinutes { get; set; } = 420;
}