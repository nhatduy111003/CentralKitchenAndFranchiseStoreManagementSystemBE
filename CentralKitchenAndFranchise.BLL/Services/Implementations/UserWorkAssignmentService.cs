using CentralKitchenAndFranchise.BLL.Exceptions;
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
        private readonly ICurrentUserService _current;

        public UserWorkAssignmentService(AppDbContext context, ICurrentUserService current)
        {
            _context = context;
            _current = current;
        }

        public async Task AssignAsync(AssignUserWorkAssignmentDto dto)
        {
            RequireAdminOrManager();
            ArgumentNullException.ThrowIfNull(dto);

            var assignmentType = NormalizeAssignmentType(dto.AssignmentType);
            var roleName = await GetUserRoleNameAsync(dto.UserId);

            ValidateRoleAssignmentRule(roleName, assignmentType);
            await ValidateAssignmentTargetAsync(assignmentType, dto.FranchiseId, dto.CentralKitchenId);

            var now = DateTime.UtcNow;
            var existingAssignments = await _context.UserWorkAssignments
                .Where(x => x.UserId == dto.UserId)
                .OrderByDescending(x => x.AssignedAt)
                .ThenByDescending(x => x.UserWorkAssignmentId)
                .ToListAsync();

            var existingAssignment = existingAssignments.FirstOrDefault();

            if (existingAssignment != null)
            {
                existingAssignment.AssignmentType = assignmentType;
                existingAssignment.FranchiseId = assignmentType == WorkAssignmentTypes.Franchise
                    ? dto.FranchiseId
                    : null;
                existingAssignment.CentralKitchenId = assignmentType == WorkAssignmentTypes.CentralKitchen
                    ? dto.CentralKitchenId
                    : null;
                existingAssignment.AssignedAt = now;

                if (existingAssignments.Count > 1)
                {
                    _context.UserWorkAssignments.RemoveRange(existingAssignments.Skip(1));
                }
            }
            else
            {
                await _context.UserWorkAssignments.AddAsync(new UserWorkAssignment
                {
                    UserId = dto.UserId,
                    AssignmentType = assignmentType,
                    FranchiseId = assignmentType == WorkAssignmentTypes.Franchise
                        ? dto.FranchiseId
                        : null,
                    CentralKitchenId = assignmentType == WorkAssignmentTypes.CentralKitchen
                        ? dto.CentralKitchenId
                        : null,
                    AssignedAt = now
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(int userId)
        {
            RequireAdminOrManager();

            var entities = await _context.UserWorkAssignments
                .Where(x => x.UserId == userId)
                .ToListAsync();

            if (entities.Count == 0)
                throw new InvalidOperationException("Assignment not found.");

            _context.UserWorkAssignments.RemoveRange(entities);
            await _context.SaveChangesAsync();
        }

        public async Task<UserWorkAssignmentResponse?> GetByUserAsync(int userId)
        {
            RequireAdminOrManager();

            return await _context.UserWorkAssignments
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.AssignedAt)
                .ThenByDescending(x => x.UserWorkAssignmentId)
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
            RequireAdminOrManager();

            assignmentType = NormalizeAssignmentType(assignmentType);

            var query = _context.UserWorkAssignments
                .AsNoTracking()
                .Include(x => x.User)
                    .ThenInclude(u => u.Role)
                .AsQueryable();

            if (assignmentType == WorkAssignmentTypes.Franchise)
            {
                if (!franchiseId.HasValue)
                    throw new ArgumentException("FranchiseId is required for FRANCHISE assignment.");

                query = query.Where(x =>
                    x.AssignmentType == WorkAssignmentTypes.Franchise &&
                    x.FranchiseId == franchiseId.Value);
            }
            else
            {
                if (!centralKitchenId.HasValue)
                    throw new ArgumentException("CentralKitchenId is required for CENTRAL_KITCHEN assignment.");

                query = query.Where(x =>
                    x.AssignmentType == WorkAssignmentTypes.CentralKitchen &&
                    x.CentralKitchenId == centralKitchenId.Value);
            }

            var rows = await query
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
                .OrderByDescending(x => x.AssignedAt)
                .ThenBy(x => x.UserId)
                .ToListAsync();

            return rows
                .GroupBy(x => x.UserId)
                .Select(g => g.First())
                .ToList();
        }

        private async Task<string> GetUserRoleNameAsync(int userId)
        {
            var roleName = await _context.Users
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.Role.Name)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(roleName))
                throw new InvalidOperationException("User not found.");

            return roleName;
        }

        private async Task ValidateAssignmentTargetAsync(
            string assignmentType,
            int? franchiseId,
            int? centralKitchenId)
        {
            if (assignmentType == WorkAssignmentTypes.Franchise)
            {
                if (!franchiseId.HasValue)
                    throw new ArgumentException("FranchiseId is required for FRANCHISE assignment.");

                var franchiseExists = await _context.Franchises
                    .AsNoTracking()
                    .AnyAsync(x => x.FranchiseId == franchiseId.Value);

                if (!franchiseExists)
                    throw new InvalidOperationException("Franchise not found.");

                return;
            }

            if (!centralKitchenId.HasValue)
                throw new ArgumentException("CentralKitchenId is required for CENTRAL_KITCHEN assignment.");

            var centralKitchenExists = await _context.CentralKitchens
                .AsNoTracking()
                .AnyAsync(x => x.CentralKitchenId == centralKitchenId.Value);

            if (!centralKitchenExists)
                throw new InvalidOperationException("Central kitchen not found.");
        }

        private static string NormalizeAssignmentType(string assignmentType)
        {
            if (string.IsNullOrWhiteSpace(assignmentType))
                throw new ArgumentException("AssignmentType is required.");

            var normalized = assignmentType.Trim().ToUpperInvariant();

            return normalized switch
            {
                WorkAssignmentTypes.Franchise => WorkAssignmentTypes.Franchise,
                WorkAssignmentTypes.CentralKitchen => WorkAssignmentTypes.CentralKitchen,
                _ => throw new ArgumentException("AssignmentType must be FRANCHISE or CENTRAL_KITCHEN.")
            };
        }

        private static void ValidateRoleAssignmentRule(string roleName, string assignmentType)
        {
            if (string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(roleName, RoleNames.Manager, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{roleName} is a global role and cannot have a work assignment.");
            }

            if (string.Equals(roleName, RoleNames.StoreStaff, StringComparison.OrdinalIgnoreCase))
            {
                if (assignmentType != WorkAssignmentTypes.Franchise)
                    throw new InvalidOperationException("StoreStaff must be assigned to a FRANCHISE.");

                return;
            }

            if (string.Equals(roleName, RoleNames.KitchenStaff, StringComparison.OrdinalIgnoreCase))
            {
                if (assignmentType != WorkAssignmentTypes.CentralKitchen)
                    throw new InvalidOperationException("KitchenStaff must be assigned to a CENTRAL_KITCHEN.");

                return;
            }

            if (string.Equals(roleName, RoleNames.SupplyCoordinator, StringComparison.OrdinalIgnoreCase))
            {
                if (assignmentType != WorkAssignmentTypes.CentralKitchen)
                    throw new InvalidOperationException("SupplyCoordinator must be assigned to a CENTRAL_KITCHEN.");

                return;
            }

            throw new InvalidOperationException($"Unsupported role for work assignment: {roleName}.");
        }

        private void RequireAdminOrManager()
        {
            if (!_current.IsInRole(RoleNames.Admin) && !_current.IsInRole(RoleNames.Manager))
                throw new ForbiddenAccessException("Admin/Manager role required.");
        }
    }
}
