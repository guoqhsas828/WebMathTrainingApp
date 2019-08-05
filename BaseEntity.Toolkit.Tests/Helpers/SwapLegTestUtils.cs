using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Helpers
{

  /// <exclude></exclude>
  internal class SwapLegTestUtils
  {
    /// <exclude></exclude>
    internal static double InitialInflation()
    {
      return 100;
    }

    /// <exclude></exclude>
    internal static InflationIndex GetCPIIndex(Dt asOf)
    {
      return new InflationIndex("CPI", Currency.None, DayCount.Actual360, Calendar.NYB,
                                BDConvention.Following, Frequency.Monthly, Tenor.Empty);
    }

    /// <exclude></exclude>
    internal static InflationIndex GetCPIIndex(Dt asOf, Tenor releaseLag)
    {
      return new InflationIndex("CPI", Currency.None, DayCount.Actual360, Calendar.NYB,
                                BDConvention.Following, Frequency.Monthly, releaseLag);
    }

    /// <exclude></exclude>
    internal static InflationCurve GetCPICurve(Dt asOf, DiscountCurve dc, InflationIndex index)
    {

      Dt effective = Dt.Add(asOf, 2);
      var inflationFactor = new InflationFactorCurve(asOf)
      {
        Calibrator = new InflationCurveFitCalibrator(asOf, asOf, dc, index, new CalibratorSettings())
      };
      var retVal = new InflationCurve(asOf, InitialInflation(), inflationFactor, null);
      for (int i = 1; i <= 10; ++i)
      {
        var fix = new SwapLeg(effective, Dt.Add(effective, i, TimeUnit.Years), Currency.None, 0.0,
                              DayCount.Thirty360, Frequency.None, BDConvention.Following, Calendar.NYB, false)
        {
          IsZeroCoupon = true,
          CompoundingFrequency = Frequency.Annual
        };
        var floating = new SwapLeg(effective, Dt.Add(effective, i, TimeUnit.Years), Frequency.None, 0.0, index,
                                   Currency.None, DayCount.Actual360, BDConvention.Following, Calendar.NYB);
        retVal.AddInflationSwap(new Swap(fix, floating), 0.05);
      }
      retVal.Fit();
      return retVal;
    }

    /// <exclude></exclude>
    internal static DiscountData GetDiscountData(Dt AsOf)
    {
      return new DiscountData
      {
        AsOf = AsOf.ToStr("%D"),
        Bootst = new DiscountData.Bootstrap
        {
          MmDayCount = DayCount.Actual360,
          MmTenors = new[] { "1M", "2M", "3M", "6M", "9M" },
          MmRates = new[] { 0.011, 0.012, 0.013, 0.016, 0.019 },
          SwapDayCount = DayCount.Actual360,
          SwapFrequency = Frequency.SemiAnnual,
          SwapInterp = InterpMethod.Cubic,
          SwapExtrap = ExtrapMethod.Const,
          // of fixed leg of swap
          SwapTenors =
                                new[] { "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "30Y" },
          SwapRates =
                                new[] { 0.022, 0.023, 0.024, 0.025, 0.026, 0.027, 0.028, 0.029, 0.030, 0.035, 0.04 }
        },
        Category = "Empty",
        // null works badly
        Name = "MyDiscountCurve" // ditto
      };
    }

    /// <exclude></exclude>
    internal static DiscountCurve GetTSYCurve(Dt asOf)
    {
      var dd = new DiscountData
      {
        AsOf = asOf.ToStr("%D"),
        Bootst = new DiscountData.Bootstrap
        {
          MmDayCount = DayCount.Thirty360,
          MmTenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "5Y", "7Y", "10Y", "30Y" },
          MmRates =
                                  new[]
                                    {
                                      0.00261, 0.00289, 0.00299, 0.00318, 0.00475, 0.00639, 0.00875, 0.01059, 0.01180,
                                      0.01358
                                    }
        },
        Category = "CMT",
        // null works badly
        Name = "TSYDiscountCurve" // ditto
      };

      return dd.GetDiscountCurve();
    }

    /// <exclude></exclude>
    internal static InterestRateIndex GetLiborIndex(string tenor)
    {
      return new InterestRateIndex("USDLIBOR" + tenor, Tenor.Parse(tenor), Currency.USD,
                                   DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
    }

    /// <exclude></exclude>
    internal static ReferenceIndex GetCMSIndex(string tenor)
    {
      var fwdIndex = new InterestRateIndex("ForwardRateIndex", new Tenor(Frequency.SemiAnnual),
                                                 Currency.USD, DayCount.Actual360,
                                                 Calendar.NYB, BDConvention.Following, 0);
      return new SwapRateIndex("CMS" + tenor, Tenor.Parse(tenor), Frequency.SemiAnnual, Currency.USD, DayCount.Actual360,
                               Calendar.NYB, BDConvention.Following, 2, fwdIndex);
    }

    /// <exclude></exclude>
    internal static ReferenceIndex GetCMTIndex(string tenor)
    {
      var fwdIndex = new InterestRateIndex("ForwardRateIndex", new Tenor(Frequency.SemiAnnual),
                                                 Currency.USD, DayCount.Thirty360,
                                                 Calendar.NYB, BDConvention.Following, 0);
      return new SwapRateIndex("CMT" + tenor, Tenor.Parse(tenor), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360,
                               Calendar.NYB, BDConvention.Following, 2, fwdIndex);
    }


    /// <exclude></exclude>
    internal static RateResets GetCPIResets(double initial, Dt asOf)
    {
      var rr = new RateResets();
      var rdt = new Dt(1, asOf.Month, asOf.Year);
      for (int i = 0; i < 100; i++)
      {
        rr.AllResets.Add(rdt, initial * (1.0 - i * 1e-3));
        rdt = Dt.Add(rdt, -1, TimeUnit.Months);
      }
      return rr;
    }


    /// <exclude></exclude>
    internal static RateResets GetHistoricalResets(Dt asOf, Frequency freq, CycleRule rule, Calendar calendar, string tenor)
    {
      List<RateReset> rateResets = new List<RateReset>();
      Dt past = Dt.Add(asOf, -5, TimeUnit.Years);
      if (freq == Frequency.Weekly)
        past = Dt.AddWeeks(past, -1, rule);
      else if (freq == Frequency.Monthly)
        past = Dt.AddMonths(past, -1, rule);
      else
        past = Dt.AddDays(past, -1, calendar);
      Dt rd = past;
      int i = 0;
      while (rd < asOf)
      {
        ++i;
        rateResets.Add(new RateReset(rd, 0.025 + (i % 10) * 0.001));
        rd = (freq == Frequency.Weekly)
               ? Dt.AddWeeks(rd, 1, rule)
               : (freq == Frequency.Monthly) ? Dt.AddMonths(rd, 1, rule) : Dt.AddDays(rd, 1, calendar);
      }
      RateResets rr = new RateResets(rateResets);
      return rr;
    }


    /// <exclude></exclude>
    internal static RateModelParameters GetBGMRateModelParameters(Dt asOf, Tenor tenor)
    {
      var sigma = new Curve(asOf);
      sigma.Add(Dt.Add(asOf, 365), 0.25);
      sigma.Add(Dt.Add(asOf, 2 * 365), 0.20);
      return new RateModelParameters(RateModelParameters.Model.BGM, new[] { RateModelParameters.Param.Sigma },
                                     new[] { sigma },
                                     tenor, Currency.USD);
    }

    /// <exclude></exclude>
    internal static RateModelParameters GetSABRRateModelParameters(Dt asOf, Tenor tenor)
    {
      var sigma = new Curve(asOf);
      sigma.Add(Dt.Add(asOf, 365), 0.25);
      sigma.Add(Dt.Add(asOf, 2 * 365), 0.20);
      var beta = new Curve(asOf, 0.5);
      var alpha = new Curve(asOf, 0.2);
      var rho = new Curve(asOf, 0.8);
      var rateModelParameters = new RateModelParameters(RateModelParameters.Model.SABR,
                                                        new[]
                                                          {
                                                            RateModelParameters.Param.Nu,
                                                            RateModelParameters.Param.Alpha
                                                            , RateModelParameters.Param.Beta,
                                                            RateModelParameters.Param.Rho
                                                          },
                                                        new[] { sigma, alpha, beta, rho }, new Tenor(6, TimeUnit.Months),
                                                        Currency.USD);
      return rateModelParameters;
    }

    /// <exclude></exclude>
    internal static RateModelParameters GetSABRBGMRateModelParameters(Dt asOf, Tenor tenor)
    {
      var sigma = new Curve(asOf);
      sigma.Add(Dt.Add(asOf, 365), 0.25);
      sigma.Add(Dt.Add(asOf, 2 * 365), 0.20);
      var beta = new Curve(asOf, 0.5);
      var alpha = new Curve(asOf, 0.2);
      var rho = new Curve(asOf, 0.8);
      var ppar = new IModelParameter[] { alpha, beta, sigma, rho };
      var fpar = new IModelParameter[] { sigma };
      var ppN = new[]
                  {
                    RateModelParameters.Param.Alpha,
                    RateModelParameters.Param.Beta,
                    RateModelParameters.Param.Nu,
                    RateModelParameters.Param.Rho
                  };
      RateModelParameters.Param[] fpN = new RateModelParameters.Param[] { RateModelParameters.Param.Sigma };
      var fundingPar = new RateModelParameters(RateModelParameters.Model.BGM, fpN, fpar, new Tenor(Frequency.Quarterly),
                                               Currency.USD);

      var fwdparameters =
        new RateModelParameters(fundingPar, RateModelParameters.Model.SABR, ppN, ppar, new Curve(asOf, 0.75),
                                new Tenor(Frequency.SemiAnnual));
      return fwdparameters;
    }

    /// <exclude></exclude>
    internal static InflationBondPricer GetInflationBondPricer(Dt asOf, DiscountCurve dc, CalibratedCurve referenceCurve, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      InflationBond bond = new InflationBond(effective, Dt.Add(effective, "5Y"), Currency.None, BondType.None, spread,
                                             DayCount.Actual360, CycleRule.None, Frequency.Quarterly,
                                             BDConvention.Following, Calendar.NYB,
                                             (InflationIndex)referenceIndex, InitialInflation(), Tenor.Parse("3M"));
      var pars = GetSABRBGMRateModelParameters(asOf, Tenor.Parse("3M"));
      var pricer = new InflationBondPricer(bond, asOf, asOf, 1e8 * sign, dc, null, (InflationCurve)referenceCurve, null,
                                           pars);
      return pricer;

    }

    internal static InflationBondPricer GetOffTheRunInflationBondPricer(Dt asOf, DiscountCurve dc, CalibratedCurve referenceCurve, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, -1, TimeUnit.Months);
      var bond = new InflationBond(effective, Dt.Add(effective, "5Y"), Currency.None, BondType.None, spread,
                                             DayCount.Actual360, CycleRule.None, Frequency.Quarterly,
                                             BDConvention.Following, Calendar.NYB, (InflationIndex)referenceIndex, InitialInflation(), Tenor.Parse("3M"));
      var pars = GetSABRBGMRateModelParameters(asOf, Tenor.Parse("3M"));
      var pricer = new InflationBondPricer(bond, asOf, asOf, 1e8 * sign, dc, null, (InflationCurve)referenceCurve, null, pars);
      return pricer;

    }


    /// <exclude></exclude>
    internal static SwapLegPricer GetFixedSwapPricer(Dt asOf, DiscountCurve dc, double coupon, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Currency.None, coupon, DayCount.Thirty360,
                            Frequency.SemiAnnual, BDConvention.Modified, Calendar.NYB, false);
      return new SwapLegPricer(sl2, asOf, asOf, sign * 1e8, dc, null, null, null, null, null);
    }

    /// <exclude></exclude>
    internal static SwapLegPricer GetFloatingSwapPricer(Dt asOf, DiscountCurve dc, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      Frequency freq = Frequency.Quarterly;
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Frequency.Quarterly, spread, referenceIndex,
                            Currency.None, DayCount.Actual360, BDConvention.Following, Calendar.NYB);
      RateModelParameters rateModelParameters = GetBGMRateModelParameters(asOf, new Tenor(freq));
      return new SwapLegPricer(sl2, asOf, asOf, sign * 1e8, dc, referenceIndex, dc, new RateResets(0.0, 0.0),
                               rateModelParameters, null);
    }

    /// <exclude></exclude>
    internal static SwapLegPricer GetFloatingSwapPricer(Dt asOf, DiscountCurve dc, CalibratedCurve referenceCurve, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      Frequency freq = Frequency.Quarterly;
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Frequency.Quarterly, spread, referenceIndex,
                            Currency.None, DayCount.Actual360, BDConvention.Following, Calendar.NYB);
      RateModelParameters rateModelParameters = GetBGMRateModelParameters(asOf, new Tenor(freq));
      return new SwapLegPricer(sl2, asOf, asOf, sign * 1e8, dc, referenceIndex, referenceCurve, new RateResets(0.0, 0.0), rateModelParameters, null);
    }

    /// <exclude></exclude>
    internal static SwapLegPricer GetFixedSwapPricerNotionalSchedule(Dt asOf, DiscountCurve dc, double coupon, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Currency.None, coupon, DayCount.Thirty360,
                            Frequency.SemiAnnual, BDConvention.Modified, Calendar.NYB, false)
      {
        InitialExchange = true,
        IntermediateExchange = true,
        FinalExchange = true
      };
      var pricer = new SwapLegPricer(sl2, asOf, asOf, sign * 1e8, dc, null, null, null, null, null);
      var ps = pricer.GetPaymentSchedule(null, effective);
      var ntl = sl2.Notional;
      sl2.AmortizationSchedule.Add(new Amortization(effective, AmortizationType.RemainingNotionalLevels, ntl));
      int i = 2;
      foreach (var d in ps.GetPaymentDates().Where(d => d != effective))
      {
        sl2.AmortizationSchedule.Add(new Amortization(d, AmortizationType.RemainingNotionalLevels,
                                                      Math.Pow(-1.0, i - 1) * ntl * i++));
      }
      sl2.AmortizationSchedule.Remove(sl2.AmortizationSchedule.Last());
      return pricer;
    }
  }


}
