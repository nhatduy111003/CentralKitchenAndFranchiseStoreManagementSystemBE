namespace CentralKitchenAndFranchise.DAL.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;

    public ICollection<User> Users { get; set; } = new List<User>();
}
