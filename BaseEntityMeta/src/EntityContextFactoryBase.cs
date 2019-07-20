// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using log4net;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///  Configure database application
  /// </summary>
  public abstract class EntityContextFactoryBase : IEntityContextFactory
  {
    #region Data

    private Lazy<IDictionary<string, User>> _lazyUserCache;

    #endregion

    /// <summary>
    /// Initializes the database layer using the specified <see cref="IIdentityContext"/> and <see cref="IEntityPolicyFactory"/>
    /// </summary>
    /// <param name="identityContext"></param>
    /// <param name="entityPolicyFactory"></param>
    /// <returns></returns>
    protected EntityContextFactoryBase(IIdentityContext identityContext, IEntityPolicyFactory entityPolicyFactory)
    {
      if (identityContext != null)
      {
        GlobalContext.Properties["qdbid"] = identityContext;
      }

      IdentityContext = identityContext;
      EntityPolicyFactory = entityPolicyFactory;
    }

    /// <summary>
    /// Hidden method (called via reflection by internal database tools)
    /// </summary>
    public void AddUser(User user)
    {
      if (user == null)
      {
        throw new ArgumentNullException(nameof(user));
      }
      if (string.IsNullOrEmpty(user.Name))
      {
        throw new ArgumentException("UserName cannot be empty");
      }
      lock (UserCacheSyncObj)
      {
        var key = user.Name.ToLower();
        if (UserCache.ContainsKey(key))
        {
          throw new ArgumentException("User [" + user.Name + "] already exists!");
        }
        UserCache.Add(key, user);
        // Deal with null role as special case to accomodate GenSchema
        var roles = UserCache.Values.Select(u => u.Role).Where(r => r != null).ToList();
        SecurityPolicy.Init(roles);
      }
    }

    /// <summary>
    /// Load user cache from database
    /// </summary>
    public abstract void Authenticate();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    protected abstract IDictionary<string, User> LoadUserCache();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract IEntityContext Create(DateTime asOf, ReadWriteMode readWrite, bool setValidFrom);

    /// <summary>
    /// The available Users are loaded from the database and cached the first time
    /// they are needed. This cache lives for the lifetime of the process.
    /// If a process adds a new user to the database and will subsequently create 
    /// a session for that user it will need to call this method so the cache is 
    /// reloaded.
    /// The only known need for this at this time is a web application where this
    /// cache is initialized once for the IIS instance and after adding a user
    /// we will call this so we dont have to restart IIS. 
    /// </summary>
    /// <exclude />
    public void ResetUserCache()
    {
      lock (UserCacheSyncObj)
      {
        _lazyUserCache = new Lazy<IDictionary<string, User>>(LoadUserCache);
      }
    }

    /// <summary>
    /// Gets the name of the user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns></returns>
    public string GetUserName(long userId)
    {
      var user = UserCache.Values.SingleOrDefault(u => u.ObjectId == userId);
      return user == null ? "" : user.Name;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    public User GetUser(string userName)
    {
      var key = userName.ToLower();
      return UserCache[key];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userName"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public bool TryGetUser(string userName, out User user)
    {
      var key = userName.ToLower();
      return UserCache.TryGetValue(key, out user);
    }

    /// <summary>
    /// Create user and role
    /// </summary>
    /// <param name="userName">user name</param>
    public void CreateUserAndRole(string userName)
    {
      var user = ClassCache.CreateInstance<User>();
      user.Name = Environment.UserName;

      var role = ClassCache.CreateInstance<UserRole>();
      role.Name = "Administrator";
      role.Administrator = true;

      _lazyUserCache = new Lazy<IDictionary<string, User>>(() => new Dictionary<string, User> {{user.Name, user}});

      SecurityPolicy.Init(new[] { role });

      using (var context = (IEditableEntityContext)Create(DateTime.Today, ReadWriteMode.ReadWrite, false))
      {
        try
        {
          context.Save(user);
          context.Save(role);
          user.Role = role;

          context.CommitTransaction();
        }
        catch (Exception ex)
        {
          Console.WriteLine("Could not create user [{0}]: {1}\n{2}", user.Name, ex.Message, ex);
          context.RollbackTransaction();
        }
      }
    }

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    protected IDictionary<string, User> UserCache => _lazyUserCache.Value;

    /// <summary>
    /// 
    /// </summary>
    protected object UserCacheSyncObj { get; } = new object();

    /// <summary>
    /// 
    /// </summary>
    public long UserId => User.ObjectId;

    /// <summary>
    /// 
    /// </summary>
    public string UserName
    {
      get
      {
        if (IdentityContext == null)
        {
          throw new MetadataException("No identity context was initialized. This usually means that Configurator.Init() was either never called for this application, or the given container does not provide a usable IIdentityContext registration.");
        }

        return IdentityContext.GetUserName();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public User User
    {
      get
      {
        string key = UserName.ToLower();

        User user;
        if (!UserCache.TryGetValue(key, out user))
        {
          throw new MetadataException("Invalid UserName [" + key + "]");
        }

        return user;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public UserRole UserRole => User.Role;

    /// <summary>
    /// 
    /// </summary>
    public IIdentityContext IdentityContext { get; }

    /// <summary>
    /// 
    /// </summary>
    public IEntityPolicyFactory EntityPolicyFactory { get; }

    /// <summary>
    /// 
    /// </summary>
    public static DateTime MinDate { get; } = new DateTime(1753, 1, 1);

    #endregion
  }
}