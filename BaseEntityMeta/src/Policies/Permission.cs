using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Defines Create, Update, Delete permissions
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public class Permission : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [BooleanProperty]
    public bool CanCreate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [BooleanProperty]
    public bool CanUpdate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [BooleanProperty]
    public bool CanDelete { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return $"{CanCreate},{CanUpdate},{CanDelete}";
    }
  }
}
