using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  internal interface IExpectsStore : IDisposable
  {
    object GetValue(string key);
    void SetValue(string key, object value);
  }

  class ExpectsStore : IExpectsStore
  {
    private string _path;
    private Dictionary<string, object> _data;

    private ExpectsStore() { }

    public static IExpectsStore Load(string path)
    {
      if (string.IsNullOrEmpty(path))
      {
        throw new ArgumentNullException($"{nameof(path)} is empty");
      }
      var data = File.Exists(path)
        ? XmlSerialization.ReadXmlFile<Dictionary<string, object>>(path)
        : new Dictionary<string, object>();
      return new ExpectsStore {_path = path, _data = data};
    }

    public void Dispose()
    {
      if (_data != null && _path != null && BaseEntityContext.IsGeneratingExpects)
      {
        XmlSerialization.WriteXmlFile(_data, _path);
      }
    }

    public object GetValue(string key)
    {
      if (!_data.TryGetValue(key, out var value))
      {
        throw new KeyNotFoundException(
          $"Expected data not found for {key}");
      }

      return value;
    }

    public void SetValue(string key, object value)
    {
      _data[key] = value;
    }
  }

  internal class SaveValueConstraint : EqualConstraint
  {
    private static readonly object NoValue = new object();

    private readonly IExpectsStore _store;
    private readonly string _key;

    public SaveValueConstraint(IExpectsStore store, string key)
      : base(NoValue)
    {
      _store = store;
      _key = key;
    }

    public override ConstraintResult ApplyTo<TActual>(TActual actual)
    {
      _store.SetValue(_key, actual);
      return new ConstraintResult(this, actual, true);
    }
  }

  internal class ObjectMatchConstraint : EqualConstraint
  {
    public ObjectMatchConstraint(object expected) : base(expected)
    {
    }

    public override ConstraintResult ApplyTo<TActual>(TActual actual)
    {
      var hasTolerance = !Tolerance.IsUnsetOrDefault;
      var tolerance = hasTolerance ? (double) Tolerance.Amount : 0.0;
      var result = hasTolerance
        ? ObjectStatesChecker.Compare(Expected, actual, tolerance)
        : ObjectStatesChecker.Compare(Expected, actual);
      if (result == null) return new ConstraintResult(this, actual, true);

      var constraint = Is.EqualTo(result.FirstValue);
      if (hasTolerance) constraint = constraint.Within(tolerance);
      Description = $"At {result.Name}\n{constraint.Description}";
      var constraintResult = constraint.ApplyTo(result.SecondValue);
      return new ConstraintResult(this, result.SecondValue, constraintResult.Status);
    }

    private object Expected => Arguments[0];
  }

  internal static class To
  {
    #region Extension methods
    public static void IsExpected(this object actual,
      IResolveConstraint expression, string msg = null, params object[] args)
    {
      if (msg != null) Assert.That(actual, expression);
      else Assert.That(actual, expression, msg, args);
    }
    #endregion

    #region Custom constraints

    public static EqualConstraint Match(object expected)
    {
      if (expected is IExpectsStore store)
      {
        var key = GetCaseName();
        if (BaseEntityContext.IsGeneratingExpects)
        {
          return new SaveValueConstraint(store, key);
        }
        expected = store.GetValue(key);
      }

      if (expected == null) return Is.EqualTo(null);

      var type = expected.GetType();
      if (IsSimpleType(type) || 
        (type.IsArray && IsSimpleType(type.GetElementType())))
      {
        return Is.EqualTo(expected);
      }
      return new ObjectMatchConstraint(expected);
    }

    private static bool IsSimpleType(Type type)
    {
      return type.IsPrimitive || type.IsEnum ||
        Type.GetTypeCode(type) != TypeCode.Object;
    }

    private static string GetCaseName()
    {
      var key = TestContext.CurrentContext.Test?.Name;
      if (string.IsNullOrEmpty(key))
      {
        throw new ToolkitException("Test name not found");
      }

      return key;
    }

    #endregion

    /// <summary>
    ///   Returns To.Be prefix for composition.
    /// </summary>
    public static ConstraintExpression Be { get; } = new ConstraintExpression();
  }
}
