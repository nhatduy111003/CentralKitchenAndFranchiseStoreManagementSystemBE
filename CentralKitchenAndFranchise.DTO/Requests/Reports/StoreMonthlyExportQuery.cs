namespace CentralKitchenAndFranchise.DTO.Requests.Reports;

public class StoreMonthlyExportQuery
{
    // Admin/Manager phải truyền franchiseId; StoreStaff có thể bỏ trống để lấy scope assigned.
    public int? FranchiseId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    // Vietnam business timezone mặc định.
    public int? TimezoneOffsetMinutes { get; set; } = 420;
}