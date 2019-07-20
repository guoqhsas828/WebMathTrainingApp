using BaseEntity.Metadata;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.Infrastructure.Identity;
using StoreManager.Models;
using WebMathTraining.Models;

namespace Microsoft.eShopWeb.Infrastructure.Data
{

  public class CatalogContext : DbContext
  {
    public CatalogContext(DbContextOptions<CatalogContext> options) : base(options)
    {
    }


    protected override void OnModelCreating(ModelBuilder builder)
    {
      //builder.Entity<Basket>(ConfigureBasket);
      //builder.Entity<Address>(ConfigureAddress);
      //builder.Entity<BasketItem>(ConfigureBasketItem);
      //builder.Entity<Product>(ConfigurateCatalogProduct);
    }

    //private void ConfigureBasketItem(EntityTypeBuilder<BasketItem> builder)
    //{
    //  builder.Property(bi => bi.UnitPrice)
    //    .IsRequired(true)
    //    .HasColumnType("decimal(18,2)");
    //}

    //private void ConfigurateCatalogProduct(EntityTypeBuilder<Product> builder)
    //{
    //  builder.Property(cio => cio.ProductName)
    //      .HasMaxLength(50)
    //      .IsRequired();
    //}

    //private void ConfigureAddress(EntityTypeBuilder<Address> builder)
    //{
    //  builder.Property(a => a.ZipCode)
    //    .HasMaxLength(18)
    //    .IsRequired();

    //  builder.Property(a => a.Street)
    //    .HasMaxLength(180)
    //    .IsRequired();

    //  builder.Property(a => a.State)
    //    .HasMaxLength(60);

    //  builder.Property(a => a.Country)
    //    .HasMaxLength(90)
    //    .IsRequired();

    //  builder.Property(a => a.City)
    //    .HasMaxLength(100)
    //    .IsRequired();
    //}

    //private void ConfigureBasket(EntityTypeBuilder<Basket> builder)
    //{
    //  var navigation = builder.Metadata.FindNavigation(nameof(Basket.Items));

    //  navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
    //}

    public DbSet<BasketItem> BasketItems { get; set; }
    public DbSet<Basket> Baskets { get; set; }

    public DbSet<TodoItem> TodoItems { get; set; }

    public DbSet<TestResult> TestResult { get; set; }
    public DbSet<TestImage> TestImage { get; set; }
    public DbSet<TestQuestion> TestQuestion { get; set; }
    public DbSet<TestSession> TestSession { get; set; }
    public DbSet<TestGroup> TestGroup { get; set; }

    public DbSet<PluginAssembly> PluginAssembly { get; set; }
    //public DbSet<OrderItem> OrderItems { get; set; }
    //public DbSet<CatalogItem> CatalogItems { get; set; }

    //public DbSet<CatalogType> CatalogTypes { get; set; }

  }
}
