using StoreManager.Interfaces;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace StoreManager.Models
{
  public class Basket : CatalogEntityModel, IAggregateRoot
  {
    public int BasketId { get { return Id; } set { Id = value; } }
    [MaxLength(900)]
    public string BuyerId { get; set; }
    private readonly List<BasketItem> _items = new List<BasketItem>();
    public ICollection<BasketItem> Items => _items;

    public void AddItem(int catalogItemId, decimal unitPrice, int quantity = 1)
    {
      if (!Items.Any(i => i.CatalogItemId == catalogItemId))
      {
        _items.Add(new BasketItem()
        {
          CatalogItemId = catalogItemId,
          Quantity = quantity,
          UnitPrice = unitPrice
        });
        return;
      }
      var existingItem = Items.FirstOrDefault(i => i.CatalogItemId == catalogItemId);
      existingItem.Quantity += quantity;
    }
  }
}
