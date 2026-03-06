using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.EntityFrameworkCore;
using System;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserDto>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.Role)
                .Select(u => new UserDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    Status = u.Status,
                    RoleId = u.RoleId,
                    RoleName = u.Role.Name,

                    // entity dùng DateTime, DTO dùng DateTimeOffset
                    CreatedAt = new DateTimeOffset(u.CreatedAt, TimeSpan.Zero)
                })
                .ToListAsync();
        }

        public async Task<UserDto?> GetByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Where(u => u.UserId == userId)
                .Select(u => new UserDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    Status = u.Status,
                    RoleId = u.RoleId,
                    RoleName = u.Role.Name,
                    CreatedAt = new DateTimeOffset(u.CreatedAt, TimeSpan.Zero)
                })
                .FirstOrDefaultAsync();
        }

        public async Task<UserDto> CreateAsync(CreateUserRequestDto dto)
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var now = DateTime.UtcNow;

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = passwordHash,
                RoleId = dto.RoleId,
                Status = "ACTIVE",
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var roleName = await _context.Roles
                .Where(r => r.RoleId == dto.RoleId)
                .Select(r => r.Name)
                .FirstAsync();

            return new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Status = user.Status,
                RoleId = user.RoleId,
                RoleName = roleName,
                CreatedAt = new DateTimeOffset(user.CreatedAt, TimeSpan.Zero)
            };
        }

        public async Task<bool> UpdateAsync(int userId, UpdateUserRequestDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.RoleId = dto.RoleId;
            user.Status = dto.Status;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
