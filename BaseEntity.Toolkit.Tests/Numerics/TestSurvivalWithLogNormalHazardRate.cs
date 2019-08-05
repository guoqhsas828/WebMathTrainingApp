//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  [TestFixture]
  public class TestSurvivalWithLogNormalHazardRate
  {
    [Test]
    public void Test()
    {
      var s = Mean(0, 0.5);
    }

    private static double Mean(double mu, double sigma)
    {
      double s = 1, a = 1, ek = 1, s2 = sigma*sigma,
        es2 = Math.Exp(s2), eu = Math.Exp(mu-0.5*s2);
      for (int k = 1; k < 1000; ++k)
      {
        ek *= es2;
        a *= (ek/k)*eu;
        if (a < 1E-16) break;
        s += (k%2 == 0 ? 1 : -1)*a;
      }
      return s;
    }
  }
}
