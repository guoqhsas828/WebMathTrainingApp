// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IPropertyMetaCreatorFactory
  {
    /// <summary>
    /// Registers the property meta creator.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="propertyMetaCreator">The property meta creator.</param>
    void RegisterPropertyMetaCreator(Type type, IPropertyMetaCreator propertyMetaCreator);

    /// <summary>
    /// Gets the property meta creator.
    /// </summary>
    /// <param name="propertyAttribute">The property attribute.</param>
    /// <returns></returns>
    IPropertyMetaCreator GetPropertyMetaCreator(PropertyAttribute propertyAttribute);
  }
}