using System;
using System.Threading.Tasks;
using ward.Entities.Enums;
using ward.Models.Reports;

namespace ward.Services.Reports
{
    public interface IReportingService
    {
        Task<SalesReportViewModel> GetSalesReportAsync(DateTime? fromDate, DateTime? toDate, string grouping = "Daily");
        Task<PurchasesReturnsReportViewModel> GetPurchasesReturnsReportAsync(DateTime? fromDate, DateTime? toDate, int? supplierId, int? warehouseId);
        Task<ProductCategorySalesReportViewModel> GetProductCategorySalesReportAsync(DateTime? fromDate, DateTime? toDate, int? categoryId);
        Task<WarehouseStockReportViewModel> GetWarehouseStockReportAsync(int? warehouseId, int? categoryId, string? search);
        Task<StockMovementsReportViewModel> GetStockMovementsReportAsync(DateTime? fromDate, DateTime? toDate, int? warehouseId, int? productId, MovementType? movementType);
        Task<LowStockReportViewModel> GetLowStockReportAsync(int? warehouseId);
        Task<StocktakingVariancesReportViewModel> GetStocktakingVariancesReportAsync(DateTime? fromDate, DateTime? toDate, int? warehouseId);
        Task<BouquetConsumptionReportViewModel> GetBouquetConsumptionReportAsync(DateTime? fromDate, DateTime? toDate, int? bouquetProductId);
        Task<CustomerSupplierBalancesReportViewModel> GetCustomerSupplierBalancesReportAsync();
    }
}
