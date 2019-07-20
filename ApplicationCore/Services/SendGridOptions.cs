using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Services
{
  public class SendGridOptions
  {
    public string SendGridUser { get; set; }
    public string SendGridKey { get; set; }
    public string FromEmail { get; set; }
    public string FromFullName { get; set; }
    public bool IsDefault { get; set; }
    public string STORAGE_CONNSTR { get; set; }
  }
}
