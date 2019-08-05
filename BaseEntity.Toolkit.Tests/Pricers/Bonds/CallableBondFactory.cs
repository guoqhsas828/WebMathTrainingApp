using System;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  internal static class CallableBondFactory
  {

    internal static BondPricer AddCalls(
      BondPricer pricer,
      IEnumerable<CallPeriod> calls)
    {
      pricer = (BondPricer)pricer.Clone();
      var bond = (Bond)pricer.Bond.Clone();
      bond.CallSchedule.AddRange(calls);
      pricer.Product = bond;
      return pricer;
    }

    internal static BondPricer AddAmortization(
      BondPricer pricer,
      IEnumerable<Amortization> amortizations)
    {
      pricer = (BondPricer)pricer.Clone();
      var bond = (Bond)pricer.Bond.Clone();
      ((List<Amortization>)bond.AmortizationSchedule).AddRange(amortizations);
      pricer.Product = bond;
      return pricer;
    }

    internal static Amortization RemainingNotional(double level, Dt date)
    {
      return new Amortization(date,
        AmortizationType.RemainingNotionalLevels, level);
    }

    internal static CallPeriod Call(Dt date, double strikePrice)
    {
      return new CallPeriod(date, date, strikePrice,
        0, OptionStyle.European, 0);
    }
  }
}
