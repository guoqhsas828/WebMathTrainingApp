// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using StoreManager.Models;
using System;
using System.Collections;
using System.Text;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Interface for object to validate the internal states
  ///  after construction
  /// </summary>
  public interface IValidatable
  {
    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <remarks>
    ///   By default validation is metadata-driven.  Entities that enforce
    ///   additional constraints can override this method.  Methods that do
    ///   override must first call Validate() on the base class.
    /// </remarks>
    /// <param name="errors">Array of resulting errors</param>
    void Validate(ArrayList errors);
  }

  /// <summary>
  ///  Class with extension methods for Validation.
  /// </summary>
  public static class Validation
  {
    /// <summary>
    /// Validate the object and throw and exception if any errors.
    /// </summary>
    /// <param name="obj">The object to validate</param>
    /// <exception cref="ValidationException"></exception>
    /// <remarks><para>Calls the validate method and tests for any resulting errors.</para>
    /// <para>Warnings are logged and an exception is thrown if any errors are
    /// found.</para></remarks>
    public static void Validate(this IValidatable obj)
    {
      var errors = new ArrayList();
      obj.Validate(errors);
      var msg = CollectValidationErrors(errors);
      if (!string.IsNullOrEmpty(msg))
      {
        throw new ValidationException(msg);
      }
    }

    /// <summary>
    ///   Validate the object and return an array of InvalidValue items or null
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calls the validate method and tests for any resulting errors.</para>
    /// </remarks>
    ///
    /// <returns>Array of InvalidValue results or null</returns>
    ///
    public static InvalidValue[] TryValidate(this IValidatable obj)
    {
      var errors = new ArrayList();

      obj.Validate(errors);

      return (errors.Count == 0) ? null : (InvalidValue[]) (errors.ToArray(typeof (InvalidValue)));
    }

    /// <summary>
    /// Collects the validation error messages,
    ///  writes them to logger based on the loger level,
    ///  and returns the collected message.
    /// </summary>
    /// <param name="errors">The errors.</param>
    /// <returns>The collected error message.</returns>
    public static string CollectValidationErrors(ArrayList errors)
    {
      if (errors == null || errors.Count == 0) return null;

      // Test for errors and log warnings
      var sb = new StringBuilder();
      foreach (InvalidValue v in errors)
      {
        string message = String.Format("{0}{1}", String.IsNullOrWhiteSpace(v.NameOfProperty) ? String.Empty : v.NameOfProperty + " - ", v.Message);
        switch (v.ErrorLevel)
        {
          case ErrorLevel.Error:
            Logger.Error(message);
            sb.AppendLine(message);
            break;
          case ErrorLevel.Warning:
            Logger.Warn(message);
            break;
          case ErrorLevel.Info:
            Logger.Info(message);
            break;
        }
      }
      return sb.ToString();
    }

    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Validation));
  }


  /// <summary>
  ///   Interface for all WebMathTraining objects
  /// </summary>
  public interface IBaseEntityObject : IValidatable, ICloneable
  {
  }

  ///
  /// <summary>
  ///   Abstract base class for all WebMathTraining classes.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>All WebMathTraining classes derive from this object. It is designed as a simple
  ///   lightweight parent which provides a set of standard utility methods
  ///   that are best handled in a common base class.</para>
  ///
  ///   <para>This class implements the IConeable interface and all objects derived from this class
  ///   inherit this.</para>
  ///
  ///   <para>It should be noted that the Clone method implements a deep copy but each type of
  ///   object typically has a philosophy on what is copied and what is not. Often this will be
  ///   based on likely use cases for the particular object. Each class should clearly document
  ///   the Clone behaviour</para>
  /// </remarks>
  ///
  [Serializable]
  public abstract class BaseEntityObject : BaseEntityModel<long>, IBaseEntityObject
  {
    #region Methods

    /// <summary>
    ///   Validate the object and throw and exception if any errors.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calls the validate method and tests for any resulting errors.</para>
    ///
    ///   <para>Warnings are logged and an exception is thrown if any errors are
    ///   found.</para>
    /// </remarks>
    ///
    /// <returns>Array of InvalidValue results or null</returns>
    ///
    public void Validate()
    {
      Validation.Validate(this);
    }

    /// <summary>
    /// Collects the validation error messages,
    ///  writes them to logger based on the loger level,
    ///  and returns the collected message.
    /// </summary>
    /// <param name="errors">The errors.</param>
    /// <returns>The collected error message.</returns>
    public static string CollectValidationErrors(ArrayList errors)
    {
      return Validation.CollectValidationErrors(errors);
    }

    /// <summary>
    ///   Validate the object and return an array of InvalidValue items or null
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calls the validate method and tests for any resulting errors.</para>
    /// </remarks>
    ///
    /// <returns>Array of InvalidValue results or null</returns>
    ///
    public InvalidValue[] TryValidate()
    {
      return Validation.TryValidate(this);
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <remarks>
    ///   By default validation is metadata-driven.  Entities that enforce
    ///   additional constraints can override this method.  Methods that do
    ///   override must first call Validate() on the base class.
    /// </remarks>
    /// <param name="errors">Array of resulting errors</param>
    public virtual void Validate(ArrayList errors)
    {}

    /// <summary>
    ///   Return a new object that is a shallow copy (MemberwiseClone) of this instance
    /// </summary>
    public object ShallowCopy()
    {
      return MemberwiseClone();
    }

    #endregion Methods

    #region ICloneable

    /// <summary>
    ///  Return a new object that is a deep copy of this instance
    /// </summary>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the lifecycle of the referenced object).
    /// </remarks>
    public virtual object Clone()
    {
      return MemberwiseClone();
    }

    #endregion ICloneable
  }
}
