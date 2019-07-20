// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Text;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// This is used when comparing the snapshot representation of lists
  /// where the item in the list has a ChildKey defined.	It serves	as
  /// a proxy key for items in the list.
  /// </summary>
  public class ComponentKey : ObjectKey
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentKey"/> class.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <remarks></remarks>
    public ComponentKey(BaseEntityObject obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException("obj");
      }
      ClassMeta = ClassCache.Find(obj);
      if (ClassMeta == null)
      {
        throw new ArgumentException("No ClassMeta found for [" + obj + "]");
      }
      if (ClassMeta.IsEntity)
      {
        throw new ArgumentException("Cannot construct ChildKey for Entity");
      }

      PropertyList = ClassMeta.ChildKeyPropertyList;
      
      State = new object[PropertyList.Count];
      
      HashCode = ClassMeta.Name.GetHashCode();
      for (int i = 0; i < PropertyList.Count; i++)
      {
        var pm = PropertyList[i];
        var propValue = pm.GetFieldValue(obj);
        if (propValue == null)
        {
          throw new InvalidOperationException(
            String.Format("ChildKey property [{0}.{1}] cannot have null value", ClassMeta.Name, pm.Name));
        }
        HashCode ^= propValue.GetHashCode();
        State[i] = propValue;
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentKey"/> class.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <remarks></remarks>
    public ComponentKey(ComponentKey other)
    {
      ClassMeta = other.ClassMeta;
      PropertyList = ClassMeta.ChildKeyPropertyList;
      State = new object[PropertyList.Count];
      for (int i = 0; i < PropertyList.Count; i++)
      {
        State[i] = other.State[i];
      }
      HashCode = other.HashCode;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="state"></param>
    public ComponentKey(ClassMeta cm, object[] state)
    {
      ClassMeta = cm;
      PropertyList = cm.ChildKeyPropertyList;
      if (state.Length != PropertyList.Count)
      {
        throw new MetadataException(
          String.Format("Expecting [{0}] key properties; found [{1}]", PropertyList.Count, state.Length));
      }

      PropertyList = ClassMeta.ChildKeyPropertyList; 

      State = state;

      HashCode = cm.Name.GetHashCode();
      for (int i = 0; i < state.Length; i++)
        HashCode ^= State[i].GetHashCode();
    }

    /// <summary>
    /// Gets or sets the hash code.
    /// </summary>
    /// <value>The hash code.</value>
    /// <remarks></remarks>
    private int HashCode { get; set; }

    /// <summary>
    /// Return true if the specified object is a ChildKey and has the same value as this instance
    /// </summary>
    /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>.</param>
    /// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public override bool Equals(object obj)
    {
      ComponentKey otherKey = obj as ComponentKey;
      if (otherKey == null)
      {
        return false;
      }

      if (ClassMeta != otherKey.ClassMeta)
      {
        return false;
      }

      return Equals(State, otherKey.State);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
    /// <remarks></remarks>
    public override int GetHashCode()
    {
      return HashCode;
    }

    /// <summary>
    /// Equalses the specified old state.
    /// </summary>
    /// <param name="oldState">The old state.</param>
    /// <param name="newState">The new state.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    private static bool Equals(object[] oldState, object[] newState)
    {
      if (oldState == null)
      {
        return newState == null;
      }

      if (newState == null)
      {
        return false;
      }

      if (oldState.Length != newState.Length)
      {
        return false;
      }

      for (int i = 0; i < oldState.Length; i++)
      {
        if (!oldState[i].Equals(newState[i]))
          return false;
      }

      return true;
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks></remarks>
    public override string ToString()
    {
      var sb = new StringBuilder(ClassMeta.Name);
      foreach (object o in State)
      {
        sb.Append("|" + o);
      }
      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override ObjectKey Clone()
    {
      return new ComponentKey(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override bool IsSame(ObjectKey other)
    {
      var otherKey = (other as ComponentKey);
      if (otherKey == null)
      {
        return false;
      }

      if (ClassMeta != otherKey.ClassMeta)
      {
        return false;
      }

      return Equals(State, otherKey.State);
    }
  }
}