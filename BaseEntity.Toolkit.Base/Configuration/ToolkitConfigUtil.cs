/*
 * ToolkitConfigUtil.cs
 *
 * Copyright (c)    2004-2010. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Util.Configuration
{
  /// <summary>
  ///  Utility class for toolkit configuration
  /// </summary>
  public static class ToolkitConfigUtil
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ToolkitConfigUtil));

    /// <summary>
    ///   Load configuration settings from an XML root node
    /// </summary>
    /// <typeparam name="T">Type of the configuration settings</typeparam>
    /// <param name="root">Root element</param>
    /// <returns>A configuration object</returns>
    /// <exclude/>
    public static T LoadSettings<T>(XmlElement root)
    {
      Type type = typeof(T);
      FieldInfo[] fieldInfos = type.GetFields();
      object[] fields = new object[fieldInfos.Length];
      for (int i = 0; i < fieldInfos.Length; ++i)
        fields[i] = GetConfigGroup(fieldInfos[i], root);

      try
      {
        object o = FormatterServices.GetUninitializedObject(type);
        o = FormatterServices.PopulateObjectMembers(o, fieldInfos, fields);
        return (T)o;
      }
      catch (Exception ex)
      {
        throw new ToolkitConfigException("FormatterServices error", ex);
      }
    }

    /// <summary>
    /// Loads the configuration element.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="element">The element.</param>
    /// <returns>``0.</returns>
    public static T LoadElement<T>(XmlElement element)
    {
      return (T)LoadGroup(typeof(T), element, null);
    }

    /// <summary>
    ///   Get an array of all the configuration settings
    /// </summary>
    /// <param name="settings">Configuration settings object</param>
    /// <returns>An array of settings.</returns>
    public static Item[] GetSettingsList<T>(T settings)
    {
      Debug.Assert(settings != null, "settings is null.");

      List<Item> items = new List<Item>();
      Type type = settings.GetType();
      FieldInfo[] fieldInfos = type.GetFields();
      for (int i = 0; i < fieldInfos.Length; ++i)
      {
        object group = fieldInfos[i].GetValue(settings);
        GetGroupItems(fieldInfos[i].Name, group, items);
      }

      return items.ToArray();
    }

    /// <summary>
    ///   Create a string in XML format representing the default settings.
    /// </summary>
    /// <param name="settings">Configuration settings object</param>
    /// <param name="rootName">Name of the root element</param>
    /// <returns>Xml string</returns>
    public static string WriteSettingsXml<T>(T settings, string rootName)
    {
      return WriteSettingsXml(settings, rootName, false);
    }

    public static string WriteSettingsXml<T>(T settings,
      string rootName, bool includeInternalMembers)
    {
      Debug.Assert(settings != null, "settings is null.");

      TextWriter sw = new StringWriter();
      using (XmlTextWriter xw = new XmlTextWriter(sw))
      {
        xw.WriteStartElement(rootName);
        var fieldInfos = settings.GetType().GetFields();
        for (int i = 0; i < fieldInfos.Length; ++i)
        {
          xw.WriteStartElement(fieldInfos[i].Name);
          object group = fieldInfos[i].GetValue(settings);
          WriteXmlAttributes(group, xw, includeInternalMembers);
          xw.WriteEndElement();
        }
        xw.WriteEndElement();
      }

      return sw.ToString();
    }

    /// <summary>
    ///   Query the value of a configuration setting
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="settings"></param>
    /// <param name="name">Configuration name in "GROUP.ITEM" format.</param>    
    /// <returns>The current value.</returns>
    public static T GetValue<T>(object settings, string name)
    {
      if (name == null || name.Length == 0)
      {
        throw new ArgumentNullException("name cannot be empty.");
      }

      string[] tokens = name.Split('.');
      if (tokens.Length != 2)
      {
        throw new ArgumentException(String.Format(
          "Invalid setting name: {0}", name));
      }

      if (settings == null)
      {
        throw new InvalidOperationException(
          "Must call ToolkitConfigurator.Initialize() first.");
      }

      object group = GetSettingGroup(tokens[0], settings);
      object item = GetSettingItem(tokens[1], group);
      return (T)item;
    }

    #region Public Type
    /// <summary>
    ///   List item showing all the properties of a configuration item
    /// </summary>
    [Serializable]
    public class Item
    {
      internal Item(string name, Type type,
        object value, object dfltValue, string description)
      {
        Name = name; Type = type; Value = value;
        Default = dfltValue; Description = description;
      }

      /// <summary>
      ///   Name in "GROUP.ITEM" format
      /// </summary>
      public readonly string Name;

      /// <summary>
      ///   Value type
      /// </summary>
      public readonly Type Type;

      /// <summary>
      ///   Current value
      /// </summary>
      public readonly object Value;

      /// <summary>
      ///   Default value (normally recommended)
      /// </summary>
      public readonly object Default;

      /// <summary>
      ///   A short description of the setting item.
      /// </summary>
      public readonly string Description;
    }
    #endregion Public Type

    #region Load Settings from Xml
    private static object GetConfigGroup(FieldInfo info, XmlElement root)
    {
      // Settings for each group must be contained in a single XML element
      string groupName = info.Name;
      XmlElement element = GetElement(groupName, root);

      return LoadGroup(info.FieldType, element, groupName);
    }
    private static object LoadGroup(Type type,
      XmlElement element, string groupName)
    {
      if (String.IsNullOrEmpty(groupName))
        groupName = element.Name;

      object dfltGroup = GetDefaultValue(type);
      var fields = type.GetFields(BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic);
      var items = new object[fields.Length];
      for (int i = 0; i < fields.Length; ++i)
        items[i] = GetConfigItem(groupName, dfltGroup, fields[i], element);

      object group;
      try
      {
        group = FormatterServices.GetUninitializedObject(type);
        group = FormatterServices.PopulateObjectMembers(group, fields, items);
      }
      catch (Exception ex)
      {
        throw new ToolkitConfigException("FormatterServices error", ex);
      }
      // Call Validate method on the object if it is defined.
      var validate = DelegateFactory.CreateDynamicMethod(
        "Validate", type, null, new[] { type });
      if (validate != null)
        validate.Invoke(null, new[] { group });
      return group;
    }
    private static object GetConfigItem(
      string groupName, object dfltGroup,
      FieldInfo info, XmlElement element)
    {
      // Create an object of this type with default values
      Type type = info.FieldType;
      object dfltValue;
      try
      {
        dfltValue = dfltGroup == null
          ? GetDefaultValue(type)
          : info.GetValue(dfltGroup);
      }
      catch (Exception ex)
      {
        throw new ToolkitConfigException("Activator.CreateInstance error", ex);
      }

      // The desired value of this setting item.
      object value;

      // Setting data must be defined and set as an attribute
      string name = info.Name;
      string attrVal = element.GetAttribute(name);
      if (string.IsNullOrEmpty(attrVal))
      {
        if (info.IsPublic)
        {
          throw new ToolkitConfigItemMissingException(
            groupName + '.' + name, type,
            dfltValue, GetFieldDescription(info));
        }
        // Non-public field is optional.
        value = dfltValue;
      }
      else
      {
        // Read the setting value
        try
        {
          value = type.IsEnum
            ? Enum.Parse(type, attrVal)
            : Convert.ChangeType(attrVal, type);
        }
        catch (FormatException ex)
        {
          throw new ToolkitConfigReadException(String.Format(
            "Invalid config value: {0}.{1}='{2}'",
            groupName, name, attrVal), ex);
        }
        if (info.IsPublic && !(value?.Equals(dfltValue) ?? (dfltValue == null)))
        {
          logger.DebugFormat("{0}.{1}: value = {2}, rather than recommended {3}",
            groupName, name, value, dfltValue);
        }
      }
      return value;
    }
    private static object GetDefaultValue(Type type)
    {
      const BindingFlags bf = BindingFlags.NonPublic
        | BindingFlags.Public | BindingFlags.Instance;
      var ctor = type.GetConstructors(bf)
        .FirstOrDefault(c => c.GetParameters().Length == 0);
      if (ctor != null) return ctor.Invoke(EmptyArgs);
      if (!type.IsValueType) return null;
      return Activator.CreateInstance(type);
    }
    private static readonly object[] EmptyArgs = new object[0];
    private static XmlElement GetElement(string groupName, XmlElement root)
    {
      XmlNodeList elements = root.GetElementsByTagName(groupName);
      if (elements.Count > 1)
      {
        throw new ToolkitConfigReadException(String.Format(
          "{0}: Multiple occurrences not allowed", groupName), null);
      }
      if (elements.Count == 0)
      {
        throw new ToolkitConfigGroupMissingException(groupName);
      }
      return (XmlElement)elements[0];
    }
    private static string GetFieldDescription(FieldInfo fieldInfo)
    {
      object[] attrs = fieldInfo.GetCustomAttributes(
        typeof(ToolkitConfigAttribute), false);
      Debug.Assert(attrs.Length == 1,
        String.Format("Must define exactly one ToolkitConfig attribute for {0} in class {1}",
        fieldInfo.Name, fieldInfo.DeclaringType.Name));
      return ((ToolkitConfigAttribute)attrs[0]).Description;
    }
    #endregion Load Settings from Xml

    #region Query Implementation
    private static object GetSettingItem(string name, object group)
    {
      if (name == null || name.Length == 0)
      {
        throw new ArgumentNullException("name cannot be empty.");
      }
      FieldInfo info = group.GetType().GetField(name);
      return info.GetValue(group);
    }
    private static object GetSettingGroup(string name, object settings)
    {
      if (name == null || name.Length == 0)
      {
        throw new ArgumentNullException("name cannot be empty.");
      }
      FieldInfo info = settings.GetType().GetField(name);
      return info.GetValue(settings);
    }
    private static void GetGroupItems(
      string groupName, object group, List<Item> items)
    {
      object dflt = GetDefaultValue(group.GetType());
      FieldInfo[] infos = group.GetType().GetFields();
      for (int i = 0; i < infos.Length; ++i)
      {
        object dfltValue = infos[i].GetValue(dflt);
        object value = infos[i].GetValue(group);
        string desc = GetFieldDescription(infos[i]);
        items.Add(new Item(groupName + '.' + infos[i].Name,
          infos[i].FieldType, value, dfltValue, desc));
      }
      return;
    }
    private static void WriteXmlAttributes(
      object group, XmlTextWriter xw, bool includeInternalMembers)
    {
      var flag = BindingFlags.Instance | BindingFlags.Public;
      if (includeInternalMembers) flag |= BindingFlags.NonPublic;
      var infos = group.GetType().GetFields(flag);
      for (int i = 0; i < infos.Length; ++i)
      {
        object value = infos[i].GetValue(group);
        if (value != null)
          xw.WriteAttributeString(infos[i].Name, value.ToString());
      }
      return;
    }

    #endregion Query Implementation


  }
}
