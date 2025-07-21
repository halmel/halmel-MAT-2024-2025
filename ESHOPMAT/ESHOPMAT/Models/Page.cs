using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.IdentityModel.Tokens;
using Mono.TextTemplating.CodeCompilation;
using System;
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

        public AppDbContext Context { get; set; }

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
        public PageContent(PageSettings settings, AppDbContext c)
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





    }

    /// <summary>
    /// Cache manager for PageContentDictionary to optimize lookups.
    /// </summary>
    public static class PageContentDictionaryCache
    {
        private static readonly ConcurrentDictionary<Guid, PageContentDictionary> _cache = new();

        public static void Initialize(AppDbContext context)
        {
            var dictionaries = context.PageContentDictionaries.ToList();
            foreach (var dictionary in dictionaries)
            {
                _cache[dictionary.SharedId] = dictionary;
            }
        }

        public static IReadOnlyDictionary<Guid, PageContentDictionary> GetAll() =>
            new ReadOnlyDictionary<Guid, PageContentDictionary>(_cache);

        public static PageContentDictionary Get(Guid sharedId) =>
            _cache.TryGetValue(sharedId, out var dictionary) ? dictionary : null;

        public static void Update(Guid sharedId, Dictionary<string, string> data, AppDbContext context)
        {
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

        public static void Remove(Guid sharedId, AppDbContext context)
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
        public Product Product { get; set; } = null;
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