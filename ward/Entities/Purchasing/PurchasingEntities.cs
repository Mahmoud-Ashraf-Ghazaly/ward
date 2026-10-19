using System;
using System.Collections.Generic;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;

namespace ward.Entities.Purchasing
{
    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TaxNumber { get; set; }
        public string? Address { get; set; }
        public decimal CurrentBalance { get; set; } = 0; // Positive means we owe the supplier
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<PurchaseInvoice> Invoices { get; set; } = new List<PurchaseInvoice>();
        public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
    }

    public class PurchaseInvoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty; // e.g., "PINV-202610-0001"
        public string? SupplierInvoiceNumber { get; set; } // External vendor ref
        public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;
        public DateTime? DueDateUtc { get; set; }

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;

        public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; } = 0;
        public decimal TaxRatePercentage { get; set; } = 0;
        public decimal TaxAmount { get; set; } = 0;
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; } = 0;

        public string? Notes { get; set; }
        public string? IdempotencyToken { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public string? PostedByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public byte[]? RowVersion { get; set; }

        public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
        public ICollection<PurchaseReturn> Returns { get; set; } = new List<PurchaseReturn>();
        public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
    }

    public class PurchaseInvoiceItem
    {
        public int Id { get; set; }

        public int PurchaseInvoiceId { get; set; }
        public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal DiscountAmount { get; set; } = 0;
        public decimal LineTotal { get; set; }
        public decimal ReturnedQuantity { get; set; } = 0;

        public ICollection<PurchaseReturnItem> ReturnItems { get; set; } = new List<PurchaseReturnItem>();
    }

    public class PurchaseReturn
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty; // e.g., "PRET-202610-0001"

        public int PurchaseInvoiceId { get; set; }
        public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;

        public DateTime ReturnDateUtc { get; set; } = DateTime.UtcNow;
        public PurchaseReturnStatus Status { get; set; } = PurchaseReturnStatus.Draft;

        public string? Reason { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public string? Notes { get; set; }
        public string? IdempotencyToken { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string? CompletedByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
    }

    public class PurchaseReturnItem
    {
        public int Id { get; set; }

        public int PurchaseReturnId { get; set; }
        public PurchaseReturn PurchaseReturn { get; set; } = null!;

        public int PurchaseInvoiceItemId { get; set; }
        public PurchaseInvoiceItem PurchaseInvoiceItem { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineRefundTotal { get; set; }
        public ReturnDisposition Disposition { get; set; } = ReturnDisposition.Restocked;
    }

    public class SupplierPayment
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public int? PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }

        public DateTime PaymentDateUtc { get; set; } = DateTime.UtcNow;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string? CreatedByUserId { get; set; }
    }
}
