namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class RevokedToken
    {
        public long Id { get; set; }
        public string Jti { get; set; } = default!;
        public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}
