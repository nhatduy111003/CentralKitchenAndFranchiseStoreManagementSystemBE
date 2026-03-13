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
                .OrderByDescending(u => u.CreatedAt)
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
            var usernameExists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower());

            if (usernameExists)
                throw new InvalidOperationException("Tên đăng nhập đã tồn tại");

            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());

            if (emailExists)
                throw new InvalidOperationException("Email đã tồn tại");

            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleId == dto.RoleId && r.Status == "ACTIVE");

            if (role == null)
                throw new Exception("Vai trò không hợp lệ");

            if (dto.Password.Length < 8)
                throw new Exception("Mật khẩu phải từ 8 ký tự trở lên");

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

            return new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Status = user.Status,
                RoleId = role.RoleId,
                RoleName = role.Name,
                CreatedAt = new DateTimeOffset(user.CreatedAt, TimeSpan.Zero)
            };
        }

        public async Task<bool> UpdateAsync(int userId, UpdateUserRequestDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new Exception("Người dùng không tồn tại");

            var roleExists = await _context.Roles
                .AnyAsync(r => r.RoleId == dto.RoleId && r.Status == "ACTIVE");

            if (!roleExists)
                throw new Exception("Vai trò không hợp lệ");

            var validStatus = new[] { "ACTIVE", "INACTIVE" };

            if (!validStatus.Contains(dto.Status))
                throw new Exception("Trạng thái không hợp lệ");

            user.RoleId = dto.RoleId;
            user.Status = dto.Status;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                throw new Exception("Người dùng không tồn tại");

            _context.Users.Remove(user);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}
