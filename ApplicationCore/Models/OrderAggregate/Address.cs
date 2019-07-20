using System;

namespace StoreManager.Models
{
  public class Address // ValueObject
  {
    private Address()
    {
      City = "Summit";
      State = "NJ";
      Country = "US";
      ZipCode = "07901";
      Street = "";
    }

    public int AddressId { get; set; }
    public String Street { get; set; }

    public String City { get; private set; }

    public String State { get; private set; }

    public String Country { get; private set; }

    public String ZipCode { get; private set; }

    public Address(string street) : this()
    {
      Street = street;
    }
  }
}