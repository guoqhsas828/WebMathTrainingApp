// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BaseEntity.Configuration;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public static class SecurityPolicy
  {
    private static readonly Lazy<ISecurityPolicyImplementor> LazyImpl = new Lazy<ISecurityPolicyImplementor>(InitImpl);

    /// <summary>
    /// user name
    /// </summary>
    public static string UserName
    {
      get { return Impl.UserName; }
    }

    private static UserRole UserRole
    {
      get { return Impl.UserRole; }
    }

    private static ISecurityPolicyImplementor Impl
    {
      get { return LazyImpl.Value; }
    }

    private static ISecurityPolicyImplementor InitImpl()
    {
      return Configurator.Resolve<ISecurityPolicyImplementor>();
    }

    /// <summary>
    /// 
    /// </summary>
    public static void Init(ICollection<UserRole> userRoles)
    {
      if (userRoles == null || userRoles.Count == 0)
      {
        _policyCacheMap = null;
      }
      else
      {
        if (_policyCacheMap == null)
        {
          _policyCacheMap = new ConcurrentDictionary<string, PolicyCache>();
        }
        foreach (var item in userRoles)
        {
          UserRole userRole = item;
          _policyCacheMap.AddOrUpdate(
            userRole.Name, key => new PolicyCache(userRole), (key, pc) => new PolicyCache(userRole));
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    private static bool NoPolicyCache
    {
      get { return _policyCacheMap == null; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <param name="lockType"></param>
    /// <returns></returns>
    public static bool CheckEntityPolicy(PersistentObject po, LockType lockType)
    {
      switch (lockType)
      {
        case LockType.Insert:
          return CanCreate(po);
        case LockType.Update:
          return CanUpdate(po);
        case LockType.Delete:
          return CanDelete(po);
        default:
          return false;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    internal static bool CheckEntityPolicy(ObjectDelta delta)
    {
      if (NoPolicyCache)
      {
        return true;
      }

      var userRole = UserRole;

      PolicyCache policyCache;
      return _policyCacheMap.TryGetValue(userRole.Name, out policyCache) &&
             policyCache.CheckEntityPolicy(delta);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static bool CanCreate(PersistentObject po)
    {
      if (NoPolicyCache)
      {
        return true;
      }

      var userRole = UserRole;

      PolicyCache policyCache;
      return _policyCacheMap.TryGetValue(userRole.Name, out policyCache) &&
             policyCache.CheckEntityPolicy(po, ItemAction.Added);
    }

    /// <summary>
    /// Checks create permission.
    /// </summary>
    /// <param name="po">The po.</param>
    /// <exception cref="System.ServiceModel.Security.SecurityAccessDeniedException"></exception>
    public static void CheckCreate(PersistentObject po)
    {
      if (!CanCreate(po))
      {
        throw new SecurityException("Create permission denied");
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static bool CanUpdate(PersistentObject po)
    {
      if (NoPolicyCache)
      {
        return true;
      }

      var userRole = UserRole;

      PolicyCache policyCache;
      return _policyCacheMap.TryGetValue(userRole.Name, out policyCache) &&
             policyCache.CheckEntityPolicy(po, ItemAction.Changed);
    }

    /// <summary>
    /// Checks update permission.
    /// </summary>
    /// <param name="po">The po.</param>
    /// <exception cref="System.ServiceModel.Security.SecurityAccessDeniedException">Update permission denied</exception>
    public static void CheckUpdate(PersistentObject po)
    {
      if (!CanUpdate(po))
      {
        throw new SecurityException("Update permission denied");
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static bool CanDelete(PersistentObject po)
    {
      if (NoPolicyCache)
      {
        return true;
      }

      var userRole = UserRole;

      PolicyCache policyCache;
      return _policyCacheMap.TryGetValue(userRole.Name, out policyCache) &&
             policyCache.CheckEntityPolicy(po, ItemAction.Removed);
    }

    /// <summary>
    /// Checks delete permission.
    /// </summary>
    /// <param name="po">The po.</param>
    /// <exception cref="SecurityException">Delete permission denied</exception>
    public static void CheckDelete(PersistentObject po)
    {
      if (!CanDelete(po))
      {
        throw new SecurityException("Delete permission denied");
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public static bool CheckWorkflowPolicy(string workflow, string operation)
    {
      var userRole = UserRole;
      return userRole.Administrator || _policyCacheMap[userRole.Name].CheckWorkflowPolicy(workflow, operation);
    }

    /// <summary>
    /// Returns true if the specified <param name="name"/> is set, otherwise false
    /// </summary>
    public static bool CheckNamedPolicy(string name)
    {
      return CheckNamedPolicy(UserRole, name);
    }

    /// <summary>
    /// Returns true if the specified <param name="name"/> is set, otherwise false
    /// </summary>
    public static bool CheckNamedPolicy(UserRole userRole, string name)
    {
      return userRole != null && (userRole.Administrator || _policyCacheMap[userRole.Name].CheckNamedPolicy(name));
    }

    /// <summary>
    /// Returns true if the current application is enabled, otherwise false
    /// </summary>
    public static bool CheckApplicationPolicy()
    {
      return CheckApplicationPolicy(UserRole);
    }

    /// <summary>
    /// Returns true if the current application is enabled, otherwise false
    /// </summary>
    public static bool CheckApplicationPolicy(UserRole userRole)
    {
      return userRole != null && (userRole.Administrator || _policyCacheMap[userRole.Name].CheckApplicationPolicy());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static T GetPolicy<T>() where T : UserRolePolicy
    {
      if (NoPolicyCache)
      {
        return null;
      }
      var userRole = UserRole;
      var policyCache = _policyCacheMap[userRole.Name];
      return (T)policyCache.GetPolicy(typeof(T));
    }

    private static ConcurrentDictionary<string, PolicyCache> _policyCacheMap;
  }
}
