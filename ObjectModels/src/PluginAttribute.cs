using System;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// 
  /// </summary>
  [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
  public sealed class PluginAttribute : Attribute
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    public PluginAttribute(Type type)
    {
      if (!type.IsClass ||
          type.IsAbstract ||
          !typeof(IPlugin).IsAssignableFrom(type))
      {
        throw new Exception("Invalid Plugin class [" + type + "]");
      }

      PluginClassType = type;
    }

    /// <summary>
    /// 
    /// </summary>
    internal Type PluginClassType { get; private set; }
  }
}