namespace ESHOPMAT.Models
{
    public class PageItem
    {
        public string Type { get; set; } // e.g., "Label", "Image"
        public string Content { get; set; } // Text for labels, URL for images
        public int Row { get; set; }
        public int Column { get; set; }
        public int RowSpan { get; set; } = 1;
        public int ColumnSpan { get; set; } = 1;
    }

    public class PageLayout
    {
        public int Rows { get; set; } = 10; // Default number of rows
        public int Columns { get; set; } = 10; // Default number of columns
        public List<PageItem> Items { get; set; } = new List<PageItem>();
    }

}
