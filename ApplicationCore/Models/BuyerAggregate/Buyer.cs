using StoreManager.Models;
using StoreManager.Interfaces;
using Ardalis.GuardClauses;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate
{
  public class Buyer : CatalogEntityModel, IAggregateRoot
  {
    public int BuyerId { get { return Id; } set { Id = value; } }
    public string IdentityGuid { get; private set; }

    private List<PaymentMethod> _paymentMethods = new List<PaymentMethod>();

    public IEnumerable<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    private Buyer()
    {
      // required by EF
    }

    public Buyer(string identity) : this()
    {
      Guard.Against.NullOrEmpty(identity, nameof(identity));
      IdentityGuid = identity;
    }
  }
}
