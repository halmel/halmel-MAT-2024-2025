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



}
