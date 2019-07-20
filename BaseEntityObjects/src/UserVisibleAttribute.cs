// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Shared
{
  /// <summary>
  /// An attribute that can be used to specify whether an operation is directly 
  /// visible to an end user
  /// </summary>
  [AttributeUsage(AttributeTargets.Method)]
  public class UserVisibleAttribute : Attribute
  {
    /// <summary>
    /// Gets the setting.
    /// </summary>
    /// <value>
    /// The setting.
    /// </value>
    public bool IsVisible { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserVisibleAttribute"/> class.
    /// </summary>
    /// <param name="setting">The setting.</param>
    public UserVisibleAttribute(bool setting)
    {
      IsVisible = setting;
    }
  }
}
