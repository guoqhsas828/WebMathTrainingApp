using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  /// SABR calibration methods
  /// </summary>
  public static class CapSabrCalibrator
  {
    #region Methods

    /// <summary>
    /// Method that calibrates maturity dependent SABR model parameters to a set of edf and caplet volatility curves
    /// </summary>
    /// <param name="data">Caplet volatilities per strike</param>
    /// <param name="asOf">As of date</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="capMaturities">Calibration cap maturities</param>
    /// <param name="futurePrices">Prices of futures underlying edf options used in calibration</param>
    /// <param name="edfStrikes">EDF Strikes</param>
    /// <param name="capletStrikes">Caplet Strikes</param>
    /// <param name="betaCurve">beta</param>
    /// <param name="nu0">nu0</param>
    /// <param name="rho0">rho0</param>
    /// <param name="lb">lb</param>
    /// <param name="ub">ub</param>
    /// <param name="edfMaturities">Euro Dollar Future Maturities </param>
    /// <param name="alphaGuess">Alpha Guess Values </param>
    /// <param name="rhoGuess">Rho Guess Values </param>
    /// <param name="nuGuess">Nu Guess Values </param>
    /// <returns>[beta(T), alpha(T), rho(T), nu(T)] </returns>
    public static Curve[] SabrCalibrateCaplets(Dictionary<double, CalibrationOutput> data, Dt asOf, DiscountCurve referenceCurve,
                                               InterestRateIndex referenceIndex, Dt[] capMaturities, Dt[] edfMaturities, double[] futurePrices,
                                               double[] edfStrikes, double[] capletStrikes, Curve betaCurve, double nu0, double rho0, double[] lb, double[] ub,
                                               Curve alphaGuess, Curve rhoGuess, Curve nuGuess)
    {
      //Set up the Rate Option Param Collection 
      var capletPayments = RateOptionParamCollection.GetPaymentSchedule(asOf, capMaturities.Last(), referenceCurve, referenceIndex).ToArray();
      var result = new Curve[4];
      for (int i = 0; i < 4; ++i)
      {
        result[i] = new Curve(asOf);
        result[i].Add(edfMaturities, edfMaturities.Select(dt => 0.0).ToArray());
      }
      Dt lastEdfMat = edfMaturities.Last();
      int firstCapIdx = Array.FindIndex(capletPayments, p => p.RateFixing > lastEdfMat);
      //First bootstrap the Euros
      Parallel.For(0, edfMaturities.Length,
                   i =>
                   {
                     var date = edfMaturities[i];
                     var f = futurePrices[i];
                     var T = Dt.Fraction(asOf, date, DayCount.Actual365Fixed);
                     double[] inputGuess = null;
                     if ((alphaGuess != null) && (rhoGuess != null) && (nuGuess != null))
                       inputGuess = new[] {alphaGuess.Interpolate(date), rhoGuess.Interpolate(date), nuGuess.Interpolate(date)};
                     var beta = betaCurve.Interpolate(date);
                     var calibParams = SabrFitSingleFwd(data, edfStrikes, f, T, date, beta, nu0, rho0, lb, ub, inputGuess);
                     result[0].Add(date, beta);
                     for (int j = 1; j < 4; j++)
                       result[j].SetVal(i, calibParams[j - 1]);
                   });
      //now bootstrap the Caplets 
      Parallel.For(firstCapIdx, capletPayments.Length, i =>
                                                       {
                                                         var date = capletPayments[i].RateFixing;
                                                         var f = capletPayments[i].Rate;
                                                         var T = Dt.Fraction(asOf, date, referenceIndex.DayCount);
                                                         double[] inputGuess = null;
                                                         if (alphaGuess != null && (rhoGuess != null) && (nuGuess != null))
                                                           inputGuess = new[]
                                                                        {alphaGuess.Interpolate(date), rhoGuess.Interpolate(date), nuGuess.Interpolate(date)};
                                                         var beta = betaCurve.Interpolate(date);
                                                         var calibParams = SabrFitSingleFwd(data, capletStrikes, f, T, date, beta, nu0, rho0, lb, ub, inputGuess);
                                                         result[0].SetVal(i, beta);
                                                         for (int j = 1; j < 4; j++)
                                                           result[j].SetVal(i, calibParams[j - 1]);
                                                       });
      return result;
    }

    /// <summary>
    /// Method that calibrates maturity dependent SABR model parameters to a set of caplet volatility curves
    /// </summary>
    /// <param name="data">Caplet volatilities per strike</param>
    /// <param name="asOf">As of date</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="capMaturities">Calibration cap maturities</param>
    /// <param name="capletStrikes">Caplet strikes</param>
    /// <param name="betaCurve">Given beta(T)</param>
    /// <param name="nu0"></param>
    /// <param name="rho0"></param>
    /// <param name="lb"></param>
    /// <param name="ub"></param>
    /// <param name="alphaGuess"></param>
    /// <param name="rhoGuess"></param>
    /// <param name="nuGuess"></param>
    /// <returns>[beta(T), alpha(T), rho(T), nu(T)] </returns>
    public static Curve[] SabrCalibrateCaplets(Dictionary<double, CalibrationOutput> data, Dt asOf, DiscountCurve referenceCurve,
                                               InterestRateIndex referenceIndex, Dt[] capMaturities, double[] capletStrikes,
                                               Curve betaCurve, double nu0, double rho0, double[] lb, double[] ub, Curve alphaGuess,
                                               Curve rhoGuess, Curve nuGuess)
    {
      var capletPayments = RateOptionParamCollection.GetPaymentSchedule(asOf, capMaturities.Last(), referenceCurve, referenceIndex).ToArray();
      var result = new Curve[4];
      for (int i = 0; i < 4; ++i)
      {
        result[i] = new Curve(asOf);
        result[i].Add(capletPayments.Select(p => p.RateFixing).ToArray(), Enumerable.Repeat(0.0, capletPayments.Length).ToArray());
      }
      Parallel.For(0, capletPayments.Length, i =>
                                             {
                                               var date = capletPayments[i].RateFixing;
                                               var f = capletPayments[i].Rate;
                                               var T = Dt.Fraction(asOf, capletPayments[i].Expiry, referenceIndex.DayCount);
                                               double[] inputGuess = null;
                                               if (alphaGuess != null && (rhoGuess != null) && (nuGuess != null))
                                                 inputGuess = new[] {alphaGuess.Interpolate(date), rhoGuess.Interpolate(date), nuGuess.Interpolate(date)};
                                               var beta = betaCurve.Interpolate(date);
                                               var calibParams = SabrFitSingleFwd(data, capletStrikes, f, T, date, beta, nu0, rho0, lb, ub, inputGuess);
                                               result[0].SetVal(i, beta);
                                               for (int j = 1; j < 4; j++)
                                                 result[j].SetVal(i, calibParams[j - 1]);
                                             });
      return result;
    }

    private static double[] SabrFitSingleFwd(IDictionary<double, CalibrationOutput> dict, double[] strikes, double f, double T, Dt date, double beta, double nu0,
                                             double rho0, double[] lb, double[] ub, double[] inputGuess)
    {
      var work = new double[strikes.Length];
      for (int j = 0; j < strikes.Length; j++)
        work[j] = dict[strikes[j]].Curve.Interpolate(date);
      var guessVals = new double[3];
      if ((inputGuess == null) || (inputGuess.Any(g => g.ApproximatelyEqualsTo(0.0))))
      {
        double atmVol, atmDer;
        GetAtmVolAndDerivative(work, strikes, f, out atmVol, out atmDer);
        try
        {
          CapCalibrations.SabrGuessValues(beta, nu0, f, T, atmVol, atmDer, guessVals);
        }
        catch
        {
          guessVals[0] = atmVol * Math.Pow(f, 1 - beta);
          guessVals[1] = rho0;
          guessVals[2] = nu0;
        }
        if (inputGuess != null)
        {
          for (int i = 0; i < guessVals.Length; ++i)
          {
            if (!inputGuess[i].ApproximatelyEqualsTo(0.0))
              guessVals[i] = inputGuess[i];
          }
        }
      }
      else
      {
        for (int i = 0; i < inputGuess.Length; i++)
          guessVals[i] = inputGuess[i];
      }
      var calibPars = new double[3];
      try
      {
        CapCalibrations.SabrFitSingleFwd(work, strikes, f, T, beta, guessVals, calibPars, lb, ub);
      }
      catch
      {
        throw new ToolkitException(String.Format("Sabr Caplet Calibration failed for T = {0}", T));
      }
      return calibPars;
    }

    private static void GetAtmVolAndDerivative(double[] vols, double[] strikes, double fwd, out double atmVol, out double atmVolDer)
    {
      var interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);
      var interpolator = new Interpolator(interp, strikes, vols);
      atmVol = interpolator.evaluate(fwd);
      const double delta = 0.0001;
      var sigmau = interpolator.evaluate(fwd + delta);
      var sigmad = interpolator.evaluate(fwd - delta);
      atmVolDer = (sigmau - sigmad) / (2 * delta);
    }

    #endregion
  }
}
