/*
 * ToolkitConfigAttribute.cs
 *
 * Copyright (c)    2004-2010. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Util.Configuration
{
  /// <summary>
  ///   Attribut to mark a field as a toolkit config setting
  /// </summary>
  [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
  public class ToolkitConfigAttribute : Attribute
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="description">Short description</param>
    public ToolkitConfigAttribute(string description)
    {
      Description = description;
    }
    /// <summary>
    ///   Description
    /// </summary>
    public readonly string Description;
  }
}
