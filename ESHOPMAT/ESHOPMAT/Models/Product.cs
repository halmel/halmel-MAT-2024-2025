using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESHOPMAT.Models
{
    public enum ProductStockHandlingType
    {
        Chick,
        Default, // Anything that uses Amount
        Unknown
    }

    public enum ProductCategoryType
    {
        Chick,
        Chicken,
        Feed,
        Item,
        Unknown
    }
    public enum StockStatus
    {
        Green,
        Yellow,
        Red
    }

    public abstract class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public int[] ImageIds { get; set; } = Array.Empty<int>();

        [Required]
        public int Price { get; set; }

        public int? ProductTemplateId { get; set; }

        [ForeignKey(nameof(ProductTemplateId))]
        public ProductTemplate? Template { get; set; }

        [Required]
        public ProductCategoryType CategoryType { get; set; } = ProductCategoryType.Unknown;

        [Required]
        public ProductStockHandlingType StockHandlingType { get; protected set; } = ProductStockHandlingType.Unknown;

        // HatchDate can be used by both types if needed
        public DateTimeOffset? HatchDate { get; set; }

        private int? _amount = 0;

        [NotMapped]
        public int Amount
        {
            get => _amount ?? throw new InvalidOperationException("Amount is required.");
            set => _amount = value;
        }

        [Column("Amount")]
        public int? AmountStorage
        {
            get => _amount;
            set => _amount = value;
        }
    }
    public class StandardProduct : Product
    {
        public StandardProduct()
        {
            StockHandlingType = ProductStockHandlingType.Default;
        }


    }
    public class ChickProduct : Product
    {

        public ChickProduct()
        {
            StockHandlingType = ProductStockHandlingType.Chick;
        }

        public List<HatchingEvent> HatchingEvents { get; set; } = new List<HatchingEvent>();

        public void GenerateSeasonalHatchingEvents(DateTimeOffset startDate, DateTimeOffset endDate, int predictedQuantityPerWeek)
        {
            if (startDate.Month < 3 || endDate.Month > 7 || endDate < startDate)
                throw new InvalidOperationException("Season must be between March 1 and July 31.");

            var generationId = Guid.NewGuid();
            var cursor = startDate;

            while (cursor <= endDate)
            {
                HatchingEvents.Add(new HatchingEvent
                {
                    ProductId = this.Id,
                    HatchDate = cursor,
                    PredictedStock = predictedQuantityPerWeek,
                    ActualStock = null,
                    GenerationId = generationId
                });
                cursor = cursor.AddDays(7);
            }
        }

        public void DeleteEventsFrom(HatchingEvent startEvent, bool includeStartEvent)
        {
            var cutoff = startEvent.HatchDate;
            var generationId = startEvent.GenerationId;

            HatchingEvents.RemoveAll(evt =>
                evt.GenerationId == generationId &&
                evt.HatchDate >= (includeStartEvent ? cutoff : cutoff.AddDays(1)) &&
                !evt.HasActualStock);
        }

        public void MoveEventsFromToNewDay(HatchingEvent startEvent, DayOfWeek newDay, bool includeStartEvent)
        {
            var cutoff = startEvent.HatchDate;
            var generationId = startEvent.GenerationId;

            foreach (var evt in HatchingEvents)
            {
                if (evt.GenerationId != generationId ||
                    evt.HatchDate < cutoff ||
                    (!includeStartEvent && evt.HatchDate == cutoff) ||
                    evt.HasActualStock)
                    continue;

                int offset = ((int)newDay - (int)evt.HatchDate.DayOfWeek + 7) % 7;
                evt.HatchDate = evt.HatchDate.AddDays(offset);
            }
        }

        public void AddHatchingEvent(DateTimeOffset hatchDate, int predictedQuantity, Guid generationId)
        {
            HatchingEvents.Add(new HatchingEvent
            {
                ProductId = this.Id,
                HatchDate = hatchDate,
                PredictedStock = predictedQuantity,
                GenerationId = generationId
            });
        }

        public void EditHatchingEvent(HatchingEvent updatedEvent)
        {
            var existing = HatchingEvents.Find(e => e.Id == updatedEvent.Id);
            if (existing == null)
                throw new InvalidOperationException($"No hatching event found with ID = {updatedEvent.Id}.");

            existing.HatchDate = updatedEvent.HatchDate;
            existing.PredictedStock = updatedEvent.PredictedStock;
            existing.ActualStock = updatedEvent.ActualStock;
        }

        public IReadOnlyList<HatchingEvent> GetAllHatchingEvents()
        {
            return HatchingEvents.AsReadOnly();
        }
    }


    public class HatchingEvent
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key back to Product.Id
        /// </summary>
        [Required]
        public int ProductId { get; set; }

        /// <summary>
        /// Navigation property to the parent Product
        /// </summary>
        public ChickProduct? Product { get; set; }

        /// <summary>
        /// The date (any day of that week) on which the chicks hatch.
        /// </summary>
        [Required]
        public DateTimeOffset HatchDate { get; set; }


        /// <summary>
        /// The very first predicted stock we set when the event was created.
        /// This never changes, even if PredictedStock is edited later.
        /// </summary>
        [Required]
        public int PredictedStock { get; set; }


        /// <summary>
        /// Identifies which generation of hatching this event belongs to.
        /// All events created in a single generation batch share the same GenerationId.
        /// </summary>
        [Required]
        public Guid GenerationId { get; set; }


        /// <summary>
        /// The manually counted “actual” number that hatched, as soon as a user sets it.
        /// </summary>
        public int? ActualStock { get; set; }

        private int? availableStock;
        /// <summary>
        /// How many chicks are currently still available (i.e., not sold yet).
        /// This must be decremented externally whenever sales occur.
        /// </summary>
        [Required]
        public int AvailableStock
        {
            get
            {
                if (availableStock.HasValue)
                {
                    return (int)availableStock;

                }
                else
                {
                    if (ActualStock.HasValue)
                    {
                        availableStock = (int)ActualStock;
                        return (int)availableStock;
                    }
                    else
                    {
                        availableStock = PredictedStock;
                        return (int)availableStock;
                    }

                }
            }
            set
            {
                availableStock = value;

            }
        }


        /// <summary>
        /// True if we ever captured an original, manual actual stock value.
        /// (Equivalent to OriginalActualStock.HasValue.)
        /// </summary>
        [NotMapped]
        public bool HasActualStock => ActualStock.HasValue;



        public void SetActualStock(int actualQuantity)
        {
            ActualStock = actualQuantity;
            AvailableStock = (int)ActualStock - (PredictedStock - AvailableStock);
        }
        public void ReduceAvailableStock(int actualQuantity)
        {
            AvailableStock = AvailableStock - actualQuantity;
        }


        /// <summary>
        /// Returns a StockStatus (Green, Yellow, or Red) based on current availability.
        ///
        /// Uses “baseStock” = OriginalActualStock (if set); otherwise OriginalPredictedStock.
        /// Then:
        ///   - If there is NO actual stock (i.e. we rely on predicted) AND AvailableStock &lt; 10% of base → Red.
        ///   - Else if AvailableStock &lt; 50% of base → Yellow.
        ///   - Otherwise → Green.
        /// </summary>
        /// <returns>Green, Yellow, or Red</returns>
        public StockStatus GetStockStatus()
        {
            // Determine which number to treat as “base stock”:
            // – If a concrete (actual) count exists, use that.
            // – Otherwise, fall back to the original predicted.
            int baseStock = HasActualStock
                ? ActualStock!.Value
                : PredictedStock;

            if (baseStock <= 0)
            {
                // Avoid division by zero; if somehow baseStock is zero, we treat it as “no stock → Red.”
                return StockStatus.Red;
            }

            // If we do not yet have any ActualStock (so we’re still relying on predicted),
            // and there is less than 10% of that stock left, show RED.
            if (!HasActualStock)
            {
                if (AvailableStock < Math.Ceiling(baseStock * 0.10))
                {
                    return StockStatus.Red;
                }
            }

            // If less than 50% of base stock remains, show YELLOW.
            if (AvailableStock < Math.Ceiling(baseStock * 0.50))
            {
                return StockStatus.Yellow;
            }

            // Otherwise, GREEN.
            return StockStatus.Green;
        }
    }









    public class ProductTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // Foreign key to PageElement
        public int? PageElementId { get; set; }

        [ForeignKey(nameof(PageElementId))]
        public PageContent? Page { get; set; }

        // Linked products
        public ICollection<Product> Products { get; set; } = new List<Product>();

        /// <summary>
        /// Constructor to initialize a ProductTemplate with name, optional PageElement, and list of products
        /// </summary>
        public ProductTemplate(string name, IEnumerable<Product> products, PageContent? page = null)
        {
            Name = name;
            Page = page;
            PageElementId = page?.Id;

            foreach (var product in products)
            {
                product.Template = this;
                Products.Add(product);
            }
        }

        // Parameterless constructor for EF
        public ProductTemplate() { }

        /// <summary>
        /// Static helper to return a list of all template names
        /// </summary>
        public static async Task<List<string>> GetAllTemplateNamesAsync(AppDbContext context)
        {
            return await context.ProductTemplates
                .Select(pt => pt.Name)
                .Distinct()
                .ToListAsync();
        }
    }


}
