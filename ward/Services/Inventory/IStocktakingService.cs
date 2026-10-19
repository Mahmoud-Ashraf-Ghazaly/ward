using System.Collections.Generic;
using System.Threading.Tasks;
using ward.Models.Inventory;

namespace ward.Services.Inventory
{
    public interface IStocktakingService
    {
        Task<List<StocktakingListItemViewModel>> GetAllSessionsAsync(int? warehouseId = null);
        Task<StocktakingDetailsViewModel?> GetSessionDetailsAsync(int id);
        Task<StocktakingCountViewModel?> GetSessionForCountingAsync(int id);
        Task<StocktakingReviewViewModel?> GetSessionForReviewAsync(int id);
        Task<(bool Success, string? Error, int SessionId)> CreateSessionAsync(StocktakingCreateViewModel model, string? userId);
        Task<(bool Success, string? Error)> SaveCountsAsync(StocktakingCountViewModel model, string? userId);
        Task<(bool Success, string? Error)> ApproveAndPostAdjustmentsAsync(int id, string? approvalNotes, string? idempotencyToken, string? userId);
        Task<(bool Success, string? Error)> CancelSessionAsync(int id, string? userId);
        Task<string> GenerateNextStocktakingNumberAsync();
    }
}
