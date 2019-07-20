// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class ObjectKey
  {
    /// <summary>
    /// Perform a deep copy
    /// </summary>
    public abstract ObjectKey Clone();

    /// <summary>
    /// Perform a value-based comparison to the specified <param name="other" /> and return True if the same, else False
    /// </summary>
    public abstract bool IsSame(ObjectKey other);

    /// <summary>
    /// </summary>
    public ClassMeta ClassMeta { get; protected set; }

    /// <summary>
    /// 
    /// </summary>
    public IList<PropertyMeta> PropertyList { get; protected set; }

    /// <summary>
    /// 
    /// </summary>
    public object[] State { get; protected set; }
  }
}