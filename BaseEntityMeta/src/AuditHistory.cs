// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Globalization;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///
  /// </summary>
  [Serializable]
  public sealed class AuditHistory
  {
    /// <summary>
    /// 
    /// </summary>
    public int Tid { get; set; }

    /// <summary>
    /// ObjectId
    /// </summary>
    public long ObjectId { get; set; }

    /// <summary>
    /// RootObjectId
    /// </summary>
    public long RootObjectId { get; set; }

    /// <summary>
    /// RootObjectId
    /// </summary>
    public long ParentObjectId { get; set; }

    /// <summary>
    /// ValidFrom
    /// </summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>
    /// Contains the ObjectDelta serialized as XML
    /// </summary>
    public string ObjectDelta { get; set; }

    /// <summary>
    /// Returns string representation of AuditLog instance
    /// </summary>
    public override string ToString()
    {
      return string.Format("{0},{1},{2},{3}", Tid, ObjectId, ValidFrom.IsEmpty() ? "" : ValidFrom.ToString(CultureInfo.InvariantCulture), ObjectDelta);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
      var other = obj as AuditHistory;
      if (other == null) return false;
      return Equals(other);
    }

    private bool Equals(AuditHistory other)
    {
      return Tid == other.Tid && ObjectId == other.ObjectId;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      unchecked
      {
        return (Tid * 397) ^ ObjectId.GetHashCode();
      }
    }
  }
}