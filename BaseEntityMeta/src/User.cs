/*
 * User.cs - details of this user account
 *
 * Copyright (c) WebMathTraining 2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///   User account (ie login)
  /// </summary>
  /// <remarks>
  ///   There must be at least one User defined in the system, and this user must have Administrator privilege.
  /// </remarks>
  [DataContract]
  [Serializable]
  [Entity(EntityId = 1, AuditPolicy = AuditPolicy.History, TableName = "`User`", OldStyleValidFrom = true, Description = "User account. Login for the Risk system")]
  public class User : AuditedObject
  {
    #region Constructors

    /// <summary>
    ///   Default constructor for internal use
    /// </summary>
    internal User()
    {
      _creationDate = DateTime.UtcNow;
    }

    /// <summary>
    ///   Construct from raw database result set
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="objectVersion"></param>
    /// <param name="validFrom"></param>
    /// <param name="lastUpdated"></param>
    /// <param name="updatedById"></param>
    /// <param name="name"></param>
    /// <param name="lastName"></param>
    /// <param name="firstName"></param>
    /// <param name="description"></param>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="phone"></param>
    /// <param name="fax"></param>
    /// <param name="address"></param>
    /// <param name="roleId"></param>
    /// <param name="islockout"></param>
    /// <param name="isactive"></param>
    /// <param name="lastPasswordChangedDate"></param>
    /// <param name="lastLoginDate"></param>
    /// <param name="lastLockoutDate"></param>
    /// <param name="creationDate"></param>
    public User(
      long objectId,
      int objectVersion,
      DateTime validFrom,
      DateTime lastUpdated,
      long updatedById,
      string name,
      string lastName,
      string firstName,
      string description,
      string email,
      string password,
      string phone,
      string fax,
      string address,
      long roleId,
      bool islockout,
      bool isactive,
      DateTime? lastPasswordChangedDate,
      DateTime? lastLoginDate,
      DateTime? lastLockoutDate,
      DateTime? creationDate)
      : base(objectId, objectVersion, validFrom, lastUpdated, updatedById)
    {
      _name = name;
      _lastName = lastName;
      _firstName = firstName;
      _description = description;
      _email = email;
      _password = password;
      _phone = phone;
      _fax = fax;
      _address = address;
      _role = (roleId == 0) ? null : new ObjectRef(roleId, null);
      _islockout = islockout;
      _isactive = isactive;
      _lastPasswordChangedDate = lastPasswordChangedDate;
      _lastLoginDate = lastLoginDate;
      _lastLockoutDate = lastLockoutDate;
      _creationDate = creationDate;
    }

    #endregion

    #region Properties

    /// <summary>
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 32, IsKey = true)]
    public string Name
    {
      get { return _name; }
      set { _name = value; }
    }


    /// <summary>
    /// </summary>
    [StringProperty(MaxLength = 32)]
    public string LastName
    {
      get { return _lastName; }
      set { _lastName = value; }
    }


    /// <summary>
    /// </summary>
    [StringProperty(MaxLength = 32)]
    public string FirstName
    {
      get { return _firstName; }
      set { _firstName = value; }
    }

    /// <summary>
    ///   Description
    /// </summary>
    [StringProperty(MaxLength = 64)]
    public string Description
    {
      get { return _description; }
      set { _description = value; }
    }


    /// <summary>
    ///   Email address
    /// </summary>
    [StringProperty(MaxLength = 32)]
    public string Email
    {
      get { return _email; }
      set { _email = value; }
    }

    /// <summary>
    ///   Note that this could be an encrypted/hashed password, so we need lenght to be longer than normal plain password
    /// </summary>
    [StringProperty(MaxLength = 256)]
    public string Password
    {
      get { return _password; }
      set { _password = value; }
    }

    /// <summary>
    ///   return true if user is locked out
    /// </summary>
    [BooleanProperty]
    public bool IsLocked
    {
      get { return _islockout; }
      set { _islockout = value; }
    }

    /// <summary>
    ///   true if user is active
    /// </summary>
    [BooleanProperty]
    public bool IsActive
    {
      get { return _isactive; }
      set { _isactive = value; }
    }


    /// <summary>
    ///   last password changed date
    /// </summary>
    [DateTimeProperty(AllowNullValue = true)]
    public DateTime? LastPasswordChangedDate
    {
      get { return _lastPasswordChangedDate; }
      set { _lastPasswordChangedDate = value; }
    }

    /// <summary>
    ///   last login date
    /// </summary>
    [DateTimeProperty(AllowNullValue = true)]
    public DateTime? LastLoginDate
    {
      get { return _lastLoginDate; }
      set { _lastLoginDate = value; }
    }

    /// <summary>
    ///   last lockout date
    /// </summary>
    [DateTimeProperty(AllowNullValue = true)]
    public DateTime? LastLockoutDate
    {
      get { return _lastLockoutDate; }
      set { _lastLockoutDate = value; }
    }

    /// <summary>
    ///   creation date
    /// </summary>
    [DateTimeProperty(AllowNullValue = true)]
    public DateTime? CreationDate
    {
      get { return _creationDate; }
      set { _creationDate = value; }
    }

    /// <summary>
    ///   Phone number
    /// </summary>
    [StringProperty(MaxLength = 16)]
    public string PhoneNumber
    {
      get { return _phone; }
      set { _phone = value; }
    }


    /// <summary>
    ///   Fax number
    /// </summary>
    [StringProperty(MaxLength = 16)]
    public string FaxNumber
    {
      get { return _fax; }
      set { _fax = value; }
    }


    /// <summary>
    ///   Address
    /// </summary>
    [StringProperty(MaxLength = 64)]
    public string Address
    {
      get { return _address; }
      set { _address = value; }
    }

    /// <summary>
    ///   Role based security for this user
    /// </summary>
    [ManyToOneProperty]
    public UserRole Role
    {
      get { return (UserRole) ObjectRef.Resolve(_role); }
      set { _role = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   Used internally to get the ObjectId of the UserRole
    /// </summary>
    internal long RoleId
    {
      get { return (_role == null) ? 0 : _role.Id; }
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Validation of User object
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (Role == null)
      {
        InvalidValue.AddError(errors, this, "Role may not be null.");
      }
    }

    #endregion

    #region Data

    private string _address;
    private DateTime? _creationDate;
    private string _description;
    private string _email;
    private string _fax;
    private string _firstName;
    private bool _isactive = true;
    private bool _islockout;
    private DateTime? _lastLockoutDate;
    private DateTime? _lastLoginDate;
    private string _lastName;
    private DateTime? _lastPasswordChangedDate;
    private string _name;
    private string _password;
    private string _phone;
    private ObjectRef _role;

    #endregion

    /// <summary>
    ///   Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>
    ///   A <see cref="System.String" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
      return Name;
    }
  }
}