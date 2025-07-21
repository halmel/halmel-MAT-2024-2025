using ESHOPMAT.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ESHOPMAT
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Email
        public DbSet<EmailTemplate> EmailTemplates { get; set; }

        // Images
        public DbSet<PageImage> Images { get; set; }

        // Orders
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // Products
        public DbSet<Product> Products { get; set; }
        public DbSet<HatchingEvent> HatchingEvents { get; set; }

        public DbSet<ProductTemplate> ProductTemplates { get; set; }

        // Pages
        public DbSet<PageContent> Pages { get; set; }
        public DbSet<PageContentDictionary> PageContentDictionaries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Email: no config needed

            // Images
            modelBuilder.ApplyConfiguration(new PageImageConfiguration());

            // Orders
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Products
            modelBuilder.Entity<HatchingEvent>()
                .HasOne(evt => evt.Product)
                .WithMany(p => p.HatchingEvents)
                .HasForeignKey(evt => evt.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
    .HasMany(o => o.OrderItems)
    .WithOne(oi => oi.Order)
    .HasForeignKey(oi => oi.OrderId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany() // You can add a .WithMany(p => p.OrderItems) if needed
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict); // To prevent accidental product deletion

            modelBuilder.Entity<ProductTemplate>()
    .HasOne(pt => pt.Page)
    .WithMany() // Only ProductTemplate tracks the PageElement — no reverse nav
    .HasForeignKey(pt => pt.PageElementId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Template)
                .WithMany(t => t.Products)
                .HasForeignKey(p => p.ProductTemplateId)
                .OnDelete(DeleteBehavior.SetNull); // Allows deleting templates without deleting products

            modelBuilder.Entity<Product>()
    .Navigation(p => p.Template)
    .AutoInclude();
            modelBuilder.Entity<Product>()
    .HasDiscriminator<ProductStockHandlingType>("StockHandlingType")
    .HasValue<StandardProduct>(ProductStockHandlingType.Default)
    .HasValue<ChickProduct>(ProductStockHandlingType.Chick);



            // Pages
            modelBuilder.ApplyConfiguration(new PageContentConfiguration());
            modelBuilder.ApplyConfiguration(new PageContentDictionaryConfiguration());
        }

        // Preserve DeletePageAsync in AppDbContext
        public async Task DeletePageAsync(int pageId)
        {
            var page = await Pages
                .Include(p => p.Children)
                .Include(p => p.DevPage)
                    .ThenInclude(d => d.Children)
                .FirstOrDefaultAsync(p => p.Id == pageId);

            if (page == null)
                throw new KeyNotFoundException("Page not found.");

            PageContentDictionaryCache.Remove(page.SharedId, this);

            if (page.DevPage != null)
            {
                PageContentDictionaryCache.Remove(page.DevPage.SharedId, this);
            }

            foreach (var child in page.Children.ToList())
            {
                await DeletePageAsync(child.Id);
            }

            if (page.DevPage != null)
            {
                foreach (var devChild in page.DevPage.Children.ToList())
                {
                    await DeletePageAsync(devChild.Id);
                }

                Pages.Remove(page.DevPage);
            }

            if (Pages.Any(p => p.DevPageId == page.Id))
                throw new InvalidOperationException("Cannot delete a page that is referenced as a DevPage. Set references to NULL first.");

            Pages.Remove(page);
            await SaveChangesAsync();
        }
    }
    public class PageImageConfiguration : IEntityTypeConfiguration<PageImage>
    {
        public void Configure(EntityTypeBuilder<PageImage> builder)
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.FileName).HasMaxLength(255).IsRequired();
            builder.Property(i => i.Data).IsRequired();
        }
    }

    public class PageContentConfiguration : IEntityTypeConfiguration<PageContent>
    {
        public void Configure(EntityTypeBuilder<PageContent> builder)
        {
            builder.HasKey(p => p.Id);

            builder.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.DevPage)
                .WithMany()
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

            builder.Property(d => d.Data)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null))
                .IsRequired();
        }
    }
}