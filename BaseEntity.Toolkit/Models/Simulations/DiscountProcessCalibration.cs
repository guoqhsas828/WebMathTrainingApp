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
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Discount process calibration
  /// </summary>
  [Serializable]
  public class DiscountProcessCalibration : IProcessCalibration
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(DiscountProcessCalibration));
    
    #region Constructors

    /// <summary>
    /// The constructor of discount process calibration
    /// </summary>
    /// <param name="asOf">asOf date</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="fwdTenors">forward tenors</param>
    /// <param name="inputVols">input volatilities</param>
    /// <param name="assets">assets</param>
    /// <param name="factorization">factorization</param>
    /// <param name="volCurveDates">volatility curve dates</param>
    /// <param name="separableVol">The volatility is separable or not</param>
    public DiscountProcessCalibration(Dt asOf, DiscountCurve discountCurve, Tenor[] fwdTenors, object[] inputVols,  CalibrationAsset[] assets, MatrixFactorization factorization, Dt[] volCurveDates = null, bool separableVol = false)
    {
      AsOf = asOf;
      DiscountCurve = discountCurve;
      ForwardTenors = fwdTenors; 
      InputVolatilities = inputVols;
      CalibrationAssets = assets.Where(a => a.Name == discountCurve.Name).ToArray();
      Factorization = factorization; 
      VolCurveDates = volCurveDates;
      SeparableVol = separableVol; 
    }

    #endregion

    #region Properties
    /// <summary>
    /// Asof Date
    /// </summary>
    public Dt AsOf { get; private set; }
    
    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get; private set;
    }

    /// <summary>
    /// Forward tenors
    /// </summary>
    public Tenor[] ForwardTenors { get; private set; }
    
    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// Input volatilities
    /// </summary>
    public object[] InputVolatilities { get; private set; }

    /// <summary>
    /// Calibration assets
    /// </summary>
    public CalibrationAsset[] CalibrationAssets { get; private set; }

    /// <summary>
    /// volatility curve dates
    /// </summary>
    public Dt[] VolCurveDates { get; set; }

    /// <summary>
    /// the volatility is separable or not
    /// </summary>
    public bool SeparableVol { get; set; }
    
    /// <summary>
    /// Factor loadings
    /// </summary>
    public double[,] FactorLoadings { get; internal set; }

    /// <summary>
    /// Volatility curves
    /// </summary>
    public VolatilityCurve[] Volatilities { get; internal set; }

    #endregion


    #region Calibration
    /// <summary>
    /// Calibrate factorized vols
    /// </summary>
    public void Calibrate()
    {
      var modules = LoadModules();
      if (modules.IsNullOrEmpty())
        throw new ToolkitException($"Failed to find matching assets for Calibration of IProcess {DiscountCurve.Name}");
      var factorizedVols = CalibrateDiscountCurve(null, SeparableVol, modules);
      FactorLoadings = factorizedVols.FactorLoadings;
      Volatilities = factorizedVols.Volatilities; 
    }

    private List<CCRCalibrationUtils.FactorData>  LoadModules()
    {
      var modules = new List<CCRCalibrationUtils.FactorData>();
      for (int i = 0; i < CalibrationAssets.Length; i++)
      {
        var calibrationAsset = CalibrationAssets[i];
        var vol = InputVolatilities.Length == 1 ? InputVolatilities[0] : InputVolatilities[i];
        if (Factorization.PrimaryAssets.Contains(calibrationAsset))
        {
          var module = LoadPrimaryModule(calibrationAsset, vol);
          modules.Add(module);
        }

        if (Factorization.SecondaryAssets.Contains(calibrationAsset))
        {
          var module = LoadSecondaryModule(calibrationAsset, vol);
          modules.Add(module);
        }
      }
      return modules; 
    }

    private CCRCalibrationUtils.FactorData LoadPrimaryModule(CalibrationAsset asset, object vol)
    {
      var idx = Array.IndexOf(Factorization.PrimaryAssets, asset);
      return new CCRCalibrationUtils.FactorData(asset.Type, DiscountCurve, asset.Tenor, vol, Row(Factorization.FactorizedCorrelationMatrix, idx), null);
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

      var factorDataWithIdiosyncraticValue = new CCRCalibrationUtils.FactorData(asset.Type, DiscountCurve, asset.Tenor, vol, fl, idiosyncratic);

      return factorDataWithIdiosyncraticValue;
    }
    
    private ProcessCalibrationData CalibrateDiscountCurve(Dt[] volCurveDates, bool separableVol, IEnumerable<CCRCalibrationUtils.FactorData> modules)
    {
      var data = modules.Where(v => v.Obj == DiscountCurve).OrderBy(d => d.Tenor);
      var forwardTenorDates = ForwardTenors.Select(t => Dt.Add(AsOf, t)).ToArray();

      Logger.VerboseFormat("Attempting Calibration for Curve {0}", DiscountCurve.Name);

      if (data.Any(d => d.Volatility != null))
      {
        var vol = data.First(d => d.Volatility != null).Volatility;
        var swapFactors = data.Select(d =>
        {
          var idiosyncratic = d.IdiosyncraticFactor;
          if (idiosyncratic != null)
          {
            double[] f = Resize(d.Factors, Factorization.MarketFactorNames.Length);
            f[idiosyncratic.Item1] = idiosyncratic.Item2;
            return f;
          }
          return d.Factors;
        }).ToArray();

        try
        {
          VolatilityCurve[] bespokeVols;
          var fl = CCRCalibrationUtils.CalibrateForwardRateFactorLoadings(AsOf, forwardTenorDates, DiscountCurve, vol,
            data.Select(d => d.Type == CCRCalibrationUtils.MarketVariableType.SwapRate ? AsOf : Dt.Add(AsOf, d.Tenor)).ToArray(),
            data.Select(d => Dt.Add(AsOf, d.Tenor)).ToArray(), swapFactors, volCurveDates, separableVol, out bespokeVols);
          return new ProcessCalibrationData(){FactorLoadings=Resize(fl, fl.GetLength(0), Factorization.MarketFactorNames.Length), Volatilities = bespokeVols, Name = DiscountCurve.Name}; 
        }
        catch (ToolkitException e)
        {
          Logger.ErrorFormat("Calibration failed for Curve {0}. Returned error {1}", DiscountCurve.Name, e.Message);
          throw; 
        }
        catch (Exception e)
        {
          Logger.ErrorFormat("Calibration failed for Curve {0}. Returned error {1}", DiscountCurve.Name, e.Message);
          throw new ToolkitException($"Exception in CalibrateForwardRateFactorLoadings for {DiscountCurve.Name}", e);
        }
      }
      else
      {
        Logger.VerboseFormat("No Volatilities found for Curve {0}", DiscountCurve.Name);
        throw new ToolkitException($"No volatilities found for {DiscountCurve.Name}");
      }
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

    private static double[,] Resize(double[,] array, int dim0, int dim1)
    {
      var retVal = new double[dim0, dim1];
      dim0 = Math.Min(array.GetLength(0), dim0);
      dim1 = Math.Min(array.GetLength(1), dim1);
      for (var i = 0; i < dim0; ++i)
      {
        for (var j = 0; j < dim1; ++j)
        {
          retVal[i, j] = array[i, j];
        }
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
