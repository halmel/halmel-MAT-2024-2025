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


    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public int Price { get; set; }


        // Link to the ProductTemplate used by this product
        public int? ProductTemplateId { get; set; }

        [ForeignKey(nameof(ProductTemplateId))]
        public ProductTemplate? Template { get; set; }




        // Backing field for Amount
        private int? _amount =0;

        /// <summary>
        /// Only accessible if ProductType is not Chick.
        /// Throws if accessed on a Chick product.
        /// </summary>
        [NotMapped]
        public int Amount
        {
            get
            {
                if (StockHandlingType == ProductStockHandlingType.Chick)
                    throw new InvalidOperationException("Chick-type products do not use Amount. Use HatchingEvents instead.");

                if (!_amount.HasValue)
                    throw new InvalidOperationException("Amount is required for non-Chick products.");

                return _amount.Value;
            }
            set
            {
                if (StockHandlingType == ProductStockHandlingType.Chick)
                    throw new InvalidOperationException("Cannot set Amount for Chick-type products. Use HatchingEvents instead.");

                _amount = value;
            }
        }

        // This field is actually mapped to the DB
        [Column("Amount")]
        public int? AmountStorage
        {
            get => _amount;
            set => _amount = value;
        }




        [Required]
        public ProductStockHandlingType StockHandlingType { get; set; } = ProductStockHandlingType.Unknown;

        [Required]
        public ProductCategoryType CategoryType { get; set; } = ProductCategoryType.Unknown;


        // If needed for general products; for chicks, we rely on HatchingEvents
        public DateTimeOffset? HatchDate { get; set; }

        public int[] ImageIds { get; set; } = Array.Empty<int>();
        public int[] PageIds { get; set; } = Array.Empty<int>();






        /// <summary>
        /// Only used if Type == ProductType.Chick.
        /// Holds all the week‐by‐week hatching events for the season.
        /// </summary>
        public List<HatchingEvent> HatchingEvents { get; set; } = new List<HatchingEvent>();

        // === Helper Methods for Chick‐Type Products ===

        /// <summary>
        /// Automatically generates a weekly series of hatching events between <paramref name="startDate"/> and <paramref name="endDate"/>,
        /// inclusive. All weeks share the same <paramref name="predictedQuantityPerWeek"/>.
        /// Only valid if Type == Chick. Season must lie between early spring (March 1) and mid summer (July 31).
        /// </summary>
        /// <param name="startDate">The first hatch date (first week). Must be on or after March 1.</param>
        /// <param name="endDate">The final hatch date. Must be on or before July 31, and >= startDate.</param>
        /// <param name="predictedQuantityPerWeek">Predicted number of chicks for each week.</param>
        /// <exception cref="InvalidOperationException">Thrown if product is not a Chick or dates are out of season bounds.</exception>
        public void GenerateSeasonalHatchingEvents(
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            int predictedQuantityPerWeek)
        {
            if (StockHandlingType != ProductStockHandlingType.Chick)
                throw new InvalidOperationException("Only chick products can have seasonal hatching.");

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


        /// <summary>
        /// Deletes all future hatching events in the same generation as the provided event.
        /// You can choose to include or exclude the provided event itself.
        /// </summary>
        /// <param name="startEvent">The hatching event to use as a starting point.</param>
        /// <param name="includeStartEvent">Whether to include the given event itself in the deletion.</param>
        public void DeleteEventsFrom(HatchingEvent startEvent, bool includeStartEvent)
        {
            if (StockHandlingType != ProductStockHandlingType.Chick)
                throw new InvalidOperationException();

            var cutoff = startEvent.HatchDate;
            var generationId = startEvent.GenerationId;

            HatchingEvents.RemoveAll(evt =>
                evt.GenerationId == generationId &&
                evt.HatchDate >= (includeStartEvent ? cutoff : cutoff.AddDays(1)) &&
                !evt.HasActualStock); // skip events with manually confirmed stock
        }


        /// <summary>
        /// Moves all future hatching events in the same generation to a different day of the week,
        /// starting from the given event. Manual-stock events are preserved.
        /// </summary>
        /// <param name="startEvent">The hatching event to use as a starting point.</param>
        /// <param name="newDay">The new day of the week to move events to.</param>
        /// <param name="includeStartEvent">Whether to include the provided event itself in the move.</param>
        public void MoveEventsFromToNewDay(HatchingEvent startEvent, DayOfWeek newDay, bool includeStartEvent)
        {
            if (StockHandlingType != ProductStockHandlingType.Chick)
                throw new InvalidOperationException();

            var cutoff = startEvent.HatchDate;
            var generationId = startEvent.GenerationId;

            foreach (var evt in HatchingEvents)
            {
                if (evt.GenerationId != generationId)
                    continue;

                if (evt.HatchDate < cutoff || (!includeStartEvent && evt.HatchDate == cutoff))
                    continue;

                if (evt.HasActualStock)
                    continue;

                int offset = ((int)newDay - (int)evt.HatchDate.DayOfWeek + 7) % 7;
                evt.HatchDate = evt.HatchDate.AddDays(offset);
            }
        }


        /// <summary>
        /// Returns all hatching events for this product (empty list if none or not a Chick).
        /// </summary>
        public IReadOnlyList<HatchingEvent> GetAllHatchingEvents()
        {
            return HatchingEvents.AsReadOnly();
        }

        /// <summary>
        /// Adds a single hatching event (one week) manually, specifying date and predicted quantity.
        /// </summary>
        /// <param name="hatchDate">Specific date in the week.</param>
        /// <param name="predictedQuantity">Forecasted quantity.</param>
        public void AddHatchingEvent(DateTimeOffset hatchDate, int predictedQuantity, Guid generationId)
        {
            if (StockHandlingType != ProductStockHandlingType.Chick)
                throw new InvalidOperationException();

            HatchingEvents.Add(new HatchingEvent
            {
                ProductId = this.Id,
                HatchDate = hatchDate,
                PredictedStock = predictedQuantity,
                ActualStock = null,
                GenerationId = generationId
            });
        }


        /// <summary>
        /// Edits an existing HatchingEvent by copying over all its fields (except the Id/ProductId remain the same).
        /// </summary>
        /// <param name="updatedEvent">A HatchingEvent object containing its own Id (must match one in the list), and new values for HatchDate, PredictedStock, and optionally ActualStock.</param>
        /// <exception cref="InvalidOperationException">Thrown if product is not a Chick or the event is not found.</exception>
        public void EditHatchingEvent(HatchingEvent updatedEvent)
        {
            if (StockHandlingType != ProductStockHandlingType.Chick)
                throw new InvalidOperationException("Cannot edit hatching events on non‐Chick products.");

            var existing = HatchingEvents.Find(e => e.Id == updatedEvent.Id);
            if (existing == null)
                throw new InvalidOperationException($"No hatching event found with ID = {updatedEvent.Id}.");

            existing.HatchDate = updatedEvent.HatchDate;
            existing.PredictedStock = updatedEvent.PredictedStock;
            existing.ActualStock = updatedEvent.ActualStock;
        }
    }

    public enum StockStatus
    {
        Green,
        Yellow,
        Red
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
        public Product? Product { get; set; }

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
