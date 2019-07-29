using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Models.BGM;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Calibrator class that calibrates a set of caplet volatilities skew 
  /// using the vanna volga method
  /// </summary>
  public static class CapVannaVolgaCalibrator
  {
    #region Methods
    /// <summary>
    /// Calibrates Vanna-Volga parameters to set of caplet skews 
    /// </summary>
    /// <param name="data">Caplet vols</param>
    /// <param name="asOf">As of date</param>
    /// <param name="maturity">horizon</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="capletStrikes">Caplet strikes</param>
    /// <returns>Vanna-Volga parameters (as functions of maturity)</returns>
    public static Curve[] CalibrateCaplets(Dictionary<double, CalibrationOutput> data, Dt asOf, Dt maturity, DiscountCurve referenceCurve, InterestRateIndex referenceIndex, double[] capletStrikes)
    {
      //first create the rate option param collection
      var result = new Curve[6];
      for (int i = 0; i < result.Length; ++i)
        result[i] = new Curve(asOf);
      foreach (var caplet in RateOptionParamCollection.GetPaymentSchedule(asOf, maturity, referenceCurve, referenceIndex))
      {
        var T = Dt.Fraction(asOf, caplet.Expiry, referenceIndex.DayCount);
        double[] anchorVols, anchorStrikes;
        CalibrateSingleFwd(data, capletStrikes, caplet.Rate, T, caplet.RateFixing, out anchorVols, out anchorStrikes);
        for (int j = 0; j < 3; j++)
          result[j].Add(caplet.RateFixing, anchorVols[j]);
        for (int j = 3; j < 6; j++)
          result[j].Add(caplet.RateFixing, anchorStrikes[j - 3]);
      }
      return result;
    }

    /// <summary>
    /// Caplet volatility
    /// </summary>
    /// <param name="asof">As of date</param>
    /// <param name="F">Forward rate</param>
    /// <param name="K">Strike</param>
    /// <param name="date">Reset date</param>
    /// <param name="parameters">Vanna-Volga parameters</param>
    /// <param name="asymApprox">Use asymptotic expansion</param>
    /// <returns>Caplet volatility</returns>
    public static double CapletVolatility(Dt asof, double F, double K, Dt date, Curve[] parameters, bool asymApprox)
    {
      double T = Dt.Fraction(asof, date, DayCount.Actual365Fixed);
      var anchorVols = new double[3];
      var anchorStrikes = new double[3];
      for (int i = 0; i < 3; i++)
        anchorVols[i] = parameters[i].Interpolate(date);
      for (int i = 3; i < 6; i++)
        anchorStrikes[i - 3] = parameters[i].Interpolate(date);
      return VannaVolgaCalibrator.ImpliedVolatility(F, T, K, 0, 0, anchorVols, anchorStrikes, asymApprox);
    }

    private static void CalibrateSingleFwd(IDictionary<double, CalibrationOutput> data, double[] strikes, double F,
                                           double T,
                                           Dt date, out double[] anchorVols, out double[] anchorStrikes)
    {
      //Get the caplet volatilities
      var work = new double[strikes.Length];
      for (int j = 0; j < strikes.Length; j++)
        work[j] = data[strikes[j]].Curve.Interpolate(date);
      anchorVols = new double[3];
      anchorStrikes = new double[3];
      VannaVolgaCalibrator.Calibrate(work, strikes, T, F, 0, 0, F, anchorVols, anchorStrikes);
    }
    #endregion

  }
}