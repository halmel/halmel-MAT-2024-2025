using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ESHOPMAT.Models
{
        public class Product
        {
            [Key] // Marks Id as the primary key
            public int Id { get; set; }

            [Required]
            public string Name { get; set; } = string.Empty;

            [Required]
            public string Description { get; set; } = string.Empty;

            [Required]
            public int Price { get; set; }

            [Required]
            public int Amount { get; set; }

        [Required]
        public ProductType Type { get; set; } = ProductType.Unknown;

        public DateTimeOffset HatchDate { get; set; }



        public int[] ImageIds { get; set; } = Array.Empty<int>();

            public int[] PageIds { get; set; } = Array.Empty<int>();
        }

    public enum ProductType
    {
        Chick,
        Chicken,
        Feed,
        Item,
        Unknown
    }



    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

    }
}
