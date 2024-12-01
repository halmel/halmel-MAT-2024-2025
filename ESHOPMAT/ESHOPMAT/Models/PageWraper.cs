using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ESHOPMAT.Models
{
    public class PageContent
    {
        public List<PageContent> Children { get; set; } = new List<PageContent>();
        public Dictionary<string, string> Data { get; private set; }

        // Specifies the type of component (e.g., "Container", "Counter")
        public string Type { get; set; }

        // Unique name for the component instance
        public string Name { get; set; }

        public PageContent(Dictionary<string, string> sharedData = null)
        {
            Data = sharedData ?? new Dictionary<string, string>();
        }
        public PageContent(PageSettings x)
        {
            Name = x.Name;
            Type = x.Type;
            Data = new Dictionary<string, string>();
            Data[Name + ":columns"] = x.ColCount.ToString();
            Data[Name + ":rows"] = x.RowCount.ToString();

        }

        public void AddChild(PageSettings Set)
        {
            PageContent child = new PageContent() { Name= Set.Name,Type = Set.Type, Data = this.Data };
            Data[child.Name + ":position"] = $"{Set.Row},{Set.Col},{Set.RowSpan},{Set.ColSpan}";
            SetPageParams(ref child, Set);
            Children.Add(child);
        }

        // Recursively finds a parent node and adds a child to it
        public bool AddChildToParent(PageSettings child, String targetParent)
        {
            if (this.Name == targetParent)
            {
                AddChild(child);
                return true;
            }

            // Recursively check children for the target parent
            foreach (var childNode in Children)
            {
                if (childNode.AddChildToParent(child, targetParent))
                {
                    return true;
                }
            }

            return false; // Target parent not found
        }






    public void SetPageParams(ref PageContent child, PageSettings x)
        {
            switch (x.Type)
            {
                case "Container":
                    Data[child.Name+":rows"] = x.RowCount.ToString();
                    Data[child.Name+":columns"] = x.ColCount.ToString();
                    break;
                case "Counter":
                    Data[child.Name + ":count"] = x.Count.ToString();
                    break;

                default:
                    break;
            }

        }


    }

        public class PageSettings
        {
            public string Name { get; set; }
            public string Type { get; set; }

            public int Row { get; set; } =1;
            public int Col { get; set; } =1;

            public int RowSpan { get; set; } = 1;
            public int ColSpan { get; set; } = 1;

            public int RowCount { get; set; }
            public int ColCount { get; set; }

            public int Count { get; set; } = 1;

        }
}
