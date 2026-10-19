using System.Collections.Generic;
using System.Threading.Tasks;
using ward.Models.Purchasing;

namespace ward.Services.Purchasing
{
    public interface IPurchaseService
    {
        Task<PurchaseInvoiceFilterViewModel> GetInvoicesPagedAsync(PurchaseInvoiceFilterViewModel filter);
        Task<PurchaseInvoiceDetailsViewModel?> GetInvoiceDetailsAsync(int id);
        Task<PurchaseInvoiceCreateViewModel> PrepareCreateInvoiceViewModelAsync();
        Task<(bool Success, string? Error, int InvoiceId)> CreateInvoiceAsync(PurchaseInvoiceCreateViewModel model, string? userId);
        Task<(bool Success, string? Error)> PostInvoiceAsync(int invoiceId, string? userId, string? idempotencyToken = null);
        Task<(bool Success, string? Error)> CancelInvoiceAsync(int invoiceId, string? userId);
        Task<PurchaseReturnCreateViewModel?> PrepareCreateReturnViewModelAsync(int invoiceId);
        Task<(bool Success, string? Error, int ReturnId)> CreateAndProcessReturnAsync(PurchaseReturnCreateViewModel model, string? userId);
        Task<PurchaseReturnDetailsViewModel?> GetReturnDetailsAsync(int id);
        Task<List<PurchaseReturnListItemViewModel>> GetAllReturnsAsync();
        Task<List<SupplierListItemViewModel>> GetAllSuppliersAsync();
        Task<SupplierFormViewModel?> GetSupplierByIdAsync(int id);
        Task<(bool Success, string? Error, int SupplierId)> SaveSupplierAsync(SupplierFormViewModel model);
        Task<(bool Success, string? Error)> RecordSupplierPaymentAsync(SupplierPaymentFormViewModel model, string? userId);
        Task<string> GenerateNextInvoiceNumberAsync();
        Task<string> GenerateNextReturnNumberAsync();
    }
}
