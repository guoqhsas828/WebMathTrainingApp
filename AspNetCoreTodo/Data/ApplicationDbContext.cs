using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WebMathTraining.Models;

namespace WebMathTraining.Data
{
  public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
  {
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
      : base(options)
    {
    }

    public DbSet<TodoItem> TodoItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
      base.OnModelCreating(builder);
      // Customize the ASP.NET Identity model and override the defaults if needed.
      // For example, you can rename the ASP.NET Identity table names and more.
      // Add your customizations after calling base.OnModelCreating(builder);
      var userBuilder = builder.Entity<ApplicationUser>();
      userBuilder.Property(q => q.ObjectId).UseSqlServerIdentityColumn();
      userBuilder.Property(q => q.ObjectId).Metadata.AfterSaveBehavior =
        PropertySaveBehavior
          .Ignore; //Notes: this is necessary to prevent the SqlException of updating entity while complaining on Identity column

    }

  }
}
