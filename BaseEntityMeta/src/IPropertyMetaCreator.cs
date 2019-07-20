// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System.Reflection;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IPropertyMetaCreator
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="classMeta"></param>
    /// <param name="attr"></param>
    /// <param name="propInfo"></param>
    /// <returns></returns>
    PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo);
  }
}