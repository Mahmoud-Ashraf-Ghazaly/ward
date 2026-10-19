using System.Collections.Generic;
using System.Threading.Tasks;
using ward.Models.Sales;

namespace ward.Services.Sales
{
    public interface ISalesService
    {
        Task<List<PosProductDto>> GetPosProductsAsync(int warehouseId, string? searchTerm = null, int? categoryId = null);
        Task<PosProductDto?> FindProductForPosAsync(string barcodeOrSku, int warehouseId);
        Task<PosCheckoutResultDto> ProcessCheckoutAsync(PosCheckoutRequestDto request, string? userId);
        Task<SalesInvoiceFilterViewModel> GetSalesInvoicesPagedAsync(SalesInvoiceFilterViewModel filter);
        Task<SalesInvoiceDetailsViewModel?> GetSalesInvoiceDetailsAsync(int id);
        Task<string> GenerateNextSalesInvoiceNumberAsync();
        Task<List<CustomerListItemViewModel>> GetAllCustomersAsync();
        Task<CustomerFormViewModel?> GetCustomerByIdAsync(int id);
        Task<(bool Success, string? Error, int CustomerId)> SaveCustomerAsync(CustomerFormViewModel model);

        // Sales Returns
        Task<SalesReturnCreateViewModel?> PrepareCreateReturnViewModelAsync(int invoiceId);
        Task<(bool Success, string? Error, int ReturnId)> ProcessSalesReturnAsync(SalesReturnCreateViewModel model, string? userId);
        Task<SalesReturnDetailsViewModel?> GetSalesReturnDetailsAsync(int id);
        Task<List<SalesReturnListItemViewModel>> GetAllSalesReturnsAsync();
        Task<string> GenerateNextSalesReturnNumberAsync();
    }
}
