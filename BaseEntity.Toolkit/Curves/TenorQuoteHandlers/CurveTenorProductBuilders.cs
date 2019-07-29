//
//  -2014. All rights reserved.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.TenorQuoteHandlers
{
  using ProductBuilder = Func<IStandardProductTerms,
    Dt, string, IMarketQuote, IProduct>;

  static class CurveTenorProductBuilders
  {
    private static readonly ConcurrentDictionary<Type, ProductBuilder>
      Builders = new ConcurrentDictionary<Type, ProductBuilder>();

    /// <summary>
    ///   Retrieve the product builder for the specified product terms
    /// </summary>
    /// <param name="terms">The standard product terms</param>
    /// <returns>The product builder</returns>
    public static ProductBuilder Get(IStandardProductTerms terms)
    {
      return Builders.GetOrAdd(terms.GetType(), CreateProductBuilder);
    }

    /// <summary>
    ///   Create a product builder from a type implementing IStandardProductTerm
    /// </summary>
    /// 
    /// <remarks>
    ///  <para>A product builder is a delegate with the signature
    ///    <c>IProduct(IStandardProductTerms terms, Dt asOf, string tenorName, IMarketQuote quote)</c>.
    ///    In other words, it takes four parameters with the types
    ///    <c>IStandardProductTerms</c>, <c>Dt</c>, <c>string</c> and <c>IMarketQuote</c>,
    ///    in that order, and returns an instance of <c>IProduct</c>.
    ///  </para>
    /// 
    ///  <para>Given a type implementing IStandardProductTerm, we create
    ///    a product builder by Conventions.</para>
    /// 
    ///  <para>First we find all the instance methods with <see cref="ProductBuilderAttribute" />.
    ///   If no such method is found, an exception is thrown indicating the failure.
    ///   In the case where more than one methods are found, we further restrict them to the methods
    ///   with the most derived declaring type.  If there are still more than one methods, then an 
    ///   exception is thrown for multiple product builders.  Otherwise, the single method is picked.</para>
    /// 
    ///  <para>It is recommended that the IStandardProductTerm implementation always
    ///   contain one, and only one, method with the <c>ProducdBuilder</c> attribute.</para>
    /// </remarks>
    private static ProductBuilder CreateProductBuilder(Type type)
    {
      // Get all the product builder methods on the type.
      var methods = type.GetMethods(BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic|
        BindingFlags.FlattenHierarchy)
        .Where(IsProductBuilder).ToList();

      if (methods.Count == 0)
      {
        throw new ToolkitException(String.Format(
          "Product builder not found in {0}", type.FullName));
      }

      // Refine the search if more than one methods found.
      if (methods.Count > 1)
      {
        // Order by class hierarchy of the declaring type, which more derived come first.
        methods = methods.OrderBy(m => SuperLevel(m, type)).ToList();

        // Get the methods with the most derived declaring type.
        var mostDerived = methods[0].DeclaringType;
        methods = methods.Where(m => m.DeclaringType == mostDerived).ToList();

        // Error if still ambiguous!
        if (methods.Count > 1)
        {
          throw new ToolkitException(String.Format(
            "Ambiguous product builders.  Candidates are:\n  {0}",
            methods.Skip(1).Aggregate(methods[0].ToString(),
              (s, m) => s + "\n  " + m.ToString())));
        }
      }

      // Create a delegate
      return Expression.Lambda<ProductBuilder>(
        Call(methods[0], type), Parameters).Compile();
    }

    private static Expression Call(MethodInfo method, Type instanceType)
    {
      var terms = Expression.Convert(Parameters[0],
        method.DeclaringType ?? instanceType);

      var pars = method.GetParameters();
      if (pars.Length == 0)
        return Expression.Call(terms, method);

      var dt = Parameters[1];
      var tenor = Parameters[2];
      var quote = Parameters[3];
      var list = new List<Expression>();
      for (int i = 0, n = pars.Length; i < n; ++i)
      {
        var ptype = pars[i].ParameterType;
        if (ptype == typeof(Dt))
          list.Add(dt);
        else if (ptype == typeof(string))
          list.Add(tenor);
        else if (ptype == typeof(double))
          list.Add(Expression.Property(quote, "Value"));
        else if (ptype == typeof (IMarketQuote))
          list.Add(quote);
        else
          throw InvalidParameter(pars[i], method, instanceType);
      }
      return Expression.Call(terms, method, list);
    }

    private static int SuperLevel(MethodInfo info, Type type)
    {
      var declType = info.DeclaringType;
      if (declType == null)
        return int.MaxValue; // a huge number

      var ownAttr = info.GetCustomAttributes(
        typeof(ProductBuilderAttribute), false).Length > 0 ? 0 : 1;
      int level = 0;
      for (; type != null; ++level)
      {
        if (declType == type)
          return 2*level + ownAttr;
        type = type.BaseType;
      }
      return 2*level;
    }

    private static bool IsProductBuilder(MethodInfo info)
    {
      if (info.GetCustomAttributes(typeof (ProductBuilderAttribute),
        true).Length == 0)
      {
        return false;
      }
      if (!HasValidParameters(info))
      {
        throw InvalidParameters(info, info.DeclaringType ?? info.ReflectedType);
      }
      if (!info.ReturnType.GetInterfaces()
        .Any(t => typeof (IProduct).IsAssignableFrom(t)))
      {
        throw InvalidReturnType(info, info.DeclaringType ?? info.ReflectedType);
      }
      return true;
    }

    private static bool HasValidParameters(MethodInfo info)
    {
      var pars = info.GetParameters();
      var count1 = pars.Count(p => p.ParameterType == typeof(Dt));
      var count2 = pars.Count(p => p.ParameterType == typeof(string));
      var count3 = pars.Count(p => p.ParameterType == typeof (double)
        || p.ParameterType == typeof (IMarketQuote));
      return pars.Length == count2 + count2 + count3 &&
        count1 <= 1 && count2 <= 1 && count3 <= 1;
    }

    private static Exception InvalidReturnType(
      MethodInfo method, Type type)
    {
      return new ToolkitException(String.Format(
        "Product builder with invalid return type, method {0}, of type {1}",
        method, method.DeclaringType ?? type));
    }

    private static Exception InvalidParameters(
      MethodInfo method, Type type)
    {
      return new ToolkitException(String.Format(
        "Product builder with invalid parameters, method {0}, of type {1}",
        method, method.DeclaringType ?? type));
    }

    private static Exception InvalidParameter(
      ParameterInfo paramInfo, MethodInfo method, Type type)
    {
      return new ToolkitException(String.Format(
        "Product builder with invalid parameter {0}, method {1}, of type {2}",
        paramInfo.Name, method, method.DeclaringType ?? type));
    }

    private static readonly ParameterExpression[] Parameters =
    {
      Expression.Parameter(typeof (IStandardProductTerms), "terms"),
      Expression.Parameter(typeof (Dt), "asOf"),
      Expression.Parameter(typeof (string), "tenorName"),
      Expression.Parameter(typeof (IMarketQuote), "quote"),
    };
  }
}
