/*
 * MetadataException.cs -
 *
 * Copyright (c) WebMathTraining 2002-2008. All rights reserved.
 *
 * $Id: MetadataException.cs,v 1.1 2006/11/21 22:14:53 mtraudt Exp $
 *
 */

using System;
using System.Reflection;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
	/// <summary>
	/// Used to indicate problem with compiled metadata
  /// </summary>
  [Serializable]
	public class MetadataException : Exception
	{
		/// <summary>
		/// Construct exception with specified message
		/// </summary>
		public MetadataException(string message) : base(message)
		{
		}

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataException"/> class.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is null. </exception>
    ///   
    /// <exception cref="T:System.Runtime.Serialization.SerializationException">The class name is null or <see cref="P:System.Exception.HResult"/> is zero (0). </exception>
    protected MetadataException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  public class InvalidPropertyTypeException : MetadataException
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="classMeta"></param>
    /// <param name="propInfo"></param>
    public InvalidPropertyTypeException(ClassMeta classMeta, PropertyInfo propInfo)
      : base(FormatMessage(classMeta, propInfo))
    {
    }

    private static string FormatMessage(ClassMeta classMeta, PropertyInfo propInfo)
    {
      return String.Format(
        "Invalid PropertyType [{0}] for [{1}.{2}]",
        propInfo.PropertyType.Name, classMeta.Name, propInfo.Name);
    }
  }
  
	/// <summary>
	/// Used to indicate that one or more constraints not valid
	/// </summary>
	/// <remarks>
	/// An InvalidValueException is used to indicate that one or more
	/// class invariants are in error.
  /// </remarks>
  [Serializable]
	public class InvalidValueException : MetadataException
	{
		/// <summary>
		/// Construct instance with specified array of constraint violations
		/// </summary>
		public InvalidValueException(InvalidValue[] errors) : base("Invalid value(s)")
		{
			errors_ = errors;
		}

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidValueException"/> class.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is null. </exception>
    ///   
    /// <exception cref="T:System.Runtime.Serialization.SerializationException">The class name is null or <see cref="P:System.Exception.HResult"/> is zero (0). </exception>
    protected InvalidValueException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
      errors_ = (InvalidValue[]) info.GetValue("errors_", typeof (InvalidValue[]));
		}

		/// <summary>
		/// Array of <see cref="WebMathTraining.Shared.InvalidValue">InvalidValue instances</see> for this exception.  
		/// </summary>
		public InvalidValue[] Errors
		{
			get { return errors_; }
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
    public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
      info.AddValue("errors_", errors_);

      // Base
      base.GetObjectData(info, context);
    }

		private InvalidValue[] errors_;
	}
}
