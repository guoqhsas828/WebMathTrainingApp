using System;
using System.Reflection;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  public static class ConversionUtil
  {
    /// <summary>
    ///   Set value of a property or field of an object
    /// </summary>
    /// <param name="obj">Object to set property</param>
    /// <param name="propertyOrFieldName">Name of the property or field to set</param>
    /// <param name="propertyOrFieldValue">Value of the property or field to set</param>
    /// <param name="removeSpaces">If true, remove all spaces in property name</param>
    public static void SetValue(
      object obj,
      string propertyOrFieldName,
      object propertyOrFieldValue,
      bool removeSpaces
      )
    {
      string name = propertyOrFieldName;
      if (removeSpaces)
      {
        System.Text.RegularExpressions.Regex rgx
          = new System.Text.RegularExpressions.Regex(@"\s+");
        name = rgx.Replace(name, "");
      }

      Type type = null;
      FieldInfo fi = null;
      PropertyInfo pi = obj.GetType().GetProperty(name,
        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Static
        | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null)
      {
        fi = obj.GetType().GetField(name,
          BindingFlags.GetField | BindingFlags.Public | BindingFlags.Static
          | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (fi == null)
          throw new Exception("No property or field with the name '"
            + propertyOrFieldName
            + (removeSpaces ? " (" + name + ")" : "")
            + "' found in type '" + obj.GetType().FullName + "'");
        else
          type = fi.FieldType;
      }
      else
        type = pi.PropertyType;

      object value = GetObject(type, propertyOrFieldValue, false, null);
      if (pi != null)
        pi.SetValue(obj, value, null);
      else
        fi.SetValue(obj, value);
      return;
    }

    /// <summary>
    ///   Return an object converted to a given type
    /// </summary>
    /// <param name="type">required type</param>
    /// <param name="obj">object</param>
    /// <param name="useBlankValue">indicate if want to use blank value</param>
    /// <param name="blankValue">value for a blank (empty string "")</param>
    /// <returns>the object of the required type</returns>
    /// <exclude />
    public static object GetObject(Type type, object obj, bool useBlankValue, object blankValue)
    {
      // check null
      if (obj == null)
        return null;

      // determine directly if the obj is of the required type
      if (type.IsInstanceOfType(obj))
        return obj;

      // if an array type is required, get array
      if (type.IsArray)
        return GetObjectArray(type, obj, useBlankValue, blankValue);

      // if the type is enum
      if (type.IsEnum)
      {
        // Strip space and bracket in the enum
        System.Text.RegularExpressions.Regex rx =
          new System.Text.RegularExpressions.Regex(@"\s*\(.*\)\s*|\s+");
        string value = rx.Replace(obj.ToString(), "");
        if (value.Length == 0 && useBlankValue)
          return blankValue;
        return Enum.Parse(type, value, true);
      }

      // if the object is numeric type, try cast it to the required type
      if (obj is double || obj is Double || obj is int || obj is Int32 || obj is Int64)
      {
        if (type.Equals(typeof(Dt)))
          return Dt.FromExcelDate((double)obj);
        else
          return Convert.ChangeType(obj, type);
      }

      // look at the object cache
      if (obj is string)
      {
        string name = (string)obj;

        if (name.Length == 0)
        {
          if (useBlankValue)
            return blankValue;
          else if (type.Equals(typeof(Dt)))
            return new Dt();
          else if (type.IsValueType)
            return Convert.ChangeType(0, type);
          else if (type.IsClass)
            return null;
        }
      }

      try
      {
        return Convert.ChangeType(obj, type);
      }
      catch { }

      throw new Exception("Cannot convert object '" + obj.ToString() + "' to Type '" + type.ToString());
    }

    /// <summary>
    ///   Convert object to an array type
    /// </summary>
    /// <remarks>This function can only handle one-dimensional array at this moment.</remarks>
    /// <param name="arrayType">required array type</param>
    /// <param name="obj">object</param>
    /// <param name="useBlankValue">indicate if want to use blank value</param>
    /// <param name="blankValue">value for a blank element (empty string "")</param>
    /// <returns>converted object</returns>
    /// <exclude />
    public static object GetObjectArray(Type arrayType, object obj, bool useBlankValue, object blankValue)
    {
      if (obj == null)
        return null;

      Type elemType = arrayType.GetElementType();
      System.Collections.ArrayList list = new System.Collections.ArrayList();

      if (obj is string)
      {
        string value = (string)obj;
        if (value.Contains(","))
          obj = value.Split(',');
      }
      if (obj is Array)
      {
        Array objs = (Array)obj;
        if (objs.Length == 0)
          return null;
        for (int i = 0; i < objs.Length; ++i)
        {
          object o = GetObject(elemType, objs.GetValue(i), useBlankValue, blankValue);
          list.Add(o);
        }
      }
      else
      {
        object o = GetObject(elemType, obj, useBlankValue, blankValue);
        list.Add(o);
      }
      return list.ToArray(elemType);
    }

  } // class ReflectionUtil
}
