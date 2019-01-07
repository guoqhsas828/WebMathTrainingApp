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

      builder.Entity<TestResult>(b =>
      {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedOnAdd();
      });

      //var resultBuilder = builder.Entity<TestResult>();

      //resultBuilder.Property(p => p.Id).UseSqlServerIdentityColumn();
      //resultBuilder.Property(p => p.Id).Metadata.AfterSaveBehavior = PropertySaveBehavior.Ignore;

    }

    public DbSet<TestQuestion> TestQuestions { get; set; }

    public DbSet<TestImage> TestImages { get; set; }

    public DbSet<TestSession> TestSessions { get; set; }

    public DbSet<TestResult> TestResults { get; set; }
  }
}
