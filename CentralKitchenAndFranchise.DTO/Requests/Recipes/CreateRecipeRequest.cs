using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Recipes;

public class CreateRecipeRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    public string? Instructions { get; set; }
}