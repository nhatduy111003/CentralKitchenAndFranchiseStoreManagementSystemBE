using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class FranchiseService : IFranchiseService
    {
        private readonly AppDbContext _context;

        public FranchiseService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<FranchiseDto>> GetAllAsync()
        {
            return await _context.Franchises
                .Select(f => new FranchiseDto
                {
                    FranchiseId = f.FranchiseId,
                    Name = f.Name,
                    Type = f.Type,
                    Status = f.Status,
                    Address = f.Address,
                    Location = f.Location
                })
                .ToListAsync();
        }

        public async Task<FranchiseDto?> GetByIdAsync(int id)
        {
            var f = await _context.Franchises.FindAsync(id);
            if (f == null) return null;

            return new FranchiseDto
            {
                FranchiseId = f.FranchiseId,
                Name = f.Name,
                Type = f.Type,
                Status = f.Status,
                Address = f.Address,
                Location = f.Location
            };
        }

        public async Task<int> CreateAsync(FranchiseCreateDto dto)
        {
            var franchise = new Franchise
            {
                Name = dto.Name,
                Type = dto.Type,
                Status = dto.Status,
                Address = dto.Address,
                Location = dto.Location
            };

            _context.Franchises.Add(franchise);
            await _context.SaveChangesAsync();

            return franchise.FranchiseId;
        }

        public async Task<bool> UpdateAsync(int id, FranchiseCreateDto dto)
        {
            var franchise = await _context.Franchises.FindAsync(id);
            if (franchise == null) return false;

            franchise.Name = dto.Name;
            franchise.Type = dto.Type;
            franchise.Status = dto.Status;
            franchise.Address = dto.Address;
            franchise.Location = dto.Location;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var franchise = await _context.Franchises.FindAsync(id);
            if (franchise == null) return false;

            _context.Franchises.Remove(franchise);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
