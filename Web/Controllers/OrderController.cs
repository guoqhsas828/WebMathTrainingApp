using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.Web.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Interfaces;
using StoreManager.Models;
using StoreManager.Specifications;

namespace Microsoft.eShopWeb.Web.Controllers
{
  [ApiExplorerSettings(IgnoreApi = true)]
  [Authorize] // Controllers that mainly require Authorization still use Controller/View; other pages use Pages
  [Route("[controller]/[action]")]
  public class OrderController : Controller
  {
    private readonly IOrderRepository _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IAsyncRepository<Customer> _customeRepository;
    public OrderController(IOrderRepository orderRepository, IUriComposer uriComposer, IAsyncRepository<Customer> customerRepo)
    {
      _orderRepository = orderRepository;
      _uriComposer = uriComposer;
      _customeRepository = customerRepo;
    }

    [HttpGet()]
    public async Task<IActionResult> MyOrders()
    {
      var orders = await _orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(User.Identity.Name));
      
      var viewModel = orders
          .Select(o => new OrderViewModel()
          {
            OrderDate = o.OrderDate,
            OrderItems = o.SalesOrderLines?.Select(oi => new OrderItemViewModel()
            {
              Discount = 0,
              PictureUrl = _uriComposer.ComposePicUri(oi.Product.ProductImageUrl),
              ProductId = oi.Product.Id,
              ProductName = oi.Product.ProductName,
              UnitPrice = Convert.ToDecimal(oi.Price),
              Units = Convert.ToInt32(oi.Quantity)
            }).ToList(),
            OrderNumber = o.Id,
            //ippingAddress = o.ShipToAddress,
            OrderNotes = o.Remarks,
            Status = o.CustomerRefNumber,
            Total = Convert.ToDecimal(o.Total),
          });
      return View(viewModel);
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> Detail(int orderId)
    {
      var customerOrders = await _orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(User.Identity.Name));
      var order = customerOrders.FirstOrDefault(o => o.Id == orderId);
      if (order == null)
      {
        return BadRequest("No such order found for this user.");
      }

      var customer = _customeRepository.ListAsync(new CustomerSpecification(order.SalesOrderName)).Result.FirstOrDefault();
      var viewModel = new OrderViewModel()
      {
        OrderDate = order.OrderDate,
        OrderItems = order.SalesOrderLines?.Select(oi => new OrderItemViewModel()
        {
          Discount = 0,
          PictureUrl = _uriComposer.ComposePicUri(oi.Product.ProductImageUrl),
          ProductId = oi.Product.Id,
          ProductName = oi.Product.ProductName,
          UnitPrice = Convert.ToDecimal(oi.Price),
          Units = Convert.ToInt32(oi.Quantity)
        }).ToList(),
        OrderNumber = order.Id,
        ShippingAddress = new Address(customer?.Address),
        OrderNotes = order.Remarks,
        Status = order.CustomerRefNumber,
        Total = Convert.ToDecimal( order.Total)
      };
      return View(viewModel);
    }
  }
}
