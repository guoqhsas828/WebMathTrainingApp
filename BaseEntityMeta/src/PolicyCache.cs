using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Metadata.Policies;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Provides fast access to <see cref="UserRolePolicy"/> instances for a single UserRole
  /// </summary>
  internal class PolicyCache
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userRole"></param>
    public PolicyCache(UserRole userRole)
    {
      if (userRole == null)
      {
        throw new ArgumentNullException("userRole");
      }
      _userRole = userRole;
      _lazyNamedPolicy = new Lazy<NamedPolicy>(InitNamedPolicy);
      _lazyApplicationPolicy = new Lazy<ApplicationPolicy>(InitApplicationPolicy);
      _lazyEntityPolicyMap = new Lazy<IDictionary<Type, IList<IEntityPolicy>>>(InitEntityPolicyMap);
      _lazyWorkflowPolicy = new Lazy<WorkflowPolicy>(InitWorkflowPolicy);
    }

    #endregion

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool CheckEntityPolicy(PersistentObject po, ItemAction action)
    {
      if (_userRole.Administrator)
      {
        return true;
      }
      if (_userRole.ReadOnly)
      {
        return false;
      }
      IList<IEntityPolicy> list = GetEntityPolicy(po.GetType());
      if (list == null)
      {
        return false;
      }
      foreach (var policy in list)
      {
        if (!policy.CheckPolicy(po, action))
          return false;
      }
      return true;
    }

    public bool CheckEntityPolicy(ObjectDelta delta)
    {
      if (_userRole.Administrator)
      {
        return true;
      }
      if (_userRole.ReadOnly)
      {
        return false;
      }
      IList<IEntityPolicy> list = GetEntityPolicy(delta.ClassMeta.Type);
      if (list == null)
      {
        return false;
      }
      foreach (var policy in list)
      {
        if (!policy.CheckPolicy(delta))
          return false;
      }
      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<IEntityPolicy> GetEntityPolicy(Type entityType)
    {
      if (entityType == null)
      {
        throw new ArgumentNullException("entityType");
      }
      if (!typeof(PersistentObject).IsAssignableFrom(entityType))
      {
        throw new ArgumentException("Invalid entityType [" + entityType.Name + "]");
      }
      IList<IEntityPolicy> policies;
      EntityPolicyMap.TryGetValue(entityType, out policies);
      return policies;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="workflow"></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public bool CheckWorkflowPolicy(string workflow, string operation)
    {
      return WorkflowPolicy != null && WorkflowPolicy.CheckPolicy(workflow, operation);
    }

    /// <summary>
    /// Returns true if the specified <param name="name"/> is set, otherwise false
    /// </summary>
    public bool CheckNamedPolicy(string name)
    {
      if (_userRole.Administrator) return true;
      return NamedPolicy != null && NamedPolicy.CheckPolicy(name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool CheckApplicationPolicy()
    {
      return ApplicationPolicy == null || ApplicationPolicy.CheckPolicy();
    }

    private IDictionary<Type, IList<IEntityPolicy>> InitEntityPolicyMap()
    {
      var map = new Dictionary<Type, IList<IEntityPolicy>>();

      foreach (var cm in ClassCache.FindAll().Where(cm => cm.IsRootEntity))
      {
        if (cm.EntityPolicy != null)
        {
          map[cm.Type] = new List<IEntityPolicy>(new[] { cm.EntityPolicy });
        }
        else
        {
          var type = cm.Type;
          foreach (var policy in PolicyMap.Values.OfType<IEntityPolicy>().Where(p => p.IsApplicable(type)))
          {
            IList<IEntityPolicy> list;
            if (!map.TryGetValue(cm.Type, out list))
            {
              list = new List<IEntityPolicy>();
              map[cm.Type] = list;
            }
            list.Add(policy);
          }
        }
      }

      return map;
    }

    private WorkflowPolicy InitWorkflowPolicy()
    {
      UserRolePolicy policy;
      PolicyMap.TryGetValue(typeof(WorkflowPolicy).Name, out policy);
      return (WorkflowPolicy)policy;
    }

    private NamedPolicy InitNamedPolicy()
    {
      UserRolePolicy policy;
      PolicyMap.TryGetValue(typeof(NamedPolicy).Name, out policy);
      return (NamedPolicy)policy;
    }

    private ApplicationPolicy InitApplicationPolicy()
    {
      UserRolePolicy policy;
      PolicyMap.TryGetValue(typeof(ApplicationPolicy).Name, out policy);
      return (ApplicationPolicy)policy;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="policyType"></param>
    /// <returns></returns>
    public UserRolePolicy GetPolicy(Type policyType)
    {
      UserRolePolicy policy;
      return PolicyMap.TryGetValue(policyType.Name, out policy) ? policy : null;
    }

    /// <summary>
    /// 
    /// </summary>
    private IDictionary<string, UserRolePolicy> PolicyMap
    {
      get { return _userRole.PolicyMap; }
    }

    /// <summary>
    /// 
    /// </summary>
    private IDictionary<Type, IList<IEntityPolicy>> EntityPolicyMap
    {
      get { return _lazyEntityPolicyMap.Value; }
    }

    /// <summary>
    /// 
    /// </summary>
    private WorkflowPolicy WorkflowPolicy
    {
      get { return _lazyWorkflowPolicy.Value; }
    }

    /// <summary>
    /// 
    /// </summary>
    private NamedPolicy NamedPolicy
    {
      get { return _lazyNamedPolicy.Value; }
    }

    /// <summary>
    /// 
    /// </summary>
    private ApplicationPolicy ApplicationPolicy
    {
      get { return _lazyApplicationPolicy.Value; }
    }

    #region Data

    private readonly UserRole _userRole;
    private readonly Lazy<NamedPolicy> _lazyNamedPolicy;
    private readonly Lazy<ApplicationPolicy> _lazyApplicationPolicy;
    private readonly Lazy<IDictionary<Type, IList<IEntityPolicy>>> _lazyEntityPolicyMap;
    private readonly Lazy<WorkflowPolicy> _lazyWorkflowPolicy;

    #endregion
  }
}