using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.UnitOfWork;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses.Ingredients;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class IngredientService : Interfaces.IIngredientService
{
    private readonly IUnitOfWork _uow;

    public IngredientService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<IngredientResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Ingredients.GetAllAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<IngredientResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Ingredients.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Ingredient {id} not found.");
        return ToDto(entity);
    }

    public async Task<IngredientResponse> CreateAsync(CreateIngredientRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit)) throw new ArgumentException("Unit is required.");

        var entity = new Ingredient
        {
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Ingredients.AddAsync(entity, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Ingredient name already exists.");
        }

        return ToDto(entity);
    }

    public async Task<IngredientResponse> UpdateAsync(int id, UpdateIngredientRequest request, CancellationToken ct = default)
    {
        var entity = await _uow.Ingredients.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Ingredient {id} not found.");

        entity.Name = request.Name.Trim();
        entity.Unit = request.Unit.Trim();
        entity.Status = string.IsNullOrWhiteSpace(request.Status) ? entity.Status : request.Status.Trim();

        _uow.Ingredients.Update(entity);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Ingredient name already exists.");
        }

        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Ingredients.GetByIdAsync(id, ct);
        if (entity is null) return;

        _uow.Ingredients.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private static IngredientResponse ToDto(Ingredient x) => new()
    {
        Id = x.IngredientId,
        Name = x.Name,
        Unit = x.Unit,
        Status = x.Status,

        // entity DateTime -> DTO DateTimeOffset
        CreatedAt = new DateTimeOffset(x.CreatedAt, TimeSpan.Zero)
    };
}
