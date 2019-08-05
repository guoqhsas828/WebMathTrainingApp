//
// Copyright (c)    2002-2015. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{

  [TestFixture]
  public class SwapBreakDateTests
  {
    [TestCase("Plain")]
    [TestCase("WithCorrectiveOverlay")]
    public void PvConsistency(string caseName)
    {
      const string name = "SwapPricer";

      var fixture = new PvConsistencyTests(caseName);
      fixture.SetUp();
      try
      {
        var pricer = (SwapPricer) fixture.LoadPricer(name);

        // Set the break date
        var breakDate = new Dt(20180630);
        pricer.PayerSwapPricer.SwapLeg.NextBreakDate
          = pricer.ReceiverSwapPricer.SwapLeg.NextBreakDate
            = breakDate;

        // monthly exposure dates...
        var exposureDates = new UniqueSequence<Dt>();
        foreach (var date in PvConsistencyTests.GetExposureDates(
          pricer.AsOf, pricer.Product.Maturity, Frequency.Monthly))
        {
          exposureDates.Add(date);
        }
        // ...plus the 7 days around the break date
        for (int d = -3; d <= 3; ++d)
        {
          exposureDates.Add(breakDate + d);
        }

        // check Pv consistency on all the exposure dates
        fixture.CheckPvConsistency(pricer, exposureDates);
      }
      finally
      {
        fixture.TearDown();
      }
    }
  }
}
