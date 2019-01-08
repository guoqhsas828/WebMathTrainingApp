using System;
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
using WebMathTraining.Data;
using WebMathTraining.Models;
using WebMathTraining.Services;
using WebMathTraining.Utilities;

namespace WebMathTraining
{
  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Use SQL Database if in Azure, otherwise, use localdb
            var connStr = "";
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
            {
                connStr = Configuration.GetConnectionString("ProdDbConnection");
                services.AddDbContext<ApplicationDbContext>(options =>
                  options.UseSqlServer(connStr));

                services.AddDbContext<TestDbContext>(options => options.UseSqlServer(connStr));
            }
            else
            {
                connStr = Configuration.GetConnectionString("DefaultConnection");
                services.AddDbContext<ApplicationDbContext>(options =>
                  options.UseSqlServer(connStr));

                services.AddDbContext<TestDbContext>(options =>
                options.UseSqlServer(connStr));
            }


            services.AddIdentity<ApplicationUser, IdentityRole>(
          //  opts =>
          //  {
          //opts.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
          //opts.User.RequireUniqueEmail = true;

          //opts.Password.RequireNonAlphanumeric = true;
          //opts.Password.RequireLowercase = true;
          //opts.Password.RequireUppercase = true;
          //opts.Password.RequireDigit = true;
          //opts.Password.RequiredLength = 6;

          ////// Auth 1/2: Allow cookie authentication
          ////opts.Cookies.ApplicationCookie.AuthenticationScheme = "Cookie";
          ////opts.Cookies.ApplicationCookie.AutomaticAuthenticate = true;
          ////opts.Cookies.ApplicationCookie.AutomaticChallenge = true;
          ////opts.Cookies.ApplicationCookie.LoginPath = "/UserAccount/Login";
          ////opts.Cookies.ApplicationCookie.AccessDeniedPath = "/UserAccount/AccessDenied";
          //  }
          )
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders()
        .AddErrorDescriber<CustomIdentityErrorDescriber>();

            //Automatically perform database migration
            services.BuildServiceProvider().GetService<ApplicationDbContext>().Database.Migrate();
            services.BuildServiceProvider().GetService<TestDbContext>().Database.Migrate();
            // Register a custom password validator
            // Have to add after AddIdentity Service. Otherwise the built-in password validation won't work
            services.AddTransient<IPasswordValidator<ApplicationUser>, NoNamePasswordValidator>();

            // Register a custom user validator
            services.AddTransient<IUserValidator<ApplicationUser>, EmailUserValidator>();

            // Register a custom policy
            services.AddTransient<IAuthorizationHandler, CountryAuthorizationHandler>();

            //Register a resource policy
            services.AddTransient<IAuthorizationHandler, ActionApprovalHandler>();

            // Add application services.
            services.AddTransient<IEmailSender, EmailSender>();

            services.AddScoped<ITodoItemService, TodoItemService>();

            services.AddScoped<IAppUserManageService, AppUserManageService>();

          services.AddScoped<ITestQuestionService, TestQuestionService>();

          services.AddScoped<ITestSessionService, TestSessionService>();

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

            services.AddMvc();
        }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
        app.UseDatabaseErrorPage();
      }
      else
      {
        app.UseExceptionHandler("/Home/Error");
      }

      app.UseStaticFiles();

      //ApplicationDbContextSeed.Seed(app.ApplicationServices);

      app.UseAuthentication();

      app.UseMvc(routes =>
      {
        routes.MapRoute(
          name: "default",
          template: "{controller=Todo}/{action=Index}/{id?}");
      });

    }

  }
}
