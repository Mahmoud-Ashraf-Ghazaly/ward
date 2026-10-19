using System;
using System.Collections.Generic;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;

namespace ward.Entities.Sales
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;
        public DateTime? DueDateUtc { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

        public decimal SubTotal { get; set; }
        public decimal TaxRatePercentage { get; set; } = 0;
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }

        public string? Notes { get; set; }
        public byte[]? RowVersion { get; set; }

        public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<InvoiceReturn> Returns { get; set; } = new List<InvoiceReturn>();
    }

    public class InvoiceItem
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
        public decimal ReturnedQuantity { get; set; } = 0;

        public ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();
    }

    public class InvoiceReturn
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;

        public int InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public DateTime ReturnDateUtc { get; set; } = DateTime.UtcNow;
        public string? Reason { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public ReturnStatus Status { get; set; } = ReturnStatus.Draft;

        public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
    }

    public class ReturnItem
    {
        public int Id { get; set; }

        public int InvoiceReturnId { get; set; }
        public InvoiceReturn InvoiceReturn { get; set; } = null!;

        public int InvoiceItemId { get; set; }
        public InvoiceItem InvoiceItem { get; set; } = null!;

        public decimal Quantity { get; set; }
        public decimal RefundUnitPrice { get; set; }
        public decimal LineRefundTotal { get; set; }

        public int? RestockToWarehouseId { get; set; }
        public Warehouse? RestockToWarehouse { get; set; }

        public ReturnDisposition Disposition { get; set; } = ReturnDisposition.Restocked;
    }

    public class Payment
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public DateTime PaymentDateUtc { get; set; } = DateTime.UtcNow;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
    }
}
