using System;
using System.ComponentModel.DataAnnotations;

namespace StoreManager.Models
{
  public class Branch : BaseEntityModel
  {
    public int BranchId
    {
      get { return Id; }
      set { Id = value; }
    }

    [Required] [MaxLength(64)] public string BranchName { get; set; }
    [MaxLength(512)] public string Description { get; set; }
    [Display(Name = "Currency")] public int CurrencyId { get; set; }

    [Display(Name = "Street Address")]
    [MaxLength(256)]
    public string Address { get; set; }

    [MaxLength(128)] public string City { get; set; }
    [MaxLength(128)] public string State { get; set; }

    [Display(Name = "Zip Code")]
    [MaxLength(32)]
    public string ZipCode { get; set; }

    [MaxLength(32)] public string Phone { get; set; }
    [MaxLength(128)] public string Email { get; set; }

    [Display(Name = "Contact Person")]
    [MaxLength(128)]
    public string ContactPerson { get; set; }
  }
}
