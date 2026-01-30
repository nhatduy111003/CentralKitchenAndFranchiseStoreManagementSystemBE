using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Repositories.Interfaces
{
    public interface IRevokedTokenRepository
    {
        Task<bool> IsRevokedAsync(string jti);
        Task RevokeAsync(string jti, DateTime? expiresAt);
    }

}
