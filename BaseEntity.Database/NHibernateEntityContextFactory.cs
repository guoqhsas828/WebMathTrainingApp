// 
// Copyright (c) WebMathTraining Inc 2002-2016. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using log4net;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public sealed class NHibernateEntityContextFactory : EntityContextFactoryBase
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(NHibernateEntityContextFactory));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="identityContext"></param>
    /// <param name="entityPolicyFactory"></param>
    public NHibernateEntityContextFactory(IIdentityContext identityContext, IEntityPolicyFactory entityPolicyFactory)
      : base(identityContext, entityPolicyFactory)
    {
      if (entityPolicyFactory == null || entityPolicyFactory is NullEntityPolicyFactory)
      {
        // Skip authentication
      }
      else
      {
        Authenticate();
      }

      ResetUserCache();
    }

    /// <summary/>
    /// <param name="asOf"/><param name="readWrite"/><param name="setValidFrom"/>
    /// <returns/>
    public override IEntityContext Create(DateTime asOf, ReadWriteMode readWrite, bool setValidFrom)
    {
      return new NHibernateEntityContext(asOf, readWrite, setValidFrom);
    }

    /// <summary>
    /// Load user cache from database
    /// </summary>
    public override void Authenticate()
    {
      if (IdentityContext == null)
      {
        throw new MetadataException("No identity context was initialized. This usually means that BaseEntity.Shared.Configurator.Init() was either never called for this application, or the given container does not provide a usable IIdentityContext registration.");
      }

      using (var conn = new RawConnection())
      using (IDbCommand cmd = conn.CreateCommand())
      {
        bool found = false;

        string userName = IdentityContext.GetUserName();
        cmd.CommandText = "SELECT IsActive,IsLocked FROM [User] WHERE Name='" + userName + "'";
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            found = true;

            var isActive = (bool)reader[0];
            if (!isActive)
            {
              throw Logger.Exception(
                new InvalidUserException("User account [" + userName + "] is marked inactive."));
            }

            var isLocked = (bool)reader[1];
            if (isLocked)
            {
              throw Logger.Exception(
                new InvalidUserException("User account [" + userName + "] is locked."));
            }
          }
        }

        if (!found)
        {
          throw Logger.Exception(
            new InvalidUserException("Invalid user [" + userName + "]."));
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    protected override IDictionary<string, User> LoadUserCache()
    {
      IDictionary<string, User> map;

      Logger.Debug("LoadUserCache");

      lock (UserCacheSyncObj)
      {
        using (var conn = new RawConnection())
        {
          var userMap = LoadUserMap(conn);
          var userRoleMap = LoadUserRoleMap(conn, userMap);
          foreach (var user in userMap.Values)
          {
            ResolveObjectRef(user, "UpdatedBy", userMap);
            ResolveObjectRef(user, "Role", userRoleMap);
          }
          map = userMap.Values.ToDictionary(u => u.Name.ToLower());
          SecurityPolicy.Init(userRoleMap.Values);
        }
      }

      Logger.Debug("LoadUserCache");

      return map;
    }

    private static IDictionary<long, User> LoadUserMap(RawConnection conn)
    {
      var userMap = new Dictionary<long, User>();

      using (IDbCommand cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT ObjectId,ObjectVersion,ValidFrom,LastUpdated,UpdatedById,Name,LastName,FirstName,Description,Email,Password,PhoneNumber,FaxNumber,Address,RoleId,IsLocked,IsActive,LastPasswordChangedDate,LastLoginDate,LastLockoutDate,CreationDate FROM [User]";
        using (IDataReader reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            var validFrom = reader.GetDateTime(2);
            if (validFrom == SessionFactory.SqlMinDate)
              validFrom = DateTime.MinValue;

            var user = new User(
              reader.GetValue<long>(0),
              reader.GetValue<int>(1),
              validFrom,
              reader.GetUtcDateTime(3),
              reader.GetValue<long>(4),
              reader.GetValue<string>(5),
              reader.GetValue<string>(6),
              reader.GetValue<string>(7),
              reader.GetValue<string>(8),
              reader.GetValue<string>(9),
              reader.GetValue<string>(10),
              reader.GetValue<string>(11),
              reader.GetValue<string>(12),
              reader.GetValue<string>(13),
              reader.GetValue<long>(14),
              reader.GetValue<bool>(15),
              reader.GetValue<bool>(16),
              reader.GetNullableUtcDateTime(17),
              reader.GetNullableUtcDateTime(18),
              reader.GetNullableUtcDateTime(19),
              reader.GetNullableUtcDateTime(20));

            userMap[user.ObjectId] = user;
          }
        }
      }

      return userMap;
    }

    private static IDictionary<long, UserRole> LoadUserRoleMap(RawConnection conn, IDictionary<long, User> userMap)
    {
      var userRoleMap = new Dictionary<long, UserRole>();

      using (IDbCommand cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT ObjectId,ObjectVersion,ValidFrom,LastUpdated,UpdatedById,Name,ReadOnly,Administrator,ExtendedData FROM [UserRole]";
        using (IDataReader reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            var validFrom = reader.GetDateTime(2);
            if (validFrom == SessionFactory.SqlMinDate)
              validFrom = DateTime.MinValue;

            var userRole = new UserRole(
              reader.GetValue<long>(0),
              reader.GetValue<int>(1),
              validFrom,
              reader.GetUtcDateTime(3),
              reader.GetValue<long>(4),
              reader.GetValue<string>(5),
              reader.GetValue<bool>(6),
              reader.GetValue<bool>(7));

            var extendedDataXml = reader.GetValue<string>(8);
            if (string.IsNullOrEmpty(extendedDataXml))
            {
              userRole.PolicyMap = new Dictionary<string, UserRolePolicy>();
            }
            else
            {
              using (var sr = new XmlEntityReader(extendedDataXml))
              {
                sr.ReadEntity(userRole);
              }
            }

            ResolveObjectRef(userRole, "UpdatedBy", userMap);

            userRoleMap[userRole.ObjectId] = userRole;
          }
        }
      }

      return userRoleMap;
    }

    /// <summary>
    /// Resolve the ObjectRef using the specified lookup map
    /// </summary>
    private static void ResolveObjectRef<T>(PersistentObject po, string propName, IDictionary<long, T> entityMap)
    {
      var cm = ClassCache.Find(po);
      var pm = (ManyToOnePropertyMeta)cm.GetProperty(propName);

      var objectRef = pm.GetObjectRef(po);
      if (objectRef == null || objectRef.IsNull) return;
      pm.SetValue(po, entityMap[objectRef.Id]);
    }

    #region Properties

    /// <summary>
    /// Maximum number of parameters to use in a single parameterized query.
    /// </summary>
    /// <remarks>
    /// <para>For now, just hard-code a value appropriate for SQL Server.  When we add
    /// support for other servers we will need to make this server dependent.</para>
    /// </remarks>
    public static int BatchSize => 2000;

    #endregion
  }
}