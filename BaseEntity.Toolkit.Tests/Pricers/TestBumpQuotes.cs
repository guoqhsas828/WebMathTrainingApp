//
// Copyright (c)    2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestBumpQuotes : ToolkitTestBase
  {
    private class TestCase
    {
      public double Spread;
      public double BumpSize;
      public bool Relative;
      public bool Up;
      public double ExpectedBumpAmount;
    }

    private readonly TestCase[] testCases = new []
    {
      new TestCase {Spread = 0.2, BumpSize = 0.1, Relative = true, Up = true, ExpectedBumpAmount = 0.02},
      new TestCase {Spread = -0.2, BumpSize = 0.1, Relative = true, Up = true, ExpectedBumpAmount = 0.02},
      new TestCase {Spread = 0.2, BumpSize = -0.1, Relative = true, Up = true, ExpectedBumpAmount = -0.0181818181818182},
      new TestCase {Spread = -0.2, BumpSize = -0.1, Relative = true, Up = true, ExpectedBumpAmount = -0.0222222222222222},
      new TestCase {Spread = 0.2, BumpSize = 0.1, Relative = true, Up = false, ExpectedBumpAmount = -0.0181818181818182},
      new TestCase {Spread = -0.2, BumpSize = 0.1, Relative = true, Up = false, ExpectedBumpAmount = -0.0222222222222222},
      new TestCase {Spread = 0.2, BumpSize = -0.1, Relative = true, Up = false, ExpectedBumpAmount = 0.02},
      new TestCase {Spread = -0.2, BumpSize = -0.1, Relative = true, Up = false, ExpectedBumpAmount = 0.02},

      new TestCase {Spread = 0.2, BumpSize = 20, Relative = false, Up = true, ExpectedBumpAmount = 0.002},
      new TestCase {Spread = 0.2, BumpSize = 20, Relative = false, Up = false, ExpectedBumpAmount = -0.002},
      new TestCase {Spread = -0.2, BumpSize = 20, Relative = false, Up = true, ExpectedBumpAmount = 0.002},
      new TestCase {Spread = -0.2, BumpSize = 20, Relative = false, Up = false, ExpectedBumpAmount = -0.002},
      
      new TestCase {Spread = 0.0002, BumpSize = 10, Relative = false, Up = false, ExpectedBumpAmount = -0.0001},
      new TestCase {Spread = -0.0002, BumpSize = 10, Relative = false, Up = true, ExpectedBumpAmount = 0.0001}
    };

    [Test]
    public void CdsQuoteHandlerBumpQuote()
    {
      const double eps = 1e-9;
      var cdsProduct = new CDS(Dt.Today(), 5, 1, Calendar.NYB);
      var ct = new CurveTenor("CDS Tenor", cdsProduct, 0.0);

      foreach (var test in testCases)
      {
        double expectedReturnValue = (test.Up ? 1.0 : -1.0) * test.ExpectedBumpAmount * 10000.0;

        var cdsQuoteHandler = new CurveTenorQuoteHandlers.CDSQuoteHandler(test.Spread, QuotingConvention.CreditSpread, null, 0.0);
        BumpFlags flags = 0x0;
        if (test.Relative)
          flags |= BumpFlags.BumpRelative;
        if (!test.Up)
          flags |= BumpFlags.BumpDown;
        double r1 = cdsQuoteHandler.BumpQuote(ct, test.BumpSize, flags);
        var testDescrip = String.Format("{0} {1} bump Spread {2} by BumpSize {3}",
          test.Relative ? "Relative" : "Absolute", test.Up ? "Up" : "Down",
          test.Spread, test.BumpSize);
        Assert.AreEqual(expectedReturnValue, r1, eps, testDescrip);
      }
    }
  }
}
