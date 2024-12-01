
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ESHOPMAT.Data
{
    public class PageDbContext(DbContextOptions<PageDbContext> options) : DbContext(options)
    {
        public DbSet<ESHOPMAT.Models.Page> Page { get; set; } = default!;
    }
}
