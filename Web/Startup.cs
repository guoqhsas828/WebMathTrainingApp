﻿using Ardalis.ListStartupServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.HealthChecks;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using Microsoft.eShopWeb.Infrastructure.Extensions;
using StoreManager.Data;
using StoreManager.Interfaces;
using StoreManager.Models;
using StoreManager.Services;

namespace Microsoft.eShopWeb.Web
{
  public class Startup
  {
    private IServiceCollection _services;

    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureDevelopmentServices(IServiceCollection services)
    {
      // use in-memory database
      //ConfigureInMemoryDatabases(services);

      // use real database
      ConfigureProductionServices(services);
    }

    private void ConfigureInMemoryDatabases(IServiceCollection services)
    {
      // use in-memory database
      services.AddDbContext<CatalogContext>(c =>
        c.UseInMemoryDatabase("Catalog"));

      // Add Identity DbContext
      services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("Identity"));

      ConfigureServices(services);
    }

    public void ConfigureProductionServices(IServiceCollection services)
    {
      // use real database
      // Requires LocalDB which can be installed with SQL Server Express 2016
      // https://www.microsoft.com/en-us/download/details.aspx?id=54284

      ConfigureServices(services);
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      ServiceConfigurationHelper.ConfigSharedServices(Configuration, services);
      ServiceConfigurationHelper.ConfigureCatalogServices(Configuration, services);

      services.AddScoped<ICatalogViewModelService, CachedCatalogViewModelService>();
      services.AddScoped<IBasketViewModelService, BasketViewModelService>();
      services.AddScoped<IOrderService, OrderService>();
      services.AddScoped<IOrderRepository, OrderRepository>();
      services.AddScoped<CatalogViewModelService>();

      services.AddMvc(options =>
          {
            options.Conventions.Add(new RouteTokenTransformerConvention(
              new SlugifyParameterTransformer()));
          }
        )
        .AddRazorPagesOptions(options =>
        {
          options.Conventions.AuthorizePage("/Basket/Checkout");
          options.AllowAreas = true;
        })
        .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

      services.AddHttpContextAccessor();
      services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new Info {Title = "My API", Version = "v1"}); });

      services.AddHealthChecks()
        .AddCheck<HomePageHealthCheck>("home_page_health_check")
        .AddCheck<ApiHealthCheck>("api_health_check");

      services.Configure<ServiceConfig>(config =>
      {
        config.Services = new List<ServiceDescriptor>(services);

        config.Path = "/allservices";
      });

      _services = services; // used to debug registered services
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
      //app.UseDeveloperExceptionPage();
      app.UseHealthChecks("/health",
        new HealthCheckOptions
        {
          ResponseWriter = async (context, report) =>
          {
            var result = JsonConvert.SerializeObject(
              new
              {
                status = report.Status.ToString(),
                errors = report.Entries.Select(e => new
                {
                  key = e.Key,
                  value = Enum.GetName(typeof(HealthStatus), e.Value.Status)
                })
              });
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(result);
          }
        });
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
        app.UseShowAllServicesMiddleware();
        app.UseDatabaseErrorPage();
      }
      else
      {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
      }

      app.UseHttpsRedirection();
      app.UseStaticFiles();
      app.UseCookiePolicy();

      app.UseAuthentication();

      // Enable middleware to serve generated Swagger as a JSON endpoint.
      app.UseSwagger();

      // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
      // specifying the Swagger JSON endpoint.
      app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"); });

      app.UseMvc(routes =>
      {
        routes.MapRoute(
          name: "identity",
          template: "Identity/{controller=Account}/{action=Register}/{id?}");

        routes.MapRoute(
          name: "default",
          template: "{controller:slugify=Home}/{action:slugify=Index}/{id?}");
      });
    }
  }
}
