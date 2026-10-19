using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ward.Entities.Enums;

namespace ward.Models.Purchasing
{
    public class PurchaseInvoiceListItemViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SupplierInvoiceNumber { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime InvoiceDateUtc { get; set; }
        public DateTime? DueDateUtc { get; set; }
        public PurchaseInvoiceStatus Status { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance => GrandTotal - PaidAmount;
        public int ItemsCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class PurchaseInvoiceFilterViewModel
    {
        public string? SearchQuery { get; set; }
        public int? SupplierId { get; set; }
        public int? WarehouseId { get; set; }
        public PurchaseInvoiceStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public List<PurchaseInvoiceListItemViewModel> Invoices { get; set; } = new List<PurchaseInvoiceListItemViewModel>();
    }

    public class PurchaseInvoiceItemFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Product is required.")]
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = "pcs";

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0.0001, 100000.0000, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Unit cost is required.")]
        [Range(0.00, 100000.00, ErrorMessage = "Unit cost cannot be negative.")]
        public decimal UnitCost { get; set; }

        [Range(0.00, 100000.00, ErrorMessage = "Discount cannot be negative.")]
        public decimal DiscountAmount { get; set; } = 0;

        public decimal LineTotal => Math.Max(0, (Quantity * UnitCost) - DiscountAmount);
    }

    public class PurchaseInvoiceCreateViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Invoice number is required.")]
        [StringLength(50, ErrorMessage = "Invoice number cannot exceed 50 characters.")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Supplier invoice number cannot exceed 50 characters.")]
        public string? SupplierInvoiceNumber { get; set; }

        [Required(ErrorMessage = "Supplier is required.")]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Warehouse is required.")]
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Invoice date is required.")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Range(0.00, 1000000.00, ErrorMessage = "Discount amount cannot be negative.")]
        public decimal DiscountAmount { get; set; } = 0;

        [Range(0.00, 100.00, ErrorMessage = "Tax rate must be between 0% and 100%.")]
        public decimal TaxRatePercentage { get; set; } = 0; // e.g. 5% or 15% VAT

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        public bool PostImmediately { get; set; } = true;

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();

        public List<PurchaseInvoiceItemFormViewModel> Items { get; set; } = new List<PurchaseInvoiceItemFormViewModel>();
    }

    public class PurchaseInvoiceItemDetailsViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal EligibleReturnQuantity => Math.Max(0, Quantity - ReturnedQuantity);
    }

    public class PurchaseInvoiceDetailsViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SupplierInvoiceNumber { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierContactPerson { get; set; }
        public string? SupplierPhone { get; set; }
        public string? SupplierEmail { get; set; }
        public string? SupplierTaxNumber { get; set; }
        public string? SupplierAddress { get; set; }
        public decimal SupplierCurrentBalance { get; set; }

        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;

        public DateTime InvoiceDateUtc { get; set; }
        public DateTime? DueDateUtc { get; set; }
        public PurchaseInvoiceStatus Status { get; set; }

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRatePercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance => GrandTotal - PaidAmount;

        public string? Notes { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public string? PostedByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public List<PurchaseInvoiceItemDetailsViewModel> Items { get; set; } = new List<PurchaseInvoiceItemDetailsViewModel>();
        public List<PurchaseReturnListItemViewModel> Returns { get; set; } = new List<PurchaseReturnListItemViewModel>();
        public List<SupplierPaymentListItemViewModel> Payments { get; set; } = new List<SupplierPaymentListItemViewModel>();
    }

    public class PurchaseReturnItemFormViewModel
    {
        public int PurchaseInvoiceItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal PurchasedQuantity { get; set; }
        public decimal AlreadyReturnedQuantity { get; set; }
        public decimal EligibleQuantity { get; set; }

        [Range(0.00, 100000.00, ErrorMessage = "Return quantity cannot be negative.")]
        public decimal ReturnQuantity { get; set; } = 0;

        public decimal UnitCost { get; set; }
        public decimal LineRefundTotal => ReturnQuantity * UnitCost;
        public ReturnDisposition Disposition { get; set; } = ReturnDisposition.Restocked;
    }

    public class PurchaseReturnCreateViewModel
    {
        public int PurchaseInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
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

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();

        public List<PurchaseReturnItemFormViewModel> Items { get; set; } = new List<PurchaseReturnItemFormViewModel>();
    }

    public class PurchaseReturnListItemViewModel
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int PurchaseInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime ReturnDateUtc { get; set; }
        public PurchaseReturnStatus Status { get; set; }
        public string? Reason { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public int ItemsCount { get; set; }
    }

    public class PurchaseReturnItemDetailsViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineRefundTotal { get; set; }
        public ReturnDisposition Disposition { get; set; }
    }

    public class PurchaseReturnDetailsViewModel
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int PurchaseInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime ReturnDateUtc { get; set; }
        public PurchaseReturnStatus Status { get; set; }
        public string? Reason { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public string? Notes { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string? CompletedByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public List<PurchaseReturnItemDetailsViewModel> Items { get; set; } = new List<PurchaseReturnItemDetailsViewModel>();
    }

    public class SupplierFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Supplier name is required.")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [Phone]
        [StringLength(50)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? TaxNumber { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;
        public decimal CurrentBalance { get; set; }
    }

    public class SupplierListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public decimal CurrentBalance { get; set; }
        public int InvoicesCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class SupplierPaymentFormViewModel
    {
        [Required(ErrorMessage = "Supplier is required.")]
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public int? PurchaseInvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }

        [Required(ErrorMessage = "Payment amount is required.")]
        [Range(0.01, 10000000.00, ErrorMessage = "Payment amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Payment date is required.")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        [StringLength(250)]
        public string? Notes { get; set; }
    }

    public class SupplierPaymentListItemViewModel
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int? PurchaseInvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime PaymentDateUtc { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserId { get; set; }
    }
}
