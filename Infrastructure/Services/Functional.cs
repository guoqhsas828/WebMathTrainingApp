using StoreManager.Data;
using StoreManager.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace StoreManager.Services
{
  public class Functional : IFunctional
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IRoles _roles;
    private readonly SuperAdminDefaultOptions _superAdminDefaultOptions;

    public Functional(UserManager<ApplicationUser> userManager,
      RoleManager<IdentityRole> roleManager,
      ApplicationDbContext context,
      SignInManager<ApplicationUser> signInManager,
      IRoles roles,
      IOptions<SuperAdminDefaultOptions> superAdminDefaultOptions)
    {
      _userManager = userManager;
      _roleManager = roleManager;
      _context = context;
      _signInManager = signInManager;
      _roles = roles;
      _superAdminDefaultOptions = superAdminDefaultOptions.Value;
    }

    public async Task InitAppData()
    {
      try
      {
        if (_context.ProductType.Any())
          return;

        if (!_context.BillType.Any())
        {
          await _context.BillType.AddAsync(new BillType {BillTypeName = "Default"});
          await _context.SaveChangesAsync();

        }

        if (!_context.Branch.Any())
        {
          await _context.Branch.AddAsync(new Branch {BranchName = "Allison's Shop", City="", Phone="9085174456"});
          await _context.SaveChangesAsync();
        }

        if (!_context.Warehouse.Any())
        {
          await _context.Warehouse.AddAsync(new Warehouse { WarehouseName = "Default" });
          await _context.SaveChangesAsync();

        }

        if (!_context.CashBank.Any())
        {
          await _context.CashBank.AddAsync(new CashBank {CashBankName = "Default"});
          await _context.SaveChangesAsync();
        }

        if (!_context.Currency.Any())
        {
          await _context.Currency.AddAsync(new Currency { CurrencyName = "USD", CurrencyCode = "USD" });
          await _context.SaveChangesAsync();
        }

        if (!_context.InvoiceType.Any())
        {
          await _context.InvoiceType.AddAsync(new InvoiceType {InvoiceTypeName = "Default"});
          await _context.SaveChangesAsync();
        }


        if (!_context.PaymentType.Any())
        {
          await _context.PaymentType.AddAsync(new PaymentType { PaymentTypeName = "CashPayment" });
          await _context.SaveChangesAsync();
        }

        if (!_context.PurchaseType.Any())
        {
          await _context.PurchaseType.AddAsync(new PurchaseType { PurchaseTypeName = "Local Purchase" });
          await _context.SaveChangesAsync();
        }

        if (!_context.SalesType.Any())
        {
          await _context.SalesType.AddAsync(new SalesType { SalesTypeName = "Online Sale" });
          await _context.SaveChangesAsync();
        }

        if (!_context.ShipmentType.Any())
        {
          await _context.ShipmentType.AddAsync(new ShipmentType { ShipmentTypeName = "Pickup" });
          await _context.SaveChangesAsync();
        }

        if (!_context.UnitOfMeasure.Any())
        {
          await _context.UnitOfMeasure.AddAsync(new UnitOfMeasure { UnitOfMeasureName = "Cup" });
          await _context.UnitOfMeasure.AddAsync(new UnitOfMeasure { UnitOfMeasureName = "Bottle" });
          await _context.UnitOfMeasure.AddAsync(new UnitOfMeasure { UnitOfMeasureName = "Pcs" });
          await _context.SaveChangesAsync();
        }

        if (!_context.ProductType.Any())
        {
          await _context.ProductType.AddRangeAsync(
            GetPreconfiguredCatalogTypes());

          await _context.SaveChangesAsync();
        }

        if (!_context.CatalogBrand.Any())
        {
          _context.CatalogBrand.AddRange(
            GetPreconfiguredCatalogBrands());

          await _context.SaveChangesAsync();
        }


        if (!_context.Product.Any())
        {
          var products = GetPreconfiguredItems();
          await _context.Product.AddRangeAsync(products);
          await _context.SaveChangesAsync();
        }

        if (!_context.CustomerType.Any())
        {
          await _context.CustomerType.AddAsync(new CustomerType {CustomerTypeName = "Default"});
          await _context.SaveChangesAsync();
        }

        if (!_context.Customer.Any())
        {
          List<Customer> customers = new List<Customer>()
          {
            new Customer {CustomerName = "Hanari Carnes", Address = "Rua do Paço, 67"},
            new Customer {CustomerName = "Old World Delicatessen", Address = "2743 Bering St."}
          };
          await _context.Customer.AddRangeAsync(customers);
          await _context.SaveChangesAsync();

        }

        if (!_context.VendorType.Any())
        {
          await _context.VendorType.AddAsync(new VendorType {VendorTypeName = "Default"});
          await _context.SaveChangesAsync();
        }

        if (!_context.Vendor.Any())
        {
          var vendors = new List<Vendor>()
          {
            new Vendor {VendorName = "Exotic Liquids", Address = "49 Gilbert St."},
            new Vendor
            {
              VendorName = "New England Seafood Cannery", Address = "Order Processing Dept. 2100 Paul Revere Blvd."
            }
          };
          await _context.Vendor.AddRangeAsync(vendors);
          await _context.SaveChangesAsync();

        }

      }
      catch (Exception)
      {

        throw;
      }
    }

    static IEnumerable<CatalogBrand> GetPreconfiguredCatalogBrands()
    {
      return new List<CatalogBrand>()
      {
        new CatalogBrand() {Brand = "Other"},
        new CatalogBrand() {Brand = "Allison"},
        new CatalogBrand() {Brand = "Jason"},
      };
    }

    static IEnumerable<Product> GetPreconfiguredItems()
    {
      return new List<Product>()
      {
        new Product()
        {
          ProductTypeId = 3, CatalogBrandId = 3, Description = "Lemonade made from Allison's shop", ProductName = "Allison's Lemonade",
          DefaultBuyingPrice = 0.5, CurrencyId = 1, UnitOfMeasureId = 1, 
          DefaultSellingPrice = 1.25, ProductImageUrl = "http://catalogbaseurltobereplaced/images/products/1.png"
        },
        new Product()
        {
          ProductTypeId = 2, CatalogBrandId = 3, Description = "Bottle of Lemonade", ProductName = "Lemonade(s)",
          DefaultBuyingPrice = 1.5, CurrencyId = 1, UnitOfMeasureId = 2,
          DefaultSellingPrice = 3.00,
          ProductImageUrl = "http://catalogbaseurltobereplaced/images/products/lemonade-clipart.png"
        },
        new Product()
        {
          ProductTypeId = 3, CatalogBrandId = 2, Description = "Milk shake", ProductName = "MilkShake",
          DefaultBuyingPrice = 0.5, CurrencyId = 1, UnitOfMeasureId = 3,
          DefaultSellingPrice = 1.5, ProductImageUrl = "http://catalogbaseurltobereplaced/images/products/2.png"
        },
        new Product()
        {
          ProductTypeId = 3, CatalogBrandId = 3, Description = "Lemonade made from Elerie's shop", ProductName = "Elerie's Lemonade",
          DefaultBuyingPrice = 0.5, CurrencyId = 1, UnitOfMeasureId = 1,
          DefaultSellingPrice = 1.25, ProductImageUrl = "http://catalogbaseurltobereplaced/images/products/1.png"
        },
      };
    }

    static IEnumerable<ProductType> GetPreconfiguredCatalogTypes()
    {
      return new List<ProductType>()
      {
        new ProductType() {ProductTypeName = "Other"},
        new ProductType() {ProductTypeName = "Drink"},
        new ProductType() {ProductTypeName = "Cookies"},
      };
    }

    public async Task SendEmailBySendGridAsync(string apiKey,
      string fromEmail,
      string fromFullName,
      string subject,
      string message,
      string email)
    {
      var client = new SendGridClient(apiKey);
      var msg = new SendGridMessage()
      {
        From = new EmailAddress(fromEmail, fromFullName),
        Subject = subject,
        PlainTextContent = message,
        HtmlContent = message
      };
      msg.AddTo(new EmailAddress(email, email));
      await client.SendEmailAsync(msg);

    }

    public async Task SendEmailByGmailAsync(string fromEmail,
      string fromFullName,
      string subject,
      string messageBody,
      string toEmail,
      string toFullName,
      string smtpUser,
      string smtpPassword,
      string smtpHost,
      int smtpPort,
      bool smtpSSL)
    {
      var body = messageBody;
      var message = new MailMessage();
      message.To.Add(new MailAddress(toEmail, toFullName));
      message.From = new MailAddress(fromEmail, fromFullName);
      message.Subject = subject;
      message.Body = body;
      message.IsBodyHtml = true;

      using (var smtp = new SmtpClient())
      {
        var credential = new NetworkCredential
        {
          UserName = smtpUser,
          Password = smtpPassword
        };
        smtp.Credentials = credential;
        smtp.Host = smtpHost;
        smtp.Port = smtpPort;
        smtp.EnableSsl = smtpSSL;
        await smtp.SendMailAsync(message);

      }

    }

    public async Task CreateDefaultSuperAdmin()
    {
      await EnsureRolesAsync(_roleManager);
      await CreateUser(_superAdminDefaultOptions.Email, _superAdminDefaultOptions.Password, _superAdminDefaultOptions.UserName, "");
      var adminUser = await _userManager.FindByEmailAsync(_superAdminDefaultOptions.Email);
      await _roles.AddToRoles(adminUser.Id);
    }

    private async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
      await _roles.GenerateRolesFromPagesAsync();
      var alreadyExists = await roleManager.RoleExistsAsync(Constants.AdministratorRole);

      if (alreadyExists) return;

      await roleManager.CreateAsync(new IdentityRole(Constants.AdministratorRole));
    }

    public async Task CreateUserProfile(ApplicationUser existingUser, string firstName, string lastName)
    {
      var profile = _context.UserProfile.SingleOrDefault(x => x.ApplicationUserId.Equals(existingUser.Id));
      if (profile == null)
      {
        //await _roles.GenerateRolesFromPagesAsync();
        //add to user profile
        profile = new UserProfile
        {
          FirstName = firstName,
          LastName = lastName,
          Email = existingUser.Email,
          UserName = existingUser.UserName,
          Password = existingUser.PasswordHash,
          ConfirmPassword = existingUser.PasswordHash,
          ApplicationUserId = existingUser.Id
        };
        await _context.UserProfile.AddAsync(profile);
        await _context.SaveChangesAsync();
       // await _roles.AddToRoles(existingUser.Id);
      }
    }

    public async Task CreateCustomerProfile(ApplicationUser existingUser, string firstName, string lastName)
    {
      var customer = _context.Customer.SingleOrDefault(x => x.Email.Equals(existingUser.Email));
      if (customer == null)
      {
        //add to user profile
        customer = new Customer
        {
          CustomerName = firstName + " " + lastName,
          Email = existingUser.Email,
          Phone = existingUser.PhoneNumber
        };
        await _context.Customer.AddAsync(customer);
        await _context.SaveChangesAsync();
      }
    }

    public async Task<IdentityResult> CreateUser(string email, string passwd, string firstName, string lastName)
    {
      try
      {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
          await CreateUserProfile(existingUser, firstName, lastName);
          await CreateCustomerProfile(existingUser, firstName, lastName);
          existingUser.PhoneNumberConfirmed = true;
          return await _userManager.UpdateAsync(existingUser);
        }

        var newUser = new ApplicationUser {Email = email, UserName = email, EmailConfirmed = true};

        var result = await _userManager.CreateAsync(newUser, passwd);
        if (result.Succeeded)
        {
          await CreateUserProfile(newUser, firstName, lastName);
          await CreateCustomerProfile(newUser, firstName, lastName);
        }

        return result;
      }
      catch (Exception)
      {

        throw;
      }
    }

    public async Task<string> UploadFile(List<IFormFile> files, IHostingEnvironment env, string uploadFolder)
    {
      var result = "";

      var webRoot = env.WebRootPath;
      var uploads = System.IO.Path.Combine(webRoot, uploadFolder);
      var extension = "";
      var filePath = "";
      var fileName = "";


      foreach (var formFile in files)
      {
        if (formFile.Length > 0)
        {
          extension = System.IO.Path.GetExtension(formFile.FileName);
          fileName = Guid.NewGuid().ToString() + extension;
          filePath = System.IO.Path.Combine(uploads, fileName);

          using (var stream = new FileStream(filePath, FileMode.Create))
          {
            await formFile.CopyToAsync(stream);
          }

          result = fileName;

        }
      }

      return result;
    }

  }
}
