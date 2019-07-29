/*
 * CorrelationFactor.cs
 *
 * Helper factory classes for correlation objects
 *
 *  -2008. All rights reserved.
 *
 * TBD: Remove need for functions which filter based on notional after Hehui's changes. RTD Nov05
 *
 */

using System;
using System.Collections;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Base
{

  ///
  /// <summary>
  ///   Helper factory methods for basket correlation objects
  /// </summary>
  ///
  /// <remarks>
  ///   This class provides constructor and conversion routines for basket correlations.
  /// </remarks>
  ///
  public abstract class CorrelationFactory : BaseEntityObject
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CorrelationFactory));

    #region Constructors

    /// <exclude/>
    protected CorrelationFactory() { }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Create a factor correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Does a best-fit from the specified correlation object.</para>
    ///   <para>Note that the name of the correlation object is copied.</para>
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    ///
    /// <returns>FactorCorrelation from correlation</returns>
    /// 
    public static FactorCorrelation
    CreateFactorCorrelation(Correlation correlation)
    {
      int nBasket = correlation.BasketSize;
      FactorCorrelation corr;

      if (correlation is SingleFactorCorrelation)
      {
        double[] data = new double[nBasket];
        for (int i = 0, idx = 0; i < nBasket; ++i)
          data[idx++] = ((SingleFactorCorrelation)correlation).GetFactor();
        corr = new FactorCorrelation(correlation.Names, 1, data);
      }
      else if (correlation is FactorCorrelation)
      {
        // no need to convert
        return (FactorCorrelation)correlation;
      }
      else // not factor correlation
      {
        const int numFactors = 1;
        double[] origData = new double[nBasket * nBasket];
        for (int i = 0, idx = 0; i < nBasket; ++i)
          for (int j = 0; j < nBasket; j++)
            origData[idx++] = correlation.GetCorrelation(i, j);
        corr = FactorFit(correlation.Names, origData, numFactors);
      }
      corr.Name = correlation.Name;
      return corr;
    }

    /// <summary>
    ///   Create a factor correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   <para>It chooses a subset of names with non-zero notionals</para>
    ///   <para>Note that the name of the correlation object is copied.</para>
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    /// <param name="notionals">Array of notionals</param>
    ///
    /// <returns>FactorCorrelation from correlation</returns>
    /// 
    public static FactorCorrelation
    CreateFactorCorrelation(Correlation correlation, double[] notionals)
    {
      // count positive notionals
      int nBasket = 0;
      for (int i = 0; i < notionals.Length; ++i)
        if (notionals[i] != 0.0) ++nBasket;

      // Do we have to choose a subset?
      if (nBasket == correlation.BasketSize)
        return CreateFactorCorrelation(correlation);

      // We have select a subset
      string[] names = new string[nBasket];
      // For single factor correlations, allow mising names
      bool useNames = (correlation is SingleFactorCorrelation && correlation.Names.Length <=1) ? false : true;
      if (useNames)
      {
        if (notionals.Length > correlation.Names.Length)
        {
          throw new ArgumentException(String.Format(
            "Consituents of correlation (len={0}) and notionals (len={1}) not match.",
            correlation.Names.Length, notionals.Length));
        }
        for (int i = 0, j = 0; i < notionals.Length; ++i)
          if (notionals[i] != 0.0)
            names[j++] = correlation.Names[i];
      }
      else
      {
        for (int i = 0, j = 0; i < notionals.Length; ++i)
          if (notionals[i] != 0.0)
            names[j++] = String.Format("Name {0}", j + 1);
      }

      FactorCorrelation corr;
      if (correlation is FactorCorrelation)
      {
        int nFactors = ((FactorCorrelation)correlation).NumFactors;
        double[] data = new double[nBasket * nFactors];
        for (int f = 0, idx = 0; f < nFactors; ++f)
          for (int i = 0; i < notionals.Length; ++i)
            if (notionals[i] != 0.0)
              data[idx++] = ((FactorCorrelation)correlation).GetFactor(f, i);
        corr = new FactorCorrelation(names, nFactors, data);
      }
      else // not factor correlation
      {
        const int numFactors = 1;
        double[] origData = new double[nBasket * nBasket];
        for (int i = 0, idx = 0; i < notionals.Length; ++i)
          if (notionals[i] != 0.0)
          {
            for (int j = 0; j < notionals.Length; j++)
              if (notionals[j] != 0.0)
                origData[idx++] = correlation.GetCorrelation(i, j);
          }
        corr = FactorFit(names, origData, numFactors);
      }
      corr.Name = correlation.Name;
      return corr;
    }

    /// <summary>
    ///   Create a factor correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   <para>It chooses a subset of names with non-zero notionals</para>
    ///   <para>Note that the name of the correlation object is copied.</para>
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    /// <param name="names">Array of names to get correlations for</param>
    ///
    /// <returns>FactorCorrelation from correlation</returns>
    /// 
    public static FactorCorrelation
    CreateFactorCorrelation(Correlation correlation, string[] names)
    {
      // Look up the correlations we need
      int[] index = new int[names.Length];
      if (correlation is SingleFactorCorrelation)
      {
        // Ignore correlation names for Single factor correlations
        for (int i = 0; i < names.Length; i++)
          index[i] = i;
      }
      else
      {
        for (int i = 0; i < names.Length; i++)
        {
          index[i] = correlation.Index(names[i]);
          if (index[i] < 0)
            throw new ArgumentException(String.Format("Missing correlation for name {0}", names[i]));
        }
      }

      // Do we have to choose a subset? Assume we have a perfect match for now.
      if (correlation.BasketSize == names.Length)
        return CreateFactorCorrelation(correlation);

      // Get correlations
      FactorCorrelation corr;
      if (correlation is FactorCorrelation)
      {
        int nFactors = ((FactorCorrelation)correlation).NumFactors;
        double[] data = new double[names.Length * nFactors];

        for (int f = 0, idx = 0; f < nFactors; ++f)
          for (int i = 0; i < names.Length; ++i)
            data[idx++] = ((FactorCorrelation)correlation).GetFactor(f, index[i]);

        corr = new FactorCorrelation(names, nFactors, data);
      }
      else // not factor correlation
      {
        const int numFactors = 1;
        double[] origData = new double[names.Length * names.Length];
        for (int i = 0, idx = 0; i < names.Length; ++i)
          for (int j = 0; j < names.Length; j++)
            origData[idx++] = correlation.GetCorrelation(index[i], index[j]);
        corr = FactorFit(names, origData, numFactors);
      }

      corr.Name = correlation.Name;
      return corr;
    }

    /// <summary>
    ///   Find the best fit of factor correlation from another correlation object
    /// </summary>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    /// <param name="maxFactors">Maximum number of factors to fit</param>
    ///
    /// <returns>FactorCorrelation from correlation</returns>
    /// 
    public static FactorCorrelation
    CreateFactorCorrelation(Correlation correlation, int maxFactors)
    {
      int nBasket = correlation.BasketSize;
      FactorCorrelation corr;

      if (correlation is SingleFactorCorrelation)
      {
        int nFactors = ((FactorCorrelation)correlation).NumFactors;
        double[] data = new double[nBasket * nFactors];
        for (int f = 0, idx = 0; f < nFactors; ++f)
          for (int i = 0; i < nBasket; ++i)
            data[idx++] = ((FactorCorrelation)correlation).GetFactor(f, i);
        corr = new FactorCorrelation(correlation.Names, nFactors, data);
      }
      else if (correlation is FactorCorrelation
                       && ((FactorCorrelation)correlation).NumFactors <= maxFactors)
      {
        // no need to convert
        return (FactorCorrelation)correlation;
      }
      else // convert
      {
        int numFactors = maxFactors;
        double[] origData = new double[nBasket * nBasket];
        for (int i = 0, idx = 0; i < nBasket; ++i)
          for (int j = 0; j < nBasket; j++)
            origData[idx++] = correlation.GetCorrelation(i, j);
        corr = FactorFit(correlation.Names, origData, numFactors);
      }

      corr.Name = correlation.Name;
      return corr;
    }

    /// <summary>
    ///   Create a single factor correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Does a best-fit from the specified correlation object.</para>
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    ///
    /// <returns>SingleFactorCorrelation from Correlation</returns>
    /// 
    public static SingleFactorCorrelation
    CreateSingleFactorCorrelation(Correlation correlation)
    {
      if (correlation is SingleFactorCorrelation)
        return (SingleFactorCorrelation)correlation;
      else
      {
        FactorCorrelation factorCorr = CorrelationFactory.CreateFactorCorrelation(correlation);
        double[] data = factorCorr.Correlations;
        double sum = 0;
        for (int i = 0; i < data.Length; ++i)
          sum += data[i];
        SingleFactorCorrelation corr
            = new SingleFactorCorrelation(factorCorr.Names, sum / data.Length);
        corr.Name = correlation.Name;

        // compute the standard error and maximum error in correlations
        double stdErr = 0, maxErr = 0;
        for (int i = 0; i < corr.BasketSize; ++i)
          for (int j = 0; j < i; ++j)
          {
            double r0 = correlation.GetCorrelation(i, j);
            double r1 = corr.GetCorrelation(i, j);
            double d = Math.Abs(r1 - r0);
            maxErr = Math.Max(maxErr, d);
            stdErr += d * d;
          }
        if (corr.BasketSize > 1)
          stdErr /= corr.BasketSize * (corr.BasketSize - 1);
        stdErr = Math.Sqrt(stdErr);
        corr.StdError = stdErr;
        corr.MaxError = maxErr;

        // done
        return corr;
      }
    }

    /// <summary>
    ///   Create a single factor correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   Does a best-fit from the specified correlation object.
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    /// <param name="notionals">Array of notionals</param>
    ///
    public static SingleFactorCorrelation
    CreateSingleFactorCorrelation(Correlation correlation, double[] notionals)
    {
      // Hack for now. Won't need after Hehui's changes to correlations
      FactorCorrelation c = CreateFactorCorrelation(correlation, notionals);
      return CreateSingleFactorCorrelation(c);
    }

    /// <summary>
    ///   Create a General Matrix Correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   Does a best-fit from the specified correlation object.
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    ///
    /// <returns>GeneralCorrelation from a Correlation</returns>
    /// 
    public static GeneralCorrelation
    CreateGeneralCorrelation(Correlation correlation)
    {
      int nBasket = correlation.BasketSize;

      if (correlation is GeneralCorrelation)
        // no need to convert
        return (GeneralCorrelation)correlation;

      double[] data = new double[nBasket * nBasket];
      for (int i = 0, idx = 0; i < nBasket; ++i)
        for (int j = 0; j < nBasket; j++)
          data[idx++] = correlation.GetCorrelation(i, j);

      GeneralCorrelation corr = new GeneralCorrelation(correlation.Names, data);
      corr.Name = correlation.Name;
      return corr;
    }

    /// <summary>
    ///   Create a General Matrix Correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   It chooses a subset of names with positive notionals
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    /// <param name="notionals">Array of notionals</param>
    ///
    /// <returns>GeneralCorrelation from Correlation</returns>
    /// 
    public static GeneralCorrelation
    CreateGeneralCorrelation(Correlation correlation, double[] notionals)
    {
      // count positive notionals
      int nBasket = 0;
      for (int i = 0; i < notionals.Length; ++i)
        if (notionals[i] != 0.0) ++nBasket;

      // Do we have to choose a subset?
      if (nBasket == correlation.BasketSize)
        return CreateGeneralCorrelation(correlation);

      // Choose a subset
      string[] names = new string[nBasket];
      double[] data = new double[nBasket * nBasket];
      for (int i = 0, idx = 0, sidx = 0; i < notionals.Length; ++i)
      {
        if (notionals[i] != 0.0)
        {
          names[sidx++] = correlation.Names[i];
          for (int j = 0; j < notionals.Length; j++)
            if (notionals[j] != 0.0)
              data[idx++] = correlation.GetCorrelation(i, j);
        }
      }

      GeneralCorrelation corr = new GeneralCorrelation(names, data);
      corr.Name = correlation.Name;
      return corr;
    }

    /// <summary>
    ///   Create a General Matrix Correlation from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   It chooses a subset of names
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    /// <param name="names">Array of names to get correlations for</param>
    ///
    /// <returns>GeneralCorrelation from Correlation</returns>
    /// 
    public static GeneralCorrelation
    CreateGeneralCorrelation(Correlation correlation, string[] names)
    {
      // Look up the correlations we need
      int[] index = new int[names.Length];
      for (int i = 0; i < names.Length; i++)
      {
        index[i] = correlation.Index(names[i]);
        if (index[i] < 0)
          throw new ArgumentException(String.Format("Missing correlation for name {0}", names[i]));
      }

      // Do we have to choose a subset? Assume we have a perfect match for now.
      if (correlation.BasketSize == names.Length)
        return CreateGeneralCorrelation(correlation);

      // Get correlations
      double[] data = new double[names.Length * names.Length];
      for (int i = 0, idx = 0; i < names.Length; i++)
        for (int j = 0; j < names.Length; j++)
          data[idx++] = correlation.GetCorrelation(index[i], index[j]);

      GeneralCorrelation corr = new GeneralCorrelation(names, data);
      corr.Name = correlation.Name;
      return corr;
    }

    // Helper function which
    //   (1) fits general matrix into factors
    //   (2) computes the maximum error
    private static FactorCorrelation
    FactorFit(string[] names,
                         double[] matrix,
                         int numFactors)
    {
      int nBasket = names.Length;
      double[] data = new double[nBasket * numFactors];
      double stdErr = BasketCorrelationModel.fitFactors(numFactors, nBasket, 1, matrix, data);
      FactorCorrelation corr = new FactorCorrelation(names, numFactors, data);
      corr.StdError = stdErr;

      // compute the maximum error
      double maxErr = 0;
      for (int i = 0; i < nBasket; ++i)
        for (int j = 0; j < i; ++j)
        {
          double r1 = corr.GetCorrelation(i, j);
          double r0 = matrix[nBasket * i + j];
          maxErr = Math.Max(maxErr, Math.Abs(r1 - r0));
        }
      corr.MaxError = maxErr;

      return corr;
    }

    /// <summary>
    ///   Create a base correlation object from a combination of weighted base correlations.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The constructed base correlation object can be either a curve (skew) or a surface,
    ///   depending on the input parameters.  If the array <c>baseCorrelations</c> contains base
    ///   correlation curves only, then the return object is a curve.  If any element of the
    ///   array <c>baseCorrelations</c> is a surface, then the return object is a surface. 
    ///   In either case, base correlations are combined using the specified weights.</para>
    /// </remarks>
    ///
    /// <param name="baseCorrelations">Base correlation surfaces to combine</param>
    /// <param name="weights">Weights to combine base correlations</param>
    /// <param name="strikeInterp">Interpolation method between strikes</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes</param>
    /// <param name="timeInterp">Interpolation method between dates</param>
    /// <param name="timeExtrap">Extrapolation method for dates</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen for strikes</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen for strikes</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public static BaseCorrelationObject
    CreateCombinedBaseCorrelation(
      BaseCorrelationObject[] baseCorrelations,
      double[] weights,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod timeInterp,
      ExtrapMethod timeExtrap,
      double min,
      double max
      )
    {
      // Validation
      if (baseCorrelations == null || baseCorrelations.Length < 1)
        throw new ArgumentException("Must specify Base correlations");
      if (baseCorrelations.Length != weights.Length)
        throw new ArgumentException("Base correlations and strike should be of the same size");

      // Find the combined tenor dates
      BaseCorrelationTermStruct bcRef = null;
      ArrayList list = new ArrayList();
      for (int i = 0; i < baseCorrelations.Length; ++i)
      {
        BaseCorrelationObject bc = baseCorrelations[i];
        if (bc is BaseCorrelationTermStruct)
        {
          // check the consistency of term structure calibration methods
          if (bcRef == null)
            bcRef = (BaseCorrelationTermStruct)bc;
          else if (bcRef.CalibrationMethod != ((BaseCorrelationTermStruct)bc).CalibrationMethod)
            throw new ArgumentException("Inconsistent calibration methods");

          Dt[] di = ((BaseCorrelationTermStruct)bc).Dates;
          for (int j = 0; j < di.Length; ++j)
          {
            Dt dt = di[j];
            int pos = list.BinarySearch(dt);
            if (pos < 0)
              list.Insert(~pos, dt);
          }
        }
      }

      Dt[] dates = new Dt[list.Count];
      for (int i = 0; i < list.Count; ++i)
        dates[i] = (Dt)list[i];

      if (dates.Length < 1)
        dates = new Dt[1];
      int nTenors = dates.Length;
      BaseCorrelation[] bcs = new BaseCorrelation[nTenors];

      // We need a work array
      BaseCorrelation[] work = new BaseCorrelation[baseCorrelations.Length];
      for (int j = 0; j < nTenors; ++j)
      {
        for (int i = 0; i < baseCorrelations.Length; ++i)
        {
          BaseCorrelationObject b = baseCorrelations[i];
          if (b is BaseCorrelationTermStruct)
            work[i] = ((BaseCorrelationTermStruct)b).GetBaseCorrelation(dates[j]);
          else if (b is BaseCorrelation)
            work[i] = (BaseCorrelation)b;
          else
            throw new ArgumentException("The JoinSurfaces from mixed base correlation is not allowed");
        }
        for (int i = 1; i < work.Length; ++i)
          if (work[i].Method != work[0].Method ||
              work[i].StrikeMethod != work[0].StrikeMethod)
            throw new ArgumentException("All the base correlations must have the same method and strike method");

        BaseCorrelation bcorr = new BaseCorrelation(
          work, weights, work[0].Method, work[0].StrikeMethod, work[0].StrikeEvaluator,
          strikeInterp, strikeExtrap, min, max);
        bcorr.Extended = (max > 1);
        bcs[j] = bcorr;
      }

        // Construct base correlation term structure
        BaseCorrelationTermStruct bcts = new BaseCorrelationTermStruct(dates, bcs);
        bcts.Extended = (max > 1);
        bcts.Interp = InterpFactory.FromMethod(timeInterp, timeExtrap, min, max);
        bcts.CalibrationMethod = bcRef.CalibrationMethod;
        return bcts;
    }

    /// <summary>
    ///   Create a correlation term structure from another correlation object
    /// </summary>
    ///
    /// <remarks>
    ///   It chooses a subset of names with no-zero notionals.
    /// </remarks>
    ///
    /// <param name="correlation">Correlation to construct from</param>
    /// <param name="notionals">Array of notionals</param>
    ///
    /// <exclude />
    public static CorrelationTermStruct
    CreateCorrelationTermStruct(Correlation correlation, double[] notionals)
    {
      // basic correlation data
      double[] origData = correlation.Correlations;
      int stride = origData.Length;
      int nDates = 1;
      Dt[] dates = new Dt[1];
      if( correlation is CorrelationTermStruct)
      {
        CorrelationTermStruct tc = (CorrelationTermStruct)correlation;
        nDates = tc.Dates.Length;
        dates = new Dt[nDates];
        for(int t = 0; t < nDates; ++t)
          dates[t] = tc.Dates[t];
        stride = origData.Length / nDates;
        if(stride <= 0)
          throw new ArgumentException("data length cannot smaller than number of dates");
      }
      int nFactors = stride <= correlation.BasketSize ? 1 : (stride / correlation.BasketSize);
      double[] data = null;

      // count positive notionals
      int nBasket = 0;
      for (int i = 0; i < notionals.Length; ++i)
        if (notionals[i] != 0.0) ++nBasket;

      // Do we have to choose a subset?
      if (nBasket < correlation.BasketSize)
      {
        // We have select a subset
        data = new double[nDates * nBasket * nFactors];
        for(int t = 0, idx = 0; t < nDates; ++t)
        {
          int baseIdx = t * stride;
          for (int f = 0; f < nFactors; ++f) {
            for (int i = 0; i < notionals.Length; ++i)
              if (notionals[i] != 0.0) {
                int offset = correlation.BasketSize * f + i;
                if(offset >= stride)
                  offset = stride - 1;
                data[idx++] = origData[baseIdx + offset];
              }
          }
        }
      }
      else 
      {
        data = new double[origData.Length];
        for(int i = 0; i < origData.Length; ++i)
          data[i] = origData[i];
      }

      // copy names
      string[] names = new string[nBasket];
      // For single factor correlations, allow mising names
      bool useNames = (nFactors <= 1 && stride <= 1 && correlation.Names.Length == 0) ? false : true;
      for (int i = 0, j = 0; i < notionals.Length; ++i)
        if (notionals[i] != 0.0)
          names[j++] = useNames ? correlation.Names[i] : String.Format("Name {0}", j + 1);

      // new term structure
      return new CorrelationTermStruct(names, data, dates);
    }

    /// <summary>
    ///   Create a correlation term structure template from base correlation object
    /// </summary>
    /// <exclude />
    public static CorrelationTermStruct
    CreateCorrelationTermStruct(BaseCorrelationObject bco, SurvivalCurve[] sc, Dt asOf)
    {
      int basketSize = sc.Length;
      if (basketSize <= 0)
        return null;
      string[] names = new string[basketSize];
      for (int i = 0; i < basketSize; ++i)
      {
        names[i] = sc[i].Name;
      }

      if (bco is BaseCorrelationTermStruct)
      {
        BaseCorrelationTermStruct bct = (BaseCorrelationTermStruct)bco;
        Dt[] dates = bct.Dates;
        double[] data = new double[dates.Length];
        return new CorrelationTermStruct(names, data, dates);
      }
      else
        return new CorrelationTermStruct(names, new double[1], new Dt[1] { asOf });
    }

    /// <summary>
    ///   Create a correlation term structure template from base correlation object
    /// </summary>
    /// <exclude />
    internal static CorrelationTermStruct
    CreateCorrelationTermStruct(BaseCorrelationObject bco, 
      string[] names, SurvivalCurve[] sc, Dt asOf)
    {
      int basketSize = sc.Length;
      if (basketSize <= 0)
        return null;
      if (names == null)
      {
        names = new string[basketSize];
        for (int i = 0; i < basketSize; ++i)
        {
          names[i] = sc[i].Name;
        }
      }

      if (bco is BaseCorrelationTermStruct)
      {
        BaseCorrelationTermStruct bct = (BaseCorrelationTermStruct)bco;
        Dt[] dates = bct.Dates;
        double[] data = new double[dates.Length];
        return new CorrelationTermStruct(names, data, dates);
      }
      else
        return new CorrelationTermStruct(names, new double[1], new Dt[1] { asOf });
    }

    #endregion // Methods

  } // class CorrelationFactory

}
