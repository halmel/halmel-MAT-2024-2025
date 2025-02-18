using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.IdentityModel.Tokens;
using Mono.TextTemplating.CodeCompilation;
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

        public Guid DevSharedId { get; set; }

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

        // Parent ID for the recursive relationship
        public int? DevPageId { get; set; }

        public PageContent? Parent { get; set; }

        // Recursive relationship for child components
        public List<PageContent> Children { get; set; } = new List<PageContent>();
        private PageContent devPage;

        public PageContent DevPage
        {
            get
            {
                // Ensure that devPage is initialized if it's null
                if (devPage == null)
                {
                    InitializeDevPage();
                }
                return devPage;
            }
            set
            {
                // Optionally allow devPage to be set manually
                devPage = value;
            }
        }

        public void InitializeDevPage()
        {
            // Return early if devPage is already initialized
            if (devPage != null)
            {
                return;
            }

            // Throw exception if the page is not a root page or if it is a dev page
            if (!IsRoot || IsDev)
            {
                return;
            }

            // Create and assign the development page
            devPage = CreateDevPage();
        }


        public PageContent CreateDevPage()
        {
            var devPage = new PageContent
            {
                Name = Name,
                SharedId = this.DevSharedId,  // Use DevSharedId for the dev page
                DevSharedId = this.DevSharedId,  // Use the existing DevSharedId
                IsDev = true,  // Mark it as a dev page
                IsRoot = this.IsRoot,  // Maintain IsRoot status
                Type = this.Type,  // Copy the Type
                Context = this.Context,  // Copy the DbContext reference (if needed)
                ParentId = this.ParentId,  // Copy the ParentId
                Parent = this.Parent,  // Copy the Parent reference
                DevPageId = this.DevPageId,  // Copy the DevPageId (if needed)
            };

            // Initialize the data for the dev page
            devPage.InitializeData();
            devPage.Data = new Dictionary<string, string>(this.Data);  // Copy the data dictionary

            // Copy children if any
            if (this.Children.Any())
            {
                foreach (var child in this.Children)
                {
                    // Recursively add the children to the dev page
                    var childCopy = child.CreateDevPage();  // Assuming child has a similar CreateDevPage method
                    childCopy.ParentId = devPage.Id;  // Set the parent for the child copy
                    devPage.Children.Add(childCopy);
                }
            }

            return devPage;
        }



        public void PushDevVersion()
        {

            if (DevPage == null)
            {
                throw new InvalidOperationException("No development version available to push.");
            }

            // Copy properties from the DevPage to the original page (excluding Children and DevPage)
            var devData = DevPage.GetType().GetProperties()
                .Where(prop => prop.CanWrite &&
                              prop.Name != nameof(IsDev) &&
                              prop.Name != nameof(Id) &&
                              prop.Name != nameof(SharedId) &&
                              prop.Name != nameof(DevSharedId) && // Added DevSharedId
                              prop.Name != nameof(Children) &&
                              prop.Name != nameof(DevPage) &&
                              prop.Name != nameof(Parent) && // Added Parent
                              prop.Name != nameof(ParentId)) // Added ParentId
                .ToDictionary(prop => prop, prop => prop.GetValue(DevPage));

            foreach (var prop in devData)
            {
                prop.Key.SetValue(this, prop.Value);
            }

            // ***Improved Child Handling***
            PushDevVersionForChildren(); // No argument needed anymore

            this.IsDev = false;
            Context.Pages.Update(this);
            Context.SaveChanges();
        }

        public void PushDevVersionForChildren()
        {
            // 1. Clear existing Children in the production version
            this.Children.Clear();

            // 2. Iterate through the *dev* children and create copies for production
            foreach (var devChild in DevPage.Children)
            {
                var prodChild = new PageContent // Create a *new* PageContent instance
                {
                    Name = devChild.Name,
                    SharedId = this.SharedId, // Use the dev child's SharedId
                    DevSharedId = this.DevSharedId,
                    Type = devChild.Type,
                    IsDev = false, // Production child!
                    IsRoot = devChild.IsRoot,
                    Context = this.Context,  // Important: Use the same context
                    ParentId = this.Id,       // Set the parent ID
                    Parent = this,  // Copy the Parent reference
                    DevPageId = this.DevPageId,  // Copy the DevPageId (if needed)
                    // ... copy other properties as needed
                };


                // Recursively handle children of the dev child
                CopyDevChildrenToProd(devChild, prodChild);


                this.Children.Add(prodChild); // Add the *new* production child
            }
        }

        private void CopyDevChildrenToProd(PageContent devChild, PageContent prodChild)
        {
            foreach (var devGrandChild in devChild.Children)
            {
                var prodGrandChild = new PageContent
                {
                    Name = devGrandChild.Name,
                    SharedId = devGrandChild.SharedId,
                    DevSharedId = devGrandChild.DevSharedId,
                    Type = devGrandChild.Type,
                    IsDev = false,
                    IsRoot = devGrandChild.IsRoot,
                    Context = this.Context,
                    ParentId = prodChild.Id, // Parent is the prodChild we just created
                    Parent = prodChild,  // Copy the Parent reference
                    // ... copy other properties
                };
                CopyDevChildrenToProd(devGrandChild, prodGrandChild); // Recursive call
                prodChild.Children.Add(prodGrandChild);
            }
        }




        public bool IsRoot { get; set; } = true;
        public bool IsDev { get; set; } = false;
        public int? ProductId { get; set; }
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
                DevSharedId = Guid.NewGuid();
                InitializeData();
            }
            else
            {
                SharedId = settings.ShareId;
                DevSharedId = settings.DevShareId;
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


        public void InitializeData()
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
            SetValue("Image", string.Join(",", product.ImageIds), "Product");
            SetValue("Id", product.Id.ToString(), "Product");
            ProductId = product.Id;

        }

         
        private void InitializeComponentData(PageSettings settings)
        {

            // Use namespaced keys
            SetValue("type", settings.Type.ToString());
            //if (!settings.IsRoot)
            //{
            //    SetValue("parent",Parent.Name);
            //}

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
            childSettings.DevShareId = DevSharedId;

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

            // Parent-Child Relationship (Restrict Delete to Prevent Cycles)
            builder.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // DevPage Relationship (Restrict Delete to Prevent Circular References)
            builder.HasOne(p => p.DevPage)
                .WithMany()  // No inverse navigation
                .HasForeignKey(p => p.DevPageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(p => p.SharedId).IsRequired();
        }
    }

    public class PageContentDictionaryConfiguration : IEntityTypeConfiguration<PageContentDictionary>
    {
        public void Configure(EntityTypeBuilder<PageContentDictionary> builder)
        {
            builder.HasKey(d => d.SharedId);

            // Store dictionary as JSON string
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

        /// <summary>
        /// Deletes a page and its related data, ensuring cache is updated.
        /// </summary>
        public async Task DeletePageAsync(int pageId)
        {
            var page = await Pages
                .Include(p => p.Children)  // Load child pages
                .Include(p => p.DevPage)   // Load associated DevPage
                    .ThenInclude(d => d.Children)  // Load children of the DevPage
                .FirstOrDefaultAsync(p => p.Id == pageId);

            if (page == null)
                throw new KeyNotFoundException("Page not found.");

            // Remove associated PageContentDictionary entries from cache and DB
            PageContentDictionaryCache.Remove(page.SharedId, this);

            if (page.DevPage != null)
            {
                PageContentDictionaryCache.Remove(page.DevPage.SharedId, this);
            }

            // Recursively delete child pages of the current page
            foreach (var child in page.Children.ToList())
            {
                await DeletePageAsync(child.Id);
            }

            // If the page has a DevPage, delete its child pages recursively as well
            if (page.DevPage != null)
            {
                foreach (var devChild in page.DevPage.Children.ToList())
                {
                    await DeletePageAsync(devChild.Id);
                }

                // Remove the DevPage after its children are deleted
                Pages.Remove(page.DevPage);
            }

            // Ensure the page is not referenced as a DevPage by other pages
            if (Pages.Any(p => p.DevPageId == page.Id))
                throw new InvalidOperationException("Cannot delete a page that is referenced as a DevPage. Set references to NULL first.");

            // Remove the main page
            Pages.Remove(page);

            await SaveChangesAsync();
        }

    }

    /// <summary>
    /// Cache manager for PageContentDictionary to optimize lookups.
    /// </summary>
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

        public static IReadOnlyDictionary<Guid, PageContentDictionary> GetAll() =>
            new ReadOnlyDictionary<Guid, PageContentDictionary>(_cache);

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
        public Guid DevShareId { get; set; }

        public int Row { get; set; } = 1;
        public int Col { get; set; } = 1;
        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;
        public int RowHeight { get; set; } = 16;
        public bool IsRoot { get; set; } = false;
        public int RowCount { get; set; } = 1;
        public int ColCount { get; set; } = 1;
        public int Count { get; set; } = 1;
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public Product Product { get; set; } = new Product();
        public List<Product> ProductList { get; set; } = new List<Product>();

        public Dictionary<string, object> GetFilteredProperties()
        {
            var filteredData = new Dictionary<string, object>();

            switch (Type)
            {
                case ComponentType.Container:
                    filteredData["RowCount"] = RowCount;
                    filteredData["ColCount"] = ColCount;
                    filteredData["RowHeight"] = RowHeight;
                    break;
                case ComponentType.Counter:
                    filteredData["Count"] = Count;
                    break;
                case ComponentType.TextBlock:
                    filteredData["Text"] = Text;
                    filteredData["Title"] = Title;
                    break;
                case ComponentType.Image:
                    filteredData["ImageUrl"] = ImageUrl;
                    break;
                case ComponentType.OrderingBar:
                    filteredData["Id"] = "0"; // Placeholder for ordering bar
                    break;
                case ComponentType.ProductImage:
                    filteredData["Image"] = "0"; // Placeholder for product image
                    break;
                case ComponentType.ProductTitleDescription:
                    filteredData["Title"] = "Placeholder";
                    filteredData["Description"] = "Placeholder";
                    break;
                case ComponentType.ProductList:
                    filteredData["ProductList"] = ProductList.Select(p => p.Id).ToList();
                    break;
                default:
                    break;
            }

            return filteredData;
        }
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