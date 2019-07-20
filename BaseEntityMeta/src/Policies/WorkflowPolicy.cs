// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Iesi.Collections;
using BaseEntity.Shared;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
using HashedSet = System.Collections.Generic.HashSet<object>;
#endif

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// 
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public sealed class WorkflowPolicy : UserRolePolicy
  {
    /// <summary>
    /// 
    /// </summary>
    public WorkflowPolicy()
    {
      Permissions = new Dictionary<string, WorkflowPermissions>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="workflow"></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public bool CheckPolicy(string workflow, string operation)
    {
      WorkflowPermissions p;
      return Permissions.TryGetValue(workflow, out p) && p.Contains(operation);
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [ComponentCollectionProperty]
    public IDictionary<string, WorkflowPermissions> Permissions { get; set; }
  }

  /// <summary>
  /// 
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public sealed class WorkflowPermissions : BaseEntityObject, IEnumerable
  {
    /// <summary>
    /// 
    /// </summary>
    public WorkflowPermissions()
    {
      Operations = new List<string>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    public bool Contains(string operation)
    {
      return Operations.Contains(operation);
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [ElementCollectionProperty(ElementType = typeof(string))]
    public IList<string> Operations { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return Operations.GetEnumerator();
    }
  }
}
