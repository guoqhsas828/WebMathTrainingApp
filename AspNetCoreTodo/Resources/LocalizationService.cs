using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Localization;

namespace WebMathTraining.Resources
{
  public class LocService
  {
    private readonly IStringLocalizer _localizer;

    public LocService(IStringLocalizerFactory factory)
    {
      var type = typeof(SharedResource);
      var assemblyName = new AssemblyName(type.GetTypeInfo().Assembly.FullName);
      _localizer = factory.Create("SharedResource", assemblyName.Name);
    }

    public LocalizedString GetLocalizedHtmlString(string key, string splitSymbol = null)
    {
      if (string.IsNullOrEmpty(splitSymbol))
         return _localizer[key.ToLower()];
      else
      {
        var sbr = new StringBuilder();
        var name = "";
        foreach (var str in key.ToLower().Split(splitSymbol))
        {
          var lsr = _localizer[str];
          name = lsr.Name;
          sbr.Append(lsr.Value);
        }

        return new LocalizedString(name, sbr.ToString());
      }
    }
  }

  public class SharedResource
  {
  }
}
