using System.Collections.Generic;
using System.Threading.Tasks;
using ward.Entities.Catalog;
using ward.Entities.Production;
using ward.Models.Production;

namespace ward.Services.Production
{
    public interface IBouquetService
    {
        Task<List<BouquetRecipeListItemViewModel>> GetAllRecipesAsync();
        Task<BouquetRecipeFormViewModel?> GetRecipeByBouquetProductIdAsync(int bouquetProductId);
        Task<BouquetRecipeFormViewModel?> GetRecipeByIdAsync(int recipeId);
        Task<(bool Success, string? ErrorMessage, int RecipeId)> SaveRecipeAsync(BouquetRecipeFormViewModel model, string? userId);
        Task<(bool Success, string? ErrorMessage)> DeleteRecipeAsync(int recipeId);
        Task<RecipeCalculationResultDto?> CalculateRequirementsAsync(int bouquetProductId, decimal bouquetQuantity);
        Task<ComponentAvailabilityResultDto?> ValidateAvailabilityAsync(int bouquetProductId, decimal bouquetQuantity, int warehouseId);
        Task<(bool Success, string? ErrorMessage)> AssembleBouquetsAsync(AssembleBouquetsViewModel request, string? userId);
        Task<List<RecipeSnapshotDto>> GetRecipeSnapshotsAsync(int bouquetProductId);
        Task<List<Product>> GetAvailableBouquetProductsAsync(int? currentBouquetProductId = null);
        Task<List<Product>> GetAvailableComponentProductsAsync();
    }
}
