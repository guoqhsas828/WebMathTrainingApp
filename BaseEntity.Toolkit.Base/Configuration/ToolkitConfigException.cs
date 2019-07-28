/*
 * ToolkitConfigException.cs
 *
 * Copyright (c)    2004-2010. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Toolkit configuration exception
  /// </summary>
  [Serializable]
  public class ToolkitConfigException : ToolkitException
  {
    public ToolkitConfigException(
      string message, Exception innerException)
      :base(message, innerException)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolkitConfigException"/> class.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is null. </exception>
    ///   
    /// <exception cref="T:System.Runtime.Serialization.SerializationException">The class name is null or <see cref="P:System.Exception.HResult"/> is zero (0). </exception>
    protected ToolkitConfigException(
      SerializationInfo info, 
      StreamingContext context) 
      : base(info, context)
    {
    }
  }

  /// <summary>
  ///  Toolkit configuration exception indicating errors reading xml nodes
  /// </summary>
  [Serializable]
  public class ToolkitConfigReadException : ToolkitConfigException
  {
    public ToolkitConfigReadException(
      string message, Exception innerException)
      : base(message, innerException)
    { }
  }

  /// <summary>
  ///  Toolkit configuration exception indicating the missing of a configuration group
  /// </summary>
  [Serializable]
  public class ToolkitConfigGroupMissingException : ToolkitConfigException
  {
    internal ToolkitConfigGroupMissingException(string groupName)
      : base("Missing config group: " + groupName, null)
    {
      GroupName = groupName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolkitConfigException"/> class.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is null. </exception>
    ///   
    /// <exception cref="T:System.Runtime.Serialization.SerializationException">The class name is null or <see cref="P:System.Exception.HResult"/> is zero (0). </exception>
    protected ToolkitConfigGroupMissingException(
      SerializationInfo info, 
      StreamingContext context) 
      : base(info, context)
    {
      GroupName = info.GetString("GroupName");
    }

    /// <summary>
    /// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with information about the exception.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is a null reference (Nothing in Visual Basic). </exception>
    ///   
    /// <PermissionSet>
    ///   <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*"/>
    ///   <IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter"/>
    ///   </PermissionSet>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      base.GetObjectData(info, context);
      info.AddValue("GroupName", GroupName);
    }

    /// <summary>
    /// 
    /// </summary>
    public readonly string GroupName;
  }

  /// <summary>
  ///  Toolkit configuration exception indicating the missing of a configuration item
  /// </summary>
  [Serializable]
  public class ToolkitConfigItemMissingException : ToolkitConfigException
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="settingName"></param>
    /// <param name="dataType"></param>
    /// <param name="defaultValue"></param>
    /// <param name="settingDescription"></param>
    internal ToolkitConfigItemMissingException(
      string settingName,
      Type dataType,
      object defaultValue,
      string settingDescription)
      : base("Missing config setting: " + settingName, null)
    {
      SettingName = settingName;
      SettingDescription = settingDescription;
      SettingDataType = dataType;
      SettingDefaultValue = defaultValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolkitConfigException"/> class.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is null. </exception>
    ///   
    /// <exception cref="T:System.Runtime.Serialization.SerializationException">The class name is null or <see cref="P:System.Exception.HResult"/> is zero (0). </exception>
    protected ToolkitConfigItemMissingException(
      SerializationInfo info, 
      StreamingContext context) 
      : base(info, context)
    {
      SettingName = info.GetString("SettingName");
      SettingDescription = info.GetString("SettingDescription");
      SettingDataType = (Type)info.GetValue("SettingDataType", typeof (Type));
      SettingDefaultValue = info.GetValue("SettingDefaultValue", typeof (object));
    }

    /// <summary>
    /// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with information about the exception.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is a null reference (Nothing in Visual Basic). </exception>
    ///   
    /// <PermissionSet>
    ///   <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*"/>
    ///   <IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter"/>
    ///   </PermissionSet>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      base.GetObjectData(info, context);
      info.AddValue("SettingName", SettingName);
      info.AddValue("SettingDescription", SettingDescription);
      info.AddValue("SettingDataType", SettingDataType);
      info.AddValue("SettingDefaultValue", SettingDefaultValue);
    }

    /// <summary>
    /// 
    /// </summary>
    public readonly string SettingName;
    /// <summary>
    /// 
    /// </summary>
    public readonly string SettingDescription;
    /// <summary>
    /// 
    /// </summary>
    public readonly Type SettingDataType;
    /// <summary>
    /// 
    /// </summary>
    public readonly object SettingDefaultValue;
  }

}
