using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Production;
using ward.Models.Production;

namespace ward.Services.Production
{
    public class BouquetService : IBouquetService
    {
        private readonly ApplicationDbContext _context;

        public BouquetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<BouquetRecipeListItemViewModel>> GetAllRecipesAsync()
        {
            var recipes = await _context.BouquetRecipes
                .Include(r => r.BouquetProduct)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ComponentProduct)
                .AsNoTracking()
                .OrderBy(r => r.BouquetProduct.Name)
                .ToListAsync();

            var list = new List<BouquetRecipeListItemViewModel>();
            foreach (var r in recipes)
            {
                decimal compCostTotal = 0;
                foreach (var item in r.Items)
                {
                    var effQty = item.QuantityRequired * (1 + (item.WasteMarginPercentage / 100m));
                    var uCost = item.ComponentProduct.CurrentAverageCost > 0 
                        ? item.ComponentProduct.CurrentAverageCost 
                        : item.ComponentProduct.UnitPrice;
                    compCostTotal += effQty * uCost;
                }

                var estimatedUnitCost = compCostTotal + r.LaborCost + r.PackagingCost;

                list.Add(new BouquetRecipeListItemViewModel
                {
                    RecipeId = r.Id,
                    BouquetProductId = r.BouquetProductId,
                    BouquetProductName = r.BouquetProduct.Name,
                    BouquetSKU = r.BouquetProduct.SKU,
                    SellingPrice = r.BouquetProduct.UnitPrice,
                    EstimatedCost = Math.Round(estimatedUnitCost, 2),
                    ComponentCount = r.Items.Count,
                    Version = r.Version,
                    IsActive = r.IsActive,
                    UpdatedAtUtc = r.UpdatedAtUtc ?? r.CreatedAtUtc
                });
            }

            return list;
        }

        public async Task<BouquetRecipeFormViewModel?> GetRecipeByBouquetProductIdAsync(int bouquetProductId)
        {
            var recipe = await _context.BouquetRecipes
                .Include(r => r.BouquetProduct)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ComponentProduct)
                        .ThenInclude(cp => cp.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.BouquetProductId == bouquetProductId);

            if (recipe == null)
            {
                var bouquetProduct = await _context.Products.FindAsync(bouquetProductId);
                if (bouquetProduct == null) return null;

                return new BouquetRecipeFormViewModel
                {
                    BouquetProductId = bouquetProduct.Id,
                    BouquetProductName = bouquetProduct.Name,
                    BouquetSKU = bouquetProduct.SKU,
                    SellingPrice = bouquetProduct.UnitPrice,
                    Version = 1,
                    IsActive = true,
                    Items = new List<RecipeItemFormViewModel>()
                };
            }

            return MapToFormViewModel(recipe);
        }

        public async Task<BouquetRecipeFormViewModel?> GetRecipeByIdAsync(int recipeId)
        {
            var recipe = await _context.BouquetRecipes
                .Include(r => r.BouquetProduct)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ComponentProduct)
                        .ThenInclude(cp => cp.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null) return null;
            return MapToFormViewModel(recipe);
        }

        private static BouquetRecipeFormViewModel MapToFormViewModel(BouquetRecipe recipe)
        {
            return new BouquetRecipeFormViewModel
            {
                Id = recipe.Id,
                BouquetProductId = recipe.BouquetProductId,
                BouquetProductName = recipe.BouquetProduct.Name,
                BouquetSKU = recipe.BouquetProduct.SKU,
                SellingPrice = recipe.BouquetProduct.UnitPrice,
                LaborCost = recipe.LaborCost,
                PackagingCost = recipe.PackagingCost,
                Instructions = recipe.Instructions,
                IsActive = recipe.IsActive,
                Version = recipe.Version,
                Items = recipe.Items.Select(i => new RecipeItemFormViewModel
                {
                    Id = i.Id,
                    ComponentProductId = i.ComponentProductId,
                    ComponentProductName = i.ComponentProduct.Name,
                    ComponentSKU = i.ComponentProduct.SKU,
                    UnitSymbol = i.ComponentProduct.UnitOfMeasure?.Symbol ?? "pcs",
                    QuantityRequired = i.QuantityRequired,
                    WasteMarginPercentage = i.WasteMarginPercentage,
                    UnitCost = i.ComponentProduct.CurrentAverageCost > 0 ? i.ComponentProduct.CurrentAverageCost : i.ComponentProduct.UnitPrice
                }).ToList()
            };
        }

        public async Task<(bool Success, string? ErrorMessage, int RecipeId)> SaveRecipeAsync(BouquetRecipeFormViewModel model, string? userId)
        {
            if (model.BouquetProductId <= 0)
                return (false, "A valid bouquet product must be selected.", 0);

            var bouquetProduct = await _context.Products
                .Include(p => p.UnitOfMeasure)
                .FirstOrDefaultAsync(p => p.Id == model.BouquetProductId);

            if (bouquetProduct == null || !bouquetProduct.IsActive)
                return (false, "The selected bouquet product does not exist or is inactive.", 0);

            if (bouquetProduct.ProductType != ProductType.Bouquet)
                return (false, "Only products classified as 'Bouquet' can have bouquet recipes.", 0);

            if (model.Items == null || model.Items.Count == 0)
                return (false, "At least one component product is required in the recipe.", 0);

            // Check for direct self-reference
            if (model.Items.Any(i => i.ComponentProductId == model.BouquetProductId))
                return (false, "A bouquet cannot contain itself as a component.", 0);

            // Check for duplicate components in the recipe
            var componentIds = model.Items.Select(i => i.ComponentProductId).ToList();
            if (componentIds.Count != componentIds.Distinct().Count())
                return (false, "Duplicate component detected in recipe. Each component product may only appear once.", 0);

            // Validate component items exist and are not services
            var componentsInDb = await _context.Products
                .Include(p => p.UnitOfMeasure)
                .Where(p => componentIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var item in model.Items)
            {
                if (!componentsInDb.TryGetValue(item.ComponentProductId, out var compProduct))
                    return (false, $"Component product ID {item.ComponentProductId} was not found.", 0);

                if (!compProduct.IsActive)
                    return (false, $"Component product '{compProduct.Name}' is inactive.", 0);

                if (compProduct.ProductType == ProductType.Service)
                    return (false, $"Component product '{compProduct.Name}' is a service and cannot be a physical recipe component.", 0);

                if (item.QuantityRequired <= 0)
                    return (false, $"Quantity for component '{compProduct.Name}' must be greater than zero.", 0);

                if (item.WasteMarginPercentage < 0 || item.WasteMarginPercentage > 100)
                    return (false, $"Waste margin for component '{compProduct.Name}' must be between 0% and 100%.", 0);
            }

            // Check for circular recipes (depth-first cycle detection)
            var hasCycle = await CheckForCircularDependencyAsync(model.BouquetProductId, componentIds);
            if (hasCycle.HasCycle)
                return (false, $"Circular recipe rejected: {hasCycle.CyclePath}", 0);

            // Load existing recipe if any
            var recipe = await _context.BouquetRecipes
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.BouquetProductId == model.BouquetProductId);

            int versionNumber;
            if (recipe == null)
            {
                versionNumber = 1;
                recipe = new BouquetRecipe
                {
                    BouquetProductId = model.BouquetProductId,
                    Version = versionNumber,
                    LaborCost = model.LaborCost,
                    PackagingCost = model.PackagingCost,
                    Instructions = model.Instructions,
                    IsActive = model.IsActive,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _context.BouquetRecipes.Add(recipe);
            }
            else
            {
                versionNumber = recipe.Version + 1;
                recipe.Version = versionNumber;
                recipe.LaborCost = model.LaborCost;
                recipe.PackagingCost = model.PackagingCost;
                recipe.Instructions = model.Instructions;
                recipe.IsActive = model.IsActive;
                recipe.UpdatedAtUtc = DateTime.UtcNow;

                _context.RecipeItems.RemoveRange(recipe.Items);
                recipe.Items.Clear();
            }

            // Build recipe items and calculate costs
            decimal totalComponentCost = 0;
            var snapshotItemsList = new List<object>();

            foreach (var item in model.Items)
            {
                var compProduct = componentsInDb[item.ComponentProductId];
                var effectiveQty = item.QuantityRequired * (1 + (item.WasteMarginPercentage / 100m));
                var unitCost = compProduct.CurrentAverageCost > 0 ? compProduct.CurrentAverageCost : compProduct.UnitPrice;
                var totalItemCost = effectiveQty * unitCost;

                totalComponentCost += totalItemCost;

                recipe.Items.Add(new RecipeItem
                {
                    ComponentProductId = item.ComponentProductId,
                    QuantityRequired = item.QuantityRequired,
                    WasteMarginPercentage = item.WasteMarginPercentage
                });

                snapshotItemsList.Add(new
                {
                    ComponentProductId = compProduct.Id,
                    ProductName = compProduct.Name,
                    SKU = compProduct.SKU,
                    UnitSymbol = compProduct.UnitOfMeasure?.Symbol ?? "pcs",
                    QuantityRequired = item.QuantityRequired,
                    WasteMarginPercentage = item.WasteMarginPercentage,
                    EffectiveQuantity = effectiveQty,
                    UnitCost = unitCost,
                    TotalCost = Math.Round(totalItemCost, 4)
                });
            }

            var totalEstimatedUnitCost = Math.Round(totalComponentCost + model.LaborCost + model.PackagingCost, 2);

            // Update bouquet product estimated average cost
            bouquetProduct.CurrentAverageCost = totalEstimatedUnitCost;

            // Preserve historical snapshot (immutable)
            var snapshot = new RecipeSnapshot
            {
                BouquetProductId = model.BouquetProductId,
                Version = versionNumber,
                LaborCost = model.LaborCost,
                PackagingCost = model.PackagingCost,
                ComponentCostTotal = Math.Round(totalComponentCost, 2),
                TotalUnitCost = totalEstimatedUnitCost,
                Notes = string.IsNullOrWhiteSpace(model.SnapshotNotes) 
                    ? $"Recipe v{versionNumber} saved with {model.Items.Count} components." 
                    : model.SnapshotNotes.Trim(),
                SnapshotDateUtc = DateTime.UtcNow,
                CreatedByUserId = userId,
                ItemsSnapshotJson = JsonSerializer.Serialize(snapshotItemsList)
            };
            _context.RecipeSnapshots.Add(snapshot);

            await _context.SaveChangesAsync();

            return (true, null, recipe.Id);
        }

        private async Task<(bool HasCycle, string? CyclePath)> CheckForCircularDependencyAsync(int targetBouquetProductId, List<int> componentProductIds)
        {
            // Load all recipes where BouquetProductId is in our component set or downstream
            var allRecipes = await _context.BouquetRecipes
                .Include(r => r.Items)
                    .ThenInclude(i => i.ComponentProduct)
                .AsNoTracking()
                .ToListAsync();

            var recipeMap = allRecipes.ToDictionary(r => r.BouquetProductId, r => r.Items.Select(i => i.ComponentProductId).ToList());

            var visited = new HashSet<int>();
            var path = new List<int> { targetBouquetProductId };

            foreach (var compId in componentProductIds)
            {
                if (CheckDfs(compId, targetBouquetProductId, recipeMap, visited, path))
                {
                    var productNames = await _context.Products
                        .Where(p => path.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, p => p.Name);

                    var namesPath = string.Join(" -> ", path.Select(id => productNames.TryGetValue(id, out var name) ? name : $"ID:{id}"));
                    return (true, namesPath);
                }
            }

            return (false, null);
        }

        private static bool CheckDfs(int currentId, int targetId, Dictionary<int, List<int>> recipeMap, HashSet<int> visited, List<int> path)
        {
            path.Add(currentId);
            if (currentId == targetId)
                return true;

            if (visited.Contains(currentId))
            {
                path.RemoveAt(path.Count - 1);
                return false;
            }

            visited.Add(currentId);

            if (recipeMap.TryGetValue(currentId, out var subComponents))
            {
                foreach (var nextId in subComponents)
                {
                    if (CheckDfs(nextId, targetId, recipeMap, visited, path))
                        return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteRecipeAsync(int recipeId)
        {
            var recipe = await _context.BouquetRecipes
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
                return (false, "Recipe not found.");

            _context.RecipeItems.RemoveRange(recipe.Items);
            _context.BouquetRecipes.Remove(recipe);
            await _context.SaveChangesAsync();

            return (true, null);
        }

        public async Task<RecipeCalculationResultDto?> CalculateRequirementsAsync(int bouquetProductId, decimal bouquetQuantity)
        {
            if (bouquetQuantity <= 0) bouquetQuantity = 1;

            var recipe = await _context.BouquetRecipes
                .Include(r => r.BouquetProduct)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ComponentProduct)
                        .ThenInclude(cp => cp.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.BouquetProductId == bouquetProductId && r.IsActive);

            if (recipe == null) return null;

            var result = new RecipeCalculationResultDto
            {
                BouquetProductId = recipe.BouquetProductId,
                BouquetProductName = recipe.BouquetProduct.Name,
                RequestedBouquetQuantity = bouquetQuantity,
                UnitLaborCost = recipe.LaborCost,
                UnitPackagingCost = recipe.PackagingCost
            };

            decimal unitComponentsCost = 0;
            foreach (var item in recipe.Items)
            {
                var effectiveQtyPerBouquet = item.QuantityRequired * (1 + (item.WasteMarginPercentage / 100m));
                var totalQty = effectiveQtyPerBouquet * bouquetQuantity;
                var unitCost = item.ComponentProduct.CurrentAverageCost > 0 ? item.ComponentProduct.CurrentAverageCost : item.ComponentProduct.UnitPrice;
                var totalItemCost = totalQty * unitCost;

                unitComponentsCost += (effectiveQtyPerBouquet * unitCost);

                result.Components.Add(new RecipeCalculationItemDto
                {
                    ComponentProductId = item.ComponentProductId,
                    ProductName = item.ComponentProduct.Name,
                    SKU = item.ComponentProduct.SKU,
                    UnitSymbol = item.ComponentProduct.UnitOfMeasure?.Symbol ?? "pcs",
                    QuantityPerBouquet = item.QuantityRequired,
                    WasteMarginPercentage = item.WasteMarginPercentage,
                    EffectiveQuantityPerBouquet = effectiveQtyPerBouquet,
                    TotalQuantityRequired = totalQty,
                    UnitCost = unitCost,
                    TotalComponentCost = Math.Round(totalItemCost, 2)
                });
            }

            result.UnitComponentsCost = Math.Round(unitComponentsCost, 2);
            return result;
        }

        public async Task<ComponentAvailabilityResultDto?> ValidateAvailabilityAsync(int bouquetProductId, decimal bouquetQuantity, int warehouseId)
        {
            var calc = await CalculateRequirementsAsync(bouquetProductId, bouquetQuantity);
            if (calc == null) return null;

            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null) return null;

            var componentIds = calc.Components.Select(c => c.ComponentProductId).ToList();
            var stockItems = await _context.StockItems
                .Where(s => s.WarehouseId == warehouseId && componentIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, s => s.QuantityOnHand);

            var availabilityList = new List<ComponentAvailabilityItemDto>();
            bool allAvailable = true;

            foreach (var comp in calc.Components)
            {
                stockItems.TryGetValue(comp.ComponentProductId, out var currentStock);
                var isSufficient = currentStock >= comp.TotalQuantityRequired;
                if (!isSufficient) allAvailable = false;

                availabilityList.Add(new ComponentAvailabilityItemDto
                {
                    ComponentProductId = comp.ComponentProductId,
                    ProductName = comp.ProductName,
                    SKU = comp.SKU,
                    UnitSymbol = comp.UnitSymbol,
                    QuantityRequired = comp.TotalQuantityRequired,
                    AvailableStock = currentStock
                });
            }

            return new ComponentAvailabilityResultDto
            {
                BouquetProductId = bouquetProductId,
                BouquetProductName = calc.BouquetProductName,
                WarehouseId = warehouseId,
                WarehouseName = warehouse.Name,
                RequestedQuantity = bouquetQuantity,
                AllAvailable = allAvailable,
                Components = availabilityList
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> AssembleBouquetsAsync(AssembleBouquetsViewModel request, string? userId)
        {
            if (request.QuantityToAssemble <= 0)
                return (false, "Quantity to assemble must be greater than zero.");

            // Idempotency check: prevent duplicate stock movement execution
            if (!string.IsNullOrWhiteSpace(request.IdempotencyToken))
            {
                var existingMovement = await _context.StockMovements
                    .FirstOrDefaultAsync(m => m.ReferenceId == request.IdempotencyToken && m.ReferenceType == "BouquetAssembly");
                if (existingMovement != null)
                {
                    return (true, null); // Already successfully processed
                }
            }

            var recipe = await _context.BouquetRecipes
                .Include(r => r.BouquetProduct)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ComponentProduct)
                .FirstOrDefaultAsync(r => r.BouquetProductId == request.BouquetProductId && r.IsActive);

            if (recipe == null)
                return (false, "Active bouquet recipe not found for the specified product.");

            var warehouse = await _context.Warehouses.FindAsync(request.WarehouseId);
            if (warehouse == null || !warehouse.IsActive)
                return (false, "Warehouse not found or is inactive.");

            // Validate component stock in the warehouse
            var componentIds = recipe.Items.Select(i => i.ComponentProductId).ToList();
            var stockItems = await _context.StockItems
                .Where(s => s.WarehouseId == request.WarehouseId && componentIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId);

            var shortages = new List<string>();
            foreach (var item in recipe.Items)
            {
                var requiredQty = item.QuantityRequired * (1 + (item.WasteMarginPercentage / 100m)) * request.QuantityToAssemble;
                stockItems.TryGetValue(item.ComponentProductId, out var stockItem);
                var availableStock = stockItem?.QuantityOnHand ?? 0;

                if (availableStock < requiredQty)
                {
                    shortages.Add($"{item.ComponentProduct.Name} (Required: {requiredQty:G29}, Available: {availableStock:G29})");
                }
            }

            if (shortages.Count > 0)
            {
                return (false, $"Insufficient component stock in warehouse '{warehouse.Name}': " + string.Join("; ", shortages));
            }

            // Execute atomic assembly: deduct component stock, add finished bouquet stock
            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                decimal totalComponentsCostForAssembly = 0;

                // 1. Deduct component stocks and log BouquetAssemblyOut movements
                foreach (var item in recipe.Items)
                {
                    var requiredQty = item.QuantityRequired * (1 + (item.WasteMarginPercentage / 100m)) * request.QuantityToAssemble;
                    var stockItem = stockItems[item.ComponentProductId];

                    stockItem.QuantityOnHand -= requiredQty;

                    var compUnitCost = item.ComponentProduct.CurrentAverageCost > 0
                        ? item.ComponentProduct.CurrentAverageCost
                        : item.ComponentProduct.UnitPrice;

                    totalComponentsCostForAssembly += requiredQty * compUnitCost;

                    var outMovement = new StockMovement
                    {
                        ProductId = item.ComponentProductId,
                        WarehouseId = request.WarehouseId,
                        MovementType = MovementType.BouquetAssemblyOut,
                        Quantity = -requiredQty,
                        UnitCost = compUnitCost,
                        ReferenceType = "BouquetAssembly",
                        ReferenceId = request.IdempotencyToken,
                        Notes = $"Assembly of {request.QuantityToAssemble}x {recipe.BouquetProduct.Name}",
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedByUserId = userId
                    };
                    _context.StockMovements.Add(outMovement);
                }

                // 2. Add finished bouquet stock and log BouquetAssemblyIn movement
                var bouquetStockItem = await _context.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == request.BouquetProductId && s.WarehouseId == request.WarehouseId);

                if (bouquetStockItem == null)
                {
                    bouquetStockItem = new StockItem
                    {
                        ProductId = request.BouquetProductId,
                        WarehouseId = request.WarehouseId,
                        QuantityOnHand = 0,
                        QuantityReserved = 0
                    };
                    _context.StockItems.Add(bouquetStockItem);
                }

                // Calculate unit cost for this assembly run (components + labor + packaging)
                var unitCost = Math.Round((totalComponentsCostForAssembly / request.QuantityToAssemble) + recipe.LaborCost + recipe.PackagingCost, 2);

                // Update bouquet product's weighted average cost
                var currentTotalBouquetStockList = await _context.StockItems
                    .Where(s => s.ProductId == request.BouquetProductId)
                    .Select(s => s.QuantityOnHand)
                    .ToListAsync();
                var currentTotalBouquetStock = currentTotalBouquetStockList.Sum();

                if (currentTotalBouquetStock + request.QuantityToAssemble > 0)
                {
                    var totalValue = (currentTotalBouquetStock * recipe.BouquetProduct.CurrentAverageCost) + (request.QuantityToAssemble * unitCost);
                    recipe.BouquetProduct.CurrentAverageCost = Math.Round(totalValue / (currentTotalBouquetStock + request.QuantityToAssemble), 2);
                }
                else
                {
                    recipe.BouquetProduct.CurrentAverageCost = unitCost;
                }

                bouquetStockItem.QuantityOnHand += request.QuantityToAssemble;

                var inMovement = new StockMovement
                {
                    ProductId = request.BouquetProductId,
                    WarehouseId = request.WarehouseId,
                    MovementType = MovementType.BouquetAssemblyIn,
                    Quantity = request.QuantityToAssemble,
                    UnitCost = unitCost,
                    ReferenceType = "BouquetAssembly",
                    ReferenceId = request.IdempotencyToken,
                    Notes = $"Assembled {request.QuantityToAssemble} bouquets. {request.Notes}".Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };
                _context.StockMovements.Add(inMovement);

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return (false, $"Bouquet assembly failed: {ex.Message}");
            }
        }

        public async Task<List<RecipeSnapshotDto>> GetRecipeSnapshotsAsync(int bouquetProductId)
        {
            var snapshots = await _context.RecipeSnapshots
                .Where(s => s.BouquetProductId == bouquetProductId)
                .OrderByDescending(s => s.Version)
                .ThenByDescending(s => s.SnapshotDateUtc)
                .AsNoTracking()
                .ToListAsync();

            return snapshots.Select(s => new RecipeSnapshotDto
            {
                Id = s.Id,
                Version = s.Version,
                SnapshotDateUtc = s.SnapshotDateUtc,
                LaborCost = s.LaborCost,
                PackagingCost = s.PackagingCost,
                ComponentCostTotal = s.ComponentCostTotal,
                TotalUnitCost = s.TotalUnitCost,
                Notes = s.Notes,
                CreatedByUserId = s.CreatedByUserId,
                ItemsSnapshotJson = s.ItemsSnapshotJson
            }).ToList();
        }

        public async Task<List<Product>> GetAvailableBouquetProductsAsync(int? currentBouquetProductId = null)
        {
            // Bouquet products that don't yet have a recipe, OR the product currently being edited
            var usedBouquetProductIds = await _context.BouquetRecipes
                .Select(r => r.BouquetProductId)
                .ToListAsync();

            var query = _context.Products
                .Include(p => p.UnitOfMeasure)
                .Where(p => p.ProductType == ProductType.Bouquet && p.IsActive);

            if (currentBouquetProductId.HasValue)
            {
                query = query.Where(p => p.Id == currentBouquetProductId.Value || !usedBouquetProductIds.Contains(p.Id));
            }
            else
            {
                query = query.Where(p => !usedBouquetProductIds.Contains(p.Id));
            }

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<List<Product>> GetAvailableComponentProductsAsync()
        {
            // All active non-service products can serve as recipe components
            return await _context.Products
                .Include(p => p.UnitOfMeasure)
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.ProductType != ProductType.Service)
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }
    }
}
