/*
 * IOwnershipResolver.cs -
 *
 * Copyright (c) WebMathTraining 2009. All rights reserved.
 *
 */

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IOwnershipResolver
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentType"></param>
    /// <returns></returns>
    string GetCascade(Type parentType);

    /// <summary>
    /// Gets the concrete type of the owned entity.
    /// </summary>
    /// <param name="parentType">Type of the parent.</param>
    /// <returns></returns>
    Type GetOwnedConcreteType(Type parentType);
  }
}
