using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses.WorkAssignment;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class UserWorkAssignmentService : IUserWorkAssignmentService
    {
        private readonly AppDbContext _context;

        public UserWorkAssignmentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AssignAsync(AssignUserWorkAssignmentDto dto)
        {
            var userExists = await _context.Users
                .AnyAsync(x => x.UserId == dto.UserId);

            if (!userExists)
                throw new Exception("User not found");

            if (dto.AssignmentType == WorkAssignmentTypes.Franchise)
            {
                if (!dto.FranchiseId.HasValue)
                    throw new Exception("FranchiseId is required for FRANCHISE assignment");

                var franchiseExists = await _context.Franchises
                    .AnyAsync(x => x.FranchiseId == dto.FranchiseId.Value);

                if (!franchiseExists)
                    throw new Exception("Franchise not found");
            }
            else if (dto.AssignmentType == WorkAssignmentTypes.CentralKitchen)
            {
                if (!dto.CentralKitchenId.HasValue)
                    throw new Exception("CentralKitchenId is required for CENTRAL_KITCHEN assignment");

                var centralKitchenExists = await _context.CentralKitchens
                    .AnyAsync(x => x.CentralKitchenId == dto.CentralKitchenId.Value);

                if (!centralKitchenExists)
                    throw new Exception("Central kitchen not found");
            }
            else
            {
                throw new Exception("Invalid assignment type");
            }

            var existingAssignment = await _context.UserWorkAssignments
                .FirstOrDefaultAsync(x => x.UserId == dto.UserId);

            if (existingAssignment != null)
            {
                existingAssignment.AssignmentType = dto.AssignmentType;
                existingAssignment.FranchiseId = dto.AssignmentType == WorkAssignmentTypes.Franchise
                    ? dto.FranchiseId
                    : null;
                existingAssignment.CentralKitchenId = dto.AssignmentType == WorkAssignmentTypes.CentralKitchen
                    ? dto.CentralKitchenId
                    : null;
                existingAssignment.AssignedAt = DateTime.UtcNow;
            }
            else
            {
                _context.UserWorkAssignments.Add(new UserWorkAssignment
                {
                    UserId = dto.UserId,
                    AssignmentType = dto.AssignmentType,
                    FranchiseId = dto.AssignmentType == WorkAssignmentTypes.Franchise
                        ? dto.FranchiseId
                        : null,
                    CentralKitchenId = dto.AssignmentType == WorkAssignmentTypes.CentralKitchen
                        ? dto.CentralKitchenId
                        : null,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(int userId)
        {
            var entity = await _context.UserWorkAssignments
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (entity == null)
                throw new Exception("Assignment not found");

            _context.UserWorkAssignments.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<UserWorkAssignmentResponse?> GetByUserAsync(int userId)
        {
            return await _context.UserWorkAssignments
                .Where(x => x.UserId == userId)
                .Select(x => new UserWorkAssignmentResponse
                {
                    UserId = x.UserId,
                    AssignmentType = x.AssignmentType,
                    FranchiseId = x.FranchiseId,
                    CentralKitchenId = x.CentralKitchenId,
                    AssignedAt = x.AssignedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<UserInWorkAssignmentDto>> GetUsersByAssignmentAsync(
            string assignmentType,
            int? franchiseId,
            int? centralKitchenId)
        {
            var query = _context.UserWorkAssignments
                .Include(x => x.User)
                    .ThenInclude(u => u.Role)
                .AsQueryable();

            if (assignmentType == WorkAssignmentTypes.Franchise)
            {
                if (!franchiseId.HasValue)
                    throw new Exception("FranchiseId is required for FRANCHISE assignment");

                query = query.Where(x =>
                    x.AssignmentType == WorkAssignmentTypes.Franchise &&
                    x.FranchiseId == franchiseId.Value);
            }
            else if (assignmentType == WorkAssignmentTypes.CentralKitchen)
            {
                if (!centralKitchenId.HasValue)
                    throw new Exception("CentralKitchenId is required for CENTRAL_KITCHEN assignment");

                query = query.Where(x =>
                    x.AssignmentType == WorkAssignmentTypes.CentralKitchen &&
                    x.CentralKitchenId == centralKitchenId.Value);
            }
            else
            {
                throw new Exception("Invalid assignment type");
            }

            return await query
                .Select(x => new UserInWorkAssignmentDto
                {
                    UserId = x.UserId,
                    Username = x.User.Username,
                    Email = x.User.Email,
                    RoleName = x.User.Role.Name,
                    AssignmentType = x.AssignmentType,
                    FranchiseId = x.FranchiseId,
                    CentralKitchenId = x.CentralKitchenId,
                    AssignedAt = x.AssignedAt
                })
                .ToListAsync();
        }
    }
}