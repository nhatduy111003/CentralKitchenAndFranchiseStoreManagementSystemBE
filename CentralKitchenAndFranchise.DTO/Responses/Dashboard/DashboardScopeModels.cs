namespace CentralKitchenAndFranchise.DTO.Responses.Dashboard;

public class DashboardCentralKitchenScope
{
    public int CentralKitchenId { get; set; }
    public string CentralKitchenName { get; set; } = default!;
}

public class DashboardFranchiseScope
{
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public int CentralKitchenId { get; set; }
    public string CentralKitchenName { get; set; } = default!;
}