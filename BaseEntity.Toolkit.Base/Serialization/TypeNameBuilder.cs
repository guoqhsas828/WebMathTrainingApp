using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Toolkit.Base.Serialization
{
  /// <summary>
  ///  Our custom type name builder follows three simple rules:
  /// 
  ///   (1) For the types in mscorlib.dll, use the full type name only,
  ///     no assembly name appended;
  /// 
  ///   (2) For the types in assemblies, use the full name plus
  ///     the assembly name without version info and other qualifications;
  /// 
  ///   (3) For other types, use the assembly qualified name.
  ///
  ///  This helps to reduce the XML file size and enable the generated
  ///  files to be compatible cross versions and .Net frameworks.
  /// </summary>
  class TypeNameBuilder
  {
    public string GetName(Type type)
    {
      if (_nameCache.TryGetValue(type, out var name))
        return name;

      var sb = new StringBuilder();
      AppendTypeName(sb, type);
      name = sb.ToString();
      _nameCache.Add(type, name);
      return name;
    }

    private void AppendTypeName(StringBuilder sb, Type type)
    {

      if (type.IsGenericType)
      {
        sb.Append(type.GetGenericTypeDefinition().FullName);
        sb.Append('[');
        var args = type.GetGenericArguments();
        for (int i = 0, n = args.Length; i < n; ++i)
        {
          if (i > 0) sb.Append(',');
          sb.Append('[');
          AppendTypeNameWithCache(sb, args[i]);
          sb.Append(']');
        }
        sb.Append(']');
      }
      else
      {
        sb.Append(type.FullName);
      }

      // The type is in mscorlib, no assembly name.
      var assembly = type.Assembly;
      if (assembly == typeof(Type).Assembly)
        return;

      var assemblyFullName = assembly.FullName;

      if (assemblyFullName.StartsWith("BaseEntity."))
      {
        // The type is in internal assemblies, omit version, etc...
        sb.Append(", ").Append(type.Assembly.GetName().Name);
        return;
      }

      // Otherwise, use the assembly qualified full name
      sb.Append(", ").Append(assemblyFullName);
    }

    private void AppendTypeNameWithCache(StringBuilder sb, Type type)
    {
      if (_nameCache.TryGetValue(type, out var name))
      {
        sb.Append(name);
        return;
      }

      var start = sb.Length;
      AppendTypeName(sb, type);
      var length = sb.Length - start;
      if (length <= 0) return;

      name = sb.ToString(start, length);
      _nameCache.Add(type, name);
    }

    private readonly Dictionary<Type, string> _nameCache
     = new Dictionary<Type, string>();
  }
}
