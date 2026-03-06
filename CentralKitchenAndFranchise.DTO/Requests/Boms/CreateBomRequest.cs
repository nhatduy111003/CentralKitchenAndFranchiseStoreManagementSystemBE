using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Boms;

public class CreateBomRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    public List<CreateBomItemRequest> Items { get; set; } = new();
}

public class CreateBomItemRequest
{
    [Range(1, int.MaxValue)]
    public int IngredientId { get; set; }

    [Range(typeof(decimal), "1", "999999999")]
    public decimal Quantity { get; set; }
}