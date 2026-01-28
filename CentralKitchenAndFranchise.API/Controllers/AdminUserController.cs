//using CentralKitchenAndFranchise.DAL.Entities;
//using CentralKitchenAndFranchise.DTO.Requests;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

//namespace CentralKitchenAndFranchise.API.Controllers
//{
//    [ApiController]
//    [Route("api/admin/users")]
//    public class AdminUserController : ControllerBase
//    {
//        private readonly AppDbContext _context;

//        public AdminUserController(AppDbContext context)
//        {
//            _context = context;
//        }

//        // =========================
//        // USER CRUD
//        // =========================

//        [HttpGet]
//        public async Task<IActionResult> GetAll()
//        {
//            var users = await _context.Users
//                .Include(u => u.Roles)
//                .Include(u => u.OrganizationalUnits)
//                .ToListAsync();

//            return Ok(users);
//        }

//        [HttpGet("{id}")]
//        public async Task<IActionResult> GetById(Guid id)
//        {
//            var user = await _context.Users
//                .Include(u => u.Roles)
//                .Include(u => u.OrganizationalUnits)
//                .FirstOrDefaultAsync(u => u.UserId == id);

//            if (user == null) return NotFound();
//            return Ok(user);
//        }

//        [HttpPost]
//        public async Task<IActionResult> Create(CreateUserDto dto)
//        {
//            var user = new User
//            {
//                UserId = Guid.NewGuid(),
//                Status = dto.Status
//            };

//            _context.Users.Add(user);
//            await _context.SaveChangesAsync();

//            return Ok(user);
//        }

//        [HttpPut("{id}")]
//        public async Task<IActionResult> Update(Guid id, UpdateUserDto dto)
//        {
//            var user = await _context.Users.FindAsync(id);
//            if (user == null) return NotFound();

//            user.Status = dto.Status;
//            await _context.SaveChangesAsync();

//            return NoContent();
//        }

//        [HttpDelete("{id}")]
//        public async Task<IActionResult> Delete(Guid id)
//        {
//            var user = await _context.Users.FindAsync(id);
//            if (user == null) return NotFound();

//            _context.Users.Remove(user);
//            await _context.SaveChangesAsync();

//            return NoContent();
//        }

//        // =========================
//        // ROLE ASSIGNMENT
//        // =========================

//        [HttpPost("{id}/roles")]
//        public async Task<IActionResult> AssignRole(Guid id, AssignRoleRequest request)
//        {
//            var exists = await _context.UserRoleAssignments
//                .AnyAsync(x => x.UserId == id && x.RoleName == request.RoleName);

//            if (exists) return BadRequest("Role already assigned");

//            var assignment = new UserRoleAssignment
//            {
//                UserRoleAssignmentId = Guid.NewGuid(),
//                UserId = id,
//                RoleName = request.RoleName
//            };

//            _context.UserRoleAssignments.Add(assignment);
//            await _context.SaveChangesAsync();

//            return Ok();
//        }

//        [HttpDelete("{id}/roles/{roleName}")]
//        public async Task<IActionResult> RemoveRole(Guid id, string roleName)
//        {
//            var role = await _context.UserRoleAssignments
//                .FirstOrDefaultAsync(x => x.UserId == id && x.RoleName == roleName);

//            if (role == null) return NotFound();

//            _context.UserRoleAssignments.Remove(role);
//            await _context.SaveChangesAsync();

//            return NoContent();
//        }

//        // =========================
//        // ORGANIZATIONAL UNIT
//        // =========================

//        [HttpPost("{id}/ous")]
//        public async Task<IActionResult> AssignOu(Guid id, AssignOuRequest request)
//        {
//            var exists = await _context.UserOuAssignments
//                .AnyAsync(x => x.UserId == id && x.OrganizationalUnitId == request.OrganizationalUnitId);

//            if (exists) return BadRequest("OU already assigned");

//            var assignment = new UserFranchise
//            {
//                UserOuAssignmentId = Guid.NewGuid(),
//                UserId = id,
//                OrganizationalUnitId = request.OrganizationalUnitId
//            };

//            _context.UserOuAssignments.Add(assignment);
//            await _context.SaveChangesAsync();

//            return Ok();
//        }

//        // =========================
//        // AUDIT LOG
//        // =========================

//        [HttpGet("{id}/audit-logs")]
//        public async Task<IActionResult> GetAuditLogs(Guid id)
//        {
//            var logs = await _context.AuditLogs
//                .Where(x => x.UserId == id)
//                .OrderByDescending(x => x.CreatedAt)
//                .ToListAsync();

//            return Ok(logs);
//        }
//    }

//}
