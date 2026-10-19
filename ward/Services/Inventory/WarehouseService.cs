using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Inventory;
using ward.Models.Inventory;

namespace ward.Services.Inventory
{
    public interface IWarehouseService
    {
        Task<List<WarehouseListItemViewModel>> GetAllAsync(bool includeInactive = true);
        Task<WarehouseFormViewModel?> GetByIdAsync(int id);
        Task<(bool Success, string? Error)> CreateAsync(WarehouseFormViewModel model);
        Task<(bool Success, string? Error)> UpdateAsync(WarehouseFormViewModel model);
        Task<(bool Success, string? Error)> ToggleActiveAsync(int id);
    }

    public class WarehouseService : IWarehouseService
    {
        private readonly ApplicationDbContext _context;

        public WarehouseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<WarehouseListItemViewModel>> GetAllAsync(bool includeInactive = true)
        {
            var query = _context.Warehouses
                .Include(w => w.StockItems)
                .AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(w => w.IsActive);
            }

            return await query
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.Name)
                .Select(w => new WarehouseListItemViewModel
                {
                    Id = w.Id,
                    Code = w.Code,
                    Name = w.Name,
                    Location = w.Location,
                    IsDefault = w.IsDefault,
                    IsActive = w.IsActive,
                    DistinctItemCount = w.StockItems.Count(s => s.QuantityOnHand > 0),
                    TotalQuantityOnHand = w.StockItems.Sum(s => s.QuantityOnHand)
                })
                .ToListAsync();
        }

        public async Task<WarehouseFormViewModel?> GetByIdAsync(int id)
        {
            var w = await _context.Warehouses.FindAsync(id);
            if (w == null) return null;

            return new WarehouseFormViewModel
            {
                Id = w.Id,
                Code = w.Code,
                Name = w.Name,
                Location = w.Location,
                IsDefault = w.IsDefault,
                IsActive = w.IsActive
            };
        }

        public async Task<(bool Success, string? Error)> CreateAsync(WarehouseFormViewModel model)
        {
            var trimmedCode = model.Code.Trim().ToUpperInvariant();
            var exists = await _context.Warehouses.AnyAsync(w => w.Code.ToUpper() == trimmedCode);
            if (exists)
            {
                return (false, $"A warehouse with code '{trimmedCode}' already exists.");
            }

            if (model.IsDefault)
            {
                var existingDefaults = await _context.Warehouses.Where(w => w.IsDefault).ToListAsync();
                foreach (var d in existingDefaults)
                {
                    d.IsDefault = false;
                }
            }

            var warehouse = new Warehouse
            {
                Code = trimmedCode,
                Name = model.Name.Trim(),
                Location = model.Location?.Trim(),
                IsDefault = model.IsDefault,
                IsActive = model.IsActive
            };

            _context.Warehouses.Add(warehouse);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(WarehouseFormViewModel model)
        {
            var warehouse = await _context.Warehouses.FindAsync(model.Id);
            if (warehouse == null) return (false, "Warehouse not found.");

            var trimmedCode = model.Code.Trim().ToUpperInvariant();
            var exists = await _context.Warehouses.AnyAsync(w => w.Id != model.Id && w.Code.ToUpper() == trimmedCode);
            if (exists)
            {
                return (false, $"Another warehouse with code '{trimmedCode}' already exists.");
            }

            if (model.IsDefault && !warehouse.IsDefault)
            {
                var existingDefaults = await _context.Warehouses.Where(w => w.IsDefault).ToListAsync();
                foreach (var d in existingDefaults)
                {
                    d.IsDefault = false;
                }
            }

            warehouse.Code = trimmedCode;
            warehouse.Name = model.Name.Trim();
            warehouse.Location = model.Location?.Trim();
            warehouse.IsDefault = model.IsDefault;
            warehouse.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ToggleActiveAsync(int id)
        {
            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse == null) return (false, "Warehouse not found.");

            if (warehouse.IsDefault && warehouse.IsActive)
            {
                return (false, "Cannot deactivate the default warehouse. Set another warehouse as default first.");
            }

            warehouse.IsActive = !warehouse.IsActive;
            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}
