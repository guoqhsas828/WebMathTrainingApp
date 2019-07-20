// 
// Copyright (c) WebMathTraining 2002-2017. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// </summary>
  public class ObjectFactory<T>
  {
    // ReSharper disable once StaticMemberInGenericType
    private static ClassMeta _entity;

    /// <summary>
    /// </summary>
    private static ClassMeta Entity => _entity ?? (_entity = ClassCache.Find(typeof(T)));

    /// <summary>
    /// </summary>
    protected static T Create()
    {
      return (T)Entity.CreateInstance();
    }
  }
}