using ESHOPMAT.Components.Shared.Components;
using ESHOPMAT.Components.Shared.ProductPage;

namespace ESHOPMAT.Models
{
    public class ComponentDefinition
    {
        public string Name { get; set; }
        public Type BlazorComponentType { get; set; }
        public Dictionary<string, object> DefaultProperties { get; set; } = new();

        // New: Expected parameters to bind for DynamicComponent
        public Dictionary<string, Func<PageContent, bool, object>> ParameterBindings { get; set; } = new();
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

    public static class ComponentRegistry
    {
        public static readonly Dictionary<string, ComponentDefinition> Definitions = new()
        {
            ["Container"] = new ComponentDefinition
            {
                Name = "Container",
                BlazorComponentType = typeof(ContainerComponent),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["columns"] = 1,
                    ["rows"] = 1,
                    ["rowHeight"] = 16
                },
                ParameterBindings = new()
                {
                    [nameof(ContainerComponent.Page)] = (page, _) => page,
                    [nameof(ContainerComponent.IsEditing)] = (_, isEditing) => isEditing
                }
            },
            ["Counter"] = new ComponentDefinition
            {
                Name = "Counter",
                BlazorComponentType = typeof(CounterComponent),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["count"] = 0
                },
                ParameterBindings = new()
                {
                    [nameof(CounterComponent.Page)] = (page, _) => page,
                    [nameof(CounterComponent.IsEditing)] = (_, isEditing) => isEditing
                }
            },
            ["TextBlock"] = new ComponentDefinition
            {
                Name = "TextBlock",
                BlazorComponentType = typeof(TextBlockComponent),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["text"] = "",
                    ["title"] = ""
                },
                ParameterBindings = new()
                {
                    [nameof(TextBlockComponent.Page)] = (page, _) => page,
                    [nameof(TextBlockComponent.IsEditing)] = (_, isEditing) => isEditing
                }
            },
            ["Image"] = new ComponentDefinition
            {
                Name = "Image",
                BlazorComponentType = typeof(ImageComponent),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["image"] = ""
                },
                ParameterBindings = new()
                {
                    [nameof(ImageComponent.Page)] = (page, _) => page,
                    [nameof(ImageComponent.IsEditing)] = (_, isEditing) => isEditing
                }
            },
            ["OrderingBar"] = new ComponentDefinition
            {
                Name = "OrderingBar",
                BlazorComponentType = typeof(OrderingBarComponent),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["Id"] = "0" // Placeholder value
                },
                ParameterBindings = new()
                {
                    [nameof(OrderingBarComponent.Page)] = (page, _) => page,
                    [nameof(OrderingBarComponent.IsEditing)] = (_, isEditing) => isEditing
                }
            },
            ["ProductImage"] = new ComponentDefinition
            {
                Name = "ProductImage",
                BlazorComponentType = typeof(ProductImg),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["Image"] = "0" // Placeholder value
                },
                ParameterBindings = new()
                {
                    [nameof(ProductImg.Page)] = (page, _) => page,
                    [nameof(ProductImg.IsEditing)] = (_, isEditing) => isEditing
                }
            },
            ["ProductTitleDescription"] = new ComponentDefinition
            {
                Name = "ProductTitleDescription",
                BlazorComponentType = typeof(ProductTitleDescriptionComponent),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["Title"] = "Placeholder",
                    ["Description"] = "Placeholder"
                },
                ParameterBindings = new()
                {
                    [nameof(ProductTitleDescriptionComponent.Page)] = (page, _) => page,
                    [nameof(ProductTitleDescriptionComponent.IsEditing)] = (_, isEditing) => isEditing
                }
            },
            ["ProductList"] = new ComponentDefinition
            {
                Name = "ProductList",
                BlazorComponentType = typeof(ProductList),
                DefaultProperties = new Dictionary<string, object>
                {
                    ["ProductList"] = new List<Guid>() // Will be replaced by IDs
                },
                ParameterBindings = new()
                {
                    [nameof(ProductList.Page)] = (page, _) => page,
                    [nameof(ProductList.IsEditing)] = (_, isEditing) => isEditing
                }
            }
        };

        public static ComponentDefinition Get(string type) =>
            Definitions.TryGetValue(type, out var def) ? def : null;
    }

}
