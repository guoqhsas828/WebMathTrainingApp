using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Iesi.Collections;
using BaseEntity.Configuration;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
using HashedSet = System.Collections.Generic.HashSet<object>;
#endif

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Manages a set of named permissions (i.e. if the name is present the permission associated with that name is enabled)
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public sealed class NamedPolicy : UserRolePolicy
  {
    private static readonly  Lazy<ISet<string>> LazyValidNames = new Lazy<ISet<string>>(InitNames);

    /// <summary>
    /// 
    /// </summary>
    public NamedPolicy()
    {
      Permissions = new List<string>();
    }

    private static ISet<string> InitNames()
    {
      return Configurator.Resolve<ISecurityPolicyImplementor>().NamedPermissions;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public bool CheckPolicy(string name)
    {
      if (!ValidNames.Contains(name))
      {
        throw new ArgumentException("Invalid name [" + name + "]");
      }

      return Permissions.Contains(name);
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [ElementCollectionProperty(ElementType = typeof(string), ElementMaxLength = 128)]
    public IList<string> Permissions { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public static ISet<string> ValidNames
    {
      get { return LazyValidNames.Value; }
    }
  }
}
