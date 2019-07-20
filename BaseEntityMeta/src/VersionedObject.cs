// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///   Abstract base class for all versioned WebMathTraining entities.
  /// </summary>
  /// <remarks>
  ///   Versioned objects include an ObjectVersion field that
  ///   is used to perform an optimistic concurrency check when
  ///   saving instances to the database.
  /// </remarks>
  [DataContract]
  [Serializable]
  public abstract class VersionedObject : PersistentObject
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedObject"/> class.
    /// </summary>
    protected VersionedObject()
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="objectVersion"></param>
    protected VersionedObject(long objectId, int objectVersion)
      : base(objectId)
    {
      objectVersion_ = objectVersion;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedObject"/> class.
    /// </summary>
    /// <param name="other">The other.</param>
    protected VersionedObject(VersionedObject other)
      : base(other)
    {
      objectVersion_ = other.objectVersion_;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Clones the object
    /// </summary>
    /// <remarks>
    /// The <see cref="ObjectVersion">ObjectVersion</see> of the cloned instance is 0.
    /// </remarks>
    public override object Clone()
    {
      VersionedObject vo = (VersionedObject)base.Clone();

      // DONT CLONE THE VERSION
      vo.objectVersion_ = 0;

      return vo;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Version of object
    /// </summary>
    /// <remarks>
    ///   Used to perform optimistic concurrency check
    /// </remarks>
    [DataMember]
    [VersionProperty]
    public int ObjectVersion
    {
      get { return objectVersion_; }
      set { objectVersion_ = value; }
    }

    #endregion

    #region Data

    private int objectVersion_;

    #endregion
  }
}