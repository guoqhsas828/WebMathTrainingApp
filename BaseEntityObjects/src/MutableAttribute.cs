/*
 * MutableAttribute.cs
 *
 * Copyright (c) WebMathTraining 2004-2008. All rights reserved.
 *
 */
using System;

namespace BaseEntity.Shared
{
  /// <summary>
  ///   Mark a field as mutable so the object states checker
  ///   ignores the changes in the field value.
  /// </summary>
  [Serializable, AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
  public class MutableAttribute : Attribute
  {
  }
}
