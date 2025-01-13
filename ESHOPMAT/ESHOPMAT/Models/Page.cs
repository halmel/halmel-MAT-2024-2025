using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace ESHOPMAT.Models
{
    public class PageContentDictionary
    {
        [Key]
        public Guid SharedId { get; set; }
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    public class PageContent
    {
        public int Id { get; set; }

        // Shared identifier linked to dictionary
        public Guid SharedId { get; set; }

        public PageDbContext Context { get; set; }

        [NotMapped]
        public Dictionary<string, string> Data
        {
            get
            {
                    return PageContentDictionaryCache.Get(SharedId)?.Data;
                
            }
            set
            {
                PageContentDictionaryCache.Update(SharedId, value, Context);
            }
        }

        // Specifies the type of component (e.g., "Container", "Counter")
        [Required]
        public string Type { get; set; }

        // Unique name for the component instance
        [Required]
        public string Name { get; set; }

        // Parent ID for the recursive relationship
        public int? ParentId { get; set; }

        public PageContent? Parent { get; set; }

        // Recursive relationship for child components
        public List<PageContent> Children { get; set; } = new List<PageContent>();

        public bool IsRoot { get; set; } = true;
        public PageContent()
        {
        }

        // Constructor for creating root component
        // Constructor for creating root component
        public PageContent(PageSettings settings, PageDbContext c)
        {
            Context = c;
            if (settings.IsRoot)
            {
                SharedId = Guid.NewGuid();
                InitializeData();
            }
            else
            {
                SharedId = settings.ShareId;
            }

            // Initialize the cache if it is null
            if (PageContentDictionaryCache.Get(SharedId) == null)
            {
                PageContentDictionaryCache.Update(SharedId, new Dictionary<string, string>(), Context);
            }

            Name = settings.Name;
            Type = settings.Type.ToString();
            IsRoot = settings.IsRoot;
            InitializeComponentData(settings);
        }


        private void InitializeData()
        {
            if (PageContentDictionaryCache.Get(SharedId) == null)
            {
                PageContentDictionaryCache.Update(SharedId, new Dictionary<string, string>(), Context);
            }
        }

        public void SetValue(string key, string value)
        {
            var data = Data;
            data[Name + ":" + key] = value;
            Data = data;
        }

        public void SetValue(string key, string value, string name)
        {
            var data = Data;
            data[name + ":" + key] = value;
            Data = data;
        }
        /// <summary>
        /// Finds a child component by its unique name.
        /// </summary>
        /// <param name="name">The name of the child component to find.</param>
        /// <returns>The child component if found; otherwise, null.</returns>
        public PageContent GetChildByName(string name)
        {
            // Search in the immediate children
            var child = Children.FirstOrDefault(c => c.Name == name);
            if (child != null)
            {
                return child;
            }

            // Recursively search in the children's children
            foreach (var subChild in Children)
            {
                var found = subChild.GetChildByName(name);
                if (found != null)
                {
                    return found;
                }
            }

            // Return null if no match is found
            return null;
        }
        public void SetProductData(Product product)
        {
            SetValue("Title", product.Name, "Product");
            SetValue("Description", product.Description, "Product");
            SetValue("Image", product.ImageIds[0].ToString(), "Product");
            SetValue("Id", product.Id.ToString(), "Product");

        }

         
        private void InitializeComponentData(PageSettings settings)
        {

            // Use namespaced keys
            SetValue("type", settings.Type.ToString());

            switch (Type)
            {
                case "Container":
                    SetValue("columns", settings.ColCount.ToString());
                    SetValue("rows", settings.RowCount.ToString());
                    SetValue("rowHeight", settings.RowHeight.ToString());
                    break;
                case "Counter":
                    SetValue("count", settings.Count.ToString());
                    break;
                case "TextBlock":
                    SetValue("text", settings.Text.ToString());
                    SetValue("title", settings.Title.ToString());
                    break;
                case "Image":
                    SetValue("image", settings.ImageUrl.ToString());
                    break;
                //case "PruductPage":
                //    SetValue("Product:Id", settings.ProductId.ToString());
                    //break;
                case "OrderingBar":
                    SetValue("Id", "0", "Product");
                    break;
                case "ProductImage":
                    SetValue("Image","0", "Product");
                    break;
                case "ProductTitleDescription":
                    SetValue("Title", "PLeceholder", "Product");
                    //SetValue("Product:Title", settings.Product.Name.ToString(), "");
                    //SetValue("Product:Description", settings.Product.Description.ToString(), "");
                    SetValue("Description", "PLeceholder", "Product");
                    break;
                case "ProductList":
                    SetValue("ProductList", ConvertProductListToString(settings.ProductList));
                    break;
            }

            if (IsRoot)
            {
                SetValue("isRoot", "true");
            }
        }
        public string ConvertProductListToString(List<Product> products)
        {
            // Select the product IDs and join them into a comma-separated string
            return string.Join(",", products.Select(p => p.Id.ToString()));
        }


        // Method to add child component
        public PageContent AddChild(PageSettings childSettings)
        {
            childSettings.ShareId = SharedId;

            var childComponent = new PageContent(childSettings, Context)
            {
                ParentId = this.Id,
                Parent = this,
                IsRoot = false,
            };

            // Add position data for child using namespaced key
            SetValue("position",
                $"{childSettings.Row},{childSettings.Col},{childSettings.RowSpan},{childSettings.ColSpan}", childComponent.Name);

            Children.Add(childComponent);
            return childComponent;
        }

        // Method to recursively find and add child to a specific parent
        public bool AddChildToParent(PageSettings childSettings, string targetParentName)
        {
            if (Name == targetParentName)
            {
                AddChild(childSettings);
                return true;
            }

            foreach (var childNode in Children)
            {
                if (childNode.AddChildToParent(childSettings, targetParentName))
                {
                    return true;
                }
            }

            return false;
        }

        public PageSettings GetPageSettings()
        {
            // Create a new PageSettings object
            var settings = new PageSettings
            {
                Name = Name,
                Type = Data.TryGetValue($"{Name}:type", out var typeString)
    && Enum.TryParse<ComponentType>(typeString, true, out var typeEnum)
    ? typeEnum
    : ComponentType.Unknown,

                RowCount = Data.TryGetValue($"{Name}:rows", out var rows) && int.TryParse(rows, out var rowCount) ? rowCount : 1,
                ColCount = Data.TryGetValue($"{Name}:columns", out var columns) && int.TryParse(columns, out var colCount) ? colCount : 1,
                IsRoot = Data.TryGetValue($"{Name}:isRoot", out var isRoot) && bool.TryParse(isRoot, out var root) && root
            };

            // If position data exists, parse it and set the row, col, rowSpan, and colSpan
            if (Data.TryGetValue($"{Name}:position", out var positionData))
            {
                var parts = positionData.Split(',');
                if (parts.Length == 4)
                {
                    settings.Row = int.TryParse(parts[0], out var row) ? row : 1;
                    settings.Col = int.TryParse(parts[1], out var col) ? col : 1;
                    settings.RowSpan = int.TryParse(parts[2], out var rowSpan) ? rowSpan : 1;
                    settings.ColSpan = int.TryParse(parts[3], out var colSpan) ? colSpan : 1;
                }
            }

            // Return the constructed PageSettings object
            return settings;
        }

    }

    public class PageContentConfiguration : IEntityTypeConfiguration<PageContent>
    {
        public void Configure(EntityTypeBuilder<PageContent> builder)
        {
            builder.HasKey(p => p.Id);

            builder.HasMany(p => p.Children)
                .WithOne(p => p.Parent)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

            builder.Property(p => p.SharedId).IsRequired();
        }
    }


    public class PageContentDictionaryConfiguration : IEntityTypeConfiguration<PageContentDictionary>
    {
        public void Configure(EntityTypeBuilder<PageContentDictionary> builder)
        {
            builder.HasKey(d => d.SharedId);

            // Use a value converter to store the dictionary as a JSON string
            builder.Property(d => d.Data)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null))
                .IsRequired();
        }
    }


    public class PageDbContext : DbContext
    {
        public PageDbContext(DbContextOptions<PageDbContext> options) : base(options)
        {
        }

        public DbSet<PageContent> Pages { get; set; }
        public DbSet<PageContentDictionary> PageContentDictionaries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new PageContentConfiguration());
            modelBuilder.ApplyConfiguration(new PageContentDictionaryConfiguration());
        }
    }

    public static class PageContentDictionaryCache
    {
        private static readonly ConcurrentDictionary<Guid, PageContentDictionary> _cache = new();

        public static void Initialize(PageDbContext context)
        {
            var dictionaries = context.PageContentDictionaries.ToList();
            foreach (var dictionary in dictionaries)
            {
                _cache[dictionary.SharedId] = dictionary;
            }
        }

        public static IReadOnlyDictionary<Guid, PageContentDictionary> GetAll() => new ReadOnlyDictionary<Guid, PageContentDictionary>(_cache);

        public static PageContentDictionary Get(Guid sharedId)
        {
            return _cache.TryGetValue(sharedId, out var dictionary) ? dictionary : null;
        }

        public static void Update(Guid sharedId, Dictionary<string, string> data, PageDbContext context)
        {
            if (context == null)
            {
                throw new InvalidOperationException("The database context cannot be null when updating the cache.");
            }

            if (_cache.TryGetValue(sharedId, out var dictionary))
            {
                dictionary.Data = data;
                context.PageContentDictionaries.Update(dictionary);
            }
            else
            {
                var newDictionary = new PageContentDictionary { SharedId = sharedId, Data = data };
                _cache[sharedId] = newDictionary;
                context.PageContentDictionaries.Add(newDictionary);
            }

            context.SaveChanges();
        }


        public static void Remove(Guid sharedId, PageDbContext context)
        {
            if (_cache.TryRemove(sharedId, out var dictionary))
            {
                context.PageContentDictionaries.Remove(dictionary);
                context.SaveChanges();
            }
        }
    }

    public class PageSettings
    {
        public string Name { get; set; }
        public ComponentType Type { get; set; } = ComponentType.Unknown;
        public Guid ShareId { get; set; }

        public PageDbContext PageDbContext { get; set; }

        public int Row { get; set; } = 1;
        public int Col { get; set; } = 1;

        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;

        public int RowHeight { get; set; } = 1;

        public bool IsRoot { get; set; } = false;

        public int RowCount { get; set; } = 1;
        public int ColCount { get; set; } = 1;

        public int Count { get; set; } = 1;

        public string Title { get; set; } = "";
        public string Text { get; set; } = "";

        public string ImageUrl { get; set; } = "";

        public Product Product { get; set; } = new Product();

        public List<Product> ProductList { get; set; } = new List<Product>();
        }
    public enum ComponentType
    {
        Container,
        Counter,
        TextBlock,
        Image,
        OrderingBar,
        ProductImage,
        ProductTitleDescription,
        ProductList,
        Unknown
    }

}