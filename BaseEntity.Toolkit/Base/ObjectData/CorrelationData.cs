/*
 * CorrelationData.cs
 *
 * Correlation data holder
 *
 *
 */
using System;

using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   A class to hold correlation data independent of the object definitions.
  ///   For internal use only.
  ///   <preliminary/>
  /// </summary>
  /// <exclude/>
  [Serializable]  
	public class CorrelationData
  {
    #region Public Methods
    /// <summary>
    ///   Deserialize data to correlation object
    /// </summary>
    /// <returns>Correlationobject</returns>
    /// <exclude/>
    public CorrelationObject CreateCorrelationObject()
    {
      return Create(this.Data);
    }

    /// <summary>
    ///   Serialize correlation object to data item
    /// </summary>
    /// <returns>Correlationobject</returns>
    /// <exclude/>
    public static CorrelationData CreateCorrelationData(
      CorrelationObject correlation)
    {
      CorrelationData cd = new CorrelationData();
      cd.Data = GetItem(correlation);
      return cd;
    }

    #endregion Public Methods

    #region Private Implementations

    private static Item GetItem(
      CorrelationObject correlation)
    {
      Item item = new Item();
      if (correlation != null)
        item.Name = correlation.Name;

      if (correlation is SingleFactorCorrelation)
      {
        SingleFactorCorrelation corr = (SingleFactorCorrelation)correlation;
        item.EntityNames = corr.Names;
        item.Data = corr.Correlations;
        item.Type = CorrelationType.SingleFactorCorrelation;
        return item;
      }

      if (correlation is FactorCorrelation)
      {
        FactorCorrelation corr = (FactorCorrelation)correlation;
        item.EntityNames = corr.Names;
        item.Data = corr.Correlations;
        item.Type = CorrelationType.FactorCorrelation;
        return item;
      }

      if (correlation is GeneralCorrelation)
      {
        GeneralCorrelation corr = (GeneralCorrelation)correlation;
        item.EntityNames = corr.Names;
        item.Data = corr.Correlations;
        item.Type = CorrelationType.GeneralCorrelation;
        return item;
      }

      if (correlation is CorrelationTermStruct)
      {
        CorrelationTermStruct corr = (CorrelationTermStruct)correlation;
        item.EntityNames = corr.Names;
        item.Data = corr.Correlations;
        item.Dates = FromDt(corr.Dates);
        item.Type = CorrelationType.GeneralCorrelation;
        return item;
      }

      if (correlation is BaseCorrelation)
      {
        BaseCorrelation corr = (BaseCorrelation)correlation;
        double[] data = GetBaseCorrelationData(corr);
        BCParam par = new BCParam();
        par.CalibrationFailed = corr.CalibrationFailed;
        par.ErrorMessage = corr.ErrorMessage;
        par.InterpOnFactors = corr.InterpOnFactors;
        par.Method = corr.Method;
        par.NumStrikes = corr.Strikes.Length;
        par.ScalingFactor = corr.ScalingFactor;
        par.Max = InterpFactory.GetUpperBound(corr.Interp);
        par.Min = InterpFactory.GetLowerBound(corr.Interp);
        par.StrikeInterp = corr.InterpMethod;
        par.StrikeExtrap = corr.ExtrapMethod;
        par.StrikeMethod = corr.StrikeMethod;
        item.BaseCorrelationParam = par;
        item.Data = data;
        item.EntityNames = corr.EntityNames;
        item.Type = CorrelationType.BaseCorrelation;
        return item;
      }

      if (correlation is BaseCorrelationTermStruct)
      {
        BaseCorrelationTermStruct corr = (BaseCorrelationTermStruct)correlation;
        Item[] subitems = new Item[corr.BaseCorrelations.Length];
        for (int i = 0; i < subitems.Length; ++i)
          subitems[i] = GetItem(corr.BaseCorrelations[i]);
        item.SubItems = subitems;

        BCParam par = new BCParam();
        par.CalibrationMethod = corr.CalibrationMethod;
        par.Max = InterpFactory.GetUpperBound(corr.Interp);
        par.Min = InterpFactory.GetLowerBound(corr.Interp);
        par.TenorInterp = corr.InterpMethod;
        par.TenorExtrap = corr.ExtrapMethod;
        item.BaseCorrelationParam = par;

        item.Dates = FromDt(corr.Dates);
        item.EntityNames = corr.EntityNames;
        item.TenorNames = corr.TenorNames;
        item.Type = CorrelationType.BaseCorrelationTermStruct;
        return item;
      }

      if (correlation is BaseCorrelationCombined)
      {
        BaseCorrelationCombined corr = (BaseCorrelationCombined)correlation;
        Item[] subitems = new Item[corr.BaseCorrelations.Length];
        for (int i = 0; i < subitems.Length; ++i)
          subitems[i] = GetItem(corr.BaseCorrelations[i]);
        item.SubItems = subitems;

        BCParam par = new BCParam();
        par.Max = corr.MaxCorrelation;
        par.Min = corr.MinCorrelation;
        par.StrikeInterp = corr.StrikeInterp;
        par.StrikeExtrap = corr.StrikeExtrap;
        par.TenorInterp = corr.TenorInterp;
        par.TenorExtrap = corr.TenorExtrap;
        item.BaseCorrelationParam = par;

        item.Data = corr.Weights;
        item.EntityNames = corr.EntityNames;
        item.Type = CorrelationType.BaseCorrelationCombined;
        return item;
      }

      if (correlation is BaseCorrelationMixByName)
      {
        BaseCorrelationMixByName corr = (BaseCorrelationMixByName)correlation;
        Item[] subitems = new Item[corr.BaseCorrelations.Length];
        for (int i = 0; i < subitems.Length; ++i)
          subitems[i] = GetItem(corr.BaseCorrelations[i]);
        item.SubItems = subitems;
        item.EntityNames = corr.EntityNames;
        item.Type = CorrelationType.BaseCorrelationMixedByName;
        return item;
      }

      if (correlation is BaseCorrelationMixWeighted)
      {
        BaseCorrelationMixWeighted corr = (BaseCorrelationMixWeighted)correlation;
        Item[] subitems = new Item[corr.BaseCorrelations.Length];
        for (int i = 0; i < subitems.Length; ++i)
          subitems[i] = GetItem(corr.BaseCorrelations[i]);
        item.SubItems = subitems;
        item.Data = corr.Weights;
        item.EntityNames = corr.EntityNames;
        item.Type = CorrelationType.BaseCorrelationMixWeighted;
        return item;
      }

      return null;
    }

    private static CorrelationObject Create(Item item)
    {
      string[] names = item.EntityNames;
      double[] data = item.Data;
      Dt[] dates = GetDates(item);
      switch (item.Type)
      {
        case CorrelationType.SingleFactorCorrelation:
          {
            SingleFactorCorrelation corr = new SingleFactorCorrelation(names, data[0]);
            corr.Name = item.Name;
            return corr;
          }
      case CorrelationType.FactorCorrelation:
        {
          int nFactors = data.Length / Math.Max(1, names.Length);
          FactorCorrelation corr = new FactorCorrelation(names, nFactors, data);
          corr.Name = item.Name;
          return corr;
        }
      case CorrelationType.GeneralCorrelation:
        {
          GeneralCorrelation corr = new GeneralCorrelation(names, data);
          corr.Name = item.Name;
          return corr;
        }
      case CorrelationType.CorrelationTermStruct:
        {
          CorrelationTermStruct corr = new CorrelationTermStruct(names, data, dates);
          corr.Name = item.Name;
          return corr;
        }

      case CorrelationType.BaseCorrelation:
        {
          BCParam par = item.BaseCorrelationParam;
          int N = par.NumStrikes;
          double[] corrs = new double[N];
          double[] strikes = new double[N];
          double[] dp = null;
          if (data.Length >= 3 * N)
            dp = new double[N];
          for (int i = 0; i < N; ++i)
          {
            corrs[i] = data[i];
            strikes[i] = data[N + i];
            if (dp != null)
              dp[i] = data[N + N + i];
          }
          BaseCorrelation bc = new BaseCorrelation(
            par.Method, par.StrikeMethod, null, strikes, corrs);
          if (dp != null)
            bc.Detachments = dp;
          bc.Interp = InterpFactory.FromMethod(par.StrikeInterp, par.StrikeExtrap, par.Min, par.Max);
          bc.InterpOnFactors = par.InterpOnFactors;
          bc.ScalingFactor = par.ScalingFactor;
          bc.CalibrationFailed = par.CalibrationFailed;
          bc.ErrorMessage = par.ErrorMessage;
          bc.EntityNames = names;
          bc.Name = item.Name;
          return bc;
        }
      case CorrelationType.BaseCorrelationTermStruct:
        {
          BaseCorrelation[] bcs = GetBaseCorrelations<BaseCorrelation>(item);
          BaseCorrelationTermStruct bct = new BaseCorrelationTermStruct(
            dates, bcs);
          BCParam par = item.BaseCorrelationParam;
          bct.CalibrationMethod = par.CalibrationMethod;
          bct.Interp = InterpFactory.FromMethod(par.TenorInterp, par.TenorExtrap, par.Min, par.Max);
          bct.TenorNames = item.TenorNames;
          bct.EntityNames = names;
          bct.Name = item.Name;
          return bct;
        }
      case CorrelationType.BaseCorrelationCombined:
        {
          BaseCorrelationObject[] bcs = GetBaseCorrelations<BaseCorrelationObject>(item);
          BCParam par = item.BaseCorrelationParam;
          BaseCorrelationCombined bcc = new BaseCorrelationCombined(
            bcs, data, par.StrikeInterp, par.StrikeExtrap,
            par.TenorInterp, par.TenorExtrap, par.Min, par.Max);
          bcc.EntityNames = names;
          bcc.Name = item.Name;
          return bcc;
        }
      case CorrelationType.BaseCorrelationMixedByName:
        {
          BaseCorrelationObject[] bcs = GetBaseCorrelations<BaseCorrelationObject>(item);
          BaseCorrelationMixByName bcn = 
            BaseCorrelationMixByName.CreateCorrelationByNames(bcs, names);
          bcn.Name = item.Name;
          return bcn;
        }
      case CorrelationType.BaseCorrelationMixWeighted:
        {
          BaseCorrelationObject[] bcs = GetBaseCorrelations<BaseCorrelationObject>(item);
          BaseCorrelationMixWeighted bcw =
            new BaseCorrelationMixWeighted(bcs, data);
          bcw.EntityNames = names;
          bcw.Name = item.Name;
          return bcw;
        }
      }
      return null;
    }

    private static double[] GetBaseCorrelationData(BaseCorrelation corr)
    {
      int N = corr.Strikes.Length;
      int k = 2;
      if (corr.Detachments != null)
        ++k;
      double[] data = new double[k*N];
      for (int i = 0; i < N; ++i)
      {
        data[i] = corr.Correlations[i];
        data[N + i] = corr.Strikes[i];
        if (k == 3)
          data[N + N + i] = corr.Detachments[i];
      }
      return data;
    }

    private static string[] FromDt(Dt[] dates)
    {
      string[] s = new string[dates.Length];
      for (int i = 0; i < s.Length; ++i)
        s[i] = dates[i].ToStr("%D");
      return s;
    }


    private static Dt[] GetDates(Item item)
    {
      string[] s = item.Dates;
      if (s == null || s.Length < 1)
        return null;
      Dt[] dates = new Dt[s.Length];
      for (int i = 0; i < s.Length; ++i)
        dates[i] = Dt.FromStr(s[i], "%D");
      return dates;
    }

    private static T[] GetBaseCorrelations<T>(Item item)
      where T : BaseCorrelationObject
    {
      Item[] items = item.SubItems;
      T[] bcs = new T[items.Length];
      for (int i = 0; i < items.Length; ++i)
        bcs[i] = (T)Create(items[i]);
      return bcs;
    }

    #endregion Private Implementations

    #region Public Types and Data for Serialization


    /// <exclude/>
    public enum CorrelationType
    {
      /// <exclude/>
      SingleFactorCorrelation,
      /// <exclude/>
      FactorCorrelation,
      /// <exclude/>
      GeneralCorrelation,
      /// <exclude/>
      CorrelationTermStruct,
      /// <exclude/>
      CorrelationTermMixed,
      /// <exclude/>
      BaseCorrelation,
      /// <exclude/>
      BaseCorrelationTermStruct,
      /// <exclude/>
      BaseCorrelationCombined,
      /// <exclude/>
      BaseCorrelationMixWeighted,
      /// <exclude/>
      BaseCorrelationMixedByName,
    }

    /// <exclude/>
    [Serializable]
    public class BCParam
    {
      /// <exclude/>
      public int NumStrikes;
      /// <exclude/>
      public BaseCorrelationMethod Method;
      /// <exclude/>
      public BaseCorrelationStrikeMethod StrikeMethod;
      /// <exclude/>
      public BaseCorrelationCalibrationMethod CalibrationMethod;
      /// <exclude/>
      public InterpMethod StrikeInterp;
      /// <exclude/>
      public ExtrapMethod StrikeExtrap;
      /// <exclude/>
      public InterpMethod TenorInterp;
      /// <exclude/>
      public ExtrapMethod TenorExtrap;
      /// <exclude/>
      public bool InterpOnFactors;
      /// <exclude/>
      public double ScalingFactor; // factor used for scaling of original strikes
      /// <exclude/>
      public bool CalibrationFailed;
      /// <exclude/>
      public string ErrorMessage;
      /// <exclude/>
      public double Min = 0;
      /// <exclude/>
      public double Max = 1;
    }

    /// <summary>
    ///   Correlation data item
    /// </summary>
    /// <exclude/>
    [Serializable]
    public class Item
    {
      /// <exclude/>
      public CorrelationType Type;
      /// <exclude/>
      public string Name;
      /// <exclude/>
      public string[] EntityNames;
      /// <exclude/>
      public double[] Data;
      /// <exclude/>
      public string[] Dates;
      /// <exclude/>
      public string[] TenorNames;
      // Mixed correlation has component correlations
      /// <exclude/>
      public Item[] SubItems;
      /// <exclude/>
      public BCParam BaseCorrelationParam;
    }

    /// <exclude/>
    public Item Data;

    #endregion Public Types and Data for Serial;ization

  } // class CorrelationData
}
