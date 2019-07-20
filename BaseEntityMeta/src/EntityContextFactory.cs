// 
// Copyright (c) WebMathTraining 2002-2017. All rights reserved.
// 

using System;
using BaseEntity.Configuration;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Static wrapper class providing access to most commonly used members of <see cref="IEntityContextFactory" />
  /// </summary>
  public static class EntityContextFactory
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="readWrite"></param>
    /// <param name="setValidFrom"></param>
    /// <returns></returns>
    public static IEntityContext Create(DateTime asOf, ReadWriteMode readWrite = ReadWriteMode.ReadOnly, bool setValidFrom = false)
    {
      return LazyImpl.Value.Create(asOf, readWrite, setValidFrom);
    }

    /// <summary>
    /// For internal use only
    /// </summary>
    public static void AddUser(User user)
    {
      LazyImpl.Value.AddUser(user);
    }

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
    public static void ResetUserCache()
    {
      LazyImpl.Value.ResetUserCache();
    }

    /// <summary>
    /// Gets the name of the user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns></returns>
    public static string GetUserName(long userId)
    {
      return LazyImpl.Value.GetUserName(userId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    public static User GetUser(string userName)
    {
      return LazyImpl.Value.GetUser(userName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userName"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public static bool TryGetUser(string userName, out User user)
    {
      return LazyImpl.Value.TryGetUser(userName, out user);
    }

    /// <summary>
    /// 
    /// </summary>
    public static long UserId => LazyImpl.Value.UserId;

    /// <summary>
    /// 
    /// </summary>
    public static string UserName => LazyImpl.Value.UserName;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static User User => LazyImpl.Value.User;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static UserRole UserRole => User.Role;

    private static readonly Lazy<IEntityContextFactory> LazyImpl = 
      new Lazy<IEntityContextFactory>(Configurator.Resolve<IEntityContextFactory>);
  }
}