// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Some helpful extension methods
  /// </summary>
  public static class ExtensionMethods
  {
    /// <summary>
    /// Returns a full exception message, expanding out AggregateException messages and nested TargetInvocationException messages.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns></returns>
    public static string GetFullMessage(this Exception exception)
    {
      if (exception == null)
      {
        return "Unable to establish exception message";
      }
      var aggregateException = exception as AggregateException;
      if (aggregateException != null)
      {
        return string.Join("\n", aggregateException.InnerExceptions.Select(GetFullMessage));
      }
      var targetInvocationException = exception as TargetInvocationException;
      if (targetInvocationException != null)
      {
        return targetInvocationException.InnerException.GetFullMessage();
      }
      return exception.Message;
    }

    /// <summary>
    /// Unwraps the specified exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns></returns>
    public static Exception Unwrap(this Exception exception)
    {
      if (exception == null)
      {
        return null;
      }
      var aggregateException = exception as AggregateException;
      if (aggregateException != null)
      {
        return Unwrap(aggregateException.Flatten().InnerExceptions.FirstOrDefault());
      }
      var targetInvocationException = exception as TargetInvocationException;
      if (targetInvocationException != null)
      {
        return Unwrap(targetInvocationException.InnerException);
      }
      return exception;
    }

    /// <summary>
    /// Linked list enumerator
    /// </summary>
    public static IEnumerable<TSource> FromHierarchy<TSource>(
        this TSource source,
        Func<TSource, TSource> nextItem,
        Func<TSource, bool> canContinue)
    {
      for (var current = source; canContinue(current); current = nextItem(current))
      {
        yield return current;
      }
    }

    /// <summary>
    /// Linked list enumerator
    /// </summary>
    public static IEnumerable<TSource> FromHierarchy<TSource>(
        this TSource source,
        Func<TSource, TSource> nextItem)
        where TSource : class
    {
      return FromHierarchy(source, nextItem, s => s != null);
    }

    /// <summary>
    /// Extracts messages from inner exceptions (including parent exception)
    /// </summary>
    public static string ExtractInnerExceptionMessages(this Exception exception, string separator)
    {
      var messages = exception.FromHierarchy(ex => ex.InnerException).Select(ex => ex.Message);
      return string.Join(separator, messages);
    }


    /// <summary>
    /// Pluralizes the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="noneTerm">The none term.</param>
    /// <param name="singleTerm">The single term.</param>
    /// <param name="manyTerm">The many term.</param>
    /// <returns></returns>
    public static string Pluralize(this int value, string noneTerm, string singleTerm, string manyTerm)
    {
      var term = manyTerm;
      if (value == 0)
      {
        term = noneTerm;
      }
      if (value == 1)
      {
        term = singleTerm;
      }
      return string.Format("{0} {1}",
                           value,
                           term);
    }

    /// <summary>
    /// Get the assembly-qualifed name without version, culture or architecture.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns></returns>
    public static string GetAssemblyQualifiedShortName(this Type type)
    {
      return String.Format("{0}, {1}", type.FullName, type.Assembly.GetName().Name);
    }
  }
}