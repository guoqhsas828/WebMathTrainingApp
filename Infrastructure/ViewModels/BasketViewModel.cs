using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.Web.Pages.Basket
{
  public class BasketViewModel
  {
    public BasketViewModel()
    {
      StreetAddress = "582 Springfield Ave";
      OrderNotes = "Pick up";
    }

    public int Id { get; set; }
    public List<BasketItemViewModel> Items { get; set; } = new List<BasketItemViewModel>();
    public string BuyerId { get; set; }
    public string StreetAddress { get; set; }
    public string OrderNotes { get; set; }

    public decimal Total()
    {
      return Math.Round(Items.Sum(x => x.UnitPrice * x.Quantity), 2);
    }
  }
}
