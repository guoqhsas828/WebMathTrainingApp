using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using StoreManager.Data;
using StoreManager.Models;
using StoreManager.Services;

namespace Microsoft.eShopWeb.Web
{
    public class Program
    {
      public async static Task Main(string[] args)
      {
        var host = CreateWebHostBuilder(args)
          .Build();

        using (var scope = host.Services.CreateScope())
        {
          var services = scope.ServiceProvider;
          var loggerFactory = services.GetRequiredService<ILoggerFactory>();
          try
          {
            var catalogContext = services.GetRequiredService<CatalogContext>();
            await CatalogContextSeed.SeedAsync(catalogContext, loggerFactory);
            var context = services.GetRequiredService<ApplicationDbContext>();
            var functional = services.GetRequiredService<IFunctional>();
          await DbInitializer.Initialize(context, functional);
        }
          catch (Exception ex)
          {
            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogError(ex, "An error occurred seeding the DB.");
          }
        }

        host.Run();
      }

      public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
