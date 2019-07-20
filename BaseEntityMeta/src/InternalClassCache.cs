// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using BaseEntity.Configuration;
using BaseEntity.Core.Logging;
using BaseEntity.Shared;
using log4net;

namespace BaseEntity.Metadata
{
  /// <exclude/>
  public class InternalClassCache : IClassCache
  {
    private static readonly ILog Log = QLogManager.GetLogger(typeof(InternalClassCache));

    internal InternalClassCache()
    {
      var replacements = new List<Type>();

      var entityPolicyFactory = Configurator.Resolve<IEntityPolicyFactory>();

      try
      {
        var assemblies = Configurator.GetPlugins(PluginType.EntityModel)
          .Select(p => p.Assembly)
          .ToList();

        if (!assemblies.Any())
        {
          assemblies = (new BuiltInPluginLoader(string.Empty)).Load().Select(pi => pi.Assembly).ToList();
        }

        foreach (Assembly assembly in assemblies)
        {
          Log.InfoFormat("ClassCache initializing assembly: {0}", assembly.FullName);

          AssemblyCache.Add(assembly);

          foreach (Type type in assembly.GetTypes())
          {
            // Ignore any type that is not a class
            if (!type.IsClass)
              continue;

            // Ignore static classes
            if (type.IsAbstract && type.IsSealed)
              continue;

            // If the user provides a filter to restrict the classes that are mapped, then use it
            if (ClassCache.TypeFilter != null)
              if (!ClassCache.TypeFilter.Contains(type))
                continue;

            // Ignore any class that does not have a ClassMetaAttribute
            if (HasClassMetaAttribute(type))
            {
              if (Log.IsDebugEnabled)
              {
                Log.DebugFormat("Registering class: {0} [{1}]", type, type.GetHashCode());
              }

              RegisterType(type, entityPolicyFactory);
            }
            else if (HasEntityExtensionAttribute(type))
            {
              replacements.Add(type);
            }
          }
        }
      }
      catch (ReflectionTypeLoadException ex)
      {
        Log.Error("Failed to load a type. LoaderExceptions follow", ex);
        foreach (var le in ex.LoaderExceptions)
        {
          Log.Error("Loader exception: ", le);
        }
      }

      foreach (Type replacement in replacements)
      {
        ReplaceEntity(replacement);
      }

      foreach (var classMeta in FindAll())
      {
        classMeta.SecondPassInit(this);
      }

      foreach (var classMeta in FindAll())
      {
        classMeta.ThirdPassInit();
      }
    }

    private void ReplaceEntity(Type replacement)
    {
      // Get reference to ClassMeta to replace
      var oldClassMeta = GetEntityToReplace(replacement);

      // Remove all references to the ClassMeta for the BaseType
      IdCache.Remove(oldClassMeta.EntityId);
      EntityNameCache.Remove(oldClassMeta.Name);
      ClassFullNameCache.Remove(oldClassMeta.FullName);
      TypeCache.Remove(oldClassMeta.Type);
      EntityCache.Remove(oldClassMeta);

      // Copy all entity attributes from the BaseType
      var entityMetaAttr = (EntityAttribute)GetClassMetaAttribute(replacement.BaseType);
      if (String.IsNullOrEmpty(entityMetaAttr.Name))
      {
        entityMetaAttr.Name = oldClassMeta.Name;
      }

      // Add new ClassMeta to cache
      var newClassMeta = new ClassMeta(replacement, entityMetaAttr, oldClassMeta.EntityPolicy, this);

      Add(newClassMeta);

      AltTypeCache.Add(oldClassMeta.Type, newClassMeta);
      ClassFullNameCache.Add(oldClassMeta.FullName, newClassMeta);
    }

    private ClassMeta GetEntityToReplace(Type type)
    {
      Type baseType = type.BaseType;

      Debug.Assert(baseType != null);

      ClassMeta cm;
      if (!TypeCache.TryGetValue(baseType, out cm) || !cm.IsEntity)
      {
        throw new MetadataException("Invalid EntityExtension : BaseType [" + baseType + "] is not an Entity");
      }
      if (cm.IsBaseEntity)
      {
        throw new MetadataException("Invalid EntityExtension : BaseType [" + baseType + "] is a BaseEntity");
      }
      if (baseType.GetConstructors().Any(c => c.IsPublic))
      {
        throw new MetadataException("Invalid EntityExtension : BaseType [" + baseType + "] has one or more public constructors");
      }
      var constructors = baseType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (constructors.Length != 1)
      {
        throw new MetadataException("Invalid EntityExtension : BaseType [" + baseType + "] must have a single protected constructor with no parameters");
      }
      if (!ValidateConstructor(constructors[0]))
      {
        throw new MetadataException("Invalid EntityExtension : BaseType [" + baseType + "] must have a single protected constructor with no parameters");
      }

      return cm;
    }

    private static bool ValidateConstructor(ConstructorInfo c)
    {
      if (c.IsPublic) return false;
      if (c.IsPrivate) return false;
      if (c.IsAssembly) return false;
      return c.IsFamily;
    }

    private void RegisterType(Type type, IEntityPolicyFactory entityPolicyFactory)
    {
      // Check if base object
      if (type == typeof(object))
        return;

      // Check if already registered
      if (TypeCache.ContainsKey(type))
        return;

      // Make sure any base entities registered first
      RegisterType(type.BaseType, entityPolicyFactory);

      // If this is a ClassMeta then add to ClassCache
      var classMetaAttr = GetClassMetaAttribute(type);
      if (classMetaAttr != null)
      {
        var entityMetaAttr = (classMetaAttr as EntityAttribute);

        ClassMeta classMeta;
        if (entityMetaAttr != null)
        {
          var entityPolicy = (entityPolicyFactory == null) ? null : entityPolicyFactory.GetPolicy(type);
          classMeta = new ClassMeta(type, entityMetaAttr, entityPolicy, this);
        }
        else
        {
          var compMetaAttr = (classMetaAttr as ComponentAttribute);
          if (compMetaAttr != null)
          {
            classMeta = new ClassMeta(type, compMetaAttr, this);
          }
          else
          {
            throw new MetadataException(
              "Invalid ClassMetaAttribute type [" + classMetaAttr.GetType() + "]");
          }
        }

        Add(classMeta);
      }
    }

    private static bool HasClassMetaAttribute(Type type)
    {
      object[] attrs = type.GetCustomAttributes(typeof(ClassAttribute), false);
      return (attrs.Length > 0);
    }

    private static ClassAttribute GetClassMetaAttribute(Type type)
    {
      object[] attrs = type.GetCustomAttributes(typeof(ClassAttribute), false);
      return (attrs.Length > 0) ? (ClassAttribute)attrs[0] : null;
    }

    private static bool HasEntityExtensionAttribute(Type type)
    {
      object[] attrs = type.GetCustomAttributes(typeof(EntityExtensionAttribute), false);
      return (attrs.Length > 0);
    }

    internal readonly List<ClassMeta> EntityCache = new List<ClassMeta>();
    internal readonly IList<Assembly> AssemblyCache = new List<Assembly>();
    internal readonly Dictionary<Type, ClassMeta> TypeCache = new Dictionary<Type, ClassMeta>();
    internal readonly Dictionary<string, ClassMeta> EntityNameCache = new Dictionary<string, ClassMeta>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, ClassMeta> ClassFullNameCache = new Dictionary<string, ClassMeta>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<Type, ClassMeta> AltTypeCache = new Dictionary<Type, ClassMeta>();
    internal readonly Dictionary<int, ClassMeta> IdCache = new Dictionary<int, ClassMeta>();

    /// <summary>
    ///  Add specified entity to the cache
    /// </summary>
    private void Add(ClassMeta entity)
    {
      if (EntityNameCache.ContainsKey(entity.Name))
      {
        throw new ArgumentException(String.Format(
          "Cannot register entity ({0}) as another entity is already registered with this name", entity.Name));
      }

      if (entity.EntityId != 0)
      {
        if (IdCache.ContainsKey(entity.EntityId))
        {
          ClassMeta otherEntity = IdCache[entity.EntityId];

          throw new ArgumentException(String.Format(
            "Cannot register entity {0} with id={1} as entity {2} is already registered with this id", entity.Name, entity.EntityId, otherEntity.Name));
        }

        IdCache[entity.EntityId] = entity;
      }

      TypeCache[entity.Type] = entity;
      ClassFullNameCache[entity.FullName] = entity;
      EntityNameCache[entity.Name] = entity;
      EntityCache.Add(entity);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    internal IEnumerable<ClassMeta> FindAll()
    {
      return EntityCache;
    }

    /// <summary>
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public ClassMeta Find(int entityId)
    {
      ClassMeta cm;
      return IdCache.TryGetValue(entityId, out cm) ? cm : null;
    }

    /// <summary>
    /// Find the <see cref="ClassMeta"/> either by its Name or its FullName
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public ClassMeta Find(string name)
    {
      ClassMeta cm;
      if (EntityNameCache.TryGetValue(name, out cm))
      {
        return cm;
      }

      if (name.Contains("."))
      {
        return ClassFullNameCache.TryGetValue(name, out cm) ? cm : null;
      }

      return null;
    }

    /// <summary>
    ///  Return entity for given name
    /// </summary>
    public ClassMeta Find(Type type)
    {
      ClassMeta cm;
      if (TypeCache.TryGetValue(type, out cm)) return cm;
      return AltTypeCache.TryGetValue(type, out cm) ? cm : null;
    }

    /// <summary>
    /// Finds the ClassMeta for the specified object id.
    /// </summary>
    /// <param name="objectId">The object id.</param>
    /// <returns></returns>
    internal ClassMeta Find(long objectId)
    {
      var type = EntityHelper.GetClassFromObjectId(objectId);
      return Find(type);
    }

    /// <summary>
    /// Return entity for given object
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    internal ClassMeta Find(object obj)
    {
      if (obj == null) throw new ArgumentNullException("obj");
      return Find(obj.GetType());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string GenerateHistoryReaderCode(IList<string> names = null)
    {
      names = names ?? ClassCache.FindAll().Select(cm => cm.Name).ToList();

      var set = new HashSet<string>(names);

      var sb = new StringBuilder();

      foreach (var cm in ClassCache.FindAll().Where(cm => set.Contains(cm.Name)).OrderBy(cm => cm.Name))
      {
        sb.AppendLine();
        sb.Append(cm.GenerateHistoryReaderCode());
        sb.AppendLine();
      }

      return sb.ToString();
    }

    /// <summary>
    /// Return a string representation of the metamodel
    /// </summary>
    /// <returns></returns>
    public string PrintMetaModel()
    {
      Log.DebugFormat("ENTER PrintMetaModel");

      var sb = new StringBuilder();
      foreach (var cm in ClassCache.FindAll().OrderBy(cm => cm.Name))
      {
        sb.AppendLine(PrintClass(cm));
      }
      sb.AppendLine(PrintEnums());

      Log.DebugFormat("EXIT PrintMetaModel");

      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cm"></param>
    /// <returns></returns>
    public string PrintClass(ClassMeta cm)
    {
      var type = cm.Type;

      var sb = new StringBuilder();

      if (cm.Type.GetCustomAttributes(false).OfType<EntityExtensionAttribute>().Any())
      {
        sb.AppendLine("[EntityExtension]");
      }
      foreach (var attr in cm.Type.GetCustomAttributes(false).OfType<ClassAttribute>())
      {
        sb.AppendLine(string.Format("[{0}({1})]", attr.GetType().Name, GetClassAttributeProperties(attr)));
      }

      sb.Append("public");
      if (type.IsAbstract)
      {
        sb.Append(" abstract");
      }
      sb.AppendFormat(" class {0}", cm.Type.Name);
      if (cm.BaseEntity != null)
      {
        sb.AppendFormat(" : {0}", cm.BaseEntity.Type.Name);
      }

      sb.AppendLine();
      sb.AppendLine("{");

      var propMetas = cm.PropertyList.Where(_ => _.Persistent && cm.IsOwner(_)).ToList();
      for (int i = 0; i < propMetas.Count; ++i)
      {
        if (i != 0) sb.AppendLine();
        sb.AppendLine(string.Format("{0}", PrintProperty(propMetas[i])));
      }

      sb.AppendLine("}");

      return sb.ToString();
    }

    private string PrintProperty(PropertyMeta pm)
    {
      var sb = new StringBuilder();

      foreach (var attr in pm.PropertyInfo.GetCustomAttributes(false).OfType<PropertyAttribute>())
      {
        sb.AppendLine(string.Format("  [{0}({1})]", attr.GetType().Name, GetPropertyAttributeProperties(attr)));
      }

      sb.Append("  public");
      sb.Append(pm.PropertyType.IsAbstract ? " abstract" : " virtual");
      sb.AppendFormat(" {0} {1} {{ get; set; }}", PrettyPrint(pm.PropertyType), pm.PropertyInfo.Name);

      // We keep track of any non-system enum referenced by a property (to detect breaking changes)
      ProcessEnums(pm.PropertyType);

      return sb.ToString();
    }

    private static string GetClassAttributeProperties(Attribute attr)
    {
      var args = new List<string>();
      foreach (var propInfo in attr.GetType().GetProperties().OrderBy(pi => pi.Name))
      {
        string arg;
        if (IncludeClassAttributeProperty(propInfo, propInfo.GetValue(attr), out arg))
          args.Add(arg);
      }
      return string.Join(", ", args.ToArray());
    }

    private static bool IncludeClassAttributeProperty(PropertyInfo propInfo, object propValue, out string arg)
    {
      string expr = null;

      if (!IsDefaultValue(propInfo.PropertyType, propValue))
      {
        switch (propInfo.Name)
        {
          case "Category":
          case "Description":
          case "DisplayName":
          case "EntityId":
          case "IsChildEntity":
          case "OldStyleValidFrom":
          case "Name":
          case "TableName":
            expr = propInfo.PropertyType == typeof(string) ? string.Format("\"{0}\"", propValue) : propValue.ToString();
            break;
          case "Key":
          case "ChildKey":
            var key = (string[])propValue;
            expr = string.Format("new []{{\"{0}\"}}", string.Join("\",\"", key));
            break;
          case "Type":
            var type = (Type)propValue;
            expr = string.Format("typeof({0})", type.Name);
            break;
        }
        if (expr == null)
        {
          if (propInfo.PropertyType.IsEnum)
            expr = string.Format("{0}.{1}", propInfo.PropertyType.Name, propValue);
        }
      }

      if (expr == null)
      {
        arg = null;
        return false;
      }

      arg = string.Format("{0} = {1}", propInfo.Name, expr);
      return true;
    }

    private static string GetPropertyAttributeProperties(Attribute attr)
    {
      var args = new List<string>();

      foreach (var propInfo in attr.GetType().GetProperties().OrderBy(pi => pi.Name))
      {
        string arg;
        if (IncludePropertyAttributeProperty(propInfo, propInfo.GetValue(attr), out arg))
          args.Add(arg);
      }

      return string.Join(", ", args.ToArray());
    }

    private static bool IncludePropertyAttributeProperty(PropertyInfo propInfo, object propValue, out string arg)
    {
      string expr = null;

      if (!IsDefaultValue(propInfo.PropertyType, propValue))
      {
        switch (propInfo.Name)
        {
          case "Adder":
          case "AllowNull":
          case "AllowNullableKey":
          case "Cascade":
          case "CollectionType":
          case "Column":
          case "DisplayName":
          case "ExtendedData":
          case "Fetch":
          case "FormatString":
          case "IndexColumn":
          case "IndexMaxLength":
          case "IsInverse":
          case "IsKey":
          case "IsPrimaryKey":
          case "IsUnique":
          case "MaxLength":
          case "Name":
          case "Persistent":
          case "Prefix":
          case "ReadOnly":
          case "RelatedProperty":
          case "Remover":
          case "TableName":
          case "Format":
            expr = propInfo.PropertyType == typeof(string) ? string.Format("\"{0}\"", propValue) : propValue.ToString();
            break;
          case "Clazz":
          case "IndexType":
            var type = (Type)propValue;
            expr = string.Format("typeof({0})", type.Name);
            break;
        }
      }

      if (expr == null)
      {
        arg = null;
        return false;
      }

      arg = string.Format("{0} = {1}", propInfo.Name, expr);
      return true;
    }

    private static string PrettyPrint(Type type)
    {
      string result;

      if (type.IsArray)
      {
        result = PrettyPrint(type.GetElementType()) + "[" + string.Join(",", Enumerable.Range(0, type.GetArrayRank() - 1).Select(i => ",").ToArray()) + "]";
      }
      else if (type.IsGenericType)
      {
        var typeArgs = type.GetGenericArguments();
        var genericType = type.GetGenericTypeDefinition();
        if (genericType == typeof(Nullable<>))
        {
          result = PrettyPrint(typeArgs[0]) + "?";
        }
        else
        {
          result = genericType.Name.Substring(0, genericType.Name.IndexOf("`", StringComparison.InvariantCulture)) + "<" + string.Join(", ", typeArgs.Select(PrettyPrint).ToArray()) + ">";
        }
      }
      else
      {
        switch (type.Name)
        {
          case "Boolean":
            result = "bool";
            break;
          case "Int32":
            result = "int";
            break;
          case "Int64":
            result = "long";
            break;
          case "Double":
            result = "double";
            break;
          case "String":
            result = "string";
            break;
          default:
            result = type.Name;
            break;
        }
      }

      return result;
    }

    /// <summary>
    /// Get bool type to indicate if is default value or not
    /// </summary>
    /// <param name="propertyType">property type</param>
    /// <param name="propValue">property value</param>
    /// <returns></returns>
    public static bool IsDefaultValue(Type propertyType, object propValue)
    {
      return propertyType.IsValueType ? Activator.CreateInstance(propertyType).Equals(propValue) : propValue == null;
    }

    private string PrintEnums()
    {
      var sb = new StringBuilder();

      foreach (var enumType in _generatedEnums)
      {
        sb.AppendLine(string.Format("public enum {0}", enumType.Name));

        sb.AppendLine("{");

        foreach (var value in Enum.GetValues(enumType).Cast<object>())
        {
          sb.AppendLine(string.Format("  {0} = {1}", value, Convert.ToInt32(value)));
        }

        sb.AppendLine("}");
        sb.AppendLine();
      }

      return sb.ToString();
    }

    private void ProcessEnums(Type type)
    {
      string ns = type.Namespace;
      if (ns == null || ns.StartsWith("System"))
      {
        return;
      }
      if (type.IsGenericType)
      {
        foreach (var arg in type.GetGenericArguments())
          ProcessEnums(arg);
      }
      else if (type.IsEnum)
        _generatedEnums.Add(type);
    }

    private readonly ISet<Type> _generatedEnums = new HashSet<Type>();
  }
}