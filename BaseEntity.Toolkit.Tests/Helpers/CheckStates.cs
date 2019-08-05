using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  /// <summary>
  ///  Save and Check Pricer States
  /// </summary>
  public class CheckStates : IDisposable
  {
    public CheckStates(bool check, IPricer[] p)
    {
      if (!check) return;
      orig_ = p;
      saved_ = CloneUtil.CloneObjectGraph(p, CloneMethod.FastClone);
    }


    /// <summary>
    /// Asserts if two objects match by internal states.
    /// </summary>
    /// <typeparam name="T">Object type</typeparam>
    /// <param name="name">The name.</param>
    /// <param name="A">The first object to compare.</param>
    /// <param name="B">The second object to compare.</param>
    internal static void AssertMatch<T>(
      string name, T A, T B)
    {
      var result = ObjectStatesChecker.Compare(A, B);
      if (result != null)
      {
        Assert.AreEqual(result.FirstValue?.ToString(),
          result.SecondValue?.ToString(), name + result.Name);
      }
      return;
    }

    /// <summary>
    /// Asserts if two objects do not match by internal states.
    /// </summary>
    /// <typeparam name="T">Object type</typeparam>
    /// <param name="name">The name.</param>
    /// <param name="A">The first object to compare.</param>
    /// <param name="B">The second object to compare.</param>
    internal static void AssertDontMatch<T>(
      string name, T A, T B)
    {
      var result = ObjectStatesChecker.Compare(A, B);
      if (result != null)
      {
        Assert.AreNotEqual(result.FirstValue?.ToString(),
          result.SecondValue?.ToString(), name + result.Name);
      }
      return;
    }

    public void Dispose()
    {
      if (orig_ == null) return;
      for (int i = 0; i < orig_.Length; ++i)
        if (orig_[i] != null)
        {
          AssertMatch(orig_[i].Product.Description,
            Reset(saved_[i]), Reset(orig_[i]));
        }
      return;
    }

    private static PricerAndPv Reset(IPricer pricer)
    {
      if (pricer == null)
        return new PricerAndPv();
      // Make sure all the internal fields are updated.
      {
        var copricer = pricer as CDOOptionPricer;
        if (copricer != null)
        {
          copricer.Basket.Reset();
          copricer.Reset();
        }
      }
      double pv = pricer.Pv();
      {
        var cdsPricer = pricer as CDSCashflowPricer;
        if (cdsPricer != null)
        {
          cdsPricer.Reset();
        }
      }
      return new PricerAndPv() { Pricer = pricer, Pv = pv };
    }

    private IPricer[] saved_, orig_;
    private static Cashflow cf_;

    [Serializable]
    private class PricerAndPv
    {
      public IPricer Pricer;
      public double Pv;
    }
  }

}
