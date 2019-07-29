using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using System.Linq;


namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Calibration type
  /// </summary>
  public enum SabrCalibrationType
  {
    /// <summary>Give more precedence to the rate calibration</summary>
    RateOverStock,
    /// <summary>Give more precedence to the stock calibration</summary>
    StockOverRate
  }
  
  /// <summary>
  /// Sabr 2d calibrator
  /// </summary>
  public static class Sabr2DCalibrator
  {

     /// <summary>
    /// Computes the SABR guess values 
    /// </summary>
    /// <param name="betaRate">Elasticity parameter of the rate</param>
    /// <param name="betaStock">Elasticity parameter of the stock</param>
    /// <param name="nu0">Initial guess for vol vol</param>
    /// <param name="stockPrice">Stock price level</param>
    /// <param name="rateValue">Rate level</param>
    /// <param name="asOf">As of date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="stockVols">Stock option market vols</param>
    /// <param name="stockStrikes">Stock option strikes</param>
    /// <param name="rateVols">Rate swaption market vols</param>
    /// <param name="rateStrikes">Rate swaption strikes</param>
    /// <returns></returns>
    public static double[] Sabr2DGuesses(double betaRate,
                                  double betaStock,
                                  double nu0,
                                  double stockPrice,
                                  double rateValue,
                                  Dt asOf,
                                  Dt maturity,
                                  double[] stockVols,
                                  double[] stockStrikes,
                                  double[] rateVols,
                                  double[] rateStrikes)
     {
       var guessVals = new double[5];
       var T = Dt.Fraction(asOf, maturity, DayCount.Actual365Fixed);
       var rateInterpolator = new Interpolator(InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const),
                                               rateStrikes, rateVols);
       var stockInterpolator = new Interpolator(InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const),
                                                stockStrikes, stockVols);
       var atmVolRate = rateInterpolator.evaluate(rateValue);
       var atmVolStock = stockInterpolator.evaluate(stockPrice);
       var atmDerRate = CalculateDerivative(rateInterpolator, rateValue, 0.0001);
       var atmDerStock = CalculateDerivative(stockInterpolator, stockPrice, 0.01);
       SabrCalibrations.SabrGuessValues(betaStock, betaRate, nu0, stockPrice, rateValue, T, atmVolRate, atmDerRate,
                                        atmVolStock, atmDerStock, guessVals);
       return guessVals;
     }

    private static double CalculateDerivative(Interpolator interpolator, double x, double bumpSize)
    {
      double pu = interpolator.evaluate(x + bumpSize);
      double pd = interpolator.evaluate(x - bumpSize);
      return (pu - pd) / (2 * bumpSize);
    }

    /// <summary>
    /// Calibrate
    /// </summary>
    /// <param name="betaRate">Elasticity parameters of rate</param>
    /// <param name="betaStock">Elasticity parameter of stock</param>
    /// <param name="stockPrice">Stock price</param>
    /// <param name="rateValue">Rate level</param>
    /// <param name="T">Maturity</param>
    /// <param name="stockVols">Stock volatilities</param>
    /// <param name="stockStrikes">Stock strikes</param>
    /// <param name="rateVols">Rate volatilities</param>
    /// <param name="rateStrikes">Rate strikes</param>
    /// <param name="guessValues">Guess values</param>
    /// <param name="lb">Lower bound for parameters</param>
    /// <param name="ub">Upper bound for parameters</param>
    /// <param name="calibrationType">Calibration type (Rate over stock or </param>
    /// <returns>Parameters</returns>
    public static double[] Calibrate(double betaRate, double betaStock, double stockPrice,
                                  double rateValue, double T, double[] stockVols, double[] stockStrikes,
                                  double[] rateVols, double[] rateStrikes, double[] guessValues, double[] lb, double[] ub,
                                  SabrCalibrationType calibrationType)
    {
      if (calibrationType == SabrCalibrationType.RateOverStock)
      {
        //first calibrate the rates 
        var ratePars = new double[3];
        var stockPars = new double[2];
        var rateGuess = new double[3];
        var rateLb = new double[3];
        var rateUb = new double[3];
        var stockGuess = new double[2];
        var stockLb = new double[2];
        var stockUb = new double[2];
        for (int i = 0; i < 3; i++)
        {
          rateGuess[i] = guessValues[i];
          rateLb[i] = lb[i];
          rateUb[i] = ub[i];
        }
        for (int i = 0; i < 2; i++)
        {
          stockGuess[i] = guessValues[i + 3];
          stockLb[i] = lb[i + 3];
          stockUb[i] = ub[i + 3];
        }
        SabrCalibrations.SabrCalibrate3Params(rateVols, rateStrikes, rateValue, T, betaRate, rateGuess, rateLb, rateUb, ratePars);
        var nu = ratePars[2];
        SabrCalibrations.SabrCalibrate2Params(stockVols, stockStrikes, stockPrice, T, betaStock, nu, stockGuess, stockLb, stockUb, stockPars);
        int idx = 0;
        var result = new double[5];
        for (int i = 0; i < ratePars.Length; i++)
          result[idx++] = ratePars[i];
        for (int i = 0; i < stockPars.Length; i++)
          result[idx++] = stockPars[i];
        return result;
      }
      else
      {
        var ratePars = new double[2];
        var stockPars = new double[3];
        var rateGuess = new double[2];
        var stockGuess = new double[3];

        var rateLb = new double[2];
        var rateUb = new double[2];
        var stockLb = new double[3];
        var stockUb = new double[3];

        for (int i = 0; i < 2; i++)
        {
          rateGuess[i] = guessValues[i];
          rateLb[i] = lb[i];
          rateUb[i] = ub[i];
        }

        for (int i = 0; i < 2; i++)
        {
          stockGuess[i] = guessValues[i + 3];
          stockUb[i] = ub[i + 3];
          stockLb[i] = lb[i + 3];
        }

        stockGuess[2] = guessValues[2];
        stockLb[2] = lb[2];
        stockUb[2] = ub[2];
        SabrCalibrations.SabrCalibrate3Params(stockVols, stockStrikes, stockPrice, T, betaStock, stockGuess, stockLb, stockUb, stockPars);
        var nu = stockPars[2];
        SabrCalibrations.SabrCalibrate2Params(rateVols, rateStrikes, rateValue, T, betaRate, nu, rateGuess, rateLb, rateUb, ratePars);
        int idx = 0;
        var result = new double[5];
        for (int i = 0; i < ratePars.Length; i++)
          result[idx++] = ratePars[i];
        result[idx++] = nu;
        for (int i = 0; i < stockPars.Length - 1; i++)
          result[idx++] = stockPars[i];
        return result;
      }
    }

    
    
    /// <summary>
    /// Tests sabr volatilities 
    /// </summary>
    /// <param name="strikesF">Stock option strikes</param>
    /// <param name="F">Stock level</param>
    /// <param name="T">Time to maturity</param>
    /// <param name="strikesL">Rate option strikes </param>
    /// <param name="L">Rate level</param>
    /// <param name="parameters">Parameters</param>
    /// <returns>Sabr implied vols for the given parameters and strikes</returns>
    public static double[] SABR2DVols(double[] strikesF, double F, double T, double[] strikesL, double L, double[] parameters)
    {
      var result = new double[strikesL.Length + strikesF.Length];
      int idx = 0;
      for (int i = 0; i < strikesF.Length; i++)
        result[idx++] = Sabr.ImpliedVolatility(F, T, strikesF[i], parameters[0], parameters[1],
                                                            parameters[2], parameters[3]);
      for (int i = 0; i < strikesL.Length; i++)
        result[idx++] = Sabr.ImpliedVolatility(L, T, strikesL[i], parameters[4], parameters[5],
                                                            parameters[2], parameters[6]);
      return result;
    }
  }
    
  /// <summary>
  /// Shifted bgm calibration
  /// </summary>
  public static class ShiftedBgm2DCalibrator
  {
    private static void PricingError(double T, double F, double[] marketVols, double[] strikes, double sigma, double kappa, IList<double> err)
    {
     for (int i = 0; i < strikes.Length; ++i)
      {
        //double price = OptionPrice(types[i], F, strikes[i], T, sigma, kappa);
        double vol = ShiftedLogNormal.ImpliedVolatility(F, T, strikes[i], sigma, kappa);
        // Black.ImpliedVolatility(types[i], T, F, strikes[i], price);
        err[i] = vol - marketVols[i];
      }
    }

    /// <summary>
    /// Calibrate
    /// </summary>
    /// <param name="stockPrice">Stock price</param>
    /// <param name="rateValue">Rate level</param>
    /// <param name="T">Maturity</param>
    /// <param name="stockVols">Stock volatilities</param>
    /// <param name="stockStrikes">Stock strikes</param>
    /// <param name="stockTypes">Stock option types</param>
    /// <param name="rateVols">Rate volatilities</param>
    /// <param name="rateStrikes">Rate strikes</param>
    /// <param name="rateTypes">Rate option types</param>
    /// <param name="guessValues">Guess values</param>
    /// <param name="lb">Lower bound for parameters</param>
    /// <param name="ub">Upper bound for parameters</param>
    /// <returns>Parameters</returns>
    public static double[] Calibrate(double stockPrice, double rateValue, double T, double[] stockVols, double[] stockStrikes, 
      OptionType[] stockTypes, double[] rateVols, double[] rateStrikes, OptionType[] rateTypes, double[] guessValues, double[] lb, double[] ub)
    {
      DelegateOptimizerFn stockFn = DelegateOptimizerFn.Create(2, stockVols.Length,
                                                               (x, f, g) => PricingError(T, stockPrice, stockVols,
                                                                                         stockStrikes, x[0],
                                                                                         x[1], f), false);
      DelegateOptimizerFn rateFn = DelegateOptimizerFn.Create(2, rateVols.Length,
                                                              (x, f, g) =>
                                                              PricingError(T, rateValue, rateVols, rateStrikes, x[0],
                                                                           x[1], f), false);
      //Routine for initial guess
      Optimizer opt;
      opt = new NLS(2);
      opt.setMaxIterations(50000);
      opt.setMaxEvaluations(50000);
      opt.setUpperBounds(ub);
      opt.setLowerBounds(lb);
      opt.setInitialPoint(guessValues);
      var retVal = new double[4];
      try
      {
        opt.Minimize(stockFn);
        for(int i = 0; i < opt.CurrentSolution.Count; ++i)
          retVal[i] = opt.CurrentSolution[i];
        opt.Minimize(rateFn);
        for(int i = 0; i < opt.CurrentSolution.Count; ++i)
          retVal[i + 2] = opt.CurrentSolution[i];
      }
      catch (Exception )
      {
        return retVal;
      }
     
      return  retVal;
    }
   
   
    /// <summary>
    /// Returns the shifted bgm volatilities
    /// </summary>
    /// <param name="strikesF">Strikes for input stock options</param>
    /// <param name="typesF">Stock option types</param>
    /// <param name="F">Stock level</param>
    /// <param name="T">Time to maturity</param>
    /// <param name="strikesL">Strikes for input swaption</param>
    /// <param name="typesL">Swaption types</param>
    /// <param name="L">Rate level</param>
    /// <param name="pars">Parameters</param>
    /// <returns>Implied volatilities of shifted bgm for the given strikes and parameters </returns>
    public static double[] ShiftedBgmVols(double[] strikesF, OptionType[] typesF, double F, double T,
                                         double[] strikesL, OptionType[] typesL, double L, double[] pars)
    {
      var result = new double[strikesL.Length + strikesF.Length];
      int idx = 0;
      for (int i = 0; i < strikesF.Length; i++)
      {
        //double price =  OptionPrice(typesF[i], F, strikesF[i], T, pars[0], pars[1]);
        result[idx++] = ShiftedLogNormal.ImpliedVolatility(F, T, strikesF[i], pars[0], pars[1]);
        // Black.ImpliedVolatility(typesF[i], T, F, strikesF[i], price);
      }

      for (int i = 0; i < strikesL.Length; i++)
      {
        //double price = OptionPrice(typesL[i], L, strikesL[i], T, pars[2], pars[3]);
        result[idx++] = ShiftedLogNormal.ImpliedVolatility(L, T, strikesL[i], pars[2], pars[3]);
        //Black.ImpliedVolatility(typesL[i], T, L, strikesL[i], price);
      }
      return result;
    }
  }
}