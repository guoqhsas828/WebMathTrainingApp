using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Spot process calibration
  /// </summary>
  [Serializable]
  public class SpotProcessCalibration : IProcessCalibration
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(SpotProcessCalibration));
    
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">asof date</param>
    /// <param name="spotCurve">spot curve</param>
    /// <param name="fwdTenors">forward tenors</param>
    /// <param name="inputVol">input volatility</param>
    /// <param name="factorization">factorization</param>
    /// <param name="discountCalibration">discount calibration</param>
    /// <param name="calibrationAsset">calibration asset</param>
    public SpotProcessCalibration(Dt asOf, ForwardPriceCurve spotCurve, Tenor[] fwdTenors, object inputVol,  MatrixFactorization factorization, DiscountProcessCalibration discountCalibration, CalibrationAsset calibrationAsset = null)
    {
      AsOf = asOf;
      ForwardCurve = spotCurve;
      ForwardTenors = fwdTenors; 
      InputVolatility = inputVol;
      Factorization = factorization;
      DiscountCalibration = discountCalibration;
      if (calibrationAsset != null)
        CalibrationAsset = calibrationAsset; 
      else if (factorization.PrimaryAssets.Any(a => a.Name == spotCurve.Name && a.Type == CCRCalibrationUtils.MarketVariableType.SpotPrice))
        CalibrationAsset = factorization.PrimaryAssets.First(a => a.Name == spotCurve.Name && a.Type == CCRCalibrationUtils.MarketVariableType.SpotPrice);
      else if (factorization.SecondaryAssets.Any(a => a.Name == spotCurve.Name && a.Type == CCRCalibrationUtils.MarketVariableType.SpotPrice))
        CalibrationAsset = factorization.SecondaryAssets.First(a => a.Name == spotCurve.Name && a.Type == CCRCalibrationUtils.MarketVariableType.SpotPrice);
      else
        CalibrationAsset = new CalibrationAsset(spotCurve.Name, Tenor.Empty, CCRCalibrationUtils.MarketVariableType.SpotPrice);
    }

    #endregion

    #region Properties

    /// <summary>
    /// AsOf
    /// </summary>
    public Dt AsOf { get; private set; }
    
    /// <summary>
    /// Forward curve
    /// </summary>
    public ForwardPriceCurve ForwardCurve
    {
      get; private set;
    }

    /// <summary>
    /// Spot price
    /// </summary>
    public ISpot SpotPrice { get { return ForwardCurve?.Spot; } }

    /// <summary>
    /// Forward tenors
    /// </summary>
    public Tenor[] ForwardTenors { get; private set; }
    
    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// InputVolatility
    /// </summary>
    public object InputVolatility { get; private set; }

    /// <summary>
    /// Calibration asset
    /// </summary>
    public CalibrationAsset CalibrationAsset { get; private set; }

    private DiscountProcessCalibration DiscountCalibration { get; }

    /// <summary>
    /// Factorloadings
    /// </summary>
    public double[,] FactorLoadings { get; private set; }

    /// <summary>
    /// Volatility curves
    /// </summary>
    public VolatilityCurve[] Volatilities { get; private set; }

    #endregion


    #region Calibration
    /// <summary>
    /// Calibrate factorized vols
    /// </summary>
    public void Calibrate()
    {
      var module = LoadModule();

      if (module == null)
        throw new ToolkitException($"Failed to find matching assets for Calibration of IProcess {ForwardCurve.Name}");

      var factorizedVols = CalibrateSpotCurve(module.Value);
      FactorLoadings = factorizedVols.FactorLoadings;
      Volatilities = factorizedVols.Volatilities; 
    }

    private CCRCalibrationUtils.FactorData? LoadModule()
    {
      var calibrationAsset = CalibrationAsset;
      var vol = InputVolatility;
      if (Factorization.PrimaryAssets.Contains(calibrationAsset))
      {
        var module = LoadPrimaryModule(calibrationAsset, vol);
        return module;
      }

      if (Factorization.SecondaryAssets.Contains(calibrationAsset))
      {
        var module = LoadSecondaryModule(calibrationAsset, vol);
        return module;
      }

      return null;
    }

    private CCRCalibrationUtils.FactorData LoadPrimaryModule(CalibrationAsset asset, object vol)
    {
      var idx = Array.IndexOf(Factorization.PrimaryAssets, asset);
      return new CCRCalibrationUtils.FactorData(asset.Type, SpotPrice, asset.Tenor, vol, Row(Factorization.FactorizedCorrelationMatrix, idx), null);
    }
    private CCRCalibrationUtils.FactorData LoadSecondaryModule(CalibrationAsset asset, object vol)
    {
      var idx = Array.IndexOf(Factorization.SecondaryAssets, asset);
      var fl = Multiply(Row(Factorization.FactorizedBetas, idx), Factorization.FactorizedCorrelationMatrix);
      var norm = Norm(fl);
      Tuple<int, double> idiosyncratic = null;
      if (norm < 0.99)
      {
        idiosyncratic = new Tuple<int, double>(Factorization.FactorNameRef[idx], Math.Sqrt(1.0 - norm * norm));
      }
      else
      {
        fl = Scale(fl, norm);
      }

      var factorDataWithIdiosyncraticValue = new CCRCalibrationUtils.FactorData(asset.Type, SpotPrice, asset.Tenor, vol, fl, idiosyncratic);

      return factorDataWithIdiosyncraticValue;
    }
    private ProcessCalibrationData  CalibrateSpotCurve(CCRCalibrationUtils.FactorData module)
    {
      var volCollection = new VolatilityCollection(DiscountCalibration.ForwardTenors);
      volCollection.Add(DiscountCalibration.DiscountCurve, DiscountCalibration.Volatilities);
      var factorLoadingCollection = new FactorLoadingCollection(Factorization.MarketFactorNames, DiscountCalibration.ForwardTenors);
      factorLoadingCollection.AddFactors(DiscountCalibration.DiscountCurve, DiscountCalibration.FactorLoadings);
      
      var logCollection = new CalibrationUtils.CalibrationLogCollection();

      Logger.VerboseFormat("Calibrating for Curve {0}", ForwardCurve.Name);

      if (module.Volatility != null)
      {
        var fl = Resize(module.Factors, Factorization.MarketFactorNames.Length);
        var idiosyncratic = module.IdiosyncraticFactor;
        if (idiosyncratic != null)
        {
          fl[idiosyncratic.Item1] = idiosyncratic.Item2;
        }

        if (module.Type == CCRCalibrationUtils.MarketVariableType.SpotPrice)
        {
          try
          {
            factorLoadingCollection.AddFactors(SpotPrice, ToMatrix(new[] { fl }));
            var volCurve = module.Volatility;
            CCRCalibrationUtils.CalibrateSpotVolatility(AsOf, ForwardCurve, volCurve, volCollection, factorLoadingCollection, logCollection);
          }
          catch (ToolkitException e)
          {
            Logger.ErrorFormat("Calibration failed for Curve {0}. Returned error {1}", ForwardCurve.Name, e.Message);
            throw;
          }
          catch (Exception e)
          {
            Logger.ErrorFormat("Calibration failed for Curve {0}. Returned error {1}", ForwardCurve.Name, e.Message);
            throw new ToolkitException($"Exception in SpotProcessCalibration for {ForwardCurve.Name}", e);
          }
        }
        else
        {
          Logger.ErrorFormat("SpotProcessCalibration is incompatible with {0} MarketVariableType", module.Type);
          throw new ToolkitException($"Object of type {ForwardCurve.GetType()} is incompatible with MarketVariableType {module.Type}");
        }
      }
      else
      {
        Logger.ErrorFormat("Must provide vol object for {0}", ForwardCurve.Name);
        throw new ToolkitException($"No volatilities found for {ForwardCurve.Name}");
      }

      var factorLoadings = factorLoadingCollection.GetFactorsAt(SpotPrice);
      var volatilities = volCollection.GetVolsAt(SpotPrice);
      return new ProcessCalibrationData(){FactorLoadings=factorLoadings, Volatilities=volatilities, Name = ForwardCurve.Name};
    }




    #endregion

    #region Helper Methods

    private static double[] Resize(double[] array, int dim)
    {
      if (array.Length == dim)
      {
        return array;
      }
      var retVal = new double[dim];
      dim = Math.Min(array.GetLength(0), dim);
      for (var i = 0; i < dim; ++i)
      {
        retVal[i] = array[i];
      }
      return retVal;
    }

    private static double[] Multiply(double[] v, double[,] m)
    {
      var retVal = new double[m.GetLength(1)];
      for (var i = 0; i < v.Length; ++i)
      {
        for (var j = 0; j < retVal.Length; ++j)
        {
          retVal[j] += v[i] * m[i, j];
        }
      }
      return retVal;
    }

    private static T[] Row<T>(T[,] array, int idx)
    {
      var retVal = new T[array.GetLength(1)];
      for (var i = 0; i < retVal.Length; ++i)
      {
        retVal[i] = array[idx, i];
      }
      return retVal;
    }

    private static double Norm(double[] v)
    {
      double retVal = 0.0;
      for (var i = 0; i < v.Length; ++i)
      {
        retVal += v[i] * v[i];
      }
      return Math.Sqrt(retVal);
    }

    private static double[] Scale(double[] v, double factor)
    {
      for (var i = 0; i < v.Length; ++i)
      {
        v[i] /= factor;
      }
      return v;
    }

    private static double[,] ToMatrix(double[][] jaggedArray)
    {
      if (jaggedArray == null)
      {
        return null;
      }
      var rows = jaggedArray.Length;
      var cols = jaggedArray.Max(v => v.Length);
      var retVal = new double[rows, cols];
      for (var i = 0; i < rows; ++i)
      {
        for (var j = 0; j < jaggedArray[i].Length; ++j)
        {
          retVal[i, j] = jaggedArray[i][j];
        }
      }
      return retVal;
    }
    #endregion


  }
}
