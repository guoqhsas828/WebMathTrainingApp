// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Configuration;

namespace BaseEntity.Core.Services.EventService.ServiceModel
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [DataContract]
  [KnownType("EstablishKnownTypes")]
  public abstract class BaseEvent : EventArgs
  {
    private static readonly Lazy<Type[]> KnownTypes = new Lazy<Type[]>(LoadKnownTypes);

    protected BaseEvent()
    {
      Timestamp = DateTime.UtcNow;
    }

    private static Type[] LoadKnownTypes()
    {
      var knownTypes = Configurator.GetPlugins(PluginType.EntityModel)
        .SelectMany(p => p.Assembly.GetTypes().Where(IsKnownType))
        .ToArray();

      return knownTypes;
    }

    private static bool IsKnownType(Type t)
    {
      return t.IsClass && !t.IsAbstract && typeof(BaseEvent).IsAssignableFrom(t) && t.GetCustomAttributes(typeof(DataContractAttribute), false).Any();
    }

    /// <summary>
    /// Establishes the known types.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<Type> EstablishKnownTypes()
    {
      return KnownTypes.Value;
    }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>
    /// The timestamp.
    /// </value>
    [DataMember]
    public DateTime Timestamp { get; set; }
  }
}