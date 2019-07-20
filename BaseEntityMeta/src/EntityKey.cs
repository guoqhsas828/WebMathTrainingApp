// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityKey : ObjectKey
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    public EntityKey(long id)
    {
      Id = id;
      if (id == 0)
      {
        throw new ArgumentException("Invalid value: " + id);
      }
      Type type = EntityHelper.GetClassFromObjectId(Id);
      if (type == null)
      {
        throw new MetadataException(String.Format(
          "No ClassMeta found for ObjectId [{0}]", Id));
      }
      ClassMeta = ClassCache.Find(type);
      PropertyList = new[] {ClassMeta.PropertyList[0]};
      State = new object[]{Id};
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    public EntityKey(EntityKey other)
    {
      Id = other.Id;
      ClassMeta = other.ClassMeta;
      PropertyList = new[] {ClassMeta.PropertyList[0]};
      State = new object[]{Id};
    }

    /// <summary>
    /// 
    /// </summary>
    public long Id { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public bool IsTransient
    {
      get { return EntityHelper.IsTransient(Id); }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override ObjectKey Clone()
    {
      return new EntityKey(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
      var other = (obj as EntityKey);

      if (other == null)
      {
        return false;
      }

      return Id == other.Id && IsTransient == other.IsTransient;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return Id.GetHashCode();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return string.Format("{0}{1}", (IsTransient) ? "T" : null, Id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public override bool IsSame(ObjectKey other)
    {
      var otherSnapshot = other as EntityKey;
      if (otherSnapshot == null)
      {
        return Id == 0;
      }

      return Id == otherSnapshot.Id && IsTransient == otherSnapshot.IsTransient;
    }
  }
}