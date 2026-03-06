namespace CentralKitchenAndFranchise.DAL.Entities;

public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Status { get; set; } = "ACTIVE";

    // NOTE: migration full dùng DateTime (timestamptz)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Role Role { get; set; } = default!;
    public ICollection<UserFranchise> UserFranchises { get; set; } = new List<UserFranchise>();
}
