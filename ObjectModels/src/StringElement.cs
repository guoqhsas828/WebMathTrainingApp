// 
// Copyright (c) WebMathTraining 2002-2012. All rights reserved.
// 

using System.Configuration;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Represents a string within a collection in a configuration section
  /// </summary>
  public class StringElement : ConfigurationElement
  {
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    /// <value>The value.</value>
    [ConfigurationProperty("value", IsRequired = true)]
    public string Value
    {
      get { return (string)this["value"]; }

      set { this["value"] = value; }
    }
  }
}