using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class Vendor
  {
    public int VendorId { get; set; }
    [Required] [MaxLength(128)] public string VendorName { get; set; }
    [Display(Name = "Vendor Type")] public int VendorTypeId { get; set; }

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
