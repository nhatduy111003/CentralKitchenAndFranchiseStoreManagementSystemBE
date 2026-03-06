using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class UserFranchiseService : IUserFranchiseService
    {
        private readonly AppDbContext _context;

        public UserFranchiseService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AssignAsync(int userId, int franchiseId)
        {
            var userExists = await _context.Users
                .AnyAsync(x => x.UserId == userId);

            var franchiseExists = await _context.Franchises
                .AnyAsync(x => x.FranchiseId == franchiseId);

            if (!userExists || !franchiseExists)
                throw new Exception("User or Franchise not found");

            var existingAssignment = await _context.UserFranchises
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (existingAssignment != null)
            {
                // Nếu đã thuộc franchise này → báo lỗi
                if (existingAssignment.FranchiseId == franchiseId)
                    throw new Exception("User already assigned to this franchise");

                // Move sang franchise mới
                existingAssignment.FranchiseId = franchiseId;
                existingAssignment.AssignedAt = DateTime.UtcNow;
            }
            else
            {
                _context.UserFranchises.Add(new UserFranchise
                {
                    UserId = userId,
                    FranchiseId = franchiseId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(int userId, int franchiseId)
        {
            var entity = await _context.UserFranchises
                .FirstOrDefaultAsync(x => x.UserId == userId && x.FranchiseId == franchiseId);

            if (entity == null)
                throw new Exception("Assignment not found");

            _context.UserFranchises.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<int>> GetByUserAsync(int userId)
        {
            return await _context.UserFranchises
                .Where(x => x.UserId == userId)
                .Select(x => x.FranchiseId)
                .ToListAsync();
        }
        public async Task<List<UserInFranchiseDto>> GetUsersByFranchiseAsync(int franchiseId)
        {
            return await _context.UserFranchises
                .Where(x => x.FranchiseId == franchiseId)
                .Include(x => x.User)
                    .ThenInclude(u => u.Role)
                .Select(x => new UserInFranchiseDto
                {
                    UserId = x.UserId,
                    Username = x.User.Username,
                    Email = x.User.Email,
                    RoleName = x.User.Role.Name,
                    AssignedAt = x.AssignedAt
                })
                .ToListAsync();
        }
    }

}
