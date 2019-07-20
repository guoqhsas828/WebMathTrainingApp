using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class ProductType : BaseEntityModel
  {
    public int ProductTypeId
    {
      get { return Id; }
      set { Id = value; }
    }

    [Required] [MaxLength(128)] public string ProductTypeName { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
  }
}
