using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IUserFranchiseService
    {
        Task AssignAsync(int userId, int franchiseId);
        Task RemoveAsync(int userId, int franchiseId);
        Task<List<int>> GetByUserAsync(int userId);
    }

}
