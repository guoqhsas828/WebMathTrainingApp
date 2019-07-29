using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Ccr
{

  #region CCRMarketEnvironment

  /// <summary>
  /// Market environment
  /// </summary>
  [Serializable]
  public class CCRMarketEnvironment : MarketEnvironment
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(CCRMarketEnvironment));

    #region Properties

    /// <summary>
    /// Cpty currency
    /// </summary>
    public int[] CptyCcy { get; private set; }

    /// <summary>
    /// Cpty index in credits array
    /// </summary>
    public int[] CptyIndex { get; private set; }

    /// <summary>
    /// Cpty recoveries
    /// </summary>
    public double[] CptyRecoveries { get; private set; }

    /// <summary>
    /// Max grid size for simulation step
    /// </summary>
    internal Tenor GridSize { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">asOf date</param>
    /// <param name="tenors">Tenors defining the libor family modelled</param>
    /// <param name="gridSize">Max grid size for simulation step</param>
    /// <param name="cptyIndex">Counterparty curves index(cptyIndex[0] = cpty curve, cptyIndex[1] = own curve)</param>
    /// <param name="cptyRecoveries">Counterparty recovery rates</param>
    /// <param name="discountCurves">Discount curves</param>
    /// <param name="forwardCurves">Term structure of forward prices</param>
    /// <param name="creditCurves">Credit curves</param>
    /// <param name="fxRates">Fx rates</param>
    /// <param name="spotBasedCurves">Spot asset prices</param>
    /// <param name="jumpsOnDefault">The jumps on default</param>
    public CCRMarketEnvironment(Dt asOf, Dt[] tenors, Tenor gridSize, int[] cptyIndex, double[] cptyRecoveries, DiscountCurve[] discountCurves,
                                  CalibratedCurve[] forwardCurves, SurvivalCurve[] creditCurves, FxRate[] fxRates, CalibratedCurve[] spotBasedCurves,
                                  IEnumerable<IJumpSpecification> jumpsOnDefault = null)
      : base(asOf, tenors, discountCurves, forwardCurves, creditCurves, fxRates, spotBasedCurves, jumpsOnDefault)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Creating MarketEnvironment...");
        LogCurves(cptyIndex, discountCurves, forwardCurves, creditCurves, fxRates, spotBasedCurves);
      }

      GridSize = gridSize.IsEmpty ? new Tenor(Frequency.Annual) : gridSize;
      CptyIndex = cptyIndex ?? new int[0];

      if (CptyIndex.Length > cptyRecoveries.Length)
      {
        var recoveries = new List<double>();
        for (int i = 0; i < CptyIndex.Length; i++)
        {
          if (cptyRecoveries != null && cptyRecoveries.Length > i)
            recoveries.Add(cptyRecoveries[i]);
          else
          {
            var cptyCurve = creditCurves[CptyIndex[i]];

            recoveries.Add(cptyCurve.SurvivalCalibrator.RecoveryCurve.RecoveryRate(asOf));
          }
        }
        CptyRecoveries = recoveries.ToArray();
      }
      else
      {
        CptyRecoveries = cptyRecoveries ?? new double[0];
      }

      CptyCcy = Array.ConvertAll(CptyIndex, i => (i >= 0) ? CcyIndex(CreditCurves[i]) : CcyIndex(CreditCurves[~i]));
    }

    private void LogCurves(int[] cptyIndex, DiscountCurve[] discountCurves, CalibratedCurve[] forwardCurves, SurvivalCurve[] creditCurves, 
                           FxRate[] fxRates, CalibratedCurve[] spotBasedCurves)
    {
      if (cptyIndex.Length == 2 && creditCurves != null && creditCurves.Count() == 2)
      {
        if (cptyIndex[0] >= 0 && cptyIndex[0] <= 1)
          LogCurveInstance(creditCurves[cptyIndex[0]], "Cpty Curve");
        if (cptyIndex[0] >= 0 && cptyIndex[0] <= 1)
          LogCurveInstance(creditCurves[cptyIndex[1]], "Own Curve");
      }
      LogCurve("Discount Curves", discountCurves);
      LogCurve("Forward Curves", forwardCurves);
      LogSpot("Fx Rates", fxRates);
      LogCurve("Spot Based Curves", spotBasedCurves);
    }

    private void LogCurveInstance(SurvivalCurve curve, string heading)
    {
      var builder = new StringBuilder();
      if (curve == null)
      {
        builder.Append(heading);
        builder.Append(" is null");
      }
      else
      {
        builder.Append(heading);
        builder.Append(" ");
        builder.Append(curve.Name);
      }
      Logger.Debug(builder.ToString());   
    }

    private void LogCurve(string heading, CalibratedCurve[] curves)
    {
      var builder = new StringBuilder();
      if (curves != null && curves.Length > 0)
      {
        builder.Append(string.Format("{0}: ", heading));
        var i = 0;
          for (; i < curves.Length - 1; ++i)
          {
            if (curves[i] == null)
            {
              builder.Append("Curve is null, ");
            }
            else if (curves[i].Name == null)
            {
              builder.Append("Curve name is null, ");
            }
            else
            {
              builder.Append(curves[i].Name + ", ");
            }
          }
          if (curves[i] == null)
          {
            builder.Append("Curve is null");
          }
          else if (curves[i].Name == null)
          {
            builder.Append("Curve name is null");
          }
          else
          {
            builder.Append(curves[i].Name);
          }
      }
      else
      {
        if (curves == null)
        {
          builder.Append(string.Format("The {0} Array is null", heading));
        }
        else
        {
          builder.Append(string.Format("The {0} Array is empty", heading));
        }        
      }
      Logger.Debug(builder.ToString());   
    }

    private void LogSpot(string heading, ISpot[] spots)
    {
      var builder = new StringBuilder();
      if (spots != null && spots.Length > 0)
      {
        builder.Append(string.Format("{0}: ", heading));
        var i = 0;
        for (; i < spots.Length - 1; ++i)
        {
          if (spots[i] == null)
          {
            builder.Append("Spot is null, ");
          }
          else if (spots[i].Name == null)
          {
            builder.Append("Spots name is null, ");
          }
          else
          {
            builder.Append(spots[i].Name + ", ");
          }
        }
        if (spots[i] == null)
        {
          builder.Append("Spot is null");
        }
        else if (spots[i].Name == null)
        {
          builder.Append("Spots name is null");
        }
        else
        {
          builder.Append(spots[i].Name);
        }
      }
      else
      {
        if (spots == null)
        {
          builder.Append(string.Format("The {0} Array is null", heading));
        }
        else
        {
          builder.Append(string.Format("The {0} Array is empty", heading));
        }         
      }
      Logger.Debug(builder.ToString());
    }

    #endregion

    #region Methods

    /// <summary>
    /// Get cpty curve at index
    /// </summary>
    /// <param name="idx">index</param>
    /// <returns>Cpty curve or null</returns>
    internal SurvivalCurve CptyCurve(int idx)
    {
      if (idx >= CptyIndex.Length)
        return null;
      idx = CptyIndex[idx];
      if (idx < 0)
        idx = ~idx;
      return CreditCurves[idx];
    }

    /// <summary>
    /// Get cpty recovery at idx
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    internal double CptyRecovery(int idx)
    {
      return CptyRecoveries[idx];
    }

    /// <summary>
    /// Get curve currency index.
    /// </summary>
    /// <param name="curve">Curve</param>
    /// <returns>Curve currency index</returns>
    private int CcyIndex(CalibratedCurve curve)
    {
      for (int i = 0; i < DiscountCurves.Length; ++i)
      {
        if (curve.Ccy == DiscountCurves[i].Ccy)
          return i;
      }
      return 0; //assume domestic
    }

    /// <summary>
    /// Grab a subset of the discount curves
    /// </summary>
    ///<param name="bounds">start and end index of subset to get</param>
    /// <returns>Subset</returns>
    internal DiscountCurve[] GetDiscountCurves(params DiscountCurve[] bounds)
    {
      if (bounds == null || bounds.Length == 0)
        return DiscountCurves;
      return bounds.Where(DiscountCurves.Contains).ToArray();
    }

    /// <summary>
    /// Grab a subset of the credit curves
    /// </summary>
    ///<param name="bounds">start and end index of subset to get</param>
    /// <returns>Subset</returns>
    internal SurvivalCurve[] GetCreditCurves(params SurvivalCurve[] bounds)
    {
      if (bounds == null || bounds.Length == 0)
        return CreditCurves;
      return bounds.Where(CreditCurves.Contains).ToArray();
    }

    /// <summary>
    /// Grab a subset of the forward curves
    /// </summary>
    /// <param name="bounds">start and end index of subset to get</param>
    /// <returns>Subset</returns>
    internal CalibratedCurve[] GetForwardCurves(params CalibratedCurve[] bounds)
    {
      if (bounds == null || bounds.Length == 0)
        return ForwardCurves;
      return bounds.Where(ForwardCurves.Contains).ToArray();
    }

    /// <summary>
    /// Grab a subset of the fx rates
    /// </summary>
    ///<param name="bounds">start and end index of subset to get</param>
    /// <returns>Subset</returns>
    internal FxRate[] GetFxRates(params FxRate[] bounds)
    {
      if (bounds == null || bounds.Length == 0)
        return FxRates.Take(DiscountCurves.Length - 1).ToArray();
      return bounds.Where(FxRates.Contains).ToArray();
    }

    /// <summary>
    /// Grab a subset of the spot asset prices
    /// </summary>
    ///<param name="bounds">start and end index of subset to get</param>
    /// <returns>Subset</returns>
    internal ISpot[] GetSpotPrices(params ISpot[] bounds)
    {
      var spotPrices = SpotBasedCurves.Where(c => c is IForwardPriceCurve).Cast<IForwardPriceCurve>().Select(c => c.Spot).ToArray();
      if (bounds == null || bounds.Length == 0)
        return spotPrices;
      return bounds.Where(spotPrices.Contains).ToArray();
    }


    /// <summary>
    /// Simulate pathwise credit spread at a given date.
    /// </summary>
    /// <param name="t">Date index</param>
    /// <param name="path">Simulation path</param>
    /// <param name="cptySpread">Counterparty spread</param>
    /// <param name="ownSpread">Booking entity spread</param>
    /// <param name="lendSpread">Lend spread</param>
    /// <param name="borrowSpread">Borrow spread</param>
    /// <remarks>
    /// Given a survival curve <m>S_t(T_k) = \mathbb{Q}(\tau \leq T_k \big | \mathcal{F}_t)</m> and the corresponding recovery rate <m>R</m>, 
    /// the credit spread w.r.t the survival curve is defined as follows:
    /// <math env="align*">
    /// \text{Credit Spread}(T_k) := (1 - R) \cdot \frac{\frac{S_t(T_k) - S_t(T_{k-1})}{T_k - T_{k-1}}}{1-\frac{S_t(T_k) + S_t(T_{k-1})}{2}}
    /// </math>
    /// </remarks>
    internal void CreditSpread(int t, SimulatedPath path, out double cptySpread, out double ownSpread, out double lendSpread, out double borrowSpread)
    {
      cptySpread = ownSpread = lendSpread = borrowSpread = 0.0;
      if (CptyIndex.Length == 0) // no survival curve
        return;
      if (CptyIndex.Length == 1) // we only have CptyCurve. By default ownSpread = lendSpread = borrowSpread = 0.0 
      {
        cptySpread = path.EvolveRnDensity(1, t) * (1 - CptyRecoveries[0]);
        return;
      }
      cptySpread = path.EvolveRnDensity(3, t) * (1 - CptyRecoveries[0]);
      ownSpread = path.EvolveRnDensity(4, t) * (1 - CptyRecoveries[1]);
      if (CptyIndex.Length == 2) // we have CptyCurve and OwnCurve. By default, lendspread = 0.0 and borrowSpread = ownSpread
        borrowSpread = ownSpread; 
      else if (CptyIndex.Length == 3) // we have CptyCurve, OwnCurve and BorrowCurve. By default, lendspread = 0.0.
        borrowSpread = path.EvolveRnDensity(8, t) * (1 - CptyRecoveries[2]);
      else if (CptyIndex.Length == 4) // we have CptyCurve, OwnCurve, BorrowCurve and LendCurve
      {
        lendSpread = path.EvolveRnDensity(9, t) * (1 - CptyRecoveries[3]);
        borrowSpread = path.EvolveRnDensity(8, t) * (1 - CptyRecoveries[2]);
      }
    }

    #endregion
  }

  #endregion

  #region CCRMarketEnvironmentUtils

  /// <summary>
  /// Utilities
  /// </summary>
  internal static class MarketEnvironmentUtils
  {
    private static readonly log4net.ILog SurvivalCurveLogger = LogUtil.GetLogger("SurvivalCurveEvolutionLogger");
    private static readonly log4net.ILog RateCurveLogger = LogUtil.GetLogger("RateCurveEvolutionLogger");
    private static readonly log4net.ILog FxRateLogger = LogUtil.GetLogger("FxEvolutionLogger");
    private static readonly log4net.ILog FwdRateLogger = LogUtil.GetLogger("FwdPriceEvolutionLogger");
    private static readonly log4net.ILog SpotLogger = LogUtil.GetLogger("SpotEvolutionLogger");

    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(MarketEnvironmentUtils));

    /// <summary>
    /// Evolve the market environment to reflect the evolution up to dtIdx
    /// </summary>
    /// <param name="path">Simulated path</param>
    /// <param name="t">Exposure date index or bitwise complement of the index of the simulation date 
    /// immediately succeeding dt if dt does not belong to the simulation dates</param>
    /// <param name="dt">Exposure date</param>
    /// <param name="environment">Market environment to evolve</param>
    /// <param name="unilateral">Treat default unilaterally or jointly (first-to-default)</param>
    /// <param name="numeraire">Level of the numeraire asset at dt</param>
    /// <param name="discountFactor">Level of the discount factor at dt</param>
    /// <param name="cptyRn">Change of measure process to condition on event of counterparty defaulting at dt</param>
    /// <param name="ownRn">Change of measure process to condition on event of booking entity defaulting at dt</param>
    /// <param name="survivalRn">Change of measure process to condition on event of (counterparty and) booking entity surviving to dt</param>
    /// <param name="cptySpread">Counterparty spread</param>
    /// <param name="ownSpread">Own spread</param>
    /// <param name="lendSpread">Lend spread</param>
    /// <param name="borrowSpread">Borrow spread</param>
    internal static void Evolve(this SimulatedPath path, int t, Dt dt, CCRMarketEnvironment environment, bool unilateral,
                                out double numeraire, out double discountFactor,
                                out double cptyRn, out double ownRn, out double survivalRn, out double cptySpread,
                                out double ownSpread, out double lendSpread, out double borrowSpread)
    {
      numeraire = 1.0;
      discountFactor = 1.0;
      cptyRn = 1.0;
      ownRn = 1.0;
      survivalRn = 1.0;
    
      if (environment.CptyIndex.Length == 1)
        cptyRn = path.EvolveRnDensity(0, t);
      else
      {
        if (environment.CptyIndex.Length >= 2)
          if (!unilateral)
            cptyRn = path.EvolveRnDensity(0, t);
          else
            cptyRn = path.EvolveRnDensity(5, t);
      }
      if (environment.CptyIndex.Length >= 2)
        if (!unilateral)
        {
          ownRn = path.EvolveRnDensity(1, t);
          survivalRn = path.EvolveRnDensity(2, t);
        }
        else
        {
          ownRn = path.EvolveRnDensity(6, t);
          survivalRn = path.EvolveRnDensity(7, t);  // unilateral survival Rn 
        }
      if (environment.DiscountCurves.Length != 0)
      {
        double fxRate = 1.0;
        var domestic = environment.DiscountCurves[0];
        path.EvolveDiscountCurve(0, t, domestic, ref fxRate, ref numeraire);
        environment.ApplyDiscountJump(0, dt);
        if (RateCurveLogger.IsDebugEnabled)
        {
          RateCurveLogger.Debug(GetCurveArrayStringRepresentation(domestic.Points));
        }
        double ddf = domestic.Interpolate(environment.AsOf, dt);
        discountFactor = ddf;
        numeraire /= ddf; //undiscounted numeraire process
        for (int i = 1; i < environment.DiscountCurves.Length; ++i)
        {
          double numer = 1.0;
          var df = environment.DiscountCurves[i];
          path.EvolveDiscountCurve(i, t, df, ref fxRate, ref numer);
          environment.ApplyDiscountJump(i, dt);
          if (RateCurveLogger.IsDebugEnabled)
          {
            RateCurveLogger.Debug(GetCurveArrayStringRepresentation(df.Points));
          }

          double fdf = df.Interpolate(environment.AsOf, dt);
          var fx = environment.FxRates[i - 1];
          fx.Update(dt, df.Ccy, domestic.Ccy, fxRate * fdf / ddf);
          environment.ApplyFxJump(i - 1, dt);
          //Math.Min(Math.Max(fdf / ddf, 5e-2), 20));
          if (FxRateLogger.IsDebugEnabled)
          {
            FxRateLogger.DebugFormat("{0} {1}", dt, fx);
          }
        }
      }
      // update foreign/foreign fx rates by triangulation through domestic ccy 
      for (int i = environment.DiscountCurves.Length - 1; i < environment.FxRates.Length; ++i)
      {
        var domesticCcy = environment.DiscountCurves[0].Ccy;
        var f1f2 = environment.FxRates[i]; 
        var f1Domestic = environment.FxRates.Where(f1 => (f1.FromCcy == f1f2.FromCcy && f1.ToCcy == domesticCcy)
                                                      || (f1.ToCcy == f1f2.FromCcy && f1.FromCcy == domesticCcy)).First().GetRate(f1f2.FromCcy, domesticCcy);
        
        var domesticF2 = environment.FxRates.Where(f2 => (f2.FromCcy == f1f2.ToCcy && f2.ToCcy == domesticCcy)
                                                      || (f2.ToCcy == f1f2.ToCcy && f2.FromCcy == domesticCcy)).First().GetRate(domesticCcy, f1f2.ToCcy);

        var fx = f1Domestic * domesticF2;
        f1f2.Update(dt, f1f2.FromCcy, f1f2.ToCcy, fx);
        environment.ApplyFxJump(i, dt);
        if (FxRateLogger.IsDebugEnabled)
          FxRateLogger.DebugFormat("{0} {1}", dt, f1f2);
      }

      for (int i = 0; i < environment.CreditCurves.Length; ++i)
      {
        var sc = environment.CreditCurves[i];
        path.EvolveCreditCurve(i, t, sc);
        environment.ApplyCreditJump(i, dt);
        if (SurvivalCurveLogger.IsDebugEnabled)
        {
          SurvivalCurveLogger.Debug(GetCurveArrayStringRepresentation(sc.Points));
        }
      }
      for (int i = 0; i < environment.ForwardCurves.Length; ++i)
      {
        var fc = environment.ForwardCurves[i];
        path.EvolveForwardCurve(i, t, fc);
        environment.ApplyForwardJump(i, dt);
        if (FwdRateLogger.IsDebugEnabled)
        {
          FwdRateLogger.Debug(GetCurveArrayStringRepresentation(fc.Points));
        }
      }
      for (int i = 0; i < environment.SpotBasedCurves.Length; ++i)
      {
        var sp = environment.SpotBasedCurves[i] as IForwardPriceCurve;
        if (sp == null)
          continue;
        var df = sp.DiscountCurve.Interpolate(environment.AsOf, dt);
        sp.Spot.Spot = dt;
        sp.Spot.Value = path.EvolveSpotPrice(i, t) / df;
        environment.ApplySpotJump(i, dt);
        if (SpotLogger.IsDebugEnabled)
        {
          SpotLogger.DebugFormat("{0} {1} {2}", dt, sp.Spot.Name, sp.Spot.Value);
        }
      }
      environment.CreditSpread(t, path, out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
      if (Logger.IsDebugEnabled)
      {
        LogOutParams(numeraire, discountFactor, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread);
      }
    }

    private static string GetCurveArrayStringRepresentation(CurvePointArray array)
    {
      var builder = new StringBuilder();
      for (int i = 0; i < array.Length; ++i)
      {
        builder.Append("[");
        builder.Append(array[i].ToString());
        builder.Append("]");
        if (i + 1 != array.Length)
        {
          builder.Append(",");
        }
      }
      return builder.ToString();
    }

    private static void LogOutParams(double numeraire, double discountFactor, double cptyRn, double ownRn, double survivalRn, double cptySpread, double ownSpread,
      double lendSpread, double borrowSpread)
    {
      Logger.Debug(string.Format("Level of the numeraire asset at dt: {0}", numeraire));
      Logger.Debug(string.Format("Level of the discount factor at dt: {0}", discountFactor));
      Logger.Debug(string.Format("Change of measure process to condition on event of counterparty defaulting at dt: {0}", cptyRn));
      Logger.Debug(string.Format("Change of measure process to condition on event of booking entity defaulting at dt: {0}", ownRn));
      Logger.Debug(string.Format("Change of measure process to condition on event of (counterparty and) booking entity surviving to dt: {0}", survivalRn));
      Logger.Debug(string.Format("Counterparty spread: {0}", cptySpread));
      Logger.Debug(string.Format("Own spread: {0}", ownSpread));
      Logger.Debug(string.Format("Lend spread: {0}", lendSpread));
      Logger.Debug(string.Format("Borrow spread: {0}", borrowSpread));
    }

    ///  <summary>
    ///  Evolve the market environment to reflect the evolution up to dtIdx
    ///  </summary>
    ///  <param name="path">Simulated path</param>
    /// <param name="t">Exposure date index or bitwise complement of the index of the simulation date 
    ///  immediately succeeding dt if dt does not belong to the simulation dates</param>
    ///  <param name="dt">Exposure date</param>
    /// <param name="dtFrac">days between t_0 and dt</param>
    /// <param name="environment">Market environment to evolve</param>
    ///  <param name="unilateral"></param>
    ///  <param name="numeraire">Level of the numeraire asset at dt</param>
    ///  <param name="discountFactor">Level of the discount factor at dt</param>
    ///  <param name="cptyRn">Change of measure process to condition on event of counterparty defaulting at dt</param>
    ///  <param name="ownRn">Change of measure process to condition on event of booking entity defaulting at dt</param>
    ///  <param name="survivalRn">Change of measure process to condition on event of counterparty and booking entity surviving to dt</param>
    ///  <param name="cptySpread">Counterparty spread</param>
    ///  <param name="ownSpread">Own spread</param>
    /// <param name="lendSpread"></param>
    /// <param name="borrowSpread"></param>
    internal static void Evolve(this SimulatedPath path, int t, Dt dt, double dtFrac, CCRMarketEnvironment environment, bool unilateral,
                                out double numeraire, out double discountFactor,
                                out double cptyRn, out double ownRn, out double survivalRn, out double cptySpread,
                                out double ownSpread, out double lendSpread, out double borrowSpread)
    {
      numeraire = 1.0;
      discountFactor = 1.0;
      if (environment.DiscountCurves.Length != 0)
      {
        double fxRate = 1.0;
        var domestic = environment.DiscountCurves[0];
        path.EvolveDiscount(0, dtFrac, t, domestic, out fxRate, out numeraire);
        environment.ApplyDiscountJump(0, dt);
        if (RateCurveLogger.IsDebugEnabled)
          RateCurveLogger.DebugFormat("{0} {1} 3M:{2} 6M:{3} 1Y:{4} 5Y:{5}", dt, domestic.Name,
                                      domestic.F(dt, Dt.AddMonths(dt, 3, CycleRule.IMM)),
                                      domestic.F(dt, Dt.AddMonths(dt, 6, CycleRule.IMM)),
                                      domestic.F(dt, Dt.AddMonths(dt, 12, CycleRule.IMM)),
                                      domestic.F(dt, Dt.Add(dt, 1, TimeUnit.Years)));
        double ddf = domestic.Interpolate(environment.AsOf, dt);
        discountFactor = ddf;
        numeraire /= ddf; //undiscounted numeraire process
      }

      for (int i = 0; i < environment.CreditCurves.Length; ++i)
      {
        var sc = environment.CreditCurves[i];
        path.EvolveCredit(i, dtFrac, t, sc);
        environment.ApplyCreditJump(i, dt);
        if (SurvivalCurveLogger.IsDebugEnabled)
          SurvivalCurveLogger.DebugFormat("{0} {1} 1Y:{2} 5Y:{3}", dt, sc.Name,
                                          sc.ImpliedSpread(Dt.CDSMaturity(dt, "1Y")),
                                          sc.ImpliedSpread(Dt.CDSMaturity(dt, "5Y")));
      }
      if (t < 0)
        t = ~t; 
      // since we calculate default prob only within a period, not a specific date, just pass t
      cptyRn = 1.0;
      ownRn = 1.0;
      survivalRn = 1.0;

      if (environment.CptyIndex.Length == 1)
        cptyRn = path.EvolveRnDensity(0, t);
      else
      {
        if (environment.CptyIndex.Length >= 2)
          if (!unilateral)
            cptyRn = path.EvolveRnDensity(0, t);
          else
            cptyRn = path.EvolveRnDensity(5, t);
      }
      if (environment.CptyIndex.Length >= 2)
        if (!unilateral)
        {
          ownRn = path.EvolveRnDensity(1, t);
          survivalRn = path.EvolveRnDensity(2, t);
        }
        else
        {
          ownRn = path.EvolveRnDensity(6, t);
          survivalRn = path.EvolveRnDensity(7, t);  // unilateral survival Rn 
        }
   
      environment.CreditSpread(t, path, out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
    }

    
    ///  <summary>
    ///  Evolve the market environment for a specific pricer to reflect the evolution up to dtIdx. Ignores any measure changes or counterparty effects. 
    ///  </summary>
    ///  <param name="path">Simulated path</param>
    /// <param name="t">Exposure date index or bitwise complement of the index of the simulation date 
    ///  immediately succeeding dt if dt does not belong to the simulation dates</param>
    ///  <param name="dt">Exposure date</param>
    /// <param name="dtFrac">days since simulation start</param>
    /// <param name="environment">Market environment to evolve</param>
    internal static void Evolve(this SimulatedPath path, int t, Dt dt, double dtFrac, MarketEnvironment environment)
    {
      if (environment.DiscountCurves.Length != 0)
      {
        var numeraire = 1.0;
        double fxRate = 1.0;
        var domestic = environment.DiscountCurves[0];
        path.EvolveDiscount(0, dtFrac, t, domestic, out fxRate, out numeraire);
        environment.ApplyDiscountJump(0, dt);
        if (RateCurveLogger.IsDebugEnabled)
          RateCurveLogger.DebugFormat("{0} {1} 3M:{2} 6M:{3} 1Y:{4} 5Y:{5}", dt, domestic.Name,
                                      domestic.F(dt, Dt.AddMonths(dt, 3, CycleRule.IMM)),
                                      domestic.F(dt, Dt.AddMonths(dt, 6, CycleRule.IMM)),
                                      domestic.F(dt, Dt.AddMonths(dt, 12, CycleRule.IMM)),
                                      domestic.F(dt, Dt.Add(dt, 1, TimeUnit.Years)));
        double ddf = domestic.Interpolate(environment.AsOf, dt);
        for (int i = 1; i < environment.DiscountCurves.Length; ++i)
        {
          double numer = 1.0;
          var df = environment.DiscountCurves[i];
          path.EvolveDiscount(i, dtFrac, t, df, out fxRate, out numer);
          environment.ApplyDiscountJump(i, dt);
          if (RateCurveLogger.IsDebugEnabled)
            RateCurveLogger.DebugFormat("{0} {1} 3M:{2} 6M:{3} 1Y:{4} 5Y:{5}", dt, df.Name,
                                        df.F(dt, Dt.AddMonths(dt, 3, CycleRule.IMM)),
                                        df.F(dt, Dt.AddMonths(dt, 6, CycleRule.IMM)),
                                        df.F(dt, Dt.AddMonths(dt, 12, CycleRule.IMM)),
                                        df.F(dt, Dt.Add(dt, 5, TimeUnit.Years)));
          double fdf = df.Interpolate(environment.AsOf, dt);
          var fx = environment.FxRates[i - 1];
          fx.Update(dt, df.Ccy, domestic.Ccy, fxRate * fdf / ddf);
          environment.ApplyFxJump(i - 1, dt);
          //Math.Min(Math.Max(fdf / ddf, 5e-2), 20));
          if (FxRateLogger.IsDebugEnabled)
            FxRateLogger.DebugFormat("{0} {1}", dt, fx);
        }
      }
      // update foreign/foreign fx rates by triangulation through domestic ccy 
      for (int i = environment.DiscountCurves.Length - 1; i < environment.FxRates.Length; ++i)
      {
        var domesticCcy = environment.DiscountCurves[0].Ccy;
        var f1f2 = environment.FxRates[i];

        var dc1 = environment.DiscountCurves.First(dc => dc.Ccy == f1f2.FromCcy);
        var dc2 = environment.DiscountCurves.First(dc => dc.Ccy == f1f2.ToCcy);
        var f1Domestic = environment.FxRates.First(f1 => (f1.FromCcy == f1f2.FromCcy && f1.ToCcy == domesticCcy)
                                                         || (f1.ToCcy == f1f2.FromCcy && f1.FromCcy == domesticCcy)).GetRate(f1f2.FromCcy, domesticCcy);

        var domesticF2 = environment.FxRates.First(f2 => (f2.FromCcy == f1f2.ToCcy && f2.ToCcy == domesticCcy)
                                                         || (f2.ToCcy == f1f2.ToCcy && f2.FromCcy == domesticCcy)).GetRate(domesticCcy, f1f2.ToCcy);

        var fx = f1Domestic * domesticF2;
        f1f2.Update(dt, f1f2.FromCcy, f1f2.ToCcy, fx);
        environment.ApplyFxJump(i, dt);
        if (FxRateLogger.IsDebugEnabled)
          FxRateLogger.DebugFormat("{0} {1}", dt, f1f2);
      }

      for (int i = 0; i < environment.CreditCurves.Length; ++i)
      {
        var sc = environment.CreditCurves[i];
        path.EvolveCredit(i, dtFrac, t, sc);
        environment.ApplyCreditJump(i, dt);
        if (SurvivalCurveLogger.IsDebugEnabled)
          SurvivalCurveLogger.DebugFormat("{0} {1} 1Y:{2} 5Y:{3}", dt, sc.Name,
                                          sc.ImpliedSpread(Dt.CDSMaturity(dt, "1Y")),
                                          sc.ImpliedSpread(Dt.CDSMaturity(dt, "5Y")));
      }
      for (int i = 0; i < environment.ForwardCurves.Length; ++i)
      {
        var fc = environment.ForwardCurves[i];
        path.EvolveForward(i,dtFrac, t, fc);
        environment.ApplyForwardJump(i, dt);
        if (FwdRateLogger.IsDebugEnabled)
        {
          FwdRateLogger.DebugFormat("{0} {1} 3M:{2} 6M:{3} 1Y:{4} 5Y:{5}", dt, fc.Name,
                                    fc.Interpolate(Dt.AddMonths(dt, 3, CycleRule.IMM)),
                                    fc.Interpolate(Dt.AddMonths(dt, 6, CycleRule.IMM)),
                                    fc.Interpolate(dt, Dt.AddMonths(dt, 12, CycleRule.IMM)),
                                    fc.Interpolate(dt, Dt.Add(dt, 5, TimeUnit.Years)));
        }
      }
      for (int i = 0; i < environment.SpotBasedCurves.Length; ++i)
      {
        var sp = environment.SpotBasedCurves[i] as IForwardPriceCurve;
        if (sp == null)
          continue;
        var df = sp.DiscountCurve.Interpolate(environment.AsOf, dt);
        sp.Spot.Spot = dt;
        sp.Spot.Value = path.EvolveSpot(i, dtFrac, t) / df;
        environment.ApplySpotJump(i, dt);
        if (SpotLogger.IsDebugEnabled)
        {
          SpotLogger.DebugFormat("{0} {1} {2}", dt, sp.Spot.Name, sp.Spot.Value);
        }
      }
    }
    
  }

  #endregion
}