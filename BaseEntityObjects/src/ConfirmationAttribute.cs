// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;

namespace BaseEntity.Shared
{
  /// <summary>
  /// An attribute that can be used to specify a confirmation message prompt that should be display to a user
  /// before the method is called
  /// </summary>
  [AttributeUsage(AttributeTargets.Method)]
  public class ConfirmationAttribute : Attribute
  {
    /// <summary>
    /// Gets the prompt.
    /// </summary>
    /// <value>
    /// The prompt.
    /// </value>
    public string Prompt { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationAttribute"/> class.
    /// </summary>
    /// <param name="prompt">The prompt.</param>
    public ConfirmationAttribute(string prompt)
    {
      Prompt = prompt;
    }
  }
}