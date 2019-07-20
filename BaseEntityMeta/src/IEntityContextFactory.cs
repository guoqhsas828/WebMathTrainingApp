// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// For applications that support injectable entity contexts, this serves as the factory.
  /// </summary>
  public interface IEntityContextFactory
  {
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEntityContext Create(DateTime asOf, ReadWriteMode readWrite= ReadWriteMode.ReadOnly, bool setValidFrom = false);

    /// <summary>
    /// Gets the name of the user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns></returns>
    string GetUserName(long userId);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    User GetUser(string userName);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userName"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    bool TryGetUser(string userName, out User user);

    /// <summary>
    /// 
    /// </summary>
    long UserId { get; }

    /// <summary>
    /// 
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    User User { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    UserRole UserRole { get; }

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
    void ResetUserCache();

    /// <summary>
    /// For internal use only
    /// </summary>
    /// <param name="user"></param>
    void AddUser(User user);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userName"></param>
    void CreateUserAndRole(string userName);
  }
}