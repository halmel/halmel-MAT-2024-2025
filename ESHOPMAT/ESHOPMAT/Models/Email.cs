using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ESHOPMAT.Models
{
    public class EmailTemplate
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string Subject { get; set; } = "";
        public string HtmlContent { get; set; } = "";
        public string PlainTextContent { get; set; } = "";

        public string ProcessTemplate(EmailValues emailValues)
        {
            var processedHtml = HtmlContent;
            var properties = emailValues.GetType().GetProperties();

            foreach (var property in properties)
            {
                var placeholder = $"{{{{ {property.Name} }}}}";
                var value = property.GetValue(emailValues)?.ToString() ?? string.Empty;

                processedHtml = processedHtml.Replace(placeholder, value);
            }

            return processedHtml;
        }
    }

    public class EmailValues
    {
        public string ClientName { get; set; } = "";
        public string SentDate { get; set; } = DateTime.Now.ToString();

    }


    public class EmailDbContext : DbContext
    {
        public EmailDbContext(DbContextOptions<EmailDbContext> options) : base(options)
        {
        }

        public DbSet<EmailTemplate> EmailTemplates { get; set; }

    }

}
