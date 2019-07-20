using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebMathTraining.Data;

namespace WebMathTraining
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var host = BuildWebHost(args);
      InitializeDatabase(host);
      host.Run();
    }

    public static IWebHost BuildWebHost(string[] args) =>
      WebHost.CreateDefaultBuilder(args)
        .UseStartup<Startup>()//.UseKestrel(a => a.AddServerHeader = false)
        .Build();

    private static void InitializeDatabase(IWebHost host)
    {
      using (var scope = host.Services.CreateScope())
      {
        var services = scope.ServiceProvider;

        try
        {
          //SeedData.InitializeAsync(services).Wait();
          ApplicationDbContextSeed.Seed(services);
        }
        catch (Exception ex)
        {
          var logger = services.GetRequiredService<ILogger<Program>>();
          logger.LogError(ex, "An error occurred seeding the DB.");
        }
      }
    }
  }
}
