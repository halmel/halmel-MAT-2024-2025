using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SendGrid.Helpers.Mail;

namespace ESHOPMAT.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [NotMapped]
        public EmailAddress EmailAddress
        {
            get
            {
                return new EmailAddress(Address, Name);
            }
            set
            {
                Address = value.Email.ToString();
            }
        }

        [Required]
        public string TelephoneNumber { get; set; }

        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        public OrderStatus OrderStatus { get; set; } = OrderStatus.Undecided; // Default status

        public Order(int orderId, DateTime orderDate)
        {
            OrderId = orderId;
            OrderDate = orderDate;
        }

        public Order() { }

        public decimal CalculateTotal()
        {
            decimal total = 0;
            foreach (var item in OrderItems)
            {
                total += item.TotalPrice();
            }
            return total;
        }

        // Method to remove an OrderItem from the OrderItems list
        public void RemoveOrderItem(int orderItemId)
        {
            var orderItem = OrderItems.FirstOrDefault(item => item.Id == orderItemId);
            if (orderItem != null)
            {
                OrderItems.Remove(orderItem);
            }
        }
    }
    public class ContactInformation
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string TelephoneNumber { get; set; }
    }
    public enum OrderStatus
    {
        Undecided,
        Confirmed,
        Finished,
        Cancelled,
        Unknown
    }

    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int Price { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public int OrderId { get; set; } // Foreign key to Order

        [Required]
        public int Stock { get; set; } // Foreign key to Order

        [ForeignKey("OrderId")]
        public Order Order { get; set; } // Navigation property



        public OrderItem() { }

        public OrderItem(Product product, int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero.");

            ProductId = product.Id;
            Price = product.Price;
            Name = product.Name;
            Quantity = quantity;
            Stock = product.Amount;
        }

        public decimal TotalPrice()
        {
            return Price * Quantity;
        }
    }

    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade); // Cascade delete for Order and its OrderItems
        }
    }
}
