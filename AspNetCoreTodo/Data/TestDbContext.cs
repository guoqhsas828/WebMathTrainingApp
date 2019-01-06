using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WebMathTraining.Models;

namespace WebMathTraining.Data
{
  public class TestDbContext : DbContext
  {
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
      base.OnModelCreating(builder);
      // Customize the ASP.NET Identity model and override the defaults if needed.
      // For example, you can rename the ASP.NET Identity table names and more.
      // Add your customizations after calling base.OnModelCreating(builder);
      var questionBuilder = builder.Entity<TestQuestion>();
      questionBuilder.Property(q => q.ObjectId).UseSqlServerIdentityColumn();
      questionBuilder.Property(q => q.ObjectId).Metadata.AfterSaveBehavior = PropertySaveBehavior.Ignore; //Notes: this is necessary to prevent the SqlException of updating entity while complaining on Identity column

      var sessionBuilder = builder.Entity<TestSession>();

      sessionBuilder.Property(p => p.ObjectId).UseSqlServerIdentityColumn();
      sessionBuilder.Property(p => p.ObjectId).Metadata.AfterSaveBehavior = PropertySaveBehavior.Ignore;
    }

    public DbSet<TestQuestion> TestQuestions { get; set; }

    public DbSet<TestImage> TestImages { get; set; }

    public DbSet<TestSession> TestSessions { get; set; }
  }
}
