//
// Copyright (c)    2015. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture]
  public class InflationBondPricerTests
  {

    [Test]
    public void PvRiskless()
    {
      const double notional = 1000000000;
      Dt asOf = new Dt(20110609), settle = new Dt(20110613);
      var ibond = GetInflationBond();
      var dc = GetDiscountCurve(asOf);
      var inflationCurve = new InflationCurve(asOf, ibond.BaseInflation,
        new DiscountCurve(asOf, 0.02), dc, null)
      {
        Calibrator = new InflationCurveFitCalibrator(asOf, asOf, dc,
          (InflationIndex) ibond.ReferenceIndex, null),
        ReferenceIndex = ibond.ReferenceIndex
      };
      var sc = new SurvivalCurve(asOf, 0); // GetSurvivalCurve(dc);
      var pricer1 = new InflationBondPricer(ibond, asOf, settle,
        notional, dc, inflationCurve.InflationIndex, inflationCurve,
        null, null);
      var expect = pricer1.ProductPv();

      var pricer = new InflationBondPricer(ibond, asOf, settle,
        notional, dc, inflationCurve.InflationIndex, inflationCurve,
        null, null)
      {
        SurvivalCurve = sc
      };
      var pv = pricer.ProductPv();
      Assert.AreEqual(expect, pv, 1E-13*notional);

      var cf = pricer.GenerateCashflow(null, asOf);
      var cfPv = CashflowModel.Pv(cf,
        asOf, settle, dc, sc, null, 0,
        pricer.IncludeSettlePayments,
        pricer.IncludeMaturityProtection,
        pricer.DiscountingAccrued, 0,
        TimeUnit.None)*pricer.Notional;
      Assert.AreEqual(expect, cfPv, 1E-13*notional);
    }

    [Test]
    public void PvDefaultable()
    {
      const double notional = 1000000000;
      var bond = GetInflationBond();
      Dt asOf = Dt.Roll(new Dt(20110609), bond.BDConvention, bond.Calendar),
        settle= Dt.AddDays(asOf, 2, Calendar.NYB);
      var dc = GetDiscountCurve(asOf);
      var inflationCurve = new InflationCurve(asOf, bond.BaseInflation,
        new DiscountCurve(asOf, 0.02), dc, null)
      {
        Calibrator = new InflationCurveFitCalibrator(asOf, asOf, dc,
          (InflationIndex)bond.ReferenceIndex, null),
        ReferenceIndex = bond.ReferenceIndex
      };
      var sc = GetSurvivalCurve(dc);
      var pricer = new InflationBondPricer(bond, asOf, settle,
        notional, dc, inflationCurve.InflationIndex, inflationCurve,
        null, null)
      {
        SurvivalCurve = sc
      };
      var pv = pricer.ProductPv();

      var cf = pricer.GenerateCashflow(null, asOf);
      var cfPv = CashflowModel.Pv(cf,
        asOf, settle, dc, sc, null, 0,
        pricer.IncludeSettlePayments,
        pricer.IncludeMaturityProtection,
        pricer.DiscountingAccrued, 0,
        TimeUnit.None)*pricer.Notional;
      Assert.AreEqual(cfPv, pv, 1E-13*notional);
    }

    private static InflationBond GetInflationBond()
    {
      Dt effective = new Dt(20080115), maturity = new Dt(20280116);
      BondType type = BondType.USCorp;
      Currency ccy = Currency.USD;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar calendar = Calendar.NYB;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.Following;
      double coupon = 0.0125;
      return new InflationBond(effective, maturity, ccy, type,
        coupon, dayCount, CycleRule.None, freq, roll, calendar,
        GetInflationIndex(), 209.4964516, Tenor.Parse("3M"))
      {
        Description = "CIPS Bond - Market Full Additive Floor"
      };
    }

    private static InflationIndex GetInflationIndex()
    {
      return new InflationIndex("RPIGBP_INDEX", Currency.GBP,
        DayCount.ActualActualBond, Calendar.LNB,
        BDConvention.Modified, Frequency.Monthly, Tenor.Empty)
      {
        HistoricalObservations = usdYCpioyIndex
      };
    }

    private static DiscountCurve GetDiscountCurve(Dt asOf)
    {
      return new DiscountCurve(asOf, 0.03);
    }

    private static SurvivalCurve GetSurvivalCurve(DiscountCurve discountCurve)
    {
      Dt asOf = discountCurve.AsOf;
      return SurvivalCurve.FitCDSQuotes("Credit",
        asOf, asOf + 1, Currency.USD, "",CDSQuoteType.ParSpread, 250,
        new SurvivalCurveParameters(DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB,
        InterpMethod.Weighted, ExtrapMethod.Const,
        NegSPTreatment.Allow), discountCurve, new[] {"5Y"}, null, 
        new[] {250.0}, new[] {0.4}, 0, null, null, 0, false);
    }

    private static Dt _D(string input)
    {
      return input.ParseDt();
    }

    #region Historical RPI

    private static readonly RateResets usdYCpioyIndex = new RateResets
    {
      {_D("1-Jan-1998"), 161.6},
      {_D("1-Feb-1998"), 161.9},
      {_D("1-Mar-1998"), 162.2},
      {_D("1-Apr-1998"), 162.5},
      {_D("1-May-1998"), 162.8},
      {_D("1-Jun-1998"), 163},
      {_D("1-Jul-1998"), 163.2},
      {_D("1-Aug-1998"), 163.4},
      {_D("1-Sep-1998"), 163.6},
      {_D("1-Oct-1998"), 164},
      {_D("1-Nov-1998"), 164},
      {_D("1-Dec-1998"), 163.9},
      {_D("1-Jan-1999"), 164.3},
      {_D("1-Feb-1999"), 164.5},
      {_D("1-Mar-1999"), 165},
      {_D("1-Apr-1999"), 166.2},
      {_D("1-May-1999"), 166.2},
      {_D("1-Jun-1999"), 166.2},
      {_D("1-Jul-1999"), 166.7},
      {_D("1-Aug-1999"), 167.1},
      {_D("1-Sep-1999"), 167.9},
      {_D("1-Oct-1999"), 168.2},
      {_D("1-Nov-1999"), 168.3},
      {_D("1-Dec-1999"), 168.3},
      {_D("1-Jan-2000"), 168.8},
      {_D("1-Feb-2000"), 169.8},
      {_D("1-Mar-2000"), 171.2},
      {_D("1-Apr-2000"), 171.3},
      {_D("1-May-2000"), 171.5},
      {_D("1-Jun-2000"), 172.4},
      {_D("1-Jul-2000"), 172.8},
      {_D("1-Aug-2000"), 172.8},
      {_D("1-Sep-2000"), 173.7},
      {_D("1-Oct-2000"), 174},
      {_D("1-Nov-2000"), 174.1},
      {_D("1-Dec-2000"), 174},
      {_D("1-Jan-2001"), 175.1},
      {_D("1-Feb-2001"), 175.8},
      {_D("1-Mar-2001"), 176.2},
      {_D("1-Apr-2001"), 176.9},
      {_D("1-May-2001"), 177.7},
      {_D("1-Jun-2001"), 178},
      {_D("1-Jul-2001"), 177.5},
      {_D("1-Aug-2001"), 177.5},
      {_D("1-Sep-2001"), 178.3},
      {_D("1-Oct-2001"), 177.7},
      {_D("1-Nov-2001"), 177.4},
      {_D("1-Dec-2001"), 176.7},
      {_D("1-Jan-2002"), 177.1},
      {_D("1-Feb-2002"), 177.8},
      {_D("1-Mar-2002"), 178.8},
      {_D("1-Apr-2002"), 179.8},
      {_D("1-May-2002"), 179.8},
      {_D("1-Jun-2002"), 179.9},
      {_D("1-Jul-2002"), 180.1},
      {_D("1-Aug-2002"), 180.7},
      {_D("1-Sep-2002"), 181},
      {_D("1-Oct-2002"), 181.3},
      {_D("1-Nov-2002"), 181.3},
      {_D("1-Dec-2002"), 180.9},
      {_D("1-Jan-2003"), 181.7},
      {_D("1-Feb-2003"), 183.1},
      {_D("1-Mar-2003"), 184.2},
      {_D("1-Apr-2003"), 183.8},
      {_D("1-May-2003"), 183.5},
      {_D("1-Jun-2003"), 183.7},
      {_D("1-Jul-2003"), 183.9},
      {_D("1-Aug-2003"), 184.6},
      {_D("1-Sep-2003"), 185.2},
      {_D("1-Oct-2003"), 185},
      {_D("1-Nov-2003"), 184.5},
      {_D("1-Dec-2003"), 184.3},
      {_D("1-Jan-2004"), 185.2},
      {_D("1-Feb-2004"), 186.2},
      {_D("1-Mar-2004"), 187.4},
      {_D("1-Apr-2004"), 188},
      {_D("1-May-2004"), 189.1},
      {_D("1-Jun-2004"), 189.7},
      {_D("1-Jul-2004"), 189.4},
      {_D("1-Aug-2004"), 189.5},
      {_D("1-Sep-2004"), 189.9},
      {_D("1-Oct-2004"), 190.9},
      {_D("1-Nov-2004"), 191},
      {_D("1-Dec-2004"), 190.3},
      {_D("1-Jan-2005"), 190.7},
      {_D("1-Feb-2005"), 191.8},
      {_D("1-Mar-2005"), 193.3},
      {_D("1-Apr-2005"), 194.6},
      {_D("1-May-2005"), 194.4},
      {_D("1-Jun-2005"), 194.5},
      {_D("1-Jul-2005"), 195.4},
      {_D("1-Aug-2005"), 196.4},
      {_D("1-Sep-2005"), 198.8},
      {_D("1-Oct-2005"), 199.2},
      {_D("1-Nov-2005"), 197.6},
      {_D("1-Dec-2005"), 196.8},
      {_D("1-Jan-2006"), 198.3},
      {_D("1-Feb-2006"), 198.7},
      {_D("1-Mar-2006"), 199.8},
      {_D("1-Apr-2006"), 201.5},
      {_D("1-May-2006"), 202.5},
      {_D("1-Jun-2006"), 202.9},
      {_D("1-Jul-2006"), 203.5},
      {_D("1-Aug-2006"), 203.9},
      {_D("1-Sep-2006"), 202.9},
      {_D("1-Oct-2006"), 201.8},
      {_D("1-Nov-2006"), 201.5},
      {_D("1-Dec-2006"), 201.8},
      {_D("1-Jan-2007"), 202.416},
      {_D("1-Feb-2007"), 203.499},
      {_D("1-Mar-2007"), 205.352},
      {_D("1-Apr-2007"), 206.686},
      {_D("1-May-2007"), 207.949},
      {_D("1-Jun-2007"), 208.352},
      {_D("1-Jul-2007"), 208.299},
      {_D("1-Aug-2007"), 207.917},
      {_D("1-Sep-2007"), 208.49},
      {_D("1-Oct-2007"), 208.936},
      {_D("1-Nov-2007"), 210.177},
      {_D("1-Dec-2007"), 210.036},
      {_D("1-Jan-2008"), 211.08},
      {_D("1-Feb-2008"), 211.693},
      {_D("1-Mar-2008"), 213.528},
      {_D("1-Apr-2008"), 214.823},
      {_D("1-May-2008"), 216.632},
      {_D("1-Jun-2008"), 218.815},
      {_D("1-Jul-2008"), 219.964},
      {_D("1-Aug-2008"), 219.086},
      {_D("1-Sep-2008"), 218.783},
      {_D("1-Oct-2008"), 216.573},
      {_D("1-Nov-2008"), 212.425},
      {_D("1-Dec-2008"), 210.228},
      {_D("1-Jan-2009"), 211.143},
      {_D("1-Feb-2009"), 212.193},
      {_D("1-Mar-2009"), 212.709},
      {_D("1-Apr-2009"), 213.24},
      {_D("1-May-2009"), 213.856},
      {_D("1-Jun-2009"), 215.693},
      {_D("1-Jul-2009"), 215.351},
      {_D("1-Aug-2009"), 215.834},
      {_D("1-Sep-2009"), 215.969},
      {_D("1-Oct-2009"), 216.177},
      {_D("1-Nov-2009"), 216.33},
      {_D("1-Dec-2009"), 215.949},
      {_D("1-Jan-2010"), 216.687},
      {_D("1-Feb-2010"), 216.741},
      {_D("1-Mar-2010"), 217.631},
      {_D("1-Apr-2010"), 218.009},
      {_D("1-May-2010"), 218.178},
      {_D("1-Jun-2010"), 217.965},
      {_D("1-Jul-2010"), 218.011},
      {_D("1-Aug-2010"), 218.312},
      {_D("1-Sep-2010"), 218.439},
      {_D("1-Oct-2010"), 218.711},
      {_D("1-Nov-2010"), 218.803},
      {_D("1-Dec-2010"), 219.179},
      {_D("1-Jan-2011"), 220.223},
      {_D("1-Feb-2011"), 221.309},
      {_D("1-Mar-2011"), 223.467},
      {_D("1-Apr-2011"), 224.906},
      {_D("1-May-2011"), 224.906},
      {_D("1-Jun-2011"), 224.906},
    };

    #endregion
  }
}
