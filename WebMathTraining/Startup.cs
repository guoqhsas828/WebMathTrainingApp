using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using WebMathTraining.Data;
using WebMathTraining.Models;
using WebMathTraining.Resources;
using WebMathTraining.Services;
using WebMathTraining.Utilities;
using System.Threading;
using WebEssentials.AspNetCore.OutputCaching;
using WebMarkupMin.AspNetCore2;
using WebMarkupMin.Core;
using WilderMinds.MetaWeblog;
using IWmmLogger = WebMarkupMin.Core.Loggers.ILogger;
using MetaWeblogService = WebMathTraining.Services.MetaWeblogService;
using WmmNullLogger = WebMarkupMin.Core.Loggers.NullLogger;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.eShopWeb.Infrastructure.Extensions;
using StoreManager.Services;

namespace WebMathTraining
{
  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }
    //public Startup(IHostingEnvironment env)
    //{
    //  var builder = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
    //    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    //    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

    //  //if (env.IsDevelopment())
    //  //  builder.AddUserSecrets();

    //  builder.AddEnvironmentVariables();
    //  Configuration = builder.Build();
    //}

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      ServiceConfigurationHelper.ConfigSharedServices(Configuration, services);
      ServiceConfigurationHelper.ConfigureCatalogServices(Configuration, services);

      // Register a custom policy
      services.AddTransient<IAuthorizationHandler, CountryAuthorizationHandler>();

      //Register a resource policy
      services.AddTransient<IAuthorizationHandler, ActionApprovalHandler>();

      services.AddScoped<ITodoItemService, TodoItemService>();


      services.AddScoped<ITestQuestionService<int>, TestQuestionService>();

      services.AddScoped<ITestSessionService<int>, TestSessionService>();
      services.AddAuthentication();

      //// Auth 2/2: Use Cookies (step 1) to create the default authorization policy
      //services.AddAuthorization(opts =>
      //{
      //  opts.DefaultPolicy = new AuthorizationPolicyBuilder("Cookie")
      //    .RequireAuthenticatedUser().Build();

      //  opts.AddPolicy("NewZealandCustomers", policy =>
      //  {
      //    policy.RequireRole("Customers");
      //    policy.RequireClaim(ClaimTypes.Country, "New Zealand");
      //  });

      //  opts.AddPolicy("NotNewZealand", policy =>
      //  {
      //    policy.RequireAuthenticatedUser();
      //    policy.AddRequirements(new BlockCountriesRequirement(new string[]
      //    {
      //      "new Zealand"
      //    }));
      //  });

      //  opts.AddPolicy("ActionPolicy", policy =>
      //  {
      //    policy.AddRequirements(new ActionApprovalRequirement
      //      {
      //        AllowManager = true,
      //        AllowAssistant = true
      //      })
      //      ;
      //  });
      //});

      // services.AddSingleton<IClaimsTransformation, ClaimsTransformer>();
      services.AddSingleton<LocService>();
      services.AddLocalization(options => options.ResourcesPath = "Resources");

      services.AddMvc().AddViewLocalization(
        //LanguageViewLocationExpanderFormat.Suffix,
        //  opts => { opts.ResourcesPath = "Resources";}
        ).AddDataAnnotationsLocalization(
          options =>
      {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
          factory.Create(typeof(SharedResource));
      }
          );

      services.Configure<RequestLocalizationOptions>(options =>
      {
        var supportedCultures = new[]
        {
          new CultureInfo("en-US"),
          new CultureInfo("zh-CN"),
        };
        options.DefaultRequestCulture = new RequestCulture(culture: "en-US", uiCulture: "en-US");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
        options.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
      });

      // services.Configure<AppConfiguration>(options => Configuration.GetSection("AppConfiguration").Bind(options));
      services.AddSingleton<IBlogService, FileBlogService>();
      services.Configure<BlogSettings>(Configuration.GetSection("blog"));
      services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
      services.AddMetaWeblog<MetaWeblogService>();
      // Progressive Web Apps https://github.com/madskristensen/WebEssentials.AspNetCore.ServiceWorker
      services.AddProgressiveWebApp(new WebEssentials.AspNetCore.Pwa.PwaOptions
      {
        OfflineRoute = "/shared/offline/"
      });

      // Output caching (https://github.com/madskristensen/WebEssentials.AspNetCore.OutputCaching)
      services.AddOutputCaching(options =>
      {
        options.Profiles["default"] = new OutputCacheProfile
        {
          Duration = 3600
        };
      });

      //// Cookie authentication.
      services
          .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
          .AddCookie(options =>
          {
            options.LoginPath = "/login/";
            options.LogoutPath = "/logout/";
          });

      //// HTML minification (https://github.com/Taritsyn/WebMarkupMin)
      services
          .AddWebMarkupMin(options =>
          {
            options.AllowMinificationInDevelopmentEnvironment = true;
            options.DisablePoweredByHttpHeaders = true;
          })
          .AddHtmlMinification(options =>
          {
            options.MinificationSettings.RemoveOptionalEndTags = false;
            options.MinificationSettings.WhitespaceMinificationMode = WhitespaceMinificationMode.Safe;
          });
      services.AddSingleton<IWmmLogger, WmmNullLogger>(); // Used by HTML minifier

      // Bundling, minification and Sass transpilation (https://github.com/ligershark/WebOptimizer)
      services.AddWebOptimizer(pipeline =>
      {
        pipeline.MinifyJsFiles();
        //pipeline.CompileScssFiles()
        //        .InlineImages(1);
      });
      //services.AddRequestScopingMiddleware(() => scopeProvider.Value = new Scope());
      //services.AddCustomControllerActivation(Resolve);
      //services.AddCustomViewComponentActivation(Resolve);
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
      //this.Kernel = this.RegisterApplicationComponents(app);

      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
        app.UseDatabaseErrorPage();
      }
      else
      {
        app.UseExceptionHandler("/Home/Error");
      }

      app.UseWebOptimizer();
      app.UseStaticFiles();

      app.UseMetaWeblog("/metaweblog");
      var locOptions = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
      app.UseRequestLocalization(locOptions.Value);

      app.UseAuthentication();

      app.UseOutputCaching();
      app.UseWebMarkupMin();

      app.UseMvc(routes =>
      {
        routes.MapRoute(
          "Default",
          "{controller}/{action}/{id}",
          new { controller = "Home", action = "Index" });
      });

    }
  }
}
