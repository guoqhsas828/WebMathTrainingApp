using StoreManager.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using StoreManager.Models;

namespace StoreManager.Data
{
  public class CatalogContextSeed
  {
    public static async Task SeedAsync(CatalogContext catalogContext,
      ILoggerFactory loggerFactory, int? retry = 0)
    {
      int retryForAvailability = retry.Value;
      try
      {
        // TODO: Only run this if using a real database
        //catalogContext.Database.Migrate();

      }
      catch (Exception ex)
      {
        if (retryForAvailability < 10)
        {
          retryForAvailability++;
          var log = loggerFactory.CreateLogger<CatalogContextSeed>();
          log.LogError(ex.Message);
          await SeedAsync(catalogContext, loggerFactory, retryForAvailability);
        }
      }
    }



  }
}
