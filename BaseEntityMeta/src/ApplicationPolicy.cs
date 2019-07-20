// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Iesi.Collections;

using ISet = System.Collections.Generic.ISet<string>;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Used to enforce permissions on aggregate root entity instances within the database layer
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public class ApplicationPolicy : UserRolePolicy
  {
    /// <summary>
    /// 
    /// </summary>
    public ApplicationPolicy()
    {
      EnabledApps = new List<string>();
    }

    /// <summary>
    /// Called during application initialization
    /// </summary>
    /// <returns></returns>
    public bool CheckPolicy()
    {
      // An empty ApplicationPolicy disables all applications
      if (EnabledApps.Count == 0)
        return false;

      // Get name of exe or script
      var app = GetApplicationName();

      // Only done once per process so not worried about efficiency
      return EnabledApps.Any(enabledApp => String.Equals(app, enabledApp, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get name of exe or script that is executing
    /// </summary>
    /// <returns></returns>
    public static string GetApplicationName()
    {
      // Get file name for entry assembly
      string app;
      var assembly = Assembly.GetEntryAssembly();
      if (assembly == null)
      {
        app = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
      }
      else
      {
        var localPath = new Uri(assembly.CodeBase).LocalPath;
        app = Path.GetFileName(localPath);
      }

      switch (app)
      {
        case "ipy.exe":
        case "ipy64.exe":
          // Use script name instead
          app = Path.GetFileName(Environment.GetCommandLineArgs()[1]);
          break;
      }

      return app;
    }

    /// <summary>
    /// 
    /// </summary>
    [ElementCollectionProperty(ElementType = typeof(string), ElementMaxLength = 128)]
    public IList<string> EnabledApps { get; set; }
  }
}
