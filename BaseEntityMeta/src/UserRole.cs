/*
 * UserRoles.cs
 *
 * Copyright (c) WebMathTraining 2008. All rights reserved.
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Object that describes security rights for  a user
  /// </summary>
  [DataContract]
  [Serializable]
  [Entity(EntityId = 2, AuditPolicy = AuditPolicy.History, OldStyleValidFrom = true, PropertyMapping = PropertyMappingStrategy.Hybrid, TableName = "UserRole", Description = "Describes security rights for a Risk user")]
  public sealed class UserRole : AuditedObject
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public UserRole()
    {
    }

    /// <summary>
    /// Construct instance from raw database result set
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="objectVersion"></param>
    /// <param name="validFrom"></param>
    /// <param name="lastUpdated"></param>
    /// <param name="updatedById"></param>
    /// <param name="name"></param>
    /// <param name="readOnly"></param>
    /// <param name="administrator"></param>
    public UserRole(
      long objectId,
      int objectVersion,
      DateTime validFrom,
      DateTime lastUpdated,
      long updatedById,
      string name,
      bool readOnly,
      bool administrator)
      : base(objectId, objectVersion, validFrom, lastUpdated, updatedById)
    {
      _name = name;
      _readOnly = readOnly;
      _administrator = administrator;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Unique name identifying this role
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 32, IsKey = true)]
    public string Name
    {
      get { return _name; }
      set { _name = value; }
    }

    /// <summary>
    ///   Is this user currently active?
    /// </summary>
    [BooleanProperty]
    public bool ReadOnly
    {
      get { return _readOnly; }
      set { _readOnly = value; }
    }

    /// <summary>
    ///   Is this user an administrator?
    /// </summary>
    [BooleanProperty]
    public bool Administrator
    {
      get { return _administrator; }
      set { _administrator = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    [ComponentCollectionProperty(ExtendedData = true)]
    public IDictionary<string, UserRolePolicy> PolicyMap
    {
      get { return _policyMap; }
      set { _policyMap = value; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      if (_policyMap == null)
      {
        InvalidValue.AddError(errors, this, "PolicyCollection cannot be null");
      }
    }

    #endregion

    #region Data

    private string _name;
    private bool _readOnly;
    private bool _administrator;
    private IDictionary<string, UserRolePolicy> _policyMap = new Dictionary<string, UserRolePolicy>();

    #endregion
  }
}
