using Microsoft.Extensions.Options;
using System;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;

namespace WebMathTraining.Data
{
  public class BlogSettings
  {
    public string Owner { get; set; } = "The Owner";
    public int PostsPerPage { get; set; } = 2;
    public int CommentsCloseAfterDays { get; set; } = 10;
  }

  public class AppConfigOption : IOptions<AppConfiguration>
  {
    private readonly AppConfiguration _appConfiguration;
    public AppConfigOption(AppConfiguration appConfiguration)
    {
      _appConfiguration = appConfiguration;
    }

    public AppConfiguration Value => _appConfiguration;
  }

  public class AppConfiguration
  {
    public string StringSetting { get; set; }
    public int IntSetting { get; set; }

    public Dictionary<string, InnerClass> Dict { get; set; }
    public List<string> ListOfValues { get; set; }
    public MyEnum AnEnum { get; set; }

  }

  public class InnerClass
  {
    public string Name { get; set; }
    public bool IsEnabled { get; set; } = true;
  }

  public enum MyEnum
  {
    None = 0,
    Lots = 1
  }
}
