//
//
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{

  [TestFixture]
  public class TestCurveShiftScenario
  {

    [TestCase(ScenarioShiftType.Absolute, true)]
    [TestCase(ScenarioShiftType.Absolute, false)]
   // [TestCase(ScenarioShiftType.Specified, true)]
   // [TestCase(ScenarioShiftType.Specified, false)]
    [TestCase(ScenarioShiftType.Relative, true)]
    [TestCase(ScenarioShiftType.Relative, false)]
    public void TestScenarioShiftInflationCurve(ScenarioShiftType shiftType,
      bool fitDependent)
    {
      var curve = GetInflationCurve(_asOf) as CalibratedCurve;
      var pricer = GetSwapPricer(_asOf, _effective);
      var shiftValue = GetShiftValue(shiftType);
      var curveShift = new ScenarioShiftCurves(new[] {curve}, new[] {shiftValue},
        shiftType, fitDependent);
      var dt1 = Scenarios.CalcScenario(new[] {pricer}, new[] {"Pv"}, 
        new[] {curveShift}, false, true, null);
      var value1 = (double) (dt1.Rows[0])["Scenario"];
      var delta1 = (double) (dt1.Rows[0])["Delta"];

      //Test everything is restored correctly
      var dt2 = Scenarios.CalcScenario(new[] { pricer }, new[] { "Pv" }, 
        new[] { curveShift }, false, true, null);
      var value2 = (double)(dt2.Rows[0])["Scenario"];
      var delta2 = (double)(dt2.Rows[0])["Delta"];
      Assert.AreEqual(value1, value2, 1E-14);
      Assert.AreEqual(delta1, delta2, 1E-14);
    }


    private static double GetShiftValue(ScenarioShiftType shiftType)
    {
      switch (shiftType)
      {
        case ScenarioShiftType.Relative:
          return 0.05;
        case ScenarioShiftType.Absolute:
          return 5.0;
        case ScenarioShiftType.Specified:
          return 300.0;
        default:
          throw new ArgumentException("The invalid shiftType");
      }
    }


    private static SwapLeg GetInflationSwapLeg(Dt effective, Dt maturity, 
      double coupon, bool floating)
    {
      Currency ccy = Currency.GBP;
      DayCount dayCount = DayCount.ActualActual;
      Calendar calendar = Calendar.LNB;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.Modified;
     

      var iSwapLeg = floating
        ? new InflationSwapLeg(effective, maturity, ccy, 0.0, dayCount, freq,
          roll, calendar, false)
        {
          IndexationMethod = IndexationMethod.UKGilt_OldStyle,
          ProjectionType = ProjectionType.InflationRate
        }
        : new SwapLeg(effective, maturity, ccy, coupon, dayCount, freq, roll,
          calendar, false);

      iSwapLeg.Validate();
      return iSwapLeg;
    }


    private static InflationCurve GetInflationCurve(Dt asOf)
    {
      const double spotInflation = 258.8;
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var iIndex = GetInflationIndex();
      var inflFactorCurve = new InflationFactorCurve(asOf)
      {
        Calibrator = new DiscountRateCalibrator(asOf, asOf)
      };

      var inflationCurve = new InflationCurve(asOf, spotInflation, inflFactorCurve, null)
      {
        Calibrator = new InflationCurveFitCalibrator(asOf, asOf, discountCurve, 
        iIndex, null)
      };

      var mktQuotes = new double[15];
      for (int i = 0; i < 15; i++)
        mktQuotes[i] = 0.04 + 0.005 * i;

      for (int i = 0; i < mktQuotes.Length; i++)
      {
        var mat = Dt.Add(asOf, 1 + i, TimeUnit.Years);
        var fixedLeg = new SwapLeg(asOf, mat, iIndex.Currency, 0.05, iIndex.DayCount,
          Frequency.None, iIndex.Roll, iIndex.Calendar, false);
        var floatLeg = new SwapLeg(asOf, mat, iIndex.Currency, 0, iIndex.DayCount,
          Frequency.None, iIndex.Roll, iIndex.Calendar, false,
          new Tenor(Frequency.SemiAnnual), "CPI")
        {ProjectionType = ProjectionType.InflationRate};
        fixedLeg.IsZeroCoupon = true;
        floatLeg.IsZeroCoupon = true;
        floatLeg.ResetLag = Tenor.Parse("2m");
        var swap = new Swap(floatLeg, fixedLeg);
        inflationCurve.AddInflationSwap(swap, mktQuotes[i]);
      }
      inflationCurve.Fit();
      PostProcess(inflationCurve);
      return inflationCurve;
    }


    private static void PostProcess(CalibratedCurve curve)
    {
      var target = ((InflationCurve)curve).TargetCurve;
      if (target == null)
        return;

      bool inverse = ((DiscountRateCalibrator)target.Calibrator).Inverse;
      target.Tenors = new CurveTenorCollection();
      var asOf = target.AsOf;
      const DayCount dc = DayCount.Actual365Fixed;
      const Frequency freq = Frequency.Continuous;
      for (int i = 0, count = target.Count; i < count; ++i)
      {
        var maturity = target.GetDt(i);
        var df = target.GetVal(i);
        var yield = RateCalc.RateFromPrice(inverse ? (1 / df) : df, asOf, 
          maturity, dc, freq);
        var note = new Note(asOf, maturity,
          target.Ccy, yield, dc, freq, BDConvention.None, Calendar.None);
        note.Validate();
        var tenor = target.Add(note, 0.0, 1.0, 0.0, 0.0);
        var name = GetMatchedTenorName(i, maturity, curve.Tenors);
        if (name != null) tenor.Name = name;
      }
      return;
    }

    private static string GetMatchedTenorName(int idx, Dt curveDate,
       CurveTenorCollection tenors)
    {
      int count = tenors.Count;
      for (int i = idx; i < count; ++i)
        if (tenors[i].CurveDate == curveDate) return tenors[i].Name;
      for (int i = 0; i < idx; ++i)
        if (tenors[i].CurveDate == curveDate) return tenors[i].Name;
      return null;
    }

    private static SwapPricer GetSwapPricer(Dt asOf, Dt settle)
    {
      double coupon = 0.00125;
      var fixedSwapLeg = GetInflationSwapLeg(_effective, _maturity, coupon, false);
      var floatingSwapLeg = GetInflationSwapLeg(_effective, _maturity, coupon, true);
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var inflationCurve = GetInflationCurve(asOf);

      var fixedSwapLegPricer = new SwapLegPricer(fixedSwapLeg, asOf, settle, 1.0,
        discountCurve, inflationCurve.InflationIndex, discountCurve,
        HisRateResets, null, null);

      var floatingSwapLegPricer = new SwapLegPricer(floatingSwapLeg, asOf, settle, -1.0,
        discountCurve, inflationCurve.InflationIndex, inflationCurve,
        HisRateResets, null, null);

      var swapPricer = new SwapPricer(floatingSwapLegPricer, fixedSwapLegPricer);
      swapPricer.Validate();

      return swapPricer;
    }

    private static Dt _asOf = new Dt(20150818);
    private static Dt _effective = new Dt(20150818);
    private static Dt _maturity = new Dt(20250818);


    private static InflationIndex GetInflationIndex()
    {
      return new InflationIndex("RPIGBP_INDEX", Currency.GBP,
        DayCount.ActualActual, Calendar.LNB,
        BDConvention.Modified, Frequency.Monthly, Tenor.Empty)
      {
        HistoricalObservations = HisRateResets
      };
    }

    private static Dt _ToDt(string input)
    {
      return input.ParseDt();
    }

    private static readonly RateResets HisRateResets = new RateResets
    {
      {_ToDt("1-Jul-09"), 213.40},
      {_ToDt("1-Aug-09"), 214.40},
      {_ToDt("1-Sep-09"), 215.30},
      {_ToDt("1-Oct-09"), 216.00},
      {_ToDt("1-Nov-09"), 216.60},
      {_ToDt("1-Dec-09"), 218.00},
      {_ToDt("1-Jan-10"), 217.90},
      {_ToDt("1-Feb-10"), 219.20},
      {_ToDt("1-Mar-10"), 220.70},
      {_ToDt("1-Apr-10"), 222.80},
      {_ToDt("1-May-10"), 223.60},
      {_ToDt("1-Jun-10"), 224.10},
      {_ToDt("1-Jul-10"), 223.60},
      {_ToDt("1-Aug-10"), 224.50},
      {_ToDt("1-Sep-10"), 225.30},
      {_ToDt("1-Oct-10"), 225.80},
      {_ToDt("1-Nov-10"), 226.80},
      {_ToDt("1-Dec-10"), 228.40},
      {_ToDt("1-Jan-11"), 229.00},
      {_ToDt("1-Feb-11"), 231.30},
      {_ToDt("1-Mar-11"), 232.50},
      {_ToDt("1-Apr-11"), 234.40},
      {_ToDt("1-May-11"), 235.20},
      {_ToDt("1-Jun-11"), 235.20},
      {_ToDt("1-Jul-11"), 234.70},
      {_ToDt("1-Aug-11"), 236.10},
      {_ToDt("1-Sep-11"), 237.90},
      {_ToDt("1-Oct-11"), 238.00},
      {_ToDt("1-Nov-11"), 238.50},
      {_ToDt("1-Dec-11"), 239.40},
      {_ToDt("1-Jan-12"), 238.00},
      {_ToDt("1-Feb-12"), 239.90},
      {_ToDt("1-Mar-12"), 240.80},
      {_ToDt("1-Apr-12"), 242.50},
      {_ToDt("1-May-12"), 242.40},
      {_ToDt("1-Jun-12"), 241.80},
      {_ToDt("1-Jul-12"), 242.10},
      {_ToDt("1-Aug-12"), 243.00},
      {_ToDt("1-Sep-12"), 244.20},
      {_ToDt("1-Oct-12"), 245.60},
      {_ToDt("1-Nov-12"), 245.60},
      {_ToDt("1-Dec-12"), 246.80},
      {_ToDt("1-Jan-13"), 245.80},
      {_ToDt("1-Feb-13"), 247.60},
      {_ToDt("1-Mar-13"), 248.70},
      {_ToDt("1-Apr-13"), 249.50},
      {_ToDt("1-May-13"), 250.00},
      {_ToDt("1-Jun-13"), 249.70},
      {_ToDt("1-Jul-13"), 249.70},
      {_ToDt("1-Aug-13"), 251.00},
      {_ToDt("1-Sep-13"), 251.90},
      {_ToDt("1-Oct-13"), 251.90},
      {_ToDt("1-Nov-13"), 252.10},
      {_ToDt("1-Dec-13"), 253.40},
      {_ToDt("1-Jan-14"), 252.60},
      {_ToDt("1-Feb-14"), 254.20},
      {_ToDt("1-Mar-14"), 254.80},
      {_ToDt("1-Apr-14"), 255.70},
      {_ToDt("1-May-14"), 255.90},
      {_ToDt("1-Jun-14"), 256.30},
      {_ToDt("1-Jul-14"), 256.00},
      {_ToDt("1-Aug-14"), 257.00},
      {_ToDt("1-Sep-14"), 257.60},
      {_ToDt("1-Oct-14"), 257.70},
      {_ToDt("1-Nov-14"), 257.10},
      {_ToDt("1-Dec-14"), 257.50},
      {_ToDt("1-Jan-15"), 255.40},
      {_ToDt("1-Feb-15"), 256.70},
      {_ToDt("1-Mar-15"), 257.10},
      {_ToDt("1-Apr-15"), 258.00},
      {_ToDt("1-May-15"), 258.50},
      {_ToDt("1-Jun-15"), 258.90},
      {_ToDt("1-Jul-15"), 258.60},
    };
  }
}

