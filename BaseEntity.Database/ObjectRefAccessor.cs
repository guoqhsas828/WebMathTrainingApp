/*
 * ObjectRefAccessor.cs - 
 *
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using NHibernate;
using NHibernate.Properties;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// </summary>
  [Serializable]
  public class ObjectRefAccessor : IPropertyAccessor
  {
    private static readonly IInternalLogger Log = LoggerProvider.LoggerFor(typeof(ObjectRefAccessor));

    #region IPropertyAccessor Members

    /// <summary>
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public ISetter GetSetter(System.Type theClass, string propertyName)
    {
      FieldInfo field = GetField(theClass, propertyName);
      return new FieldAccessor.FieldSetter(field, theClass, field.Name);
    }

    /// <summary>
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public IGetter GetGetter(System.Type theClass, string propertyName)
    {
      FieldInfo field = GetField(theClass, propertyName);
      return new FieldAccessor.FieldGetter(field, theClass, field.Name);
    }

    #endregion

    /// <summary>
    /// 
    /// </summary>
    public bool CanAccessThroughReflectionOptimizer
    {
      get { return false; }
    }

		/// <summary>
		/// Helper method to find the Field.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> to find the Field in.</param>
		/// <param name="propertyName">The name of the Field to find.</param>
		/// <returns>
		/// The <see cref="FieldInfo"/> for the field.
		/// </returns>
		/// <exception cref="PropertyNotFoundException">
		/// Thrown when a field could not be found.
		/// </exception>
    private static FieldInfo GetField(System.Type type, string propertyName)
		{
		  var propInfo = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (propInfo == null)
      {
        throw new PropertyNotFoundException(type, propertyName);
      }

      var declaringType = propInfo.DeclaringType;
      if (declaringType == null)
      {
        throw new PropertyNotFoundException(type, propertyName);
      }

      var fields = new List<FieldInfo>();
      foreach (FieldInfo field in declaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
      {
        if (field.FieldType == typeof(ObjectRef) && IsMatch(field.Name, propertyName))
          fields.Add(field);
      }
      if (fields.Count == 0)
      {
        Log.ErrorFormat("No FieldInfo found for [{0}.{1}]", type.Name, propertyName);
        throw new PropertyNotFoundException(declaringType, propertyName);
      }
      if (fields.Count > 1)
      {
        Log.ErrorFormat("Multiple possible FieldInfos found for [{0}.{1}]", type.Name, propertyName);
        throw new PropertyNotFoundException(declaringType, propertyName);
      }

		  var result = fields[0];

      Log.DebugFormat("FieldInfo [{0}] found for [{1}.{2}]", result.Name, type.Name, propertyName);
      return result;
		}

    private static bool IsMatch(string fieldName, string propertyName)
    {
      return (fieldName.Equals(propertyName + "_", StringComparison.OrdinalIgnoreCase) ||
              fieldName.Equals("_" + propertyName, StringComparison.OrdinalIgnoreCase));
    }

  } // class ObjectRefAccessor
} //namespace BaseEntity.Database
