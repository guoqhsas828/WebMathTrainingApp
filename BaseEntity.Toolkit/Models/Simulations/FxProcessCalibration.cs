using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// FxProcessCalibration
  /// </summary>
  [Serializable]
  public class FxProcessCalibration : IProcessCalibration
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(FxProcessCalibration));

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">asOf</param>
    /// <param name="fxCurve">fx curve</param>
    /// <param name="vol">volatility</param>
    /// <param name="domesticDiscountCalibration">domestic discount calibration</param>
    /// <param name="foreignDiscountCalibration">foreign discount calibrations</param>
    /// <param name="factorization">factorization</param>
    public FxProcessCalibration(Dt asOf, FxCurve fxCurve, IVolatilitySurface vol, DiscountProcessCalibration domesticDiscountCalibration, DiscountProcessCalibration foreignDiscountCalibration, MatrixFactorization factorization)
    {
      AsOf = asOf;
      FxCurve = fxCurve;
      ForeignCurrency = foreignDiscountCalibration.DiscountCurve.Ccy;
      DomesticCurrency = domesticDiscountCalibration.DiscountCurve.Ccy;
      DomesticDiscountCalibration = domesticDiscountCalibration;
      ForeignDiscountCalibration = foreignDiscountCalibration;
      InputVolatility = vol;
      CalibrationAsset = new CalibrationAsset(fxCurve.Name, Tenor.Empty, CCRCalibrationUtils.MarketVariableType.SpotFx);
      Factorization = factorization;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Asof date
    /// </summary>
    public Dt AsOf { get; private set; }

    private DiscountProcessCalibration DomesticDiscountCalibration { get; }

    private DiscountProcessCalibration ForeignDiscountCalibration { get; }

    /// <summary>
    /// Domestic currency
    /// </summary>
    public Currency DomesticCurrency { get; private set; }

    /// <summary>
    /// Foreign currency
    /// </summary>
    public Currency ForeignCurrency { get; private set; }

    /// <summary>
    /// Domestic discount curve
    /// </summary>
    public DiscountCurve DomesticDiscountCurve
    {
      get { return DomesticDiscountCalibration.DiscountCurve; }
    }

    /// <summary>
    /// Foreign discount curve
    /// </summary>
    public DiscountCurve ForeignDiscountCurve
    {
      get { return ForeignDiscountCalibration.DiscountCurve; }
    }

    /// <summary>
    /// Fx curve
    /// </summary>
    public FxCurve FxCurve { get; set; }

    /// <summary>
    /// Fx rate
    /// </summary>
    public FxRate FxRate
    {
      get
      {
        return FxCurve.SpotFxRate;
      }
    }

    /// <summary>
    /// Input volatility
    /// </summary>
    public IVolatilitySurface InputVolatility { get; private set; }

    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// Calibration asset
    /// </summary>
    public CalibrationAsset CalibrationAsset { get; private set; }

    /// <summary>
    /// Factor loadings
    /// </summary>
    public double[,] FactorLoadings { get; internal set; }

    /// <summary>
    /// Volatility curves
    /// </summary>
    public VolatilityCurve[] Volatilities { get; internal set; }

    #endregion

    #region Calibrate

    /// <summary>
    /// Calibrate factorized vols
    /// </summary>
    public void Calibrate()
    {
      var module = LoadModule();
      if (module == null)
        throw new ToolkitException($"Failed to find matching assets for Calibration of IProcess {FxCurve.Name}");
 
      var factorizedVols = CalibrateFxCurve(module.Value);
      FactorLoadings = factorizedVols.FactorLoadings;
      Volatilities = factorizedVols.Volatilities; 
    }
    
    private ProcessCalibrationData CalibrateFxCurve(CCRCalibrationUtils.FactorData module)
    {
      var volCollection = new VolatilityCollection(DomesticDiscountCalibration.ForwardTenors);
      volCollection.Add(DomesticDiscountCurve, DomesticDiscountCalibration.Volatilities);
      volCollection.Add(ForeignDiscountCurve, ForeignDiscountCalibration.Volatilities);
      var factorLoadingCollection = new FactorLoadingCollection(Factorization.MarketFactorNames, DomesticDiscountCalibration.ForwardTenors);
      factorLoadingCollection.AddFactors(DomesticDiscountCurve, DomesticDiscountCalibration.FactorLoadings);
      factorLoadingCollection.AddFactors(ForeignDiscountCurve, ForeignDiscountCalibration.FactorLoadings);

      var logCollection = new CalibrationUtils.CalibrationLogCollection();
      
      Logger.VerboseFormat("Calibrating for Curve {0}", FxCurve.Name);

      if (module.Volatility != null)
      {
        var fl = Resize(module.Factors, Factorization.MarketFactorNames.Length);
        var idiosyncratic = module.IdiosyncraticFactor;
        if (idiosyncratic != null)
        {
          fl[idiosyncratic.Item1] = idiosyncratic.Item2;
        }
          
        if (module.Type == CCRCalibrationUtils.MarketVariableType.SpotFx)
        {
          try
          {
            factorLoadingCollection.AddFactors(FxRate, ToMatrix(new [] {fl}));
            var volCurve = module.Volatility;
            CCRCalibrationUtils.CalibrateFxVolatility(AsOf, FxCurve, volCurve, volCollection, factorLoadingCollection, logCollection);
          }
          catch (ToolkitException e)
          {
            Logger.ErrorFormat("Process Calibration failed for Curve {0}. Returned error {1}", FxCurve.Name, e.Message);
            throw;
          }
          catch (Exception e)
          {
            Logger.ErrorFormat("Process Calibration failed for Curve {0}. Returned error {1}", FxCurve.Name, e.Message);
            throw new ToolkitException($"Exception in SpotProcessCalibration for {FxCurve.Name}", e);
          }
        }
        else
        {
          Logger.ErrorFormat("Object of type FxCurve is incompatible to {0} MarketVariableType", module.Type);
          throw new ToolkitException($"Object of type {FxCurve.GetType()} is incompatible with MarketVariableType {module.Type}");
        }
      }
      else
      {
        Logger.ErrorFormat("Must provide vol object for {0}", FxCurve.Name);
        throw new ToolkitException($"No volatilities found for {FxCurve.Name}");
      }

      var factorLoadings = factorLoadingCollection.GetFactorsAt(FxRate);
      var volatilities = volCollection.GetVolsAt(FxRate);
      return new ProcessCalibrationData() {FactorLoadings = factorLoadings, Volatilities = volatilities, Name = FxCurve.Name};
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
      return new CCRCalibrationUtils.FactorData(asset.Type, FxRate, asset.Tenor, vol, Row(Factorization.FactorizedCorrelationMatrix, idx), null);
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

      var factorDataWithIdiosyncraticValue = new CCRCalibrationUtils.FactorData(asset.Type, FxRate, asset.Tenor, vol, fl, idiosyncratic);

      return factorDataWithIdiosyncraticValue;
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

    private static string ObjectId(object obj, Tenor t)
    {
      var cc = obj as CalibratedCurve;
      if (cc != null)
      {
        return t.IsEmpty ? cc.Name : string.Format("{0}.{1:S}", cc.Name, t);
      }
      return string.Empty;
    }

    private static double[,] ToMatrix(double[][] jaggedArray)
    {
      if (jaggedArray == null)
      {
        return null;
      }
      var rows = jaggedArray.Length; int cols = jaggedArray.Max(v => v.Length);
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

    private static double[] Scale(double[] v, double factor)
    {
      for (var i = 0; i < v.Length; ++i)
      {
        v[i] /= factor;
      }
      return v;
    }

    #endregion



  }
}
