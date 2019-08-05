/*
 * SensitivityTest.cs
 *
 * Base class for all sensitivity tests
 *
 * 
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Shared;

using NUnit.Framework;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Helpers.Legacy
{
  public class SensitivityTest : ToolkitTestBase
  {
    public SensitivityTest()
    { }

    public SensitivityTest(string fixtureName)
      : base(fixtureName)
    {}

    #region SummaryRiskMethods
    /// <summary>
    ///   Calculate spread deltas
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Spread01(IPricer[] pricers, IList<string> labels, params bool[] rescaleStrikes)
    {
      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      double upBump = 0.4;
      double downBump = 0.0;
      object[] pars = Parsers.Parse(new Type[] { typeof(double), typeof(double) }, Spread01Param);
      if (pars != null)
      {
        upBump = (double)pars[0];
        downBump = (double)pars[1];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        double[] values = new double[pricers.Length];
        for (int i = 0; i < pricers.Length; ++i)
        {
          timer.Resume();
          double v = CalcSpread01(pricers[i], SensitivityPriceMeasure, upBump, downBump, rescaleStrikes);
          timer.Stop();
          values[i] = v;
        }
        MatchExpects(ToResultData(values, labels, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate spread gammas
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void SpreadGamma(IPricer[] pricers, string[] labels, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      double upBump = 1.0;
      double downBump = 1.0;
      object[] pars = Parsers.Parse(new Type[] { typeof(double), typeof(double) }, SpreadGammaParam);
      if (pars != null)
      {
        upBump = (double)pars[0];
        downBump = (double)pars[1];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        double[] values = new double[pricers.Length];
        for (int i = 0; i < pricers.Length; ++i)
        {
          timer.Resume();
          double v = CalcSpreadGamma(pricers[i], SensitivityPriceMeasure, upBump, downBump, rescaleStrikes);
          timer.Stop();
          values[i] = v;
        }
        MatchExpects(ToResultData(values, labels, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate spread hedges
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void SpreadHedge(IPricer[] pricers, string[] labels, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      string tenor = "5 Year";
      double upBump = 1.0;
      double downBump = 1.0;
      object[] pars = Parsers.Parse(new Type[] { typeof(string), typeof(double), typeof(double) },
        SpreadHedgeParam);
      if (pars != null)
      {
        tenor = (string)pars[0];
        upBump = (double)pars[1];
        downBump = (double)pars[2];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        double[] values = new double[pricers.Length];
        for (int i = 0; i < pricers.Length; ++i)
        {
          timer.Resume();
          double v = CalcSpreadHedge(pricers[i], SensitivityPriceMeasure, tenor, upBump, downBump, rescaleStrikes);
          timer.Stop();
          values[i] = v;
        }

        MatchExpects(ToResultData(values, labels, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate IR01
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void IR01(IPricer[] pricers, string[] labels, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      double upBump = 4.0;
      double downBump = 0.0;
      bool recalibrate = true;
      object[] pars = Parsers.Parse(new Type[] { typeof(double), typeof(double), typeof(bool) }, IR01Param);
      if (pars != null)
      {
        upBump = (double)pars[0];
        downBump = (double)pars[1];
        recalibrate = (bool)pars[2];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        double[] values = new double[pricers.Length];
        for (int i = 0; i < pricers.Length; ++i)
        {
          timer.Resume();
          double v = CalcRate01(pricers[i], SensitivityPriceMeasure, upBump, downBump, recalibrate, rescaleStrikes);
          timer.Stop();
          values[i] = v;
        }

        MatchExpects(ToResultData(values, labels, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate Recovery01
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Recovery01(IPricer[] pricers, string[] labels, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      double upBump = 0.01;
      double downBump = 0.0;
      bool recalibrate = true;
      object[] pars = Parsers.Parse(new Type[] { typeof(double), typeof(double), typeof(bool) }, RecoveryParam);
      if (pars != null)
      {
        upBump = (double)pars[0];
        downBump = (double)pars[1];
        recalibrate = (bool)pars[2];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        double[] values = new double[pricers.Length];
        for (int i = 0; i < pricers.Length; ++i)
        {
          timer.Resume();
          double v = Toolkit.Sensitivity.Sensitivities.Recovery01(pricers[i], SensitivityPriceMeasure, upBump, downBump, recalibrate, rescaleStrikes);
          timer.Stop();
          values[i] = v;
        }
        MatchExpects(ToResultData(values, labels, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate VOD (value on default)
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void VOD(IPricer[] pricers, string[] labels, params bool[] rescaleStrikes)
    {

      using (new CheckStates(CheckPricerStates, pricers))
      {
        if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
          throw new ArgumentException("pricers and rescaleStrikes must have same length");

        Timer timer = new Timer();
        double[] values = new double[pricers.Length];
        for (int i = 0; i < pricers.Length; ++i)
        {
          timer.Resume();
          double v = Toolkit.Sensitivity.Sensitivities.VOD(pricers[i], SensitivityPriceMeasure, rescaleStrikes);
          timer.Stop();
          values[i] = v;
        }
        MatchExpects(ToResultData(values, labels, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate theta
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Theta(IPricer[] pricers, string[] labels, params bool[] rescaleStrikes)
    {

      using (new CheckStates(CheckPricerStates, pricers))
      {
        if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
          throw new ArgumentException("pricers and rescaleStrikes must have same length");
        Dt toAsOf = Dt.Add(pricers[0].AsOf, ThetaPeriod);
        Dt toSettle = Dt.Add(pricers[0].Settle, ThetaPeriod);
        bool cleanPrice = false;
        object[] pars = Parsers.Parse(new Type[] { typeof(Dt), typeof(Dt), typeof(bool) }, ThetaParam);
        if (pars != null)
        {
          toAsOf = (Dt)pars[0];
          toSettle = (Dt)pars[1];
          cleanPrice = (bool)pars[2];
        }

        Timer timer = new Timer();
        timer.Resume();
        ThetaFlags flag = ThetaFlags.None;
        if (cleanPrice) flag = flag | ThetaFlags.Clean;
        SensitivityRescaleStrikes[] rs = new SensitivityRescaleStrikes[pricers.Length];
        if (rescaleStrikes == null || rescaleStrikes.Length == 0)
          for (int i = 0; i < pricers.Length; i++) rs[i] = SensitivityRescaleStrikes.UsePricerSetting;
        else
          for (int i = 0; i < rescaleStrikes.Length; i++)
            rs[i] = rescaleStrikes[i] ? SensitivityRescaleStrikes.Yes : SensitivityRescaleStrikes.No;
        double[] values = Sensitivities.Theta(pricers, SensitivityPriceMeasure, toAsOf, toSettle, flag, rs);
        timer.Stop();
        MatchExpects(ToResultData(values, labels, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate Base correlation skew delta
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="calcAsPv">Calculate delta as Pv changes</param>
    /// <returns>ResultData</returns>
    public void BaseCorrelationSkewDelta(IPricer[] pricers, string[] labels, bool calcAsPv)
    {
      using (new CheckStates(CheckPricerStates, pricers))
      {
        TestNumeric(pricers, labels,
          delegate(object p)
          {
            if (!(p is SyntheticCDOPricer))
              return 0.0;
            SyntheticCDOPricer pricer = (SyntheticCDOPricer)p;
            if (calcAsPv)
            {
              return -pricer.Notional * BaseCorrelationBasketPricer.BaseCorrelationDelta(
                pricer, 0.0, 0.01, false, true, true);
            }
            else
            {
              double result = BaseCorrelationBasketPricer.BaseCorrelationDelta(
                pricer, 0.01, false, false);
              // Convert to bp if required
              if (Math.Abs(pricer.CDO.Fee) <= 1.0E-7)
                result *= 10000;
              return result;
            }
          });
      }
    }

    /// <summary>
    ///   Calculate Base correlation level delta
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="labels">Labels to identify pricers</param>
    /// <param name="calcAsPv">Calculate delta as Pv changes</param>
    /// <returns>ResultData</returns>
    public void BaseCorrelationLevelDelta(IPricer[] pricers, string[] labels, bool calcAsPv)
    {
      using (new CheckStates(CheckPricerStates, pricers))
      {
        TestNumeric(pricers, labels,
          delegate(object p)
          {
            if (!(p is SyntheticCDOPricer))
              return 0.0;
            SyntheticCDOPricer pricer = (SyntheticCDOPricer)p;
            if (calcAsPv)
            {
              return -pricer.Notional * BaseCorrelationBasketPricer.BaseCorrelationDelta(
                pricer, 0.01, 0.01, false, true, true);
            }
            else
            {
              double result = BaseCorrelationBasketPricer.BaseCorrelationDelta(
                pricer, 0.01, false, true);
              // Convert to bp if required
              if (Math.Abs(pricer.CDO.Fee) <= 1.0E-7)
                result *= 10000;
              return result;
            }
          });
      }
    }
    #endregion // SummaryRiskMethods

    #region RiskMethods
    /// <summary>
    ///   Calculate spread sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Spread(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      string[] bumpTenors = SpreadSensitivityBumpTenors;

      double initialBump = 0.0;
      double upBump = 1.0;
      double downBump = 1.0;
      bool bumpRelative = false;
      bool scaledDelta = true;
      BumpType bumpType = BumpType.Parallel;
      bool calcGamma = true;
      bool calcHedge = true;
      string hedgeTenor = "5 Year";
      object[] pars = Parsers.Parse(new Type[] {
        typeof(double), typeof(double), typeof(double),
        typeof(bool),  typeof(bool),
        typeof(BumpType),typeof(bool), typeof(bool),
        typeof(string)}, SpreadSensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) initialBump = (double)pars[0];
        if (pars[1] != null) upBump = (double)pars[1];
        if (pars[2] != null) downBump = (double)pars[2];
        if (pars[3] != null) bumpRelative = (bool)pars[3];
        if (pars[4] != null) scaledDelta = (bool)pars[4];
        if (pars[5] != null) bumpType = (BumpType)pars[5];
        if (pars[6] != null) calcGamma = (bool)pars[6];
        if (pars[7] != null) calcHedge = (bool)pars[7];
        if (pars[8] != null) hedgeTenor = (string)pars[8];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        System.Data.DataTable table;
        timer.Resume();
        table = InvokeSensitivitySpread(pricers, SensitivityPriceMeasure,
          initialBump, upBump, downBump, bumpRelative, scaledDelta, bumpType,
          bumpTenors, calcGamma, calcHedge, hedgeTenor, null, rescaleStrikes);
        timer.Stop();
        MatchExpects(ToResultData(table, calcGamma, calcHedge, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate spread sensitivity by the semi-analytic algorithm
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void SpreadSemiAnalytic(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      string[] bumpTenors = null;
      string measure = "";
      double initialBump = 0.0;
      double upBump = 1.0;
      double downBump = 1.0;
      bool bumpRelative = false;
      bool scaledDelta = true;
      BumpType bumpType = BumpType.Parallel;
      bool calcGamma = true;
      bool calcHedge = true;
      string hedgeTenor = "5 Year";
      object[] pars = Parsers.Parse(new Type[] {
        typeof(double), typeof(double), typeof(double),
        typeof(bool),  typeof(bool),
        typeof(BumpType),typeof(bool), typeof(bool),
        typeof(string)}, SpreadSensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) initialBump = (double)pars[0];
        if (pars[1] != null) upBump = (double)pars[1];
        if (pars[2] != null) downBump = (double)pars[2];
        if (pars[3] != null) bumpRelative = (bool)pars[3];
        if (pars[4] != null) scaledDelta = (bool)pars[4];
        if (pars[5] != null) bumpType = (BumpType)pars[5];
        if (pars[6] != null) calcGamma = (bool)pars[6];
        if (pars[7] != null) calcHedge = (bool)pars[7];
        if (pars[8] != null) hedgeTenor = (string)pars[8];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        System.Data.DataTable table;
        timer.Resume();
        table = Toolkit.Sensitivity.Sensitivities.Spread(pricers, measure, QuotingConvention.None,
          initialBump, upBump, downBump, bumpRelative, scaledDelta, bumpType,
          bumpTenors, calcGamma, calcHedge, hedgeTenor, SensitivityMethod.SemiAnalytic, null, rescaleStrikes);
        timer.Stop();
        MatchExpects(ToResultData(table, calcGamma, calcHedge, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate interest rate sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Rate(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      var table = RateTable(pricers, RateSensitivityParam, false, null, rescaleStrikes);
      MatchExpects(ToResultData(table));
    }

    /// <summary>
    ///   Calculate discount rate sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Discount(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      var table = RateTable(pricers, DiscountSensitivityParam, true, null, rescaleStrikes);
      MatchExpects(ToResultData(table));
    }

    internal DataTable RateTable(IPricer[] pricers,
      string parameters, bool discountSensitivty, string fitMethod,
      params bool[] rescaleStrikes)
    {
      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      string[] bumpTenors = RateSensitivityBumpTenors;

      double initialBump = 0.0;
      double upBump = 4.0;
      double downBump = 0.0;
      bool bumpRelative = false;
      bool scaledDelta = true;
      BumpType bumpType = BumpType.Parallel;
      bool calcGamma = true;
      bool calcHedge = true;
      string hedgeTenor = "5 Year";
      bool recalibrate = false;
      object[] pars = Parsers.Parse(new Type[] {
        typeof (double), typeof (double), typeof (double),
        typeof (bool), typeof (bool),
        typeof (BumpType), typeof (bool), typeof (bool),
        typeof (string), typeof (bool)}, parameters ?? RateSensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) initialBump = (double)pars[0];
        if (pars[1] != null) upBump = (double)pars[1];
        if (pars[2] != null) downBump = (double)pars[2];
        if (pars[3] != null) bumpRelative = (bool)pars[3];
        if (pars[4] != null) scaledDelta = (bool)pars[4];
        if (pars[5] != null) bumpType = (BumpType)pars[5];
        if (pars[6] != null) calcGamma = (bool)pars[6];
        if (pars[7] != null) calcHedge = (bool)pars[7];
        if (pars[8] != null) hedgeTenor = (string)pars[8];
        if (pars[9] != null) recalibrate = (bool)pars[9];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        System.Data.DataTable table;
        timer.Resume();
        if (UseSensitivities2)
        {
          var flags = BumpFlags.BumpInPlace;
          if (recalibrate) flags |= BumpFlags.RecalibrateSurvival;
          if (rescaleStrikes != null && rescaleStrikes.FirstOrDefault())
            flags |= BumpFlags.RemapCorrelations;
          table = Sensitivities2.Calculate(pricers, SensitivityPriceMeasure, null,
            BumpTarget.InterestRates, upBump, downBump, bumpType, flags,
            bumpTenors, scaledDelta, calcGamma,
            hedgeTenor, calcHedge, UseCache, null);
        }
        else if (discountSensitivty)
        {
          table = Sensitivities.Discount(pricers, SensitivityPriceMeasure,
            initialBump, upBump, downBump, bumpRelative,
            scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor,
            recalibrate, SensitivityMethod.FiniteDifference, null,
            null, fitMethod, null, rescaleStrikes);
        }
        else
        {
          table = Sensitivities.Rate(pricers, SensitivityPriceMeasure,
            initialBump, upBump, downBump, bumpRelative,
            scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor,
            recalibrate, null, null, fitMethod, rescaleStrikes);
        }
        timer.Stop();
        table.ExtendedProperties.Add("Elapsed", timer.Elapsed);
        return table;
      }
    }

    /// <summary>
    ///   Calculate interest rate sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Reference(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      string[] bumpTenors = null;

      double initialBump = 0.0;
      double upBump = 4.0;
      double downBump = 0.0;
      bool bumpRelative = false;
      bool scaledDelta = true;
      BumpType bumpType = BumpType.Parallel;
      bool calcGamma = true;
      bool calcHedge = true;
      string hedgeTenor = "5 Year";
      bool recalibrate = false;
      object[] pars = Parsers.Parse(new Type[] {
        typeof(double), typeof(double), typeof(double),
        typeof(bool),  typeof(bool),
        typeof(BumpType),typeof(bool), typeof(bool),
        typeof(string), typeof(bool)}, RateSensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) initialBump = (double)pars[0];
        if (pars[1] != null) upBump = (double)pars[1];
        if (pars[2] != null) downBump = (double)pars[2];
        if (pars[3] != null) bumpRelative = (bool)pars[3];
        if (pars[4] != null) scaledDelta = (bool)pars[4];
        if (pars[5] != null) bumpType = (BumpType)pars[5];
        if (pars[6] != null) calcGamma = (bool)pars[6];
        if (pars[7] != null) calcHedge = (bool)pars[7];
        if (pars[8] != null) hedgeTenor = (string)pars[8];
        if (pars[9] != null) recalibrate = (bool)pars[9];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        System.Data.DataTable table;
        timer.Resume();
        table = Sensitivities.Reference(pricers, SensitivityPriceMeasure,
          initialBump, upBump, downBump, bumpRelative,
          scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor,
          recalibrate, SensitivityMethod.FiniteDifference, null);
        timer.Stop();
        MatchExpects(ToResultData(table, calcGamma, calcHedge, timer.Elapsed));
      }
    }


    /// <summary>
    ///   Calculate default sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Default(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      bool calcHedge = true;
      string hedgeTenor = "5 Year";
      object[] pars = Parsers.Parse(new Type[] { typeof(bool), typeof(string) }, DefaultSensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) calcHedge = (bool)pars[0];
        if (pars[1] != null) hedgeTenor = (string)pars[1];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        timer.Resume();
        DataTable table = InvokeSensitivityDefault(pricers, SensitivityPriceMeasure, calcHedge, hedgeTenor, null, rescaleStrikes);
        timer.Stop();
        MatchExpects(ToResultData(table, false, calcHedge, timer.Elapsed));
      }
    }

    internal bool SemiAnalyticDerProvider(IPricer[] pricers)
    {
      bool retVal = true;
      for (int i = 0; i < pricers.Length; i++)
      {
        var p = pricers[i] as IAnalyticDerivativesProvider;
        if (p.HasAnalyticDerivatives == false)
        {
          retVal = false;
          break;
        }
      }
      return retVal;
    }

    /// <summary>
    ///   Calculate default sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void DefaultSemiAnalytic(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      bool calcHedge = true;
      string measure = "";
      string hedgeTenor = "5 Year";
      object[] pars = Parsers.Parse(new Type[] { typeof(bool), typeof(string) }, DefaultSensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) calcHedge = (bool)pars[0];
        if (pars[1] != null) hedgeTenor = (string)pars[1];
      }
      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        timer.Resume();
        System.Data.DataTable table = Sensitivities.Default(pricers, measure, calcHedge, hedgeTenor, null, SensitivityMethod.SemiAnalytic, null, rescaleStrikes);
        timer.Stop();
        MatchExpects(ToResultData(table, false, calcHedge, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate recovery sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void Recovery(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      bool recalibrate = false;
      double upBump = 0.01;
      double downBump = 0.0;
      BumpType bumpType = BumpType.Parallel;
      bool calcGamma = true;
      object[] pars = Parsers.Parse(new Type[] {
        typeof(bool), typeof(double), typeof(double),
        typeof(BumpType),typeof(bool)}, RecoverySensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) recalibrate = (bool)pars[0];
        if (pars[1] != null) upBump = (double)pars[1];
        if (pars[2] != null) downBump = (double)pars[2];
        if (pars[3] != null) bumpType = (BumpType)pars[3];
        if (pars[4] != null) calcGamma = (bool)pars[4];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        System.Data.DataTable table;
        timer.Resume();
        table = InvokeSensitivityRecovery(pricers, SensitivityPriceMeasure,
          recalibrate, upBump, downBump, bumpType, calcGamma, null, rescaleStrikes);
        timer.Stop();
        MatchExpects(ToResultData(table, calcGamma, false, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate recovery sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="rescaleStrikes">Optional boolean array indicating rescale strikes</param>
    /// <returns>ResultData</returns>
    public void RecoverySemiAnalytic(IPricer[] pricers, params bool[] rescaleStrikes)
    {

      if (!(rescaleStrikes == null || rescaleStrikes.Length == 0) && rescaleStrikes.Length != pricers.Length)
        throw new ArgumentException("pricers and rescaleStrikes must have same length");
      string measure = "";
      bool recalibrate = false;
      double upBump = 0.01;
      double downBump = 0.0;
      BumpType bumpType = BumpType.Parallel;
      bool calcGamma = true;
      object[] pars = Parsers.Parse(new Type[] {
        typeof(bool), typeof(double), typeof(double),
        typeof(BumpType),typeof(bool)}, RecoverySensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) recalibrate = false;
        if (pars[1] != null) upBump = (double)pars[1];
        if (pars[2] != null) downBump = (double)pars[2];
        if (pars[3] != null) bumpType = (BumpType)pars[3];
        if (pars[4] != null) calcGamma = (bool)pars[4];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        System.Data.DataTable table;
        timer.Resume();
        table = Sensitivities.Recovery(pricers, measure,
          recalibrate, upBump, downBump, bumpType, false, SensitivityMethod.SemiAnalytic, null, rescaleStrikes);
        timer.Stop();
        MatchExpects(ToResultData(table, false, false, timer.Elapsed));
      }
    }

    /// <summary>
    ///   Calculate correlation sensitivity
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <returns>ResultData</returns>
    public void Correlation(IPricer[] pricers)
    {

      double upBump = 0.01;
      double downBump = 0.0;
      bool bumpRelative = false;
      bool scaledDelta = true;
      BumpType bumpType = BumpType.Parallel;
      bool calcGamma = true;
      bool bumpFactors = false;
      object[] pars = Parsers.Parse(new Type[] {
        typeof(double), typeof(double),
        typeof(bool), typeof(bool), 
        typeof(BumpType),typeof(bool), typeof(bool)}, CorrelationSensitivityParam);
      if (pars != null)
      {
        if (pars[0] != null) upBump = (double)pars[0];
        if (pars[1] != null) downBump = (double)pars[1];
        if (pars[2] != null) bumpRelative = (bool)pars[2];
        if (pars[3] != null) scaledDelta = (bool)pars[3];
        if (pars[4] != null) bumpType = (BumpType)pars[4];
        if (pars[5] != null) calcGamma = (bool)pars[5];
        if (pars[6] != null) bumpFactors = (bool)pars[6];
      }

      using (new CheckStates(CheckPricerStates, pricers))
      {
        Timer timer = new Timer();
        System.Data.DataTable table;
        timer.Resume();
        table = Sensitivities.Correlation(pricers, SensitivityPriceMeasure,
          upBump, downBump, bumpRelative, scaledDelta,
          bumpType, calcGamma, bumpFactors, null);
        timer.Stop();
        MatchExpects(ToResultData(table, calcGamma, false, timer.Elapsed));
      }
    }

    private ResultData ToResultData(DataTable table)
    {
      return ToResultData(table, (double)table.ExtendedProperties["Elapsed"]);
    }

    protected ResultData ToResultData(DataTable table, double timeUsed)
    {
      return ToResultData(table, true, true, timeUsed);
    }

    /// <summary>
    ///   Convert sensitivity table to a result data object
    /// </summary>
    /// <param name="table">Data table</param>
    /// <param name="calcGamma">Gamma calculated</param>
    /// <param name="calcHedge">Hedge Calculated</param>
    /// <param name="timeUsed">Time used to complete the tests</param>
    /// <returns>ResultData</returns>
    protected ResultData ToResultData(DataTable table,
      bool calcGamma, bool calcHedge, double timeUsed)
    {
      // Total
      int rows = table.Rows.Count;
      string[] labels = new string[rows];
      double[] deltas = new double[rows];
      double[] gammas = null;
      double[] hedges = null;
      double[] hedgeNotionals = null;

      var titles = new Dictionary<string, int>();
      int count = table.Columns.Count;
      for (int i = 0; i < count; ++i)
      {
        var col = table.Columns[i];
        titles.Add(col.ColumnName, i);
      }

      int cols = 1;
      if (calcGamma && titles.ContainsKey("Gamma"))
      {
        gammas = new double[rows];
        cols++;
      }
      if (calcHedge && titles.ContainsKey("Hedge Delta"))
      {
        hedges = new double[rows];
        hedgeNotionals = new double[rows];
        cols += 2;
      }

      for (int i = 0; i < rows; i++)
      {
        System.Data.DataRow row = table.Rows[i];
        labels[i] = (string)row["Element"];
        if (titles.ContainsKey("Pricer"))
        {
          labels[i] += "/" + row["Pricer"];
        }
        if (titles.ContainsKey("Curve Tenor"))
        {
          labels[i] += "/" + row["Curve Tenor"];
        }
        deltas[i] = (double)row["Delta"];
        if (gammas != null)
          gammas[i] = (double)row["Gamma"];
        if (hedges != null)
        {
          hedges[i] = (double)row["Hedge Delta"];
          hedgeNotionals[i] = (double)row["Hedge Notional"];
        }
      }

      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }
      else if (ReorderResultTable && !BaseEntityContext.IsGeneratingExpects
        && !rd.Results[0].Labels.IsNullOrEmpty())
      {
        var expectLabels = rd.Results[0].Labels;
        var map = expectLabels.Select(s => Array.IndexOf(labels, s)).ToArray();
        if (!map.Any(i => i < 0))
        {
          labels = expectLabels;
          deltas = map.Select(i => deltas[i]).ToArray();
          if (gammas != null)
            gammas = map.Select(i => gammas[i]).ToArray();
          if (hedges != null) 
            hedges = map.Select(i => hedges[i]).ToArray();
          if (hedgeNotionals != null) 
            hedgeNotionals = map.Select(i => hedgeNotionals[i]).ToArray();
        }
      }

      int idx = 0;
      rd.Results[idx].Name = "Delta";
      rd.Results[idx].Labels = labels;
      rd.Results[idx].Actuals = deltas;
      if (gammas != null)
      {
        idx++;
        rd.Results[idx].Name = "Gamma";
        rd.Results[idx].Labels = labels;
        rd.Results[idx].Actuals = gammas;
      }
      if (hedges != null)
      {
        idx++;
        rd.Results[idx].Name = "HedgeDelta";
        rd.Results[idx].Labels = labels;
        rd.Results[idx].Actuals = hedges;
      }
      if (hedgeNotionals != null)
      {
        idx++;
        rd.Results[idx].Name = "HedgeNotional";
        rd.Results[idx].Labels = labels;
        rd.Results[idx].Actuals = hedgeNotionals;
      }
      rd.TimeUsed = timeUsed;
      return rd;
    }

    #endregion // RiskMethods

    #region Properties
    /// <summary>
    ///   Price measure (Pv, FeePv, BreakEvenPremium, etc.)
    /// </summary>
    public string SensitivityPriceMeasure { get; set; } = null;

    /// <summary>
    ///   Spread01 Parameters
    /// </summary>
    /// <remarks>
    /// Expects data in the format "upBump,downBump".
    /// </remarks>
    public string Spread01Param { get; set; } = null;

    /// <summary>
    ///   SpreadGamma Parameters
    /// </summary>
    /// <remarks>
    /// Expects data in the format "upBump,downBump".
    /// </remarks>
    public string SpreadGammaParam { get; set; } = null;

    /// <summary>
    ///   SpreadHedge Parameters
    /// </summary>
    /// <remarks>
    /// Expects data in the format "hedgeTenor,upBump,downBump".
    /// </remarks>
    public string SpreadHedgeParam { get; set; } = null;

    /// <summary>
    ///   IR01 Parameters
    /// </summary>
    /// <remarks>
    /// Expects data in the format "upBump,downBump,recalibrate".
    /// </remarks>
    public string IR01Param { get; set; } = null;

    /// <summary>
    ///   Recovery Parameters
    /// </summary>
    /// <remarks>
    /// Expects data in the format "upBump,downBump,recalibrate".
    /// </remarks>
    public string RecoveryParam { get; set; } = null;

    /// <summary>
    ///   Theta Parameters
    /// </summary>
    /// <remarks>
    /// Expects 3 data in the format "asOf,settle,clean", where date should
    /// be an eight digits integer like 20070102.
    /// </remarks>
    public string ThetaParam { get; set; } = null;

    /// <summary>
    ///   Spread Sensitivity Parameters
    /// </summary>
    /// <remarks>
    /// Expects 9 data in the format 
    ///   "initialBump,upBump,downBump,bumpRelative,scaledDelta,bumpType,calcGamma,calcHedge,hedgeTenor".
    /// </remarks>
    public string SpreadSensitivityParam { get; set; } = null;

    /// <summary>
    ///   Spread Sensitivity bump tenors
    /// </summary>
    public string[] SpreadSensitivityBumpTenors { get; set; } = null;

    /// <summary>
    ///   Interest Rate Sensitivity Parameters
    /// </summary>
    /// <remarks>
    /// Expects 10 data in the format 
    ///   "initialBump,upBump,downBump,bumpRelative,scaledDelta,bumpType,calcGamma,calcHedge,hedgeTenor,recalibrate".
    /// </remarks>
    public string RateSensitivityParam { get; set; } = null;

    /// <summary>
    ///   Interest Rate Sensitivity Parameters
    /// </summary>
    /// <remarks>
    /// Expects 10 data in the format 
    ///   "initialBump,upBump,downBump,bumpRelative,scaledDelta,bumpType,calcGamma,calcHedge,hedgeTenor,recalibrate".
    /// </remarks>
    public string DiscountSensitivityParam { get; set; } = null;

    public string[] RateSensitivityBumpTenors { get; set; } = null;

    /// <summary>
    ///   Default Sensitivity Parameters
    /// </summary>
    /// <remarks>
    /// Expects 2 data in the format "calcHedge,hedgeTenor".
    /// </remarks>
    public string DefaultSensitivityParam { get; set; } = null;

    /// <summary>
    ///   Recovery Sensitivity Parameters
    /// </summary>
    /// <remarks>
    /// Expects 5 data in the format "recalibrate,upBump,downBump,bumpType,calcGamma".
    /// </remarks>
    public string RecoverySensitivityParam { get; set; } = null;

    /// <summary>
    ///   Correlation Sensitivity Parameters
    /// </summary>
    /// <remarks>
    /// Expects 7 data in the format
    ///   "upBump,downBump,bumpRelative,scaledDelta,bumpType,calcGamma,bumpFactors".
    /// </remarks>
    public string CorrelationSensitivityParam { get; set; } = null;

    /// <summary>
    ///   Theta period for summary theta functions
    /// </summary>
    public string ThetaPeriod { get; set; } = "1 Day";

    /// <summary>
    ///   Whether to check curve states change
    /// </summary>
    public bool CheckPricerStates { get; set; } = true;

    /// <summary>
    ///   Whether to use the generic method for sensitivities.
    /// </summary>
    public bool WithGenericSensitivity { get; set; } = false;

    /// <summary>
    ///   Whether to use the generic method for sensitivities.
    /// </summary>
    public bool UseSensitivities2 { get; set; } = false;

    /// <summary>
    ///   Whether to use the cache for sensitivities2.
    /// </summary>
    public bool UseCache { get; set; } = false;

    /// <summary>
    ///   Whether to use the cache for sensitivities2.
    /// </summary>
    public bool ReorderResultTable { get; set; }

    #endregion // Properties

    #region Data

    #endregion // Data

    #region Generic sensitivity functions

    private DataTable InvokeSensitivitySpread(
      IPricer[] pricers, string measure,
      double initialBump, double upBump, double downBump,
      bool bumpRelative, bool scaledDelta,
      BumpType bumpType, string[] bumpTenors,
      bool calcGamma, bool calcHedge, string hedgeTenor,
      DataTable dataTable, bool[] rescaleStrikes)
    {
      if (UseSensitivities2)
      {
        var flags = BumpFlags.BumpInPlace | BumpFlags.NoHedgeOnTenorMismatch;
        if (rescaleStrikes != null && rescaleStrikes.FirstOrDefault())
          flags |= BumpFlags.RemapCorrelations;
        return Sensitivities2.Calculate(pricers, measure, null, BumpTarget.CreditQuotes,
          upBump, downBump, bumpType, flags, bumpTenors, scaledDelta, calcGamma,
          hedgeTenor, calcHedge, UseCache, null);
      }
      if (!WithGenericSensitivity)
      {
        return Sensitivities.Spread(pricers, measure,
          initialBump, upBump, downBump, bumpRelative, scaledDelta,
          bumpType, bumpTenors, calcGamma, calcHedge,
          hedgeTenor, dataTable, rescaleStrikes);
      }
      var evaluators = Array.ConvertAll(pricers,
        (p) => new PricerEvaluator(p, measure, false, false));
      return Sensitivities.Spread(evaluators, QuotingConvention.None,
        initialBump, upBump, downBump, bumpRelative, scaledDelta,
        bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor,
        dataTable, rescaleStrikes);
    }

    private DataTable InvokeSensitivityDefault(
      IPricer[] pricers, string measure,
      bool calcHedge, string hedgeTenor,
      DataTable dataTable, bool[] rescaleStrikes)
    {
      if (!WithGenericSensitivity)
      {
        return Sensitivities.Default(pricers, measure, calcHedge, hedgeTenor,
          dataTable, rescaleStrikes);
      }
      var evals = Array.ConvertAll(pricers,
        (p) => new PricerEvaluator(p, measure, false, false));
      return Sensitivities.Default(evals, calcHedge, hedgeTenor, dataTable, null,
        rescaleStrikes);
    }

    private DataTable InvokeSensitivityRecovery(
      IPricer[] pricers,
      string measure,
      bool recalibrate,
      double upBump,
      double downBump,
      BumpType bumpType,
      bool calcGamma,
      DataTable dataTable,
      params bool[] rescaleStrikes)
    {
      if (!WithGenericSensitivity)
      {
        return Sensitivities.Recovery(pricers, measure, recalibrate,
          upBump, downBump, bumpType, calcGamma, dataTable, rescaleStrikes);
      }
      var evals = Array.ConvertAll(pricers,
        (p) => new PricerEvaluator(p, measure, false, false));
      return Sensitivities.Recovery(evals, recalibrate,
        upBump, downBump, bumpType, calcGamma, dataTable, rescaleStrikes);
    }

    private double CalcSpread01(IPricer pricer, string measure,
      double upBump, double downBump, bool[] rescaleStrikes)
    {
      if (!UseSensitivities2)
      {
        return measure == null
          ? Sensitivities.Spread01(pricer, upBump, downBump, rescaleStrikes)
          : Sensitivities.Spread01(pricer, measure, upBump, downBump, rescaleStrikes);
      }

      var flags = BumpFlags.BumpInPlace;
      if (rescaleStrikes != null && rescaleStrikes.FirstOrDefault())
        flags |= BumpFlags.RemapCorrelations;
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, null, null,
        BumpTarget.CreditQuotes, upBump, downBump, BumpType.Uniform,
        flags, null, true, false, null, false, UseCache, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    private double CalcSpreadGamma(IPricer pricer, string measure,
      double upBump, double downBump, bool[] rescaleStrikes)
    {
      if (!UseSensitivities2)
      {
        return Sensitivities.SpreadGamma(pricer, measure, upBump, downBump, rescaleStrikes);
      }

      var flags = BumpFlags.BumpInPlace;
      if (rescaleStrikes != null && rescaleStrikes.FirstOrDefault())
        flags |= BumpFlags.RemapCorrelations;
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, measure, null,
        BumpTarget.CreditQuotes, upBump, downBump, BumpType.Uniform,
        flags, null, true, true, null, false, UseCache, null);
      return (double)(dataTable.Rows[0])["Gamma"];
    }


    //-------------------------Calculate Spread Hedge-------------------------//
    private double CalcSpreadHedge(IPricer pricer, string measure,
      string hedgeTenor, double upBump, double downBump, bool[] rescaleStrikes)
    {
      if (!UseSensitivities2)
      {
        return Sensitivities.SpreadHedge(pricer, measure, hedgeTenor,
          upBump, downBump, rescaleStrikes);
      }

      var flags = BumpFlags.BumpInPlace;
      if (rescaleStrikes != null && rescaleStrikes.FirstOrDefault())
        flags |= BumpFlags.RemapCorrelations;
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, measure, null,
        BumpTarget.CreditQuotes, upBump, downBump, BumpType.Uniform,
        flags, null, true, false, hedgeTenor, true, UseCache, null);
      return (double)(dataTable.Rows[0])["Hedge Notional"];
    }


    //------------------------Calculate Rate Delta---------------------------//
    double CalcRate01(IPricer pricer, string measure,
      double upBump, double downBump,
      bool recalibrate, params bool[] rescaleStrikes)
    {
      if (!UseSensitivities2)
      {
        return Sensitivities.IR01(pricer, measure, upBump, downBump,
          recalibrate, rescaleStrikes);
      }
      var flags = BumpFlags.BumpInPlace;
      if (recalibrate) flags |= BumpFlags.RecalibrateSurvival;
      if (rescaleStrikes != null && rescaleStrikes.FirstOrDefault())
        flags |= BumpFlags.RemapCorrelations;
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, measure, null,
        BumpTarget.InterestRates, upBump, downBump, BumpType.Uniform, flags,
        null, true, false, null, false, UseCache, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    #endregion
  }
}
