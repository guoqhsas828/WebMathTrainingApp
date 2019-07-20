// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System.Reflection;
using System.Xml.Schema;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class ScalarPropertyMeta : PropertyMeta
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected ScalarPropertyMeta(ClassMeta entity, PropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
    }
  }
}