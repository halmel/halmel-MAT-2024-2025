using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ESHOPMAT.Models
{
    public class PageImage
    {
        public int Id { get; set; }
        public string FileName { get; set; }

        [Required]
        public byte[] Data { get; set; }
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
    public class ImageDbContext : DbContext
    {
        public ImageDbContext(DbContextOptions<ImageDbContext> options) : base(options)
        {
        }

        public DbSet<PageImage> Images { get; set; } // New DbSet for PageImage

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new PageImageConfiguration());
        }
    }


}
