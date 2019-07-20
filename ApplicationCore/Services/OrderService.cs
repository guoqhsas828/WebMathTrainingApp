using System;
using StoreManager.Interfaces;
using System.Threading.Tasks;
using StoreManager.Models;
using System.Collections.Generic;
//using Ardalis.GuardClauses;
using System.Linq;
using Ardalis.GuardClauses;
using StoreManager.Specifications;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace StoreManager.Services
{
  public class OrderService : IOrderService
  {
    private readonly IAsyncRepository<SalesOrder> _orderRepository;
    private readonly ICatalogRepository<Basket> _basketRepository;
    private readonly IAsyncRepository<Product> _itemRepository;
    private readonly IAsyncRepository<Customer> _customerRepository;
    private readonly IAsyncRepository<Branch> _branchRepository;
    private readonly IEmailSender _emailSender;

    public OrderService(ICatalogRepository<Basket> basketRepository,
        IAsyncRepository<Product> itemRepository,
        IAsyncRepository<SalesOrder> orderRepository,
      IAsyncRepository<Customer> customerRepo,
      IAsyncRepository<Branch> branchRepo,
      IEmailSender emailSender)
    {
      _orderRepository = orderRepository;
      _basketRepository = basketRepository;
      _itemRepository = itemRepository;
      _customerRepository = customerRepo;
      _branchRepository = branchRepo;
      _emailSender = emailSender;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
      var basket = await _basketRepository.GetByIdAsync(basketId);
      Guard.Against.NullBasket(basketId, basket);
      Guard.Against.NullBuyerId(basket?.BuyerId, basket);
      var items = new List<SalesOrderLine>();
      int? ccy = null;
      HashSet<Branch> contacts = new HashSet<Branch>();
      foreach (var item in basket.Items)
      {
        var catalogItem = await _itemRepository.GetByIdAsync(item.CatalogItemId);
        var branch = await _branchRepository.GetByIdAsync(catalogItem.BranchId);
        if (branch != null) contacts.Add(branch);
        var itemOrdered = catalogItem;
        var orderItem = new SalesOrderLine { Product = itemOrdered,
          Price = Convert.ToDouble(item.UnitPrice), Quantity = Convert.ToDouble(item.Quantity),
          ProductId = itemOrdered.Id, Amount = item.Quantity };
        orderItem.Total = orderItem.SubTotal = orderItem.Price * orderItem.Quantity;
        if (orderItem.Product != null) ccy = orderItem.Product.CurrencyId;
        items.Add(orderItem);
      }

      var customer = _customerRepository.ListAsync(new CustomerSpecification(basket?.BuyerId)).Result.FirstOrDefault();
      var order = new SalesOrder
      {
        Amount = items.Count,
        Total = items.Sum(t => t.Total),  SalesOrderLines = items ,
        CustomerRefNumber = "Pending",
        OrderDate = DateTimeOffset.Now,
        DeliveryDate = DateTime.Now.AddDays(1),
        SalesOrderName = basket.BuyerId,
        Remarks = "Pick up",
      };

      order.SubTotal = order.Total;

      if (customer != null)
      {
        order.CustomerId = customer.Id;
        //order.
      }

      if (ccy.HasValue)
        order.CurrencyId = ccy.Value;

      if (contacts.Any())
      {
        order.BranchId = contacts.First().Id;
      }
      await _orderRepository.AddAsync(order);

      foreach (var branch in contacts.Where(b => !string.IsNullOrWhiteSpace(b.Phone)))
      {
        await SendSmsMessage(order, branch, branch.Phone);
      }
    }

    public async Task SendSmsMessage(SalesOrder order, Branch branch, string phoneNumber)
    {
      string msgText = $"SalesOrder# {order.Id} from {order.SalesOrderName}: " + Environment.NewLine;
      foreach (var orderItem in order.SalesOrderLines)
      {
        if (orderItem.Product.BranchId != branch.Id)
          continue;

        msgText += $"{orderItem.Quantity} {orderItem.Product.ProductName} " + Environment.NewLine;
      }

      await _emailSender.SendSmsMessage(msgText, phoneNumber);
    }
  }
}
