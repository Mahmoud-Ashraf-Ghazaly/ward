using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Models.Catalog;

namespace ward.Services.Catalog
{
    public interface ICategoryService
    {
        Task<List<CategoryListItemViewModel>> GetAllAsync(bool includeInactive = true);
        Task<CategoryFormViewModel?> GetByIdAsync(int id);
        Task<(bool Success, string? Error)> CreateAsync(CategoryFormViewModel model);
        Task<(bool Success, string? Error)> UpdateAsync(CategoryFormViewModel model);
        Task<(bool Success, string? Error)> DeleteAsync(int id);
    }

    public class CategoryService : ICategoryService
    {
        private readonly ApplicationDbContext _context;

        public CategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CategoryListItemViewModel>> GetAllAsync(bool includeInactive = true)
        {
            var query = _context.Categories.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            return await query
                .OrderBy(c => c.Name)
                .Select(c => new CategoryListItemViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ProductCount = c.Products.Count()
                })
                .ToListAsync();
        }

        public async Task<CategoryFormViewModel?> GetByIdAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return null;

            return new CategoryFormViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                IsActive = category.IsActive
            };
        }

        public async Task<(bool Success, string? Error)> CreateAsync(CategoryFormViewModel model)
        {
            var trimmedName = model.Name.Trim();
            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower());
            if (exists)
            {
                return (false, $"A category named '{trimmedName}' already exists.");
            }

            var category = new Category
            {
                Name = trimmedName,
                Description = model.Description?.Trim(),
                IsActive = model.IsActive
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(CategoryFormViewModel model)
        {
            var category = await _context.Categories.FindAsync(model.Id);
            if (category == null)
            {
                return (false, "Category not found.");
            }

            var trimmedName = model.Name.Trim();
            var exists = await _context.Categories.AnyAsync(c => c.Id != model.Id && c.Name.ToLower() == trimmedName.ToLower());
            if (exists)
            {
                return (false, $"Another category named '{trimmedName}' already exists.");
            }

            category.Name = trimmedName;
            category.Description = model.Description?.Trim();
            category.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return (false, "Category not found.");
            }

            if (category.Products.Any())
            {
                return (false, $"Cannot delete category '{category.Name}' because {category.Products.Count} product(s) are assigned to it. Deactivate it instead.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}
