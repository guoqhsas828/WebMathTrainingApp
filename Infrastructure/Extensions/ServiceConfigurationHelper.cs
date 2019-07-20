using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StoreManager.Data;
using StoreManager.Interfaces;
using StoreManager.Models;
using StoreManager.Services;
using WebMathTraining.Services;

namespace Microsoft.eShopWeb.Infrastructure.Extensions
{
  public static class ServiceConfigurationHelper
  {
    public static void ConfigSharedServices(IConfiguration configuration, IServiceCollection services)
    {
      var connStr = configuration.GetConnectionString(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
          ? "CatalogProdConn" : "DefaultConnection");

      services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connStr));

      CreateIdentityIfNotCreated(services, configuration);

      // Get SendGrid configuration options
      var sendgridSection = configuration.GetSection("SendGridOptions");
      if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
      {
        sendgridSection["SendGridUser"] = Environment.GetEnvironmentVariable("SendGridUser");
        sendgridSection["STORAGE_CONNSTR"] = Environment.GetEnvironmentVariable("STORAGE_CONNSTR");
      }

      services.Configure<SendGridOptions>(sendgridSection);

      // Get SMTP configuration options
      var smtpSection = configuration.GetSection("SmtpOptions");
      if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
      {
        smtpSection["AcctSid"] = Environment.GetEnvironmentVariable("TwilioSid");
        smtpSection["AcctToken"] = Environment.GetEnvironmentVariable("TwilioToken");
        smtpSection["FromNumber"] = Environment.GetEnvironmentVariable("TwilioNumber");
      }
      services.Configure<SmtpOptions>(smtpSection);

      // Add email services.
      services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
      services.AddTransient<IEmailSender, EmailSender>();
      // Add memory cache services
      services.AddMemoryCache();

      //Configure catalogSettings
      services.Configure<CatalogSettings>(configuration);
      var catalogSettings = configuration.Get<CatalogSettings>();
      var catalogUrl = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
       ? Environment.GetEnvironmentVariable("CatalogBaseUrl") : catalogSettings.CatalogBaseUrl;
      catalogSettings.CatalogBaseUrl = catalogUrl;
       services.AddSingleton<IUriComposer>(new UriComposer(catalogSettings));

      services.AddScoped<IAppUserManageService, AppUserManageService>();
      services.AddScoped<IBlobFileService, CloudBlobFileService>();

    }

    public static void ConfigureCatalogServices(IConfiguration configuration, IServiceCollection services)
    {
      var connStr = configuration.GetConnectionString(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
          ? "CatalogProdConn"
          : "DefaultConnection");

      services.AddDbContext<CatalogContext>(c =>
        c.UseSqlServer(connStr));

      services.AddTransient<IFunctional, Functional>();

      services.AddScoped(typeof(IAsyncRepository<>), typeof(EfRepository<>));
      services.AddScoped(typeof(ICatalogRepository<>), typeof(CatalogRepository<>));
      services.AddTransient<IRoles, Roles>();
      services.AddScoped<IBasketService, BasketService>();

      services.AddRouting(options =>
      {
        // Replace the type and the name used to refer to it with your own
        // IOutboundParameterTransformer implementation
        options.ConstraintMap["slugify"] = typeof(SlugifyParameterTransformer);
      });

    }

    private static void CreateIdentityIfNotCreated(IServiceCollection services, IConfiguration configuration)
    {
      // Get Identity Default Options
      IConfigurationSection identityDefaultOptionsConfigurationSection = configuration.GetSection("IdentityDefaultOptions");
      services.Configure<IdentityDefaultOptions>(identityDefaultOptionsConfigurationSection);
      var identityDefaultOptions = identityDefaultOptionsConfigurationSection.Get<IdentityDefaultOptions>();

      var sp = services.BuildServiceProvider();
      using (var scope = sp.CreateScope())
      {
        var existingUserManager = scope.ServiceProvider
          .GetService<UserManager<ApplicationUser>>();
        if (existingUserManager == null)
        {
          services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
              // Password settings
              options.Password.RequireDigit = identityDefaultOptions.PasswordRequireDigit;
              options.Password.RequiredLength = identityDefaultOptions.PasswordRequiredLength;
              options.Password.RequireNonAlphanumeric = identityDefaultOptions.PasswordRequireNonAlphanumeric;
              options.Password.RequireUppercase = identityDefaultOptions.PasswordRequireUppercase;
              options.Password.RequireLowercase = identityDefaultOptions.PasswordRequireLowercase;
              options.Password.RequiredUniqueChars = identityDefaultOptions.PasswordRequiredUniqueChars;

              // Lockout settings
              options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identityDefaultOptions.LockoutDefaultLockoutTimeSpanInMinutes);
              options.Lockout.MaxFailedAccessAttempts = identityDefaultOptions.LockoutMaxFailedAccessAttempts;
              options.Lockout.AllowedForNewUsers = identityDefaultOptions.LockoutAllowedForNewUsers;

              // User settings
              options.User.RequireUniqueEmail = identityDefaultOptions.UserRequireUniqueEmail;

              // email confirmation require
              options.SignIn.RequireConfirmedEmail = identityDefaultOptions.SignInRequireConfirmedEmail;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

          // cookie settings
          services.ConfigureApplicationCookie(options =>
          {
            // Cookie settings
            options.Cookie.HttpOnly = identityDefaultOptions.CookieHttpOnly;
            options.Cookie.Expiration = TimeSpan.FromDays(identityDefaultOptions.CookieExpiration);
            options.LoginPath = identityDefaultOptions.LoginPath; // If the LoginPath is not set here, ASP.NET Core will default to /Account/Login
            options.LogoutPath = identityDefaultOptions.LogoutPath; // If the LogoutPath is not set here, ASP.NET Core will default to /Account/Logout
            options.AccessDeniedPath = identityDefaultOptions.AccessDeniedPath; // If the AccessDeniedPath is not set here, ASP.NET Core will default to /Account/AccessDenied
            options.SlidingExpiration = identityDefaultOptions.SlidingExpiration;
          });
        }
      }
    }


    private static void ConfigureCookieSettings(IServiceCollection services)
    {
      services.Configure<CookiePolicyOptions>(options =>
      {
        // This lambda determines whether user consent for non-essential cookies is needed for a given request.
        options.CheckConsentNeeded = context => true;
        options.MinimumSameSitePolicy = SameSiteMode.None;
      });
      services.ConfigureApplicationCookie(options =>
      {
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Signout";
        options.Cookie = new CookieBuilder
        {
          IsEssential =
            true // required for auth to work without explicit user consent; adjust to suit your privacy policy
        };
      });
    }
  }
}
