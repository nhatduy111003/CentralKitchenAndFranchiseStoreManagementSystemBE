using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Repositories.Implementations
{
    public class RevokedTokenRepository : IRevokedTokenRepository
    {
        private readonly AppDbContext _db;
        public RevokedTokenRepository(AppDbContext db) => _db = db;

        public Task<bool> IsRevokedAsync(string jti)
            => _db.RevokedTokens.AnyAsync(x => x.Jti == jti);

        public async Task RevokeAsync(string jti, DateTime? expiresAt)
        {
            if (await IsRevokedAsync(jti)) return;

            _db.RevokedTokens.Add(new RevokedToken
            {
                Jti = jti,
                RevokedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            });

            await _db.SaveChangesAsync();
        }
    }

}
