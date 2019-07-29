/*
 * CorrelationTermStruct.cs
 *
 * Correlation term structure
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Data;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   Correlation term structure
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This class provides basic data structures and defines basic interface
  ///   for all correlation term structure objects.</para>
  ///   <para>At this implementation, this class is for internal use only.</para>
  /// </remarks>
  ///
  /// <exclude />
  [Serializable]
  public class CorrelationTermStruct : Correlation, ICorrelationBumpTermStruct
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CorrelationTermStruct));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///  <para>The parameter <c>data</c> is a flat, one dimensitional array of correlation/factor numbers,
    ///   such that ArrayLength = NumberOfDates x NumberOfCorrelationsPerDate.</para>
    ///
    ///  <para>At this moment, NumberOfCorrelationsPerDate can be either 1,
    ///   which means SingleFactorCorrelation,
    ///   or NumberOfNames x NumberOfFactors, which means FactorCorrelation
    ///   and the number of factors should be 1, 2, or 3.
    ///   All other values are error.</para>
    ///
    ///  <para>TODO: This is a quick fix to get things work.  It should be revisited later.</para>
    /// </remarks>
    ///
    /// <param name="names">Correlated credit names</param>
    /// <param name="data">Copula correlation data</param>
    /// <param name="dates">Array of dates matching each correlation data set</param>
    ///
    /// <returns>Created correlation term structure</returns>
    ///
    /// <exclude />
    public CorrelationTermStruct(
      string[] names, double[] data, Dt[] dates)
      : this(names, data, dates, 0.0, 1.0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationTermStruct"/> class.
    /// </summary>
    /// <param name="names">Correlated credit names</param>
    /// <param name="data">Copula correlation data</param>
    /// <param name="dates">Array of dates matching each correlation data set</param>
    /// <param name="min">The minimum correlation allowed.</param>
    /// <param name="max">The maximum correlation allowed.</param>
    internal CorrelationTermStruct(
      string[] names,  double[] data, Dt[] dates,
      double min, double max)
      : base(names, data, min, max)
    {
      // Sanity check
      if (dates == null || dates.Length == 0)
        throw new ArgumentException(String.Format("Null date array"));

      int stride = data.Length / dates.Length;
      if (data.Length != dates.Length * stride)
        throw new ArgumentException(String.Format("Correlation data is not well-formed, its length should be {1}x{2}, but got {0}.",
            data.Length, dates.Length, stride));

      if (stride != 1 && this.BasketSize != stride)
      {
        int numFactors = stride / this.BasketSize;
        if (numFactors * this.BasketSize != stride)
          throw new ArgumentException(String.Format("Number of correlations per date ({0}) not match the basket size ({1})",
              stride, this.BasketSize));
      }

      // Initialize data members
      dates_ = dates;

      // Time interpolation method
      this.Interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);

      return;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CorrelationTermStruct obj = (CorrelationTermStruct)base.Clone();

      obj.dates_ = new Dt[dates_.Length];
      for (int i = 0; i < dates_.Length; ++i)
        obj.dates_[i] = dates_[i];

      return obj;
    }

    /// <summary>
    ///   Create a generalized correlation term struct object
    ///   <preliminary/>
    /// </summary>
    /// 
    /// <remarks>
    ///   A generalized correlation object intend to be used with Hull-White
    ///   dynamic model.  It does not require that the number of factors matches
    ///   the number of names.
    /// </remarks>
    /// 
    /// <param name="names">Correlated credit names</param>
    /// <param name="data">Copula correlation data</param>
    /// <param name="dates">Array of dates matching each correlation data set</param>
    /// 
    /// <returns>CorrelationTermStruct object</returns>
    public static CorrelationTermStruct Generalized(
      string[] names, double[] data, Dt[] dates)
    {
      // Sanity check
      if (dates == null || dates.Length == 0)
        throw new ArgumentException(String.Format("Null date array"));

      int stride = data.Length / dates.Length;
      if (data.Length != dates.Length * stride)
        throw new ArgumentException(String.Format(
          "Correlation data is not well-formed, its length should be {1}x{2}, but got {0}.",
          data.Length, dates.Length, stride));

      CorrelationTermStruct cts = new CorrelationTermStruct(names, data);
      cts.dates_ = dates;
      return cts;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Correlated credit names</param>
    /// <param name="data">Copula correlation data</param>
    private CorrelationTermStruct(string[] names, double[] data)
      : base(names, data, 0.0, 1.0)
    { }

    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Create correlation term structure from a sequence of correlation objects
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is function does a lot of conversion
    ///   works before actually construct a term structure object.  It is really
    ///   a factory function and hence I decided not to make this a constructor.</para>
    ///
    ///  <para>This function arranges correlation numbers in a flat, one dimensitional array,
    ///   such that ArrayLength = NumberOfDates x NumberOfCorrelationsPerDate.</para>
    ///
    ///  <para>At this moment, NumberOfCorrelationsPerDate can be either 1,
    ///   which means SingleFactorCorrelation,
    ///   or NumberOfNames x NumberOfFactors, which means FactorCorrelation
    ///   and the number of factors should be 1, 2, or 3.
    ///   All other values are error.</para>
    ///
    ///  <para>TODO: This is a quick fix to get things work.  It should be revisited later.</para>
    /// </remarks>
    ///
    /// <param name="names">Correlated credit names</param>
    /// <param name="dates">Array of dates matching each correlation data set</param>
    /// <param name="correlations">Array of correlations</param>
    ///
    /// <returns>Created correlation term structure</returns>
    public static CorrelationTermStruct
    FromCorrelations(
        string[] names,
        Dt[] dates,
        Correlation[] correlations
        )
    {
      // Sanity check
      if (dates == null || dates.Length == 0)
        throw new ArgumentException(String.Format("Null date array"));
      if (correlations == null || correlations.Length == 0)
        throw new ArgumentException(String.Format("Null correlation array"));

      double[] data = null;
      // Check correlation: At this moment we only allow SingleFactorCorrelation and FactorCorrelation
      // with one factor.
      if (correlations[0] is SingleFactorCorrelation)
      {
        int N = correlations.Length;
        data = new double[N];
        for (int i = 0; i < N; ++i)
        {
          if (!(correlations[i] is SingleFactorCorrelation))
            throw new ArgumentException(String.Format("Correlation {0} is not SingleFactorCorrelation", i));
          data[i] = ((SingleFactorCorrelation)correlations[i]).GetFactor();
          if (names == null) names = correlations[i].Names;
        }
      }
      else if (correlations[0] is FactorCorrelation)
      {
        int basketSize = (names == null ? ((FactorCorrelation)correlations[0]).BasketSize : names.Length);
        int numFactors = ((FactorCorrelation)correlations[0]).NumFactors;
        int N = basketSize * numFactors * correlations.Length;
        data = new double[N];
        for (int i = 0, idx = 0; i < correlations.Length; ++i)
        {
          if (!(correlations[i] is FactorCorrelation))
            throw new ArgumentException(String.Format("Correlation {0} is not FactorCorrelation", i));
          FactorCorrelation corr = (FactorCorrelation)correlations[i];
          if (corr.BasketSize != basketSize)
            throw new ArgumentException(String.Format("Basket size ({1}) of correlation {0} not match {2}",
                i, corr.BasketSize, basketSize));
          if (corr.NumFactors != numFactors)
            throw new ArgumentException(String.Format("Number of factors ({1}) of correlation {0} not match {2}",
                i, corr.NumFactors, numFactors));
          double[] srcData = corr.Correlations;
          for (int j = 0; j < srcData.Length; ++j)
            data[idx++] = srcData[j];
          if (names == null) names = corr.Names;
        }
      }
      else
        throw new NotImplementedException(String.Format("Cannot create CorrelationTermStruct from type {0}",
            correlations[0].GetType()));

      CorrelationTermStruct cts = new CorrelationTermStruct(names, data, dates);
      return cts;
    }

    ///
    /// <summary>
    ///   Correlation of defaults between name i and j
    /// </summary>
    ///
    /// <remarks>This function returns the correlation at the first tenor date.</remarks>
    ///
    /// <param name="i">Index of name i</param>
    /// <param name="j">Index of name j</param>
    public override double GetCorrelation(int i, int j)
    {
      if (i == j) return 1.0;

      double[] data = Correlations;
      int stride = data.Length / Dates.Length;
      if (stride == 1)
        return data[0] * data[0];
      else if (stride == this.BasketSize)
      {
        int basketSize = this.BasketSize;
        int numFactors = stride / basketSize;
        if (1 == numFactors)
          return (data[i] * data[j]);
        else if (2 == numFactors)
          return (data[i] * data[j]
              + data[basketSize + i] * data[basketSize + j]);
        else
        {
          int basketSize2 = basketSize + basketSize;
          return (data[i] * data[j]
              + data[basketSize + i] * data[basketSize + j]
              + data[basketSize2 + i] * data[basketSize2 + j]);
        }
      }
      else
        throw new System.NotSupportedException("GetCorrelation(i,j) is not supported by generalized correlation object");
    }

    /// <summary>
    ///   Bump correlations by index
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    ///
    ///   <para>This function bumps correlation of all the tenor dates paralelly.</para>
    /// </remarks>
    ///
    /// <param name="i">Index of name i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    ///
    public override double BumpCorrelations(int i, double bump, bool relative, bool factor)
    {
      double delta = 0;
      for (int tenor = 0; tenor < Dates.Length; ++tenor)
        delta += BumpTenor(tenor, i, bump, relative, factor);
      return delta / Dates.Length;
    }

    /// <summary>
    ///  Bump all the correlations simultaneously
    /// </summary>
    ///
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in factors</returns>
    public override double BumpCorrelations(double bump, bool relative, bool factor)
    {
      double delta = 0;
      double[] data = Correlations;
      int N = data.Length;
      int stride = N / Dates.Length;
      int numFactors = stride / BasketSize;
      if (numFactors < 1)
        numFactors = 1;
      double min = (factor) ? MinFactor : MinCorrelation;
      double max = (factor) ? MaxFactor : MaxCorrelation;
      for (int i = 0; i < N; ++i)
      {
        double orig = (factor) ? data[i] : data[i] * data[i];
        double corr = relative ?
          orig * ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump)))
          : orig + bump / numFactors;
        if (corr > max)
          corr = max;
        else if (corr < min)
          corr = min;
        data[i] = (factor) ? corr : Math.Sqrt(corr);
        delta += corr - orig;
      }
      delta /= Dates.Length;
      if (stride == 1) // Single factor
        return delta;
      return delta / BasketSize;
    }

    /// <summary>
    ///   Bump correlations by index and tenor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    ///
    ///   <para>This function bumps correlations of the given tenor.</para>
    /// </remarks>
    ///
    /// <param name="tenor">Index of tenor</param>
    /// <param name="i">Index of name i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    ///
    public double BumpTenor(int tenor, int i, double bump, bool relative, bool factor)
    {
      if (tenor < 0 || tenor >= dates_.Length)
        throw new ArgumentException(String.Format("Tenor {0} is out of range", tenor));

      double[] data = Correlations;
      int stride = data.Length / Dates.Length;
      if (i >= stride)
        throw new ArgumentException(String.Format("Index {0} larger than the maximum permitted ({1})",
            i, stride));

      double min = (factor) ? MinFactor : MinCorrelation;
      double max = (factor) ? MaxFactor : MaxCorrelation;
      double delta = 0;
      int basketSize = BasketSize;
      int numFactors = stride / basketSize;
      int tenorIndex = tenor * stride;
      for (int f = 0; f < numFactors; f++)
      {
        int idx = tenorIndex + this.BasketSize * f;
        double orig = (factor) ? data[idx] : data[idx] * data[idx];
        double corr = relative ?
          orig * ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump)))
          : orig + bump / numFactors;
        if (corr > max)
          corr = max;
        else if (corr < min)
          corr = min;
        data[idx] = (factor) ? corr : Math.Sqrt(corr);
        delta += corr - orig;
      }

      return delta;
    }

    /// <summary>
    ///   Bump correlations by tenor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    ///
    ///   <para>This function bumps correlations of all the names for the given tenor.</para>
    /// </remarks>
    ///
    /// <param name="tenor">Index of tenor</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    ///
    public double BumpTenor(int tenor, double bump, bool relative, bool factor)
    {
      double delta = 0;
      int basketSize = BasketSize;
      for (int i = 0; i < basketSize; ++i)
        delta += BumpTenor(tenor, i, bump, relative, factor);
      delta /= basketSize;
      return delta;
    }

    /// <summary>
    ///   Set all the factors to the same value
    /// </summary>
    ///
    /// <param name="factor">Factor to set</param>
    public override void SetFactor(double factor)
    {
      double[] data = Correlations;
      for (int i = 0; i < data.Length; ++i)
        data[i] = factor;
    }

    /// <summary>
    ///   Set all the factors at a tenor date to the same value
    /// </summary>
    ///
    /// <param name="fromDate">The tenor from which to set factor</param>
    /// <param name="factor">Factor to set</param>
    ///
    public override void SetFactor(Dt fromDate, double factor)
    {
      if (dates_.Length == 1)
      {
        SetFactor(factor);
        return;
      }

      // More than one dates
      for (int i = 0; i < dates_.Length; ++i)
        if (dates_[i] >= fromDate)
        {
          SetFactorAtDate(i, factor);
          return;
        }

      // date is later than the last tenor
      SetFactorAtDate(dates_.Length - 1, factor);
    }

    /// <summary>
    ///   Set all the factors for all tenors from a specific date
    /// </summary>
    ///
    /// <param name="date">Starting date</param>
    /// <param name="factor">Factor to set</param>
    ///
    public void
    SetFactorFrom(Dt date, double factor)
    {
      if (dates_.Length == 1)
      {
        SetFactor(factor);
        return;
      }

      // More than one dates
      for (int i = 0; i < dates_.Length; ++i)
        if (dates_[i] >= date)
        {
          SetFactorFrom(i, factor);
          return;
        }

      // date is later than the last tenor
      throw new ArgumentException(String.Format(
          "Date {0} is later than the last tenor date {1}",
          date, dates_[dates_.Length - 1]));
    }

    /// <summary>
    ///   Set all the factors for all tenors from a specific date index
    /// </summary>
    ///
    /// <param name="dateIdx">Starting date index</param>
    /// <param name="factor">Factor to set</param>
    ///
    public void
    SetFactorFrom(int dateIdx, double factor)
    {
      if (dateIdx >= dates_.Length)
        throw new ArgumentException(String.Format("Date index {0} larger than the maximum permitted ({1})",
            dateIdx, dates_.Length));
      double[] data = Correlations;
      int size = data.Length;
      int stride = data.Length / size;
      for (int i = dateIdx * stride; i < size; ++i)
        data[i] = factor;
    }

    /// <summary>
    ///   Set all the factors at a tenor date to the same value
    /// </summary>
    ///
    /// <param name="dateIdx">Date index</param>
    /// <param name="factor">Factor to set</param>
    ///
    public void
    SetFactorAtDate(int dateIdx, double factor)
    {
      if (dateIdx >= dates_.Length)
        throw new ArgumentException(String.Format("Date index {0} larger than the maximum permitted ({1})",
            dateIdx, dates_.Length));
      double[] data = Correlations;
      int stride = data.Length / Dates.Length;
      if (stride > 1)
      {
        int stop = (dateIdx + 1) * stride;
        for (int i = dateIdx * stride; i < stop; ++i)
          data[i] = factor;
      }
      else
        data[dateIdx] = factor;
    }

    /// <summary>
    ///   Convert correlation to a data table
    /// </summary>
    ///
    /// <returns>Content orgainzed in a data table</returns>
    ///
    public override DataTable Content()
    {
      int stride = Correlations.Length / Dates.Length;
      if (Dates.Length <= 1)
      {
        // If only one tenor date, we convert it back.
        Correlation corr = (stride == 1 ?
            new SingleFactorCorrelation(Names, Correlations[0]) :
            new FactorCorrelation(Names, 1, Correlations));
        return corr.Content();
      }

      DataTable dataTable = new DataTable("CorrelationTermStruct");
      dataTable.Columns.Add(new DataColumn("Name", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Date", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Factor", typeof(double)));
      int basketSize = this.BasketSize;
      string[] names = this.Names;
      double[] data = this.Correlations;
      Dt[] dates = this.Dates;
      for (int i = 0; i < basketSize; i++)
      {
        for (int j = 0; j < dates.Length; ++j)
        {
          DataRow row = dataTable.NewRow();
          row["Name"] = names[i];
          row["Date"] = dates[j];
          row["Factor"] = (stride == 1 ? data[j]
              : data[j * stride + i]);
          dataTable.Rows.Add(row);
        }
      }

      return dataTable;
    }

    /// <summary>
    ///   Dates as integer array
    /// </summary>
    ///
    /// <returns>integer array</returns>
    public int[] GetDatesAsInt(Dt asOf)
    {
      int N = dates_.Length;
      int[] datesAsInt = new int[N];
      for (int i = 0; i < N; ++i)
        datesAsInt[i] = BasketCorrelationModel.toInt(asOf, dates_[i]);
      return datesAsInt;
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Maturity dates
    /// </summary>
    public Dt[] Dates
    {
      get { return dates_; }
    }

    /// <summary>
    ///   Interpolator for strikes
    /// </summary>
    public Interp Interp
    {
      get { return interp_; }
      set { interp_ = value; }
    }

    /// <summary>
    ///   Interpolation method for strikes
    /// </summary>
    public InterpMethod InterpMethod
    {
      get { return InterpFactory.ToInterpMethod(interp_); }
    }


    /// <summary>
    ///   Extrapolation method for strikes
    /// </summary>
    public ExtrapMethod ExtrapMethod
    {
      get { return InterpFactory.ToExtrapMethod(interp_); }
    }

    #endregion Properties

    #region Data
    private Dt[] dates_;
    private Interp interp_;
    #endregion Data

  } // class Correlation

}
