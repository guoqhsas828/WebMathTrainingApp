// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;

namespace BaseEntity.Shared
{
  /// <summary>
  ///   Describes a data related error for an object or a field of an object.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>The InvalidValue class is used with the Validate method to
  ///   provide a way of validating an object or set of related objects.</para>
  ///
  ///   <para>This is typically used for either User input validation or
  ///   general data validation.</para>
  /// </remarks>
  ///
  [Serializable]
  public class InvalidValue
  {
    #region Constructors

    /// <summary>
    ///   Constructor for an object field error or warning
    /// </summary>
    ///
    /// <param name="obj">Object with error</param>
    /// <param name="propName">Name of property of object with error</param>
    /// <param name="referenceObj">Related object (eg. Trade)</param>
    /// <param name="message">Description of error</param>
    /// <param name="level">Error level</param>
    ///
    protected InvalidValue(object obj, string propName, object referenceObj, string message, ErrorLevel level)
    {
      object_ = obj;
      propName_ = propName;
      referenceObject_ = referenceObj;
      message_ = message;
      level_ = level;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Object that is invalid
    /// </summary>
    public object Object
    {
      get { return object_; }
    }

    /// <summary>
    ///   Name of object property if property is invalid
    /// </summary>
    public string NameOfProperty
    {
      get { return propName_; }
    }

    /// <summary>
    ///   Reference object for invalid object. Typically this
    ///   is used for a parent or related object.
    /// </summary>
    public object RefenceObject
    {
      get { return referenceObject_; }
    }

    /// <summary>
    ///   Description of invalid value condition
    /// </summary>
    public string Message
    {
      get { return message_; }
    }

    /// <summary>
    ///   Error level
    /// </summary>
    public ErrorLevel ErrorLevel
    {
      get { return level_; }
    }

    #endregion

    #region Methods

    /// <summary>
    ///  Returns a string description of the data error.
    /// </summary>
    public override string ToString()
    {
      string format = (propName_ != null)
        ? ((referenceObject_ != null)
          ? "InvalidValue: {0} ({1}.{2}) : '{4}'"
          : "InvalidValue: {0} ({1}.{2}) from {3} : '{4}'")
        : ((referenceObject_ != null)
          ? "InvalidValue: {0} ({1}) : '{4}'"
          : "InvalidValue: {0} ({1}) from {3} : '{4}'");
      return String.Format(format, level_, object_ == null ? null : object_.GetType().Name, propName_, referenceObject_, message_);
    }

    /// <summary>
    ///   Convenience routine to add an error to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="propertyName">Name of property of object with error</param>
    /// <param name="referenceObj">Related object (eg. Trade)</param>
    /// <param name="message">Error message</param>
    /// <param name="level">Error level</param>
    ///
    public static void Add(ArrayList errors, object obj, string propertyName, object referenceObj, string message, ErrorLevel level)
    {
      errors.Add(new InvalidValue(obj, propertyName, referenceObj, message, level));
    }

    /// <summary>
    ///   Convenience routine to add an error to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="message">Error message</param>
    public static void AddError(ArrayList errors, object obj, string message)
    {
      errors.Add(new InvalidValue(obj, null, null, message, ErrorLevel.Error));
    }

    /// <summary>
    ///   Convenience routine to add an error to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="propertyName">Name of property of object with error</param>
    /// <param name="message">Error message</param>
    ///
    public static void AddError(ArrayList errors, object obj, string propertyName, string message)
    {
      errors.Add(new InvalidValue(obj, propertyName, null, message, ErrorLevel.Error));
    }

    /// <summary>
    ///   Convenience routine to add an error to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="propertyName">Name of property of object with error</param>
    /// <param name="referenceObj">Related object (eg. Trade)</param>
    /// <param name="message">Error message</param>
    public static void AddError(ArrayList errors, object obj, string propertyName, object referenceObj, string message)
    {
      errors.Add(new InvalidValue(obj, propertyName, referenceObj, message, ErrorLevel.Error));
    }

    /// <summary>
    ///   Convenience routine to add an warning to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="message">Description of error</param>
    ///
    public static void AddWarning(ArrayList errors, object obj, string message)
    {
      errors.Add(new InvalidValue(obj, null, null, message, ErrorLevel.Warning));
    }

    /// <summary>
    ///   Convenience routine to add an warning to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="propertyName">Name of property of object with error</param>
    /// <param name="message">Description of error</param>
    ///
    public static void AddWarning(ArrayList errors, object obj, string propertyName, string message)
    {
      errors.Add(new InvalidValue(obj, propertyName, null, message, ErrorLevel.Warning));
    }

    /// <summary>
    ///   Convenience routine to add an warning to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="propertyName">Name of property of object with error</param>
    /// <param name="referenceObj">Related object (eg. Trade)</param>
    /// <param name="message">Description of error</param>
    ///
    public static void AddWarning(ArrayList errors, object obj, string propertyName, object referenceObj, string message)
    {
      errors.Add(new InvalidValue(obj, propertyName, referenceObj, message, ErrorLevel.Warning));
    }

    /// <summary>
    ///   Convenience routine to add a Note to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="message">Description of error</param>
    ///
    public static void AddInfo(ArrayList errors, object obj, string message)
    {
      errors.Add(new InvalidValue(obj, null, null, message, ErrorLevel.Info));
    }

    /// <summary>
    ///   Convenience routine to add a Note to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="propertyName">Name of property of object with error</param>
    /// <param name="message">Description of error</param>
    ///
    public static void AddInfo(ArrayList errors, object obj, string propertyName, string message)
    {
      errors.Add(new InvalidValue(obj, propertyName, null, message, ErrorLevel.Info));
    }

    /// <summary>
    ///   Convenience routine to add a Note to a list of errors.
    /// </summary>
    ///
    /// <param name="errors">List of errors</param>
    /// <param name="obj">Object with error</param>
    /// <param name="propertyName">Name of property of object with error</param>
    /// <param name="referenceObj">Related object (eg. Trade)</param>
    /// <param name="message">Description of error</param>
    ///
    public static void AddInfo(ArrayList errors, object obj, string propertyName, object referenceObj, string message)
    {
      errors.Add(new InvalidValue(obj, propertyName, referenceObj, message, ErrorLevel.Info));
    }

    #endregion

    #region Data

    private object object_;
    private string propName_;
    private object referenceObject_;
    private string message_;
    private ErrorLevel level_;

    #endregion
  } // class InvalidValue
}