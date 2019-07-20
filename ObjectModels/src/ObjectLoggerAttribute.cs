using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Attribute indicates that a specified logger field is to be applied as a object logger. 
  /// This attribute is required for dynamic allocation of appenders to object loggers although
  /// is not required if the object logger will be configured using the log4net config file directly
  /// </summary>
  [AttributeUsage(AttributeTargets.Field)]
  public sealed class ObjectLoggerAttribute : Attribute
  {
    /// <summary>
    /// Name of the logger. This is the name displayed on the Risk Run Config UI
    /// </summary>
    [NotNull]
    public string Name { get; set; }

    /// <summary>
    /// Description for the logger. This is the tooltip included for the logger on the Risk Run Config UI
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The catogory which encapsulates the object logger. The Name and Catogory properties represent a 
    /// unique key for the Object Logger Attribute 
    /// </summary>
    [NotNull]
    public string Category { get; set; }

    /// <summary>
    /// An array of dependent object loggers for the object logger. 
    /// </summary>
    public string[] Dependencies { get; set; }
  }

}
