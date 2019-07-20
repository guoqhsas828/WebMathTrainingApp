// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///   Abstract base class for all WebMathTraining persistent entities.
  /// </summary>
  [DataContract]
  [Serializable]
  [DisplayName("Entity")]
  public abstract class PersistentObject : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    ///   Construct default instance
    /// </summary>
    protected PersistentObject()
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    protected PersistentObject(long objectId)
    {
      _objectId = objectId;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="other"></param>
    protected PersistentObject(PersistentObject other)
    {
      _objectId = other._objectId;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      this.DoValidate(errors);
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <remarks>
    /// The <see cref="ObjectId">ObjectId</see> of the cloned instance = 0.
    /// </remarks>
    public override object Clone()
    {
      var po = (PersistentObject)this.MemberwiseClone();

      // DONT CLONE THE OBJECT ID
      po._objectId = 0;

      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual PersistentObject CopyAsNew(IEditableEntityContext context)
    {
      return this.DoCopyAsNew(context);
    }

    ///<summary>
    /// Derives the values for dependent properties based on other persistent properties.
    ///</summary>
    public virtual void DeriveValues()
    {}

    /// <summary>
    /// Request Update Lock
    /// </summary>
    /// <returns>will throw exception if can not lock the object</returns>
    public void RequestUpdate()
    {
      string errorMsg;
      if (!this.TryRequestUpdate(out errorMsg))
        throw new SecurityException(errorMsg);
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Object id
    /// </summary>
    /// <remarks>
    ///   For internal use only.
    /// </remarks>
    [DataMember]
    [ObjectIdProperty(IsPrimaryKey = true)]
    [Key]
    public long ObjectId
    {
      get { return _objectId; }
      set { _objectId = value; }
    }

    /// <summary>
    /// Returns true if the <see cref="PersistentObject"/> does not have an ObjectId.
    /// </summary>
    [NotMapped]
    public bool IsAnonymous
    {
      get { return _objectId == 0; }
    }

    /// <summary>
    /// 
    /// </summary>
    [NotMapped]
    public bool IsTransient
    {
      get { return _objectId < 0; }
    }

    /// <summary>
    /// Returns true if the <see cref="PersistentObject"/> does not have a globally unique ObjectId.
    /// </summary>
    [NotMapped]
    public bool IsUnsaved
    {
      get { return _objectId <= 0; }
    }

    #endregion

    #region Data

    [NotMapped]
    private long _objectId { get { return Id; } set { Id = value; } }

    #endregion
  }
}