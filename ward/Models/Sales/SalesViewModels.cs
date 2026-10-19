using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ward.Entities.Enums;

namespace ward.Models.Sales
{
    public class PosProductDto
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal CurrentAverageCost { get; set; }
        public string UnitSymbol { get; set; } = "pcs";
        public decimal AvailableStock { get; set; }
        public ProductType ProductType { get; set; }
        public bool HasBouquetRecipe { get; set; }
    }

    public class PosCheckoutItemRequestDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(0.0001, 10000.0000, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.00, 100000.00, ErrorMessage = "Unit price cannot be negative.")]
        public decimal UnitPrice { get; set; }

        [Range(0.00, 100000.00, ErrorMessage = "Item discount cannot be negative.")]
        public decimal DiscountAmount { get; set; } = 0;
    }

    public class PosCheckoutRequestDto
    {
        [Required(ErrorMessage = "Warehouse is required.")]
        public int WarehouseId { get; set; }

        public int? CustomerId { get; set; }

        [Range(0.00, 100000.00, ErrorMessage = "Global discount cannot be negative.")]
        public decimal DiscountAmount { get; set; } = 0;

        [Range(0.00, 100.00, ErrorMessage = "Tax rate must be between 0% and 100%.")]
        public decimal TaxRatePercentage { get; set; } = 0;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        [Range(0.00, 10000000.00, ErrorMessage = "Amount received cannot be negative.")]
        public decimal AmountReceived { get; set; } = 0;

        [StringLength(100)]
        public string? PaymentReference { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public List<PosCheckoutItemRequestDto> Items { get; set; } = new List<PosCheckoutItemRequestDto>();
    }

    public class PosCheckoutResultDto
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal AmountReceived { get; set; }
        public decimal ChangeGiven { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
    }

    public class SalesInvoiceListItemViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDateUtc { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public InvoiceStatus Status { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public int ItemsCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class SalesInvoiceFilterViewModel
    {
        public string? SearchQuery { get; set; }
        public int? CustomerId { get; set; }
        public InvoiceStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public List<SalesInvoiceListItemViewModel> Invoices { get; set; } = new List<SalesInvoiceListItemViewModel>();
    }

    public class ComponentConsumptionDto
    {
        public int ComponentProductId { get; set; }
        public string ComponentProductName { get; set; } = string.Empty;
        public string ComponentSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal QuantityConsumed { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost => Math.Round(QuantityConsumed * UnitCost, 2);
    }

    public class SalesInvoiceItemDetailsViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
        public decimal LineCost => Math.Round(Quantity * UnitCost, 2);
        public decimal Profit => LineTotal - LineCost;
        public decimal MarginPercentage => LineTotal > 0 ? Math.Round((Profit / LineTotal) * 100, 1) : 0;
        public bool IsBouquetRecipeDeduction { get; set; }
        public List<ComponentConsumptionDto> ComponentConsumptions { get; set; } = new List<ComponentConsumptionDto>();
    }

    public class SalesInvoiceDetailsViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDateUtc { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRatePercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance => GrandTotal - PaidAmount;
        public decimal AmountReceived { get; set; }
        public decimal ChangeGiven { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserId { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit => GrandTotal - TotalCost;
        public decimal ProfitMargin => GrandTotal > 0 ? Math.Round((TotalProfit / GrandTotal) * 100, 1) : 0;

        public List<SalesInvoiceItemDetailsViewModel> Items { get; set; } = new List<SalesInvoiceItemDetailsViewModel>();
    }

    public class CustomerFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Customer name is required.")]
        [StringLength(150, ErrorMessage = "Customer name cannot exceed 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Phone]
        [StringLength(50)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class CustomerListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int InvoicesCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class SalesReturnItemFormViewModel
    {
        public int InvoiceItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = "pcs";
        public decimal SoldQuantity { get; set; }
        public decimal AlreadyReturnedQuantity { get; set; }
        public decimal EligibleQuantity { get; set; }

        [Range(0.00, 100000.00, ErrorMessage = "Return quantity cannot be negative.")]
        public decimal ReturnQuantity { get; set; } = 0;

        public decimal UnitPrice { get; set; }
        public decimal LineRefundTotal => Math.Round(ReturnQuantity * UnitPrice, 2);
        public ReturnDisposition Disposition { get; set; } = ReturnDisposition.Restocked;
        public bool IsBouquetRecipeDeduction { get; set; }
    }

    public class SalesReturnCreateViewModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Return number is required.")]
        public string ReturnNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Return date is required.")]
        [DataType(DataType.Date)]
        public DateTime ReturnDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Reason for return is required.")]
        [StringLength(250, ErrorMessage = "Reason cannot exceed 250 characters.")]
        public string Reason { get; set; } = string.Empty;

        public string RefundMethod { get; set; } = "Cash"; // Cash, Card, CustomerCredit

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();

        public List<SalesReturnItemFormViewModel> Items { get; set; } = new List<SalesReturnItemFormViewModel>();
    }

    public class SalesReturnListItemViewModel
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime ReturnDateUtc { get; set; }
        public ReturnStatus Status { get; set; }
        public string? Reason { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public string? RefundMethod { get; set; }
        public int ItemsCount { get; set; }
    }

    public class SalesReturnItemDetailsViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal RefundUnitPrice { get; set; }
        public decimal LineRefundTotal { get; set; }
        public ReturnDisposition Disposition { get; set; }
        public bool IsBouquetRecipeDeduction { get; set; }
        public List<ComponentConsumptionDto> RestoredComponents { get; set; } = new List<ComponentConsumptionDto>();
    }

    public class SalesReturnDetailsViewModel
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime ReturnDateUtc { get; set; }
        public ReturnStatus Status { get; set; }
        public string? Reason { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public string? RefundMethod { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserId { get; set; }

        public List<SalesReturnItemDetailsViewModel> Items { get; set; } = new List<SalesReturnItemDetailsViewModel>();
    }
}

