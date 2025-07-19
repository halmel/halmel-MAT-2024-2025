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
            get => new EmailAddress(Address, Name);
            set => Address = value.Email;
        }

        [Required]
        public string TelephoneNumber { get; set; }

        public DateTime OrderDate { get; set; }

        public List<OrderItem> OrderItems { get; set; } = new();

        public OrderStatus OrderStatus { get; set; } = OrderStatus.Undecided;

        public Order( DateTime orderDate)
        {
            OrderDate = orderDate;
        }

        public Order() { }

        public decimal CalculateTotal()
        {
            return OrderItems.Sum(item => item.TotalPrice());
        }

        public void RemoveOrderItem(int orderItemId)
        {
            var orderItem = OrderItems.FirstOrDefault(item => item.Id == orderItemId);
            if (orderItem != null)
            {
                OrderItems.Remove(orderItem);
            }
        }

        public string GetProductIdsAsString()
        {
            return string.Join(",", OrderItems.Select(item => item.ProductId));
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

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [Required]
        public int Quantity { get; set; }

        [Required]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        public OrderItem() { }

        public OrderItem(Product product, int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero.");

            Product = product;
            ProductId = product.Id;
            Quantity = quantity;
        }

        public decimal TotalPrice()
        {
            return Product.Price * Quantity;
        }
    }

}
