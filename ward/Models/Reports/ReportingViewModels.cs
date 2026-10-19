using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ward.Entities.Enums;

namespace ward.Models.Reports
{
    // 1. Sales & Gross Profit Report View Models
    public class SalesPeriodRowDto
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public int InvoicesCount { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal NetRevenue => Math.Max(0, GrandTotal - RefundedAmount);
        public decimal TotalCost { get; set; }
        public decimal GrossProfit => NetRevenue - TotalCost;
        public decimal GrossProfitMarginPercentage => NetRevenue > 0 ? Math.Round((GrossProfit / NetRevenue) * 100, 1) : 0;
    }

    public class SalesReportViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public string GroupingPeriod { get; set; } = "Daily"; // Daily or Monthly

        public int TotalInvoicesCount { get; set; }
        public decimal TotalGrossSales { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalGrandTotal { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalRefunds { get; set; }
        public decimal NetSales => Math.Max(0, TotalGrandTotal - TotalRefunds);
        public decimal TotalCostOfGoodsSold { get; set; }
        public decimal TotalGrossProfit => NetSales - TotalCostOfGoodsSold;
        public decimal OverallGrossProfitMarginPercentage => NetSales > 0 ? Math.Round((TotalGrossProfit / NetSales) * 100, 1) : 0;

        public List<SalesPeriodRowDto> Rows { get; set; } = new List<SalesPeriodRowDto>();
    }

    // 2. Purchases and Returns Report
    public class PurchaseReportRowDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDateUtc { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceDue => GrandTotal - PaidAmount;
        public PurchaseInvoiceStatus Status { get; set; }
    }

    public class PurchaseReturnReportRowDto
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public DateTime ReturnDateUtc { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string OriginalInvoiceNumber { get; set; } = string.Empty;
        public decimal TotalRefundAmount { get; set; }
        public PurchaseReturnStatus Status { get; set; }
    }

    public class PurchasesReturnsReportViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int? SupplierId { get; set; }
        public int? WarehouseId { get; set; }

        public int TotalPurchaseInvoices { get; set; }
        public decimal TotalPurchasesAmount { get; set; }
        public decimal TotalPaidToSuppliers { get; set; }
        public decimal TotalPendingSupplierBalance { get; set; }
        public int TotalPurchaseReturnsCount { get; set; }
        public decimal TotalPurchaseReturnAmount { get; set; }
        public decimal NetPurchasesAmount => TotalPurchasesAmount - TotalPurchaseReturnAmount;

        public List<PurchaseReportRowDto> Invoices { get; set; } = new List<PurchaseReportRowDto>();
        public List<PurchaseReturnReportRowDto> Returns { get; set; } = new List<PurchaseReturnReportRowDto>();
    }

    // 3. Product & Category Sales Report
    public class ProductSalesSummaryDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = "pcs";
        public decimal QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit => TotalRevenue - TotalCost;
        public decimal GrossProfitMarginPercentage => TotalRevenue > 0 ? Math.Round((GrossProfit / TotalRevenue) * 100, 1) : 0;
    }

    public class CategorySalesSummaryDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int ItemsCount { get; set; }
        public decimal TotalQuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit => TotalRevenue - TotalCost;
        public decimal GrossProfitMarginPercentage => TotalRevenue > 0 ? Math.Round((GrossProfit / TotalRevenue) * 100, 1) : 0;
    }

    public class ProductCategorySalesReportViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int? CategoryId { get; set; }

        public decimal TotalItemsSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCostOfGoodsSold { get; set; }
        public decimal TotalGrossProfit => TotalRevenue - TotalCostOfGoodsSold;
        public decimal OverallGrossProfitMarginPercentage => TotalRevenue > 0 ? Math.Round((TotalGrossProfit / TotalRevenue) * 100, 1) : 0;

        public List<CategorySalesSummaryDto> CategoryBreakdown { get; set; } = new List<CategorySalesSummaryDto>();
        public List<ProductSalesSummaryDto> ProductBreakdown { get; set; } = new List<ProductSalesSummaryDto>();
    }

    // 4. Warehouse Stock Balances Report
    public class WarehouseStockRowDto
    {
        public string WarehouseName { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = "pcs";
        public decimal QuantityOnHand { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValuation => Math.Round(QuantityOnHand * UnitCost, 2);
        public decimal MinimumStockLevel { get; set; }
        public string StockStatus => QuantityOnHand <= 0 ? "Out of Stock" : (QuantityOnHand <= MinimumStockLevel ? "Low Stock" : "Optimal");
    }

    public class WarehouseStockReportViewModel
    {
        public int? WarehouseId { get; set; }
        public int? CategoryId { get; set; }
        public string? SearchQuery { get; set; }

        public int TotalProductsTracked { get; set; }
        public decimal TotalUnitsInStock { get; set; }
        public decimal TotalInventoryValuation { get; set; }

        public List<WarehouseStockRowDto> Rows { get; set; } = new List<WarehouseStockRowDto>();
    }

    // 5. Stock Movement History Report
    public class StockMovementRowDto
    {
        public long Id { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public MovementType MovementType { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost => Math.Round(Math.Abs(Quantity) * UnitCost, 2);
        public string ReferenceType { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserId { get; set; }
    }

    public class StockMovementsReportViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int? WarehouseId { get; set; }
        public int? ProductId { get; set; }
        public MovementType? MovementType { get; set; }

        public int TotalMovementsCount { get; set; }
        public decimal TotalInQuantity { get; set; }
        public decimal TotalOutQuantity { get; set; }

        public List<StockMovementRowDto> Movements { get; set; } = new List<StockMovementRowDto>();
    }

    // 6. Low-Stock Alert Report
    public class LowStockRowDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = "pcs";
        public decimal QuantityOnHand { get; set; }
        public decimal MinimumStockLevel { get; set; }
        public decimal DeficitQuantity => Math.Max(0, MinimumStockLevel - QuantityOnHand);
        public bool IsOutOfStock => QuantityOnHand <= 0;
    }

    public class LowStockReportViewModel
    {
        public int? WarehouseId { get; set; }
        public int OutOfStockCount { get; set; }
        public int LowStockCount { get; set; }
        public List<LowStockRowDto> Rows { get; set; } = new List<LowStockRowDto>();
    }

    // 7. Stocktaking Variances Report
    public class StocktakingVarianceRowDto
    {
        public int Id { get; set; }
        public string StocktakingNumber { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime? ApprovedDateUtc { get; set; }
        public string? ApprovedByUserId { get; set; }
        public decimal TotalShortageQuantity { get; set; }
        public decimal TotalSurplusQuantity { get; set; }
        public decimal NetDifferenceQuantity { get; set; }
        public decimal TotalVarianceCost { get; set; }
        public string? Reason { get; set; }
        public int ItemsCount { get; set; }
    }

    public class StocktakingVariancesReportViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int? WarehouseId { get; set; }

        public int TotalSessionsCount { get; set; }
        public decimal TotalShortageQuantity { get; set; }
        public decimal TotalSurplusQuantity { get; set; }
        public decimal NetQuantityVariance { get; set; }
        public decimal TotalVarianceCostImpact { get; set; }

        public List<StocktakingVarianceRowDto> Rows { get; set; } = new List<StocktakingVarianceRowDto>();
    }

    // 8. Bouquet Component Consumption Report
    public class BouquetComponentUsageDto
    {
        public int ComponentProductId { get; set; }
        public string ComponentProductName { get; set; } = string.Empty;
        public string ComponentSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = "pcs";
        public decimal TotalQuantityConsumed { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost => Math.Round(TotalQuantityConsumed * UnitCost, 2);
        public int BouquetsProducedCount { get; set; }
    }

    public class BouquetConsumptionReportViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int? BouquetProductId { get; set; }

        public int TotalBouquetUnitsSold { get; set; }
        public int DistinctComponentsCount { get; set; }
        public decimal TotalComponentCost { get; set; }

        public List<BouquetComponentUsageDto> Rows { get; set; } = new List<BouquetComponentUsageDto>();
    }

    // 9. Customer and Supplier Balances Report
    public class CustomerBalanceRowDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance => Math.Max(0, TotalInvoiced - TotalPaid);
    }

    public class SupplierBalanceRowDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
    }

    public class CustomerSupplierBalancesReportViewModel
    {
        public decimal TotalCustomerReceivables { get; set; }
        public decimal TotalSupplierPayables { get; set; }

        public List<CustomerBalanceRowDto> Customers { get; set; } = new List<CustomerBalanceRowDto>();
        public List<SupplierBalanceRowDto> Suppliers { get; set; } = new List<SupplierBalanceRowDto>();
    }
}
