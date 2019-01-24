using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    public LocalizedString GetLocalizedHtmlString(string key)
    {
      return _localizer[key.ToLower()];
    }
  }

  public class SharedResource
  {
  }
}
