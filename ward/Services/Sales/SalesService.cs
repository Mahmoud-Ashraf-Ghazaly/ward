using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Production;
using ward.Entities.Sales;
using ward.Models.Sales;

namespace ward.Services.Sales
{
    public class SalesService : ISalesService
    {
        private readonly ApplicationDbContext _context;

        public SalesService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PosProductDto>> GetPosProductsAsync(int warehouseId, string? searchTerm = null, int? categoryId = null)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.UnitOfMeasure)
                .Include(p => p.BouquetRecipe)
                .Where(p => p.IsActive)
                .AsNoTracking();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(term) ||
                    p.SKU.ToLower().Contains(term) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
            }

            var products = await query
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .Take(100)
                .ToListAsync();

            var productIds = products.Select(p => p.Id).ToList();

            var stockMap = await _context.StockItems
                .Where(s => s.WarehouseId == warehouseId && productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, s => s.QuantityOnHand);

            return products.Select(p => new PosProductDto
            {
                Id = p.Id,
                SKU = p.SKU,
                Barcode = p.Barcode,
                Name = p.Name,
                CategoryName = p.Category?.Name ?? "General",
                UnitPrice = p.UnitPrice,
                CurrentAverageCost = p.CurrentAverageCost,
                UnitSymbol = p.UnitOfMeasure?.Symbol ?? "pcs",
                AvailableStock = stockMap.TryGetValue(p.Id, out var qty) ? qty : 0,
                ProductType = p.ProductType,
                HasBouquetRecipe = p.BouquetRecipe != null && p.BouquetRecipe.IsActive
            }).ToList();
        }

        public async Task<PosProductDto?> FindProductForPosAsync(string barcodeOrSku, int warehouseId)
        {
            if (string.IsNullOrWhiteSpace(barcodeOrSku))
                return null;

            var clean = barcodeOrSku.Trim().ToLower();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.UnitOfMeasure)
                .Include(p => p.BouquetRecipe)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsActive && 
                    ((p.Barcode != null && p.Barcode.ToLower() == clean) || p.SKU.ToLower() == clean));

            if (product == null)
                return null;

            var stock = await _context.StockItems
                .Where(s => s.WarehouseId == warehouseId && s.ProductId == product.Id)
                .Select(s => s.QuantityOnHand)
                .FirstOrDefaultAsync();

            return new PosProductDto
            {
                Id = product.Id,
                SKU = product.SKU,
                Barcode = product.Barcode,
                Name = product.Name,
                CategoryName = product.Category?.Name ?? "General",
                UnitPrice = product.UnitPrice,
                CurrentAverageCost = product.CurrentAverageCost,
                UnitSymbol = product.UnitOfMeasure?.Symbol ?? "pcs",
                AvailableStock = stock,
                ProductType = product.ProductType,
                HasBouquetRecipe = product.BouquetRecipe != null && product.BouquetRecipe.IsActive
            };
        }

        public async Task<PosCheckoutResultDto> ProcessCheckoutAsync(PosCheckoutRequestDto request, string? userId)
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                return new PosCheckoutResultDto { Success = false, ErrorMessage = "Cart is empty. Add at least one item to checkout." };
            }

            // 1. Idempotency guard: prevent duplicate checkout execution
            if (!string.IsNullOrWhiteSpace(request.IdempotencyToken))
            {
                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.IdempotencyToken == request.IdempotencyToken);

                if (existingInvoice != null)
                {
                    return new PosCheckoutResultDto
                    {
                        Success = true,
                        InvoiceId = existingInvoice.Id,
                        InvoiceNumber = existingInvoice.InvoiceNumber,
                        GrandTotal = existingInvoice.GrandTotal,
                        PaidAmount = existingInvoice.PaidAmount,
                        AmountReceived = existingInvoice.AmountReceived,
                        ChangeGiven = existingInvoice.ChangeGiven,
                        PaymentMethod = request.PaymentMethod
                    };
                }
            }

            var warehouse = await _context.Warehouses.FindAsync(request.WarehouseId);
            if (warehouse == null || !warehouse.IsActive)
            {
                return new PosCheckoutResultDto { Success = false, ErrorMessage = "Selected warehouse not found or is inactive." };
            }

            // Load products from DB
            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var productsInDb = await _context.Products
                .Include(p => p.UnitOfMeasure)
                .Include(p => p.BouquetRecipe)
                    .ThenInclude(b => b!.Items)
                        .ThenInclude(bi => bi.ComponentProduct)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            // Server-side recalculation of prices & subtotal
            decimal subTotal = 0;
            foreach (var cartItem in request.Items)
            {
                if (!productsInDb.TryGetValue(cartItem.ProductId, out var prod))
                {
                    return new PosCheckoutResultDto { Success = false, ErrorMessage = $"Product ID {cartItem.ProductId} was not found." };
                }

                if (!prod.IsActive)
                {
                    return new PosCheckoutResultDto { Success = false, ErrorMessage = $"Product '{prod.Name}' is no longer active." };
                }

                if (cartItem.Quantity <= 0)
                {
                    return new PosCheckoutResultDto { Success = false, ErrorMessage = $"Quantity for '{prod.Name}' must be greater than zero." };
                }

                // Use server unit price
                var linePrice = prod.UnitPrice;
                var disc = Math.Max(0, cartItem.DiscountAmount);
                var lineTotal = Math.Max(0, (cartItem.Quantity * linePrice) - disc);
                subTotal += lineTotal;
            }

            var globalDiscount = Math.Max(0, request.DiscountAmount);
            var taxableSubtotal = Math.Max(0, subTotal - globalDiscount);
            var taxRate = Math.Clamp(request.TaxRatePercentage, 0m, 100m);
            var taxAmount = Math.Round(taxableSubtotal * (taxRate / 100m), 2);
            var grandTotal = Math.Round(taxableSubtotal + taxAmount, 2);

            // Payment & Cash Validation
            decimal amountReceived = request.AmountReceived;
            decimal changeGiven = 0;

            if (request.PaymentMethod == PaymentMethod.Cash)
            {
                if (amountReceived < grandTotal)
                {
                    return new PosCheckoutResultDto
                    {
                        Success = false,
                        ErrorMessage = $"Cash received (${amountReceived:F2}) is less than the grand total (${grandTotal:F2})."
                    };
                }
                changeGiven = Math.Round(amountReceived - grandTotal, 2);
            }
            else
            {
                // Electronic or non-cash: exact amount received
                amountReceived = grandTotal;
                changeGiven = 0;
            }

            var invoiceNumber = await GenerateNextSalesInvoiceNumberAsync();

            // Begin atomic transaction
            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    InvoiceDateUtc = DateTime.UtcNow,
                    CustomerId = request.CustomerId > 0 ? request.CustomerId : null,
                    Status = InvoiceStatus.FullyPaid,
                    SubTotal = Math.Round(subTotal, 2),
                    DiscountAmount = globalDiscount,
                    TaxRatePercentage = taxRate,
                    TaxAmount = taxAmount,
                    GrandTotal = grandTotal,
                    PaidAmount = grandTotal,
                    AmountReceived = amountReceived,
                    ChangeGiven = changeGiven,
                    IdempotencyToken = request.IdempotencyToken,
                    CreatedByUserId = userId,
                    Notes = request.Notes?.Trim()
                };

                _context.Invoices.Add(invoice);

                // Process stock deductions
                foreach (var cartItem in request.Items)
                {
                    var product = productsInDb[cartItem.ProductId];
                    var itemLineTotal = Math.Max(0, (cartItem.Quantity * product.UnitPrice) - cartItem.DiscountAmount);

                    var invoiceItem = new InvoiceItem
                    {
                        Invoice = invoice,
                        ProductId = product.Id,
                        WarehouseId = request.WarehouseId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = product.UnitPrice,
                        DiscountAmount = cartItem.DiscountAmount,
                        LineTotal = itemLineTotal
                    };

                    invoice.Items.Add(invoiceItem);

                    // Check if bouquet with active recipe
                    if (product.ProductType == ProductType.Bouquet && product.BouquetRecipe != null && product.BouquetRecipe.IsActive)
                    {
                        invoiceItem.IsBouquetRecipeDeduction = true;
                        decimal totalComponentsCost = 0;

                        // Deduct each component from warehouse
                        foreach (var recipeItem in product.BouquetRecipe.Items)
                        {
                            var effectivePerUnit = recipeItem.QuantityRequired * (1 + (recipeItem.WasteMarginPercentage / 100m));
                            var totalCompQtyNeeded = effectivePerUnit * cartItem.Quantity;

                            var compStockItem = await _context.StockItems
                                .FirstOrDefaultAsync(s => s.ProductId == recipeItem.ComponentProductId && s.WarehouseId == request.WarehouseId);

                            var availableStock = compStockItem?.QuantityOnHand ?? 0;
                            if (availableStock < totalCompQtyNeeded)
                            {
                                return new PosCheckoutResultDto
                                {
                                    Success = false,
                                    ErrorMessage = $"Insufficient stock for component '{recipeItem.ComponentProduct.Name}'. Required: {totalCompQtyNeeded:G29}, Available: {availableStock:G29} in {warehouse.Name}."
                                };
                            }

                            // Deduct stock
                            compStockItem!.QuantityOnHand -= totalCompQtyNeeded;

                            var compCost = recipeItem.ComponentProduct.CurrentAverageCost > 0
                                ? recipeItem.ComponentProduct.CurrentAverageCost
                                : recipeItem.ComponentProduct.UnitPrice;

                            totalComponentsCost += totalCompQtyNeeded * compCost;

                            // Record component consumption for audit & profit reporting
                            invoiceItem.ComponentConsumptions.Add(new InvoiceItemComponentConsumption
                            {
                                ComponentProductId = recipeItem.ComponentProductId,
                                QuantityConsumed = totalCompQtyNeeded,
                                UnitCost = compCost
                            });

                            // Record StockMovement for component
                            _context.StockMovements.Add(new StockMovement
                            {
                                ProductId = recipeItem.ComponentProductId,
                                WarehouseId = request.WarehouseId,
                                MovementType = MovementType.SalesOut,
                                Quantity = -totalCompQtyNeeded,
                                UnitCost = compCost,
                                ReferenceType = "SalesInvoice",
                                ReferenceId = invoiceNumber,
                                Notes = $"Component consumed for Bouquet '{product.Name}' x{cartItem.Quantity:G29}",
                                CreatedAtUtc = DateTime.UtcNow,
                                CreatedByUserId = userId
                            });
                        }

                        // Set historical unit cost snapshot for bouquet line item
                        var estimatedUnitCost = (totalComponentsCost / cartItem.Quantity) + product.BouquetRecipe.LaborCost + product.BouquetRecipe.PackagingCost;
                        invoiceItem.UnitCost = Math.Round(estimatedUnitCost, 2);
                    }
                    else
                    {
                        // Ordinary product (e.g. fresh flower, vase, card, or pre-assembled bouquet)
                        invoiceItem.IsBouquetRecipeDeduction = false;
                        invoiceItem.UnitCost = product.CurrentAverageCost;

                        var stockItem = await _context.StockItems
                            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.WarehouseId == request.WarehouseId);

                        var availableStock = stockItem?.QuantityOnHand ?? 0;
                        if (availableStock < cartItem.Quantity)
                        {
                            return new PosCheckoutResultDto
                            {
                                Success = false,
                                ErrorMessage = $"Insufficient stock for '{product.Name}'. Available: {availableStock:G29}, Requested: {cartItem.Quantity:G29} in {warehouse.Name}."
                            };
                        }

                        stockItem!.QuantityOnHand -= cartItem.Quantity;

                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = product.Id,
                            WarehouseId = request.WarehouseId,
                            MovementType = MovementType.SalesOut,
                            Quantity = -cartItem.Quantity,
                            UnitCost = product.CurrentAverageCost,
                            ReferenceType = "SalesInvoice",
                            ReferenceId = invoiceNumber,
                            Notes = $"Sale of {product.Name}",
                            CreatedAtUtc = DateTime.UtcNow,
                            CreatedByUserId = userId
                        });
                    }
                }

                // Add payment record
                var payment = new Payment
                {
                    Invoice = invoice,
                    PaymentDateUtc = DateTime.UtcNow,
                    Amount = grandTotal,
                    PaymentMethod = request.PaymentMethod,
                    ReferenceNumber = request.PaymentReference?.Trim(),
                    CreatedByUserId = userId,
                    Notes = request.PaymentMethod == PaymentMethod.Cash 
                        ? $"Cash received: ${amountReceived:F2}, Change: ${changeGiven:F2}" 
                        : null
                };
                _context.Payments.Add(payment);

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return new PosCheckoutResultDto
                {
                    Success = true,
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    GrandTotal = grandTotal,
                    PaidAmount = grandTotal,
                    AmountReceived = amountReceived,
                    ChangeGiven = changeGiven,
                    PaymentMethod = request.PaymentMethod
                };
            }
            catch (DbUpdateConcurrencyException)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return new PosCheckoutResultDto
                {
                    Success = false,
                    ErrorMessage = "Stock conflict detected under concurrent checkout. Inventory was modified by another cashier. Please refresh and retry."
                };
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return new PosCheckoutResultDto
                {
                    Success = false,
                    ErrorMessage = $"Checkout failed: {ex.Message}"
                };
            }
        }

        public async Task<SalesInvoiceFilterViewModel> GetSalesInvoicesPagedAsync(SalesInvoiceFilterViewModel filter)
        {
            var query = _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var q = filter.SearchQuery.Trim().ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(q) ||
                    (i.Customer != null && i.Customer.Name.ToLower().Contains(q)) ||
                    (i.Notes != null && i.Notes.ToLower().Contains(q)));
            }

            if (filter.CustomerId.HasValue && filter.CustomerId.Value > 0)
            {
                query = query.Where(i => i.CustomerId == filter.CustomerId.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(i => i.Status == filter.Status.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDateUtc >= filter.FromDate.Value.ToUniversalTime());
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDateUtc <= filter.ToDate.Value.ToUniversalTime().AddDays(1));
            }

            filter.TotalCount = await query.CountAsync();

            var page = Math.Max(1, filter.PageNumber);
            var pageSize = filter.PageSize > 0 ? filter.PageSize : 15;

            filter.Invoices = await query
                .OrderByDescending(i => i.InvoiceDateUtc)
                .ThenByDescending(i => i.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new SalesInvoiceListItemViewModel
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDateUtc = i.InvoiceDateUtc,
                    CustomerName = i.Customer != null ? i.Customer.Name : "Walk-in Customer",
                    Status = i.Status,
                    SubTotal = i.SubTotal,
                    DiscountAmount = i.DiscountAmount,
                    TaxAmount = i.TaxAmount,
                    GrandTotal = i.GrandTotal,
                    PaidAmount = i.PaidAmount,
                    ItemsCount = i.Items.Count,
                    CreatedAtUtc = i.InvoiceDateUtc
                })
                .ToListAsync();

            return filter;
        }

        public async Task<SalesInvoiceDetailsViewModel?> GetSalesInvoiceDetailsAsync(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Payments)
                .Include(i => i.Items)
                    .ThenInclude(it => it.Product)
                        .ThenInclude(p => p.UnitOfMeasure)
                .Include(i => i.Items)
                    .ThenInclude(it => it.ComponentConsumptions)
                        .ThenInclude(cc => cc.ComponentProduct)
                            .ThenInclude(cp => cp.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return null;

            var primaryPayment = invoice.Payments.OrderByDescending(p => p.PaymentDateUtc).FirstOrDefault();

            decimal totalCost = 0;
            var itemsList = new List<SalesInvoiceItemDetailsViewModel>();

            foreach (var item in invoice.Items)
            {
                var itemCost = item.Quantity * item.UnitCost;
                totalCost += itemCost;

                var consumptions = item.ComponentConsumptions.Select(c => new ComponentConsumptionDto
                {
                    ComponentProductId = c.ComponentProductId,
                    ComponentProductName = c.ComponentProduct.Name,
                    ComponentSKU = c.ComponentProduct.SKU,
                    UnitSymbol = c.ComponentProduct.UnitOfMeasure?.Symbol ?? "pcs",
                    QuantityConsumed = c.QuantityConsumed,
                    UnitCost = c.UnitCost
                }).ToList();

                itemsList.Add(new SalesInvoiceItemDetailsViewModel
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductSKU = item.Product.SKU,
                    UnitSymbol = item.Product.UnitOfMeasure?.Symbol ?? "pcs",
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    UnitCost = item.UnitCost,
                    DiscountAmount = item.DiscountAmount,
                    LineTotal = item.LineTotal,
                    IsBouquetRecipeDeduction = item.IsBouquetRecipeDeduction,
                    ComponentConsumptions = consumptions
                });
            }

            return new SalesInvoiceDetailsViewModel
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDateUtc = invoice.InvoiceDateUtc,
                CustomerId = invoice.CustomerId,
                CustomerName = invoice.Customer?.Name ?? "Walk-in Customer",
                CustomerPhone = invoice.Customer?.Phone,
                CustomerEmail = invoice.Customer?.Email,
                Status = invoice.Status,
                SubTotal = invoice.SubTotal,
                DiscountAmount = invoice.DiscountAmount,
                TaxRatePercentage = invoice.TaxRatePercentage,
                TaxAmount = invoice.TaxAmount,
                GrandTotal = invoice.GrandTotal,
                PaidAmount = invoice.PaidAmount,
                AmountReceived = invoice.AmountReceived,
                ChangeGiven = invoice.ChangeGiven,
                PaymentMethod = primaryPayment?.PaymentMethod ?? PaymentMethod.Cash,
                PaymentReference = primaryPayment?.ReferenceNumber,
                Notes = invoice.Notes,
                CreatedByUserId = invoice.CreatedByUserId,
                TotalCost = Math.Round(totalCost, 2),
                Items = itemsList
            };
        }

        public async Task<string> GenerateNextSalesInvoiceNumberAsync()
        {
            var prefix = $"INV-{DateTime.UtcNow:yyyyMM}-";
            var count = await _context.Invoices
                .CountAsync(i => i.InvoiceNumber.StartsWith(prefix));
            return $"{prefix}{(count + 1):D4}";
        }

        public async Task<List<CustomerListItemViewModel>> GetAllCustomersAsync()
        {
            return await _context.Customers
                .Include(c => c.Invoices)
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CustomerListItemViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Email = c.Email,
                    InvoicesCount = c.Invoices.Count,
                    IsActive = c.IsActive
                })
                .ToListAsync();
        }

        public async Task<CustomerFormViewModel?> GetCustomerByIdAsync(int id)
        {
            var c = await _context.Customers.FindAsync(id);
            if (c == null) return null;

            return new CustomerFormViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                Email = c.Email,
                Address = c.Address,
                IsActive = c.IsActive
            };
        }

        public async Task<(bool Success, string? Error, int CustomerId)> SaveCustomerAsync(CustomerFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return (false, "Customer name is required.", 0);

            Customer? customer;
            if (model.Id > 0)
            {
                customer = await _context.Customers.FindAsync(model.Id);
                if (customer == null) return (false, "Customer not found.", 0);
            }
            else
            {
                customer = new Customer { CreatedAtUtc = DateTime.UtcNow };
                _context.Customers.Add(customer);
            }

            customer.Name = model.Name.Trim();
            customer.Phone = model.Phone?.Trim();
            customer.Email = model.Email?.Trim();
            customer.Address = model.Address?.Trim();
            customer.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return (true, null, customer.Id);
        }

        // Sales Returns Implementation
        public async Task<SalesReturnCreateViewModel?> PrepareCreateReturnViewModelAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                    .ThenInclude(it => it.Product)
                        .ThenInclude(p => p.UnitOfMeasure)
                .Include(i => i.Items)
                    .ThenInclude(it => it.Warehouse)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                return null;

            if (invoice.Status != InvoiceStatus.Confirmed &&
                invoice.Status != InvoiceStatus.FullyPaid &&
                invoice.Status != InvoiceStatus.PartiallyReturned)
            {
                return null;
            }

            var firstItem = invoice.Items.FirstOrDefault();
            var warehouseId = firstItem?.WarehouseId ?? 1;
            var warehouseName = firstItem?.Warehouse?.Name ?? "Primary Warehouse";

            var nextReturnNumber = await GenerateNextSalesReturnNumberAsync();

            var returnItems = invoice.Items.Select(item =>
            {
                var eligibleQty = Math.Max(0, item.Quantity - item.ReturnedQuantity);
                var unitPrice = item.UnitPrice;
                return new SalesReturnItemFormViewModel
                {
                    InvoiceItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductSKU = item.Product.SKU,
                    UnitSymbol = item.Product.UnitOfMeasure?.Symbol ?? "pcs",
                    SoldQuantity = item.Quantity,
                    AlreadyReturnedQuantity = item.ReturnedQuantity,
                    EligibleQuantity = eligibleQty,
                    ReturnQuantity = 0,
                    UnitPrice = unitPrice,
                    Disposition = ReturnDisposition.Restocked,
                    IsBouquetRecipeDeduction = item.IsBouquetRecipeDeduction
                };
            }).ToList();

            return new SalesReturnCreateViewModel
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerName = invoice.Customer?.Name ?? "Walk-in Customer",
                WarehouseId = warehouseId,
                WarehouseName = warehouseName,
                ReturnNumber = nextReturnNumber,
                ReturnDate = DateTime.Today,
                RefundMethod = "Cash",
                Items = returnItems
            };
        }

        public async Task<(bool Success, string? Error, int ReturnId)> ProcessSalesReturnAsync(SalesReturnCreateViewModel model, string? userId)
        {
            if (string.IsNullOrWhiteSpace(model.Reason))
            {
                return (false, "Reason for return is required.", 0);
            }

            // 1. Idempotency guard: prevent duplicate return processing
            if (!string.IsNullOrWhiteSpace(model.IdempotencyToken))
            {
                var existingReturn = await _context.InvoiceReturns
                    .FirstOrDefaultAsync(r => r.IdempotencyToken == model.IdempotencyToken);
                if (existingReturn != null)
                {
                    return (true, null, existingReturn.Id);
                }
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                    .ThenInclude(it => it.Product)
                .Include(i => i.Items)
                    .ThenInclude(it => it.ComponentConsumptions)
                        .ThenInclude(cc => cc.ComponentProduct)
                .FirstOrDefaultAsync(i => i.Id == model.InvoiceId);

            if (invoice == null)
            {
                return (false, "Sales invoice not found.", 0);
            }

            if (invoice.Status == InvoiceStatus.Cancelled || invoice.Status == InvoiceStatus.Draft || invoice.Status == InvoiceStatus.FullyReturned)
            {
                return (false, $"Invoice status '{invoice.Status}' is not eligible for return.", 0);
            }

            var itemsToReturn = model.Items.Where(i => i.ReturnQuantity > 0).ToList();
            if (!itemsToReturn.Any())
            {
                return (false, "Please specify a return quantity greater than zero for at least one item.", 0);
            }

            var invoiceItemsMap = invoice.Items.ToDictionary(i => i.Id);

            foreach (var retItem in itemsToReturn)
            {
                if (!invoiceItemsMap.TryGetValue(retItem.InvoiceItemId, out var invItem))
                {
                    return (false, $"Invoice line item ID {retItem.InvoiceItemId} was not found on this invoice.", 0);
                }

                var remainingEligible = invItem.Quantity - invItem.ReturnedQuantity;
                if (retItem.ReturnQuantity > remainingEligible)
                {
                    return (false, $"Cannot return {retItem.ReturnQuantity:G29} for '{invItem.Product.Name}'. Remaining eligible quantity is {remainingEligible:G29}.", 0);
                }

                if (retItem.ReturnQuantity <= 0)
                {
                    return (false, $"Return quantity for '{invItem.Product.Name}' must be greater than zero.", 0);
                }
            }

            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var returnNumber = string.IsNullOrWhiteSpace(model.ReturnNumber)
                    ? await GenerateNextSalesReturnNumberAsync()
                    : model.ReturnNumber.Trim();

                var returnRecord = new InvoiceReturn
                {
                    ReturnNumber = returnNumber,
                    InvoiceId = invoice.Id,
                    ReturnDateUtc = model.ReturnDate.ToUniversalTime(),
                    Reason = model.Reason.Trim(),
                    Status = ReturnStatus.Completed,
                    RefundMethod = model.RefundMethod ?? "Cash",
                    Notes = model.Notes?.Trim(),
                    IdempotencyToken = model.IdempotencyToken,
                    CreatedByUserId = userId
                };

                decimal totalRefund = 0;

                foreach (var retItem in itemsToReturn)
                {
                    var invItem = invoiceItemsMap[retItem.InvoiceItemId];
                    var returnQty = retItem.ReturnQuantity;

                    var effectiveUnitPrice = invItem.Quantity > 0
                        ? Math.Round(invItem.LineTotal / invItem.Quantity, 2)
                        : invItem.UnitPrice;

                    var lineRefund = Math.Round(returnQty * effectiveUnitPrice, 2);
                    totalRefund += lineRefund;

                    invItem.ReturnedQuantity += returnQty;

                    var returnItemEntity = new ReturnItem
                    {
                        InvoiceReturn = returnRecord,
                        InvoiceItemId = invItem.Id,
                        Quantity = returnQty,
                        RefundUnitPrice = effectiveUnitPrice,
                        LineRefundTotal = lineRefund,
                        Disposition = retItem.Disposition,
                        RestockToWarehouseId = retItem.Disposition == ReturnDisposition.Restocked ? invItem.WarehouseId : null
                    };
                    returnRecord.Items.Add(returnItemEntity);

                    // Restore stock ONLY if item accepted back into sellable inventory (Restocked)
                    if (retItem.Disposition == ReturnDisposition.Restocked)
                    {
                        // Recipe-based bouquet: restore component quantities
                        if (invItem.IsBouquetRecipeDeduction && invItem.ComponentConsumptions.Any())
                        {
                            var returnRatio = returnQty / invItem.Quantity;

                            foreach (var consumption in invItem.ComponentConsumptions)
                            {
                                var compQtyToRestore = Math.Round(consumption.QuantityConsumed * returnRatio, 4);

                                if (compQtyToRestore > 0)
                                {
                                    var compStock = await _context.StockItems
                                        .FirstOrDefaultAsync(s => s.ProductId == consumption.ComponentProductId && s.WarehouseId == invItem.WarehouseId);

                                    if (compStock == null)
                                    {
                                        compStock = new StockItem
                                        {
                                            ProductId = consumption.ComponentProductId,
                                            WarehouseId = invItem.WarehouseId,
                                            QuantityOnHand = 0
                                        };
                                        _context.StockItems.Add(compStock);
                                    }

                                    compStock.QuantityOnHand += compQtyToRestore;

                                    _context.StockMovements.Add(new StockMovement
                                    {
                                        ProductId = consumption.ComponentProductId,
                                        WarehouseId = invItem.WarehouseId,
                                        MovementType = MovementType.ReturnIn,
                                        Quantity = compQtyToRestore,
                                        UnitCost = consumption.UnitCost,
                                        ReferenceType = "SalesReturn",
                                        ReferenceId = returnNumber,
                                        Notes = $"Bouquet component restocked from Return {returnNumber} ({invItem.Product.Name})",
                                        CreatedAtUtc = DateTime.UtcNow,
                                        CreatedByUserId = userId
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Ordinary product stock restoration
                            var stockItem = await _context.StockItems
                                .FirstOrDefaultAsync(s => s.ProductId == invItem.ProductId && s.WarehouseId == invItem.WarehouseId);

                            if (stockItem == null)
                            {
                                stockItem = new StockItem
                                {
                                    ProductId = invItem.ProductId,
                                    WarehouseId = invItem.WarehouseId,
                                    QuantityOnHand = 0
                                };
                                _context.StockItems.Add(stockItem);
                            }

                            stockItem.QuantityOnHand += returnQty;

                            _context.StockMovements.Add(new StockMovement
                            {
                                ProductId = invItem.ProductId,
                                WarehouseId = invItem.WarehouseId,
                                MovementType = MovementType.ReturnIn,
                                Quantity = returnQty,
                                UnitCost = invItem.UnitCost,
                                ReferenceType = "SalesReturn",
                                ReferenceId = returnNumber,
                                Notes = $"Restocked from Sales Return {returnNumber}",
                                CreatedAtUtc = DateTime.UtcNow,
                                CreatedByUserId = userId
                            });
                        }
                    }
                    // For Damaged, Unsellable, or Discarded dispositions, inventory is not restored.
                }

                returnRecord.TotalRefundAmount = totalRefund;
                _context.InvoiceReturns.Add(returnRecord);

                // Update invoice status: FullyReturned vs PartiallyReturned
                var allItemsFullyReturned = invoice.Items.All(i => i.ReturnedQuantity >= i.Quantity);
                invoice.Status = allItemsFullyReturned ? InvoiceStatus.FullyReturned : InvoiceStatus.PartiallyReturned;

                // Record financial refund payment
                var refundPayment = new Payment
                {
                    Invoice = invoice,
                    PaymentDateUtc = DateTime.UtcNow,
                    Amount = -totalRefund,
                    PaymentMethod = model.RefundMethod == "CustomerCredit" ? PaymentMethod.GiftCard : PaymentMethod.Cash,
                    ReferenceNumber = $"REFUND-{returnNumber}",
                    CreatedByUserId = userId,
                    Notes = $"Refund for Return {returnNumber} via {model.RefundMethod}"
                };
                _context.Payments.Add(refundPayment);

                invoice.PaidAmount = Math.Max(0, invoice.PaidAmount - totalRefund);

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return (true, null, returnRecord.Id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (transaction != null) await transaction.RollbackAsync();
                return (false, "Concurrency conflict detected while processing sales return. Please retry.", 0);
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                return (false, $"Failed to process sales return: {ex.Message}", 0);
            }
        }

        public async Task<SalesReturnDetailsViewModel?> GetSalesReturnDetailsAsync(int id)
        {
            var ret = await _context.InvoiceReturns
                .Include(r => r.Invoice)
                    .ThenInclude(i => i.Customer)
                .Include(r => r.Items)
                    .ThenInclude(ri => ri.InvoiceItem)
                        .ThenInclude(ii => ii.Product)
                            .ThenInclude(p => p.UnitOfMeasure)
                .Include(r => r.Items)
                    .ThenInclude(ri => ri.InvoiceItem)
                        .ThenInclude(ii => ii.Warehouse)
                .Include(r => r.Items)
                    .ThenInclude(ri => ri.InvoiceItem)
                        .ThenInclude(ii => ii.ComponentConsumptions)
                            .ThenInclude(cc => cc.ComponentProduct)
                                .ThenInclude(cp => cp.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (ret == null) return null;

            var firstItem = ret.Items.FirstOrDefault();
            var warehouseName = firstItem?.InvoiceItem?.Warehouse?.Name ?? "Primary Warehouse";

            var itemsList = new List<SalesReturnItemDetailsViewModel>();
            foreach (var ri in ret.Items)
            {
                var restoredComponents = new List<ComponentConsumptionDto>();
                if (ri.InvoiceItem.IsBouquetRecipeDeduction && ri.Disposition == ReturnDisposition.Restocked && ri.InvoiceItem.Quantity > 0)
                {
                    var ratio = ri.Quantity / ri.InvoiceItem.Quantity;
                    foreach (var comp in ri.InvoiceItem.ComponentConsumptions)
                    {
                        restoredComponents.Add(new ComponentConsumptionDto
                        {
                            ComponentProductId = comp.ComponentProductId,
                            ComponentProductName = comp.ComponentProduct.Name,
                            ComponentSKU = comp.ComponentProduct.SKU,
                            UnitSymbol = comp.ComponentProduct.UnitOfMeasure?.Symbol ?? "pcs",
                            QuantityConsumed = Math.Round(comp.QuantityConsumed * ratio, 4),
                            UnitCost = comp.UnitCost
                        });
                    }
                }

                itemsList.Add(new SalesReturnItemDetailsViewModel
                {
                    Id = ri.Id,
                    ProductId = ri.InvoiceItem.ProductId,
                    ProductName = ri.InvoiceItem.Product.Name,
                    ProductSKU = ri.InvoiceItem.Product.SKU,
                    UnitSymbol = ri.InvoiceItem.Product.UnitOfMeasure?.Symbol ?? "pcs",
                    Quantity = ri.Quantity,
                    RefundUnitPrice = ri.RefundUnitPrice,
                    LineRefundTotal = ri.LineRefundTotal,
                    Disposition = ri.Disposition,
                    IsBouquetRecipeDeduction = ri.InvoiceItem.IsBouquetRecipeDeduction,
                    RestoredComponents = restoredComponents
                });
            }

            return new SalesReturnDetailsViewModel
            {
                Id = ret.Id,
                ReturnNumber = ret.ReturnNumber,
                InvoiceId = ret.InvoiceId,
                InvoiceNumber = ret.Invoice?.InvoiceNumber ?? "",
                CustomerName = ret.Invoice?.Customer?.Name ?? "Walk-in Customer",
                WarehouseName = warehouseName,
                ReturnDateUtc = ret.ReturnDateUtc,
                Status = ret.Status,
                Reason = ret.Reason,
                TotalRefundAmount = ret.TotalRefundAmount,
                RefundMethod = ret.RefundMethod,
                Notes = ret.Notes,
                CreatedByUserId = ret.CreatedByUserId,
                Items = itemsList
            };
        }

        public async Task<List<SalesReturnListItemViewModel>> GetAllSalesReturnsAsync()
        {
            return await _context.InvoiceReturns
                .Include(r => r.Invoice)
                    .ThenInclude(i => i.Customer)
                .Include(r => r.Items)
                .AsNoTracking()
                .OrderByDescending(r => r.ReturnDateUtc)
                .ThenByDescending(r => r.Id)
                .Select(r => new SalesReturnListItemViewModel
                {
                    Id = r.Id,
                    ReturnNumber = r.ReturnNumber,
                    InvoiceId = r.InvoiceId,
                    InvoiceNumber = r.Invoice != null ? r.Invoice.InvoiceNumber : "",
                    CustomerName = (r.Invoice != null && r.Invoice.Customer != null) ? r.Invoice.Customer.Name : "Walk-in Customer",
                    ReturnDateUtc = r.ReturnDateUtc,
                    Status = r.Status,
                    Reason = r.Reason,
                    TotalRefundAmount = r.TotalRefundAmount,
                    RefundMethod = r.RefundMethod,
                    ItemsCount = r.Items.Count
                })
                .ToListAsync();
        }

        public async Task<string> GenerateNextSalesReturnNumberAsync()
        {
            var prefix = $"RET-{DateTime.UtcNow:yyyyMM}-";
            var count = await _context.InvoiceReturns
                .CountAsync(r => r.ReturnNumber.StartsWith(prefix));
            return $"{prefix}{(count + 1):D4}";
        }
    }
}
