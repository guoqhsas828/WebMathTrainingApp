using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Calibration utils
  /// </summary>
  public static partial class CalibrationUtils
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CalibrationUtils));

    #region CalibrationLog

    /// <summary>
    /// Calibration perfomance and implied results
    /// </summary>
    public class CalibrationLog
    {
      #region Data

      private readonly Curve impliedQuotes_;

      #endregion

      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="asOf">Calibration as of date</param>
      /// <param name="expiries">Expiry dates</param>
      /// <param name="exception">Exception</param>
      /// <param name="impliedQuotes">Implied quotes</param>
      public CalibrationLog(Dt asOf, Dt[] expiries, string exception, double[] impliedQuotes)
      {
        AsOf = asOf;
        impliedQuotes_ = new Curve(asOf);
        impliedQuotes_.Add(expiries, impliedQuotes);
        Expiries = expiries;
        Exception = exception;
      }

      #endregion

      #region Properties

      /// <summary>
      /// As of date
      /// </summary>
      internal Dt AsOf { get; set; }

      /// <summary>
      /// Expiry dates
      /// </summary>
      internal Dt[] Expiries { get; set; }

      /// <summary>
      /// Exception messages which may arise during calibration. Null if performed as expected
      /// </summary>
      public string Exception { get; set; }

      #endregion

      #region Methods

      /// <summary>
      /// Implied quote for the expiry point (or linear interp if point outside the calibrated grid)  
      /// </summary>
      /// <param name="expiry">Expiry</param>
      /// <returns>Implied quote</returns>
      public double ImpliedQuote(Dt expiry)
      {
        return impliedQuotes_.Interpolate(expiry);
      }

      /// <summary>
      /// Produce a string representation of the CalibrationLog class
      /// </summary>
      /// <returns>a string representation of the CalibrationLog instance</returns>
      public override string ToString()
      {
        var builder = new StringBuilder();
        builder.Append(string.Format("Calibration as of date {0}{1}", AsOf.ToString(), Environment.NewLine));
        if (this.Expiries != null)
        {
          foreach (var dt in Expiries)
          {
            builder.Append(string.Format("Expiry dates {0}{1}", dt.ToString(), Environment.NewLine));
          }
        }
        if (Exception == null)
        {
          builder.Append(string.Format("Calibration was successful{0}", Environment.NewLine));
        }
        else
        {
          builder.Append(string.Format("Exception occured within Calibration giving rise to error: {0}{1}", Exception, Environment.NewLine));
        }

        builder.Append(string.Format("Implied Quotes {0}{1}", impliedQuotes_ != null ? impliedQuotes_.ToString() : "Implied Quote is null", Environment.NewLine));
        return builder.ToString();
      }

      #endregion
    }

    #endregion

    #region CalibrationLogCollection

    /// <summary>
    /// Record calibration results
    /// </summary>
    public class CalibrationLogCollection
    {
      #region Data

      private readonly Dictionary<string, CalibrationLog> data_;

      #endregion

      #region Constructors

      /// <summary>
      /// Default constructor
      /// </summary>
      public CalibrationLogCollection()
        : this(1)
      {}

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="capacity">Capacity</param>
      public CalibrationLogCollection(int capacity)
      {
        data_ = new Dictionary<string, CalibrationLog>(capacity);
      }

      #endregion

      #region Methods
      /// <summary>
      /// Copy to destination
      /// </summary>
      /// <param name="destination"></param>
      public void Copy(CalibrationLogCollection destination)
      {
        foreach (var tuple in data_)
        {
          destination.data_[tuple.Key] = tuple.Value;
        }
      }


      /// <summary>
      /// Access CalibrationLog
      /// </summary>
      /// <param name="id">Id</param>
      /// <returns>Calibration Log</returns>
      public CalibrationLog this[string id]
      {
        get { return data_[id]; }
      }

      /// <summary>
      /// Get implied quote for given calibration result, expiry and strike
      /// </summary>
      /// <param name="result">Result name</param>
      /// <param name="expiry">Expiry date</param>
      /// <returns>Implied quote</returns>
      public double ImpliedQuote(string result, Dt expiry)
      {
        CalibrationLog log;
        if (data_.TryGetValue(result, out log))
          return log.ImpliedQuote(expiry);
        throw new ArgumentException(String.Format("{0} not found", result));
      }

      /// <summary>
      /// Produces a string repreentation of the CalibrationLogCollection class
      /// </summary>
      /// <returns>a string repreentation of the CalibrationLogCollection instance</returns>
      public override string ToString()
      {
        var builder = new StringBuilder();
        foreach (var tuple in data_)
        {
          builder.Append(string.Format("CalibrationLog entry with key {0} and value {1}{2}", tuple.Key, tuple.Value.ToString(), Environment.NewLine));
        }
        return builder.ToString();
      }

      /// <summary>
      /// Add element log to the collection
      /// </summary>
      /// <param name="id">Log id</param>
      /// <param name="log">Log object</param>
      internal void Add(string id, CalibrationLog log)
      {
        data_[id] = log;
      }

      /// <summary>
      /// Clear data
      /// </summary>
      internal void Clear()
      {
        data_.Clear();
      }

      #endregion

      #region Properties

      /// <summary>
      /// Id of CalibrationLog objects
      /// </summary>
      public string[] Id
      {
        get { return data_.Keys.ToArray(); }
      }

      #endregion
    }

    #endregion

    #region Utilities

    private static double Recovery(this SurvivalCurve survivalCurve, Dt dt)
    {
      if (survivalCurve == null)
        return 0.0;
      if (survivalCurve.SurvivalCalibrator == null)
        return 0.0;
      if (survivalCurve.SurvivalCalibrator.RecoveryCurve == null)
        return 0.0;
      return survivalCurve.SurvivalCalibrator.RecoveryCurve.Interpolate(dt);
    }
    #endregion
    
    #region CreditMarket

    /// <summary>
    /// Calibrate credit volatility from quoted cds/cdx options
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="underlier">Terms of the underlying credit index</param>
    /// <param name="credit">Underlying credit</param>
    /// <param name="recovery">Recovery rate</param>
    /// <param name="atmBlackVol">ATM Black vol quotes </param>
    /// <param name="discountCurve">Discount curves</param>
    /// <param name="logCollection">Log of calibration results</param>
    /// <param name="quadPts">Number of quadrature points</param>
    /// <remarks> 
    /// The  dynamic copula model assumes that default time i is driven by the terminal value of 
    /// <m>M^i_t := \int_0^\infty \frac{\lambda(s)}{1 + \int_0^s\lambda^2(u)\,du}dW^i_s</m> with <m>\lambda_i(t)>0</m> 
    /// as follows <m>\tau_i := F_i^{-1}\left(\Phi\!\left(\sqrt{1 - \rho^2_i}Z_i + \rho_i M^i_\infty\right)\right),</m> where <m>F_i(\cdot)</m> is the marginal distribution of <m>\tau_i</m>,  
    /// <m>\Phi(\cdot)</m> is the standard gaussian cdf, <m>Z_i</m> are standard gaussians random variables and <m>W^i_t</m> correlated Brownian motions
    /// </remarks> 
    public static VolatilityCurve CalibrateCreditVolatilities(
      Dt asOf,
      CDS underlier,
      SurvivalCurve credit,
      double recovery,
      VolatilityCurve atmBlackVol,
      DiscountCurve discountCurve,
      CalibrationLogCollection logCollection,
      int quadPts)
    {
      var schedule = underlier.Schedule;
      var couponTimes = new List<double> {(asOf <= schedule.GetPeriodStart(0)) ? Dt.FractDiff(asOf, schedule.GetPeriodStart(0)) : 0.0};
      var accruals = new List<double>();
      for (int j = 0; j < schedule.Count; ++j)
      {
        if (schedule.GetPaymentDate(j) > asOf)
        {
          couponTimes.Add(Dt.FractDiff(asOf, schedule.GetPaymentDate(j)));
          accruals.Add(schedule.Fraction(j, underlier.DayCount));
        }
      }
      var tenors = (atmBlackVol.Tenors == null) ? null : atmBlackVol.Tenors.Where(t => t.Maturity < underlier.Maturity).ToArray();
      if (tenors == null || tenors.Length == 0)
        throw new ArgumentException("No valid CDS option expiries found");
      var expiryDates = tenors.Select(t => t.Maturity).ToArray();
      var expiries = tenors.Select(t => Dt.FractDiff(asOf, t.Maturity)).ToArray();
      var volQuotes = tenors.Select(t => atmBlackVol.Interpolate(t.Maturity)).ToArray();
      var target = new Curve(asOf);
      foreach (var t in tenors)
        target.Add(t.Maturity, 0.0);
      var implied = new double[expiries.Length];
      recovery = (recovery > 0) ? recovery : credit.Recovery(underlier.Maturity);
      string message = null;
      try
      {
        Native.CalibrationUtils.CalibrateCreditVol(credit, recovery, discountCurve, couponTimes.ToArray(), accruals.ToArray(), expiries, volQuotes, target,
                           quadPts, implied);
      }
      catch (Exception ex)
      {
        message = ex.Message;
      }
      logCollection.Add(credit.Name, new CalibrationLog(asOf, expiryDates, message, implied));
      var retVal = new VolatilityCurve(asOf);
      for (int i = 0; i < target.Count; ++i)
        retVal.AddVolatility(target.GetDt(i), target.GetVal(i));
      retVal.Fit();
      return retVal;
    }

    #endregion

    #region SpotAsset

    /// <summary>
    /// Calibrate spot asset volatility curve from forward asset vol
    /// </summary>
    /// <param name="description">Log id</param>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Forward Libor tenors</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="spot">Spot price</param>
    /// <param name="atmBlackVol">Quoted ATM Black vol of forward price</param>
    /// <param name="liborVolatilities">Libor volatilities</param>
    /// <param name="liborFactors">Libor factor loadings</param>
    /// <param name="spotFactors">spot asset factor loadings</param>
    ///<param name="logCollection">Calibration info</param>
    /// <returns>True if calibration succeded</returns>
    public static VolatilityCurve CalibrateSpotVolatilities(
      string description,
      Dt asOf,
      Dt[] forwardTenors,
      DiscountCurve discountCurve,
      ISpot spot,
      VolatilityCurve atmBlackVol,
      VolatilityCurve[] liborVolatilities,
      double[,] liborFactors,
      double[,] spotFactors,
      CalibrationLogCollection logCollection)
    {
      if (liborFactors == null || spotFactors == null)
        throw new ArgumentException("Factor loadings must be non null");
      if (liborVolatilities == null)
        throw new ArgumentException("Libor vols not found");
      var resetTimes = Array.ConvertAll(forwardTenors, dt => Dt.FractDiff(asOf, dt));
      var modelVols = new double[resetTimes.Length];
      var spotVolatility = new Curve(asOf);
      foreach(var dt  in forwardTenors)
        spotVolatility.Add(dt, 0.0);
      string message = null;
      try
      {
        Native.CalibrationUtils.CalibrateSpotVol(spotFactors.GetLength(1), resetTimes, discountCurve, liborFactors, liborVolatilities, spot.Value,
                          spotFactors, spotVolatility, atmBlackVol, liborVolatilities.All(v=>v.DistributionType == DistributionType.Normal), modelVols);
      }
      catch (Exception ex)
      {
        message = ex.Message;
      }
      var retVal = new VolatilityCurve(asOf) {DistributionType = DistributionType.LogNormal};
      for (int i = 0; i < spotVolatility.Count; ++i)
        retVal.AddVolatility(spotVolatility.GetDt(i), spotVolatility.GetVal(i));
      retVal.Fit();
      if (logCollection == null)
        logCollection = new CalibrationLogCollection(1);
      var modelImpliedVols = new Curve(asOf);
      modelImpliedVols.Add(forwardTenors, modelVols);
      var expiries = atmBlackVol.Tenors.Select(t => t.Maturity).ToArray();
      logCollection.Add(description, new CalibrationLog(asOf, expiries, message, Array.ConvertAll(expiries, modelImpliedVols.Interpolate)));
      return retVal;
    }

    /// <summary>
    /// Calibrate flat spot asset volatility and factor loadings in the projective LMM framework
    /// </summary>
    /// <param name="description">Log id</param>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Reset dates</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="spot">Spot asset</param>
    /// <param name="atmBlackVol">Quoted ATM Black vol of forward asset price</param>
    /// <param name="spotFactors">Overwritten by spot factor loadings</param>
    /// <param name="liborVols">Caplet vols</param>
    /// <param name="liborFactors">Caplet factor loadings</param>
    /// <param name="logCollection">CalibrationInfo</param>
    /// <param name="initialGuess">initialGuess[0] = spot factor correlation, initialGuess[1] = spot price vol</param>
    /// <returns>Spot price vol</returns>
    ///<remarks> 
    /// The projective model is based on markov projections of the libor rate processes, and requires a separable libor instantaneous volatility of the form <m>\sigma_i(t):=\psi_i \phi(t)</m>
    /// The projective spot model assumes that the libor rates and spot asset price are each driven by one gaussian factor, namely <m>M^i_t := \int_0^t \phi_{i}(s)dW^i_s, </m>  with <m>\langle W^i,W^j\rangle_t = \rho t</m> 
    /// </remarks>
    public static VolatilityCurve CalibrateSpotVolatilities(
      string description,
      Dt asOf,
      Dt[] forwardTenors,
      DiscountCurve discountCurve,
      ISpot spot,
      VolatilityCurve atmBlackVol,
      VolatilityCurve[] liborVols,
      double[,] liborFactors,
      ref double[,] spotFactors,
      CalibrationLogCollection logCollection,
      double[] initialGuess)
    {
      var resetTimes = Array.ConvertAll(forwardTenors, dt => Dt.FractDiff(asOf, dt));
      var modelVols = new double[resetTimes.Length];
      var spotVol = new Curve(asOf, 0.0);
      var spotFactorLoadings = new double[2];
      string message = null;
      try
      {
        Native.CalibrationUtils.CalibrateSemiAnalyticSpotVol(resetTimes, discountCurve, liborFactors, liborVols, spot.Value, spotVol, spotFactorLoadings, atmBlackVol, liborVols.All(v=>v.DistributionType == DistributionType.Normal),
          modelVols, initialGuess ?? new double[0]);
      }
      catch (Exception ex)
      {
        message = ex.Message;
      }
      var retVal = new VolatilityCurve(asOf) {DistributionType = DistributionType.LogNormal};
      for (int i = 0; i < spotVol.Count; ++i)
        retVal.AddVolatility(spotVol.GetDt(i), spotVol.GetVal(i));
      retVal.Fit();
      spotFactors = new double[1, spotFactorLoadings.Length];
      for (int i = 0; i < spotFactorLoadings.Length; ++i)
        spotFactors[0, i] = spotFactorLoadings[i];
      if (logCollection == null)
        logCollection = new CalibrationLogCollection(1);
      var modelImpliedVols = new Curve(asOf);
      modelImpliedVols.Add(forwardTenors, modelVols);
      var expiries = atmBlackVol.Tenors.Select(t => t.Maturity).ToArray();
      logCollection.Add(description, new CalibrationLog(asOf, expiries, message, Array.ConvertAll(expiries, modelImpliedVols.Interpolate)));
      return retVal;
    }

    #endregion

    #region FxMarket

    /// <summary>
    /// Calibrate spot FX volatility curve from FX option data
    /// </summary>
    /// <param name="description">Log id</param>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Forward Libor tenors</param>
    /// <param name="domestic">Domestic libor</param>
    /// <param name="foreign">Foreign libor</param>
    /// <param name="fxRate">Spot FX rate</param>
    /// <param name="atmBlackVol">Quoted ATM Black vol of forward FX</param>
    /// <param name="domesticLiborVolatilities">domestic Libor volatilities</param>
    /// <param name="domesticLiborFactors">foreign Libor factor loadings</param>
    /// <param name="foreignLiborVolatilities">foreign Libor volatilities</param>
    /// <param name="foreignLiborFactors">foreign Libor factor loadings</param>
    /// <param name="fxFactors">FX rate factor loadings</param>
    ///<param name="logCollection">Calibration info</param>
    /// <returns>True if calibration succeded</returns>
    public static VolatilityCurve CalibrateFxVolatilities(
      string description,
      Dt asOf,
      Dt[] forwardTenors,
      DiscountCurve domestic,
      DiscountCurve foreign,
      FxRate fxRate,
      VolatilityCurve atmBlackVol,
      VolatilityCurve[] domesticLiborVolatilities,
      VolatilityCurve[] foreignLiborVolatilities,
      double[,] domesticLiborFactors,
      double[,] foreignLiborFactors,
      double[,] fxFactors,
      CalibrationLogCollection logCollection)
    {
      if (domesticLiborFactors == null || foreignLiborVolatilities == null || fxFactors == null)
        throw new ArgumentException("Factor loadings must be non null");
      if (domesticLiborVolatilities == null || foreignLiborVolatilities == null)
        throw new ArgumentException("Libor vols not found");
      var resetTimes = Array.ConvertAll(forwardTenors, dt => Dt.FractDiff(asOf, dt));
      var modelVols = new double[resetTimes.Length];
      var fxVolatility = new Curve(asOf);
      foreach (var dt in forwardTenors)
        fxVolatility.Add(dt, 0.0);
      string message = null;
      try
      {
        Native.CalibrationUtils.CalibrateFxVol(fxFactors.GetLength(1), resetTimes, domestic, foreign, domesticLiborFactors, domesticLiborVolatilities,
                       foreignLiborFactors, foreignLiborVolatilities, fxRate.GetRate(foreign.Ccy, domestic.Ccy),
                       fxFactors, fxVolatility, atmBlackVol, domesticLiborVolatilities.All(v => v.DistributionType == DistributionType.Normal),
                       foreignLiborVolatilities.All(v => v.DistributionType == DistributionType.Normal), modelVols);
      }
      catch (Exception ex)
      {
        message = ex.Message;
      }
      var retVal = new VolatilityCurve(asOf) {DistributionType = DistributionType.LogNormal};
      for (int i = 0; i < fxVolatility.Count; ++i)
        retVal.AddVolatility(fxVolatility.GetDt(i), fxVolatility.GetVal(i));
      retVal.Fit();
      if (logCollection == null)
        logCollection = new CalibrationLogCollection(1);
      var modelImpliedVols = new Curve(asOf);
      modelImpliedVols.Add(forwardTenors, modelVols);
      var expiries = atmBlackVol.Tenors.Select(t => t.Maturity).ToArray();
      logCollection.Add(description, new CalibrationLog(asOf, expiries, message, Array.ConvertAll(expiries, modelImpliedVols.Interpolate)));
      return retVal;
    }

    /// <summary>
    /// Calibrate flat spot fx volatility and fx factor loadings by an optimization procedure in the projective 2-currency LMM framework
    /// </summary>
    /// <param name="description">Log id</param>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Reset dates</param>
    /// <param name="domestic">Domestic libor</param>
    /// <param name="foreign">Foreign libor</param>
    /// <param name="fxRate">Spot FX rate</param>
    /// <param name="atmBlackVol">Quoted ATM Black vol of forward FX</param>
    /// <param name="domesticLiborVolatilities">domestic Libor volatilities</param>
    /// <param name="domesticLiborFactors">foreign Libor factor loadings</param>
    /// <param name="foreignLiborVolatilities">foreign Libor volatilities</param>
    /// <param name="foreignLiborFactors">foreign Libor factor loadings</param>
    /// <param name="fxFactors">Overwritten by calibrated fx factors</param>
    /// <param name="logCollection">CalibrationInfo</param>
    /// <param name="initialGuess">initialGuess[0] = fx factor correlation <m>\rho^2_{fx}</m>, initialGuess[1] = spot fx volatility coefficient <m>\sigma_{fx}</m> </param>
    /// <returns>True if calibration succeded</returns>
    /// <remarks> 
    /// The projective model is based on markov projections of the libor rate processes, and requires a separable libor instantaneous volatility of the form <m>\sigma_i(t):=\psi_i \phi(t)</m>
    /// The projective two-currency LMM assumes that each libor family is driven by one factor, namely <m>M^i_t := \int_0^t \phi_{i}(s)dW^i_s, </m>  with <m>\langle W^i,W^j\rangle_t = \rho t</m> 
    /// The spot fx rate is driven by an affine combination of the driving martingales, so that <m>\sigma_{fx}(t) :=  \sigma_{fx} (\rho_{fx} \phi_1(t) + \sqrt{1 - \rho_{fx}^2}\phi_2(t)).</m> </remarks>
    public static VolatilityCurve CalibrateFxVolatilities(
      string description,
      Dt asOf,
      Dt[] forwardTenors,
      DiscountCurve domestic,
      DiscountCurve foreign,
      FxRate fxRate,
      VolatilityCurve atmBlackVol,
      VolatilityCurve[] domesticLiborVolatilities,
      VolatilityCurve[] foreignLiborVolatilities,
      double[,] domesticLiborFactors,
      double[,] foreignLiborFactors,
      ref double[,] fxFactors,
      CalibrationLogCollection logCollection,
      double[] initialGuess)
    {
      //interpolate Fx quotes at libor reset dates
      if (domesticLiborVolatilities == null || foreignLiborVolatilities == null)
        throw new ArgumentException("Libor vols not found");
      if (initialGuess != null && initialGuess.Length >= 2)
        initialGuess = (double[])initialGuess.Clone();
      else
        initialGuess = new double[0];
      var times = Array.ConvertAll(forwardTenors, dt => Dt.FractDiff(asOf, dt));
      var modelVols = new double[times.Length];
      var fxVol = new Curve(asOf, 0.0);
      var fxFactorLoadings = new double[2];
      string message = null;
      try
      {
        Native.CalibrationUtils.CalibrateSemiAnalyticFxVol(times, domestic, foreign, domesticLiborFactors, domesticLiborVolatilities, foreignLiborFactors, foreignLiborVolatilities,
                                   fxRate.GetRate(foreign.Ccy, domestic.Ccy), fxVol, fxFactorLoadings, atmBlackVol,
                                   domesticLiborVolatilities.All(v => v.DistributionType == DistributionType.Normal),
                                   foreignLiborVolatilities.All(v => v.DistributionType == DistributionType.Normal), modelVols, initialGuess);
      }
      catch (Exception ex)
      {
        message = ex.Message;
      }
      fxFactors = new double[1,fxFactorLoadings.Length];
      for (int i = 0; i < fxFactorLoadings.Length; ++i)
        fxFactors[0, i] = fxFactorLoadings[i];
      var retVal = new VolatilityCurve(asOf) {DistributionType = DistributionType.LogNormal};
      for (int i = 0; i < fxVol.Count; ++i)
        retVal.AddVolatility(fxVol.GetDt(i), fxVol.GetVal(i));
      retVal.Fit();
      if (logCollection == null)
        logCollection = new CalibrationLogCollection(1);
      var modelImpliedVols = new Curve(asOf);
      modelImpliedVols.Add(forwardTenors, modelVols);
      var expiries = atmBlackVol.Tenors.Select(t => t.Maturity).ToArray();
      logCollection.Add(description, new CalibrationLog(asOf, expiries, message, Array.ConvertAll(expiries, modelImpliedVols.Interpolate)));
      return retVal;
    }

    #endregion

    #region LiborMarket

    /// <summary>
    /// Map forward forward volatilities for a standard set of tenors to caplet volatilities (square root average integrated volatility) for a set of bespoke tenors
    /// </summary>
    /// <param name="asOf">asOf date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="standardCapletTenors">Standard set of tenors</param>
    /// <param name="fwdFwdVols">Forward forward volatilities for standard tenors</param>
    /// <param name="bespokeCapletTenors">Bespoke set of tenors</param>
    /// <param name="bespokeCapletFactors">Factor loadings for bespoke rates</param>
    /// <param name="curveDates">Running time grid</param>
    /// <param name="distributionType">Distribution type</param>
    /// <returns>Vols for bespoke family of libor rates</returns>
    public static IEnumerable<VolatilityCurve> MapCapletVolatilities(Dt asOf, DiscountCurve discountCurve, Dt[] standardCapletTenors, Curve[] fwdFwdVols,
                                                                     Dt[] bespokeCapletTenors, double[,] bespokeCapletFactors, Dt[] curveDates,
                                                                     DistributionType distributionType)
    {
      var bespokeVols = bespokeCapletTenors.Select((dt, i) =>
                                                   {
                                                     var retVal = new Curve(asOf);
                                                     var reset = (i == 0) ? asOf : bespokeCapletTenors[i - 1];
                                                     if (curveDates != null)
                                                     {
                                                       foreach (var curveDt in curveDates)
                                                       {
                                                         if (curveDt <= retVal.AsOf)
                                                           continue;
                                                         if (curveDt >= reset)
                                                           break;
                                                         retVal.Add(curveDt, 0.0);
                                                       }
                                                     }
                                                     retVal.Add(reset, 0.0);
                                                     return retVal;
                                                   }).ToArray();
      Native.CalibrationUtils.MapCapletVolatilities(discountCurve, standardCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), fwdFwdVols,
                            bespokeCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), bespokeCapletFactors, bespokeVols,
                            distributionType == DistributionType.Normal);
      return bespokeVols.Select(c =>
                                {
                                  var retVal = new VolatilityCurve(c.AsOf) {DistributionType = distributionType};
                                  for (int i = 0; i < c.Count; ++i)
                                    retVal.AddVolatility(c.GetDt(i), c.GetVal(i));
                                  retVal.Fit();
                                  return retVal;
                                });
    }


    /// <summary>
    /// Calibrate bespoke caplet volatilities and factor loadings from ATM swaption volatility surface and swap rate factor loadings
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Libor curve</param>
    /// <param name="swaptionExpiries">Swaption expiry tenors</param>
    /// <param name="swaptionTenors">Swaption underlier tenors</param>
    /// <param name="atmSwaptionVolSurface">Swaption ATM vol surface</param>
    /// <param name="swapRateEffective">Effective date of swap rate for factor loadings calibration</param>
    /// <param name="swapRateMaturities">Maturity date of swap rate for factor loadings calibration</param>
    /// <param name="swapRateFactorLoadings">Historical factor loadings of swap rates</param>
    /// <param name="bespokeCapletTenors">Tenors of bespoke caplets</param>
    /// <param name="bespokeCapletVols">Overridden by bespoke caplet vols</param>
    /// <param name="bespokeCapletFactors">Overridden by bespoke caplet factors</param>
    /// <param name="distributionType">Distribution type</param>
    /// <param name="separableVol">Construct rank one (in case of perfect correlation) separable volatilities</param>
    public static void FromAtmSwaptionVolatilitySurface(Dt asOf, DiscountCurve discountCurve, Tenor[] swaptionExpiries, Tenor[] swaptionTenors, double[,] atmSwaptionVolSurface, Dt[] swapRateEffective, Dt[] swapRateMaturities,
                                                          double[,] swapRateFactorLoadings, Dt[] bespokeCapletTenors, out VolatilityCurve[] bespokeCapletVols, out double[,] bespokeCapletFactors, DistributionType distributionType,
      bool separableVol)
    {
      FromAtmSwaptionVolatilitySurface(asOf, discountCurve, null,
        swaptionExpiries, swaptionTenors, atmSwaptionVolSurface,
        swapRateEffective, swapRateMaturities, swapRateFactorLoadings,
        bespokeCapletTenors, out bespokeCapletVols, out bespokeCapletFactors,
        distributionType, separableVol);
    }

    internal static void FromAtmSwaptionVolatilitySurface(Dt asOf,
      DiscountCurve discountCurve, DiscountCurve projection,
      Tenor[] swaptionExpiries, Tenor[] swaptionTenors, double[,] atmSwaptionVolSurface,
      Dt[] swapRateEffective, Dt[] swapRateMaturities,
      double[,] swapRateFactorLoadings, Dt[] bespokeCapletTenors,
      out VolatilityCurve[] bespokeCapletVols, out double[,] bespokeCapletFactors,
      DistributionType distributionType,
      bool separableVol)
    {
      bespokeCapletFactors = new double[bespokeCapletTenors.Length,swapRateFactorLoadings.GetLength(1)];
      var bespokeVols = bespokeCapletTenors.Select((dt, i) =>
                                                   {
                                                     var retVal = new Curve(asOf);
                                                     if (i == 0)
                                                     {
                                                       retVal.Add(asOf, 0.0);
                                                       return retVal;
                                                     }
                                                     for (int j = 0; j < i; ++j)
                                                       retVal.Add(bespokeCapletTenors[j], 0.0);
                                                     return retVal;
                                                   }).ToArray();
      try
      {
        Native.CalibrationUtils.CalibrateFromSwaptionVolatility(discountCurve, projection, swapRateEffective.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(),
                                        swapRateMaturities.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), swapRateFactorLoadings, atmSwaptionVolSurface,
                                        swaptionExpiries.Select(t => Dt.FractDiff(asOf, Dt.Add(asOf, t))).ToArray(),
                                        swaptionTenors.Select(t => (double)t.Days).ToArray(),
                                        bespokeCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), bespokeVols, bespokeCapletFactors,
                                        (distributionType == DistributionType.Normal), separableVol);
      }
      catch (Exception)
      {
        throw new ArgumentException(
          String.Format(
            "Calibration of {0} Libor Rate factor loadings/ volatilities from {0} Swap rate factor loading and swaption volatility surface has failed.",
            discountCurve.Ccy));
      }
      bespokeCapletVols = bespokeVols.Select(c =>
                                             {
                                               var retVal = new VolatilityCurve(c.AsOf)
                                               {
                                                 DistributionType = distributionType,
                                                 Interp = new SquareLinearVolatilityInterp(),
                                               };
                                               for (int i = 0; i < c.Count; ++i)
                                                 retVal.AddVolatility(c.GetDt(i), c.GetVal(i));
                                               retVal.Fit();
                                               return retVal;
                                             }).ToArray();
    }


    /// <summary>
    /// Calibrate bespoke caplet volatilities from ATM swaption volatility surface given factor loadings
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Libor curve</param>
    /// <param name="swaptionExpiries">Swaption expiry tenors</param>
    /// <param name="swaptionTenors">Swaption underlier tenors</param>
    /// <param name="atmSwaptionVolSurface">Swaption ATM vol surface</param>
    /// <param name="bespokeCapletTenors">Tenors of bespoke caplets</param>
    /// <param name="bespokeCapletFactors">Overridden by bespoke caplet factors</param>
    /// <param name="distributionType">Distribution type</param>
    /// <param name="separableVol">Construct rank one (in case of perfect correlation) separable volatilities</param>
    public static IEnumerable<VolatilityCurve> FromAtmSwaptionVolatilitySurface(Dt asOf, DiscountCurve discountCurve, Tenor[] swaptionExpiries, Tenor[] swaptionTenors, double[,] atmSwaptionVolSurface, Dt[] bespokeCapletTenors, double[,] bespokeCapletFactors, DistributionType distributionType, bool separableVol)
    {
      var bespokeVols = bespokeCapletTenors.Select((dt, i) =>
                                                   {
                                                     var retVal = new Curve(asOf);
                                                     if (i == 0)
                                                     {
                                                       retVal.Add(asOf, 0.0);
                                                       return retVal;
                                                     }
                                                     for (int j = 0; j < i; ++j)
                                                       retVal.Add(bespokeCapletTenors[j], 0.0);
                                                     return retVal;
                                                   }).ToArray();
      try
      {
        Native.CalibrationUtils.CalibrateFromSwaptionVolatility(discountCurve, atmSwaptionVolSurface, swaptionExpiries.Select(t => Dt.FractDiff(asOf, Dt.Add(asOf, t))).ToArray(),
                                        swaptionTenors.Select(t => (double)t.Days).ToArray(),
                                        bespokeCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), bespokeVols, bespokeCapletFactors,
                                        (distributionType == DistributionType.Normal), separableVol);

      }
      catch (Exception)
      {
        throw new ArgumentException(
          String.Format(
            "Calibration of {0} Libor Rate volatilities from {0} swaption volatility surface has failed.",
            discountCurve.Ccy));
      }
      return bespokeVols.Select(c =>
                                {
                                  var retVal = new VolatilityCurve(c.AsOf) {DistributionType = distributionType};
                                  for (int i = 0; i < c.Count; ++i)
                                    retVal.AddVolatility(c.GetDt(i), c.GetVal(i));
                                  retVal.Fit();
                                  return retVal;
                                });
    }


    /// <summary>
    /// Calibrate forward rate factor loadings and volatilities from caplet volatility cube and proxy swap rate factor loadings.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="standardCapletTenors">Standard caplet tenors, with reset frequency three months or six months</param>
    /// <param name="fwdFwdVols">Calibrated forward forward volatilities for set of standard tenors</param>
    /// <param name="bespokeCapletTenors">Bespoke tenors for forward rates.</param>
    /// <param name="swapRateEffective">Effective date of the swap</param>
    /// <param name="swapRateMaturities">Swap rate maturities</param>
    /// <param name="swapRateFactorLoadings">Swap rate factor loadings</param>
    /// <param name="curveDates">Dates for bespoke caplet volatility curves</param>
    /// <param name="distributionType">Distribution type</param>
    /// <param name="bespokeCapletVols">Bespoke volatilities for forward rates</param>
    /// <returns>Bespoke factor loadings for forward rates</returns>
    /// <remarks>
    /// In the definition of the market environment, a set of forward tenors, <m>T_0, T_1, \cdots, T_n, </m> 
    /// are provided. Let <m>F_i(t)</m> denote the forward LIBOR rate <m>F(t; T_{i-1}, T_i)</m> from <m>T_{i-1}</m>
    /// to <m>T_i</m> as of time <m>t</m>. The forward rates are assumed to follow either Lognormal distribution:
    /// <math>
    /// d F_i(t) = \mu_i(t)F_i(t) dt + s_i F_i(t) \sum_{j=1}^d \varphi_{ij} dW_j(t), \ \ \ \ \ i = 1, \cdots, n 
    /// </math>
    /// or Normal distribution:
    /// <math>
    /// d F_i(t) = \mu_i(t) dt + s_i \sum_{j=1}^d \varphi_{ij} dW_j(t), \ \ \ \ \ i = 1, \cdots, n 
    /// </math>
    /// , where <m>d</m> is the number of factors, <m>[\varphi_{ij}]</m> are bespoke factor loadings with 
    /// <m>\sum_{j=1}^d \varphi_{ij}^2 = 1,</m> and <m>s_i</m> is the constant bespoke volatility for forward 
    /// rate <m>F_i(t)</m>. 
    /// <para>
    /// For one-factor model <m>(d = 1)</m>, each bespoke factor loading <m>\varphi_{ij}</m> is equal to <m>1</m>.
    /// </para>
    /// <para>
    /// For multi-factor model <m>(d \geq 1)</m>, the bespoke factor loading <m>[\varphi_{ij}]</m> is a
    /// <m>n \times d</m> matrix. For each LIBOR market, choose two proxy swap rates <m>S_t^1</m> and <m>S_t^2</m>,
    /// where <m>S_t^1</m> is the swap rate with first reset date <m>T_0</m> and payment dates <m>T_1, T_2 \cdots, T_{\chi_1}, </m> 
    /// and <m>S_t^2</m> is the swap rate with first reset date <m>T_0</m> and payment dates <m>T_1, T_2 \cdots, T_{\chi_2}</m>. 
    /// Assume <m>\chi_1</m> and <m>\chi_2</m> are positive integers with <m>1 \leq \chi_1 \lt \chi_2 \leq n.</m> Then the bespoke 
    /// factor loading <m>[\varphi_{ij}]</m> is assumed to have the following struncture: 
    /// <list>
    /// <item><description>When <m>1 \leq i \leq \chi_1, \varphi_{ij} = \varphi_{\chi_1,j}, </m> for any <m>1 \leq j \leq d. </m> </description></item>
    /// <item><description>When <m>\chi_2 \leq i \leq n, \varphi_{ij} = \varphi_{\chi_2,j}, </m> for any <m>1 \leq j \leq d. </m> </description></item>
    /// <item><description>When <m>\chi_1 \leq i \leq \chi_2, </m> interpolate on the ambient space between row <m>\chi_1</m>
    /// and row <m>\chi_2</m> in matrix <m>[\varphi_{ij}]</m>. Let 
    /// <math>
    /// S_{ij} = \frac{T_{\chi_2}-T_i}{T_{\chi_2}-T_{\chi_1}}\varphi_{\chi_1,j} + \frac{T_i-T_{\chi_1}}{T_{\chi_2}-T_{\chi_1}}\varphi_{\chi_2,j}.
    /// </math>
    ///  We define
    /// <math env = "align*">
    /// \varphi_{ij} = \begin{cases} 
    ///  \sqrt{\frac{T_{\chi_2} - T_i}{T_{\chi_2} - T_{\chi_1}}\varphi^2_{\chi_1,j} + \frac{T_i - T_{\chi_1}}{T_{\chi_2} - T_{\chi_1}}\varphi^2_{\chi_2,j}}, \ \ \ \   \text{if } S_{ij} > 0.\\
    ///  -\sqrt{\frac{T_{\chi_2} - T_i}{T_{\chi_2} - T_{\chi_1}}\varphi^2_{\chi_1,j} + \frac{T_i - T_{\chi_1}}{T_{\chi_2} - T_{\chi_1}}\varphi^2_{\chi_2,j}},  \ \ \ \ 
    /// \text{if } S_{ij} \leq 0. \\
    /// \end{cases}
    /// </math>
    /// </description></item>
    /// </list>.
    /// </para>
    /// For full description of the modeling, please see the technical paper.
    /// </remarks>
    public static double[,] CalibrateCapletFactorLoadings(Dt asOf, DiscountCurve discountCurve, Dt[] standardCapletTenors, Curve[] fwdFwdVols,
                                                          Dt[] bespokeCapletTenors, Dt[] swapRateEffective, Dt[] swapRateMaturities,
                                                          double[,] swapRateFactorLoadings, Dt[] curveDates, DistributionType distributionType, 
                                                          out VolatilityCurve[] bespokeCapletVols)
    {
      var bespokeVols = bespokeCapletTenors.Select(dt =>
                                                   {
                                                     var retVal = new Curve(asOf);
                                                     foreach (var curveDt in curveDates)
                                                     {
                                                       if (curveDt <= retVal.AsOf)
                                                         continue;
                                                       if (curveDt >= dt)
                                                         break;
                                                       retVal.Add(curveDt, 0.0);
                                                     }
                                                     retVal.Add(dt, 0.0);
                                                     return retVal;
                                                   }).ToArray();
      var bespokeFactors = new double[bespokeVols.Length,swapRateFactorLoadings.GetLength(1)];
      try
      {
        Native.CalibrationUtils.CalibrateLiborFactors(discountCurve, swapRateEffective.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(),
                              swapRateMaturities.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(),
                              swapRateFactorLoadings, standardCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), fwdFwdVols,
                              bespokeCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), bespokeVols, bespokeFactors,
                              distributionType == DistributionType.Normal);
      }
      catch (Exception)
      {

        throw new ArgumentException(String.Format("Calibration of {0} Libor Rate factor loadings from {0} Swap rate factor loading has failed.",
                                  discountCurve.Ccy));
      }
      bespokeCapletVols = bespokeVols.Select(c =>
                                             {
                                               var retVal = new VolatilityCurve(c.AsOf) {DistributionType = distributionType};
                                               for (int i = 0; i < c.Count; ++i)
                                                 retVal.AddVolatility(c.GetDt(i), c.GetVal(i));
                                               retVal.Fit();
                                               return retVal;
                                             }).ToArray();
      return bespokeFactors;
    }

    #endregion

    #region Correlation

    /// <summary>
    /// Get factor dimension to explain 1 - error of the variance
    /// </summary>
    /// <param name="correlationMatrix">Correlation matrix</param>
    /// <param name="error">Error</param>
    /// <returns>Min number of factors to explain 1 - error of the variance</returns>
    public static int GetFactorDimension(double[,] correlationMatrix, double error)
    {
      return Native.CalibrationUtils.ChooseFactorDimension(correlationMatrix, error);
    }

    /// <summary>
    /// Get error given factor dimension 
    /// </summary>
    /// <param name="correlationMatrix">Correlation matrix</param>
    /// <param name="numFactors">Error</param>
    /// <returns>Error for the given dimension</returns>
    public static double GetErrorGivenFactorDimension(double[,] correlationMatrix, int numFactors)
    {
      return Native.CalibrationUtils.ErrorForGivenFactorDimension(correlationMatrix, numFactors);
    }

    /// <summary>
    /// Interpolate factor loadings linearly on spherical coordinates
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="factorLoadings">Factor loadings</param>
    /// <param name="tenors">Tenors</param>
    /// <param name="interpolatedTenors">Tenors to interpolate</param>
    /// <returns>Interpolated factor loadings</returns>
    public static double[,] InterpolateFactorLoadings(Dt asOf, double[,] factorLoadings, Dt[] tenors, Dt[] interpolatedTenors)
    {
      if (factorLoadings.GetLength(0) != tenors.Length)
        throw new ArgumentException(String.Format("{0} number of rows expected", tenors.Length), "factorLoadings");
      if (tenors.Length == 1)
      {
        var retVal = new double[interpolatedTenors.Length,factorLoadings.GetLength(1)];
        for (int i = 0; i < retVal.GetLength(0); ++i)
          for (int j = 0; j < retVal.GetLength(1); ++j)
            retVal[i, j] = factorLoadings[0, j];
        return retVal;
      }
      {
        var retVal = new double[interpolatedTenors.Length,factorLoadings.GetLength(1)];
        Native.CalibrationUtils.InterpolateFactorLoadings(factorLoadings, tenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(), retVal,
                                  interpolatedTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray());
        return retVal;
      }
    }

    /// <summary>
    /// Perturb factor loadings in local coordinates
    /// </summary>
    /// <param name="factorLoadings">Factor loadings</param>
    /// <param name="tenor">Tenor to bumps</param>
    /// <param name="bumpSize">Bump size</param>
    /// <param name="bumpRelative">Bump relative</param>
    /// <returns>Perturbed factor loadings</returns>
    public static double[] PerturbFactorLoadings(double[,] factorLoadings, int tenor, double[] bumpSize, bool bumpRelative)
    {
      var retVal = new double[factorLoadings.GetLength(1)];
      Native.CalibrationUtils.PerturbFactorLoadings(factorLoadings, tenor, retVal, bumpSize, bumpRelative);
      return retVal;
    }

    /// <summary>
    /// Factorize a correlation matrix by minimizing the sum of weighted squared errors
    /// </summary>
    /// <param name="correlationMatrix">Positive semidefinite matrix</param>
    /// <param name="weights">Error weights</param>
    /// <param name="normalize">True if sum of squared factors should be normalized to 1.0</param>
    /// <param name="factorCount">Desired number of factors</param>
    /// <param name="retVal">Factor loadings</param>
    /// <returns>Sum of squared weighted errors</returns>
    public static double FactorizeCorrelationMatrix(double[,] correlationMatrix, double[,] weights, bool[] normalize,
                                                    int factorCount, double[,] retVal)
    {
      if (weights == null || weights.Length != correlationMatrix.Length)
        weights = new double[correlationMatrix.GetLength(0),correlationMatrix.GetLength(1)];
      for (int i = 0; i < correlationMatrix.GetLength(0); ++i)
        for (int j = 0; j < correlationMatrix.GetLength(1); ++j)
          weights[i, j] = 1.0;
      int[] n;
      if (normalize == null || normalize.Length != correlationMatrix.GetLength(0))
      {
        n = new int[correlationMatrix.GetLength(0)];
        for (int i = 0; i < n.Length; ++i)
          n[i] = 1;
      }
      else
        n = Array.ConvertAll(normalize, c => c ? 1 : 0);
      double error = 0.0;
      Native.CalibrationUtils.FactorizeCorrelationMatrix(correlationMatrix, weights, n, factorCount, retVal, ref error);
      return error;
    }

    /// <summary>
    /// Factorize a correlation matrix by Cholesky Decomposition and Non-linear Least Square approximation.
    /// </summary>
    /// <param name="correlationMatrix">Positive semidefinite correlation matrix</param>
    /// <param name="systemFactorCount">Systemic factor count</param>
    /// <param name="truncateOnly">If true, only set the factor loadings as truncated and rescaled Cholesky factors.
    /// If false, apply NLS approximation with initial guess equal to the truncated and rescaled Cholesky factors. </param>
    /// <remarks>
    /// <para>
    /// This function overwrite the correlation matrix with the row of factors. It returns the actual number of factors, which equals to 
    /// the rank if systemFactor is non-positive or larger than the rank.
    /// </para>
    /// <para>
    /// If the parameter systemFactorCount is positive and less than the rank of the correlation matrix, the primary factor loadings
    /// are calibrated from truncated Cholesky factors, and NLS approximation if truncateOnly is false. Otherwise the correlation matrix
    /// contains the Cholesky factors.
    /// </para>
    /// </remarks>
    public static void FactorizeCorrelationMatrix(double[,] correlationMatrix, int systemFactorCount, bool truncateOnly)
    {
      int factorCount = Native.CalibrationUtils.FactorizeCorrelations(correlationMatrix, systemFactorCount, truncateOnly);
    }

    /// <summary>
    ///  Get eigen values of the specified correlation matrix
    /// </summary>
    /// <param name="correlationMatrix">The correlation matrix</param>
    /// <param name="eigenValues">Array to receive the eigen values</param>
    public static void GetEigenValues(double[,] correlationMatrix, double[] eigenValues)
    {
      Native.CalibrationUtils.GetEigenValues(correlationMatrix, eigenValues);
    }
    #endregion
  }
}
