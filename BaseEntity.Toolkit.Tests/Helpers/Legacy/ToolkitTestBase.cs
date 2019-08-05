using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Helpers.Legacy
{

  /// <summary>
  /// Base class of most toolkit tests.
  /// </summary>
  public class ToolkitTestBase
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolkitTestBase"/> class.
    /// </summary>
    /// <remarks></remarks>
    public ToolkitTestBase() : this(null)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolkitTestBase"/> class.
    /// </summary>
    /// <remarks></remarks>
    public ToolkitTestBase(string fixtureName)
    {
      _properties = FixtureBuilder.Get(this, fixtureName);
      _disabledTests = FixtureBuilder.GetIgnoredMethods(this, _properties);
    }

    #endregion

    #region Set-up and tear-down methods

    [OneTimeSetUp]
    public void InitializeInstance()
    {
      FixtureBuilder.SetProperties(this, _properties);
    }

    [OneTimeTearDown]
    public void FinishInstance()
    {
      if (_results == null || !BaseEntityContext.IsGeneratingExpects
        || !string.IsNullOrEmpty(ExpectsFilePath))
      {
        return;
      }
      // Save expects
      SaveExpectsCollection(ExpectsFilePath,
        new ResultCollection {List = _results?.ToArray()});
    }

    [SetUp]
    public void InitializeCase()
    {
      CheckTestExclusion();
    }

    private void CheckTestExclusion()
    {
      var test = TestContext.CurrentContext.Test;
      if (test == null || _disabledTests == null ||
        !_disabledTests.Contains(test.Name))
      {
        return;
      }
      NUnit.Framework.Assert.Ignore("Ignored through configuration");
    }

    private readonly IDictionary<string, string> _properties;
    private readonly ISet<string> _disabledTests;
    private IList<ResultData> _results;

    #endregion

    #region Test Helpers

    /// <summary>Get all the fixtures specified in the ".test" files</summary>
    public static IEnumerable<IDictionary<string, string>> GetAllFixtures(
      Assembly assembly)
    {
      return FixtureBuilder.GetAllFixtures(assembly);
    }

    internal static bool TryGetTestProperty(
      string name, Type type, out object value)
    {
      var bag = TestContext.CurrentContext.Test?.Properties;
      if (bag == null || !bag.ContainsKey(name))
      {
        value = null;
        return false;
      }

      var data = bag["name"];
      if (data == null || type.IsInstanceOfType(data))
      {
        value = data;
        return true;
      }

      var single = data.FirstOrDefault();
      if (type.IsInstanceOfType(single))
      {
        value = single;
        return true;
      }

      value = Convert.ChangeType(single, type);
      return true;
    }

    /// <summary>
    /// Timings the specified action.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>Time used in milliseconds</returns>
    /// <remarks></remarks>
    protected static double Timing(Action action)
    {
      var timer = new Stopwatch();
      timer.Start();
      action();
      return timer.ElapsedMilliseconds;
    }

    #endregion

    #region Test utilities

    /// <summary>
    ///    Test numberic method
    /// </summary>
    /// <param name="obj">object</param>
    /// <param name="label">labels</param>
    /// <param name="fn">Function to invoke</param>
    /// <returns>ResultData</returns>
    public void TestNumeric<T>(
      T obj, string label, Func<T, double> fn)
    {
      TestNumeric(new[] { obj }, new[] { label }, fn);
    }

    /// <summary>
    ///    Calculate an array of values
    /// </summary>
    /// 
    /// <remarks>
    ///   This function calculate an array of values by applying the
    ///   the function to each of the input objects. 
    /// </remarks>
    /// 
    /// <param name="objs">List of objects</param>
    /// <param name="fn">Function to invoke</param>
    /// <returns>Array of values</returns>
    public static double[] CalcValues<T>(
      IList<T> objs, Func<T, double> fn)
    {
      int N = objs.Count;
      double[] values = new double[N];
      for (int i = 0; i < N; ++i)
        values[i] = fn(objs[i]);
      return values;
    }

    /// <summary>
    ///    Test numberic method
    /// </summary>
    /// <param name="objs">List of objects</param>
    /// <param name="labels">List labels</param>
    /// <param name="fn">Function to invoke</param>
    /// <returns>ResultData</returns>
    public void TestNumeric<T>(
      IList<T> objs, IList<string> labels,
      Func<T, double> fn)
    {
      int N = objs.Count;
      double[] values = new double[N];
      Timer timer = new Timer();
      for (int i = 0; i < N; ++i)
      {
        timer.resume();
        double v = fn(objs[i]);
        timer.stop();
        values[i] = v;
      }
      MatchExpects(values, labels, timer.getElapsed());
    }

    /// <summary>
    ///    Test numberic method
    /// </summary>
    /// <param name="objs">List of objects</param>
    /// <param name="labels">List labels</param>
    /// <param name="fn">Function to invoke</param>
    /// <returns>ResultData</returns>
    public void TestNumeric<T>(
      IList<object> objs, IList<T> pars,
      IList<string> labels,
      Func<object, T, double> fn)
    {
      int N = objs.Count;
      double[] values = new double[N];
      Timer timer = new Timer();
      for (int i = 0; i < N; ++i)
      {
        timer.resume();
        double v = fn(objs[i], pars[i]);
        timer.stop();
        values[i] = v;
      }
      MatchExpects(values, labels, timer.getElapsed());
    }

    /// <summary>
    /// Matches the expects.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="labels">The labels.</param>
    /// <param name="timeUsed">The time used.</param>
    /// <remarks></remarks>
    public void MatchExpects(
      IList<double> result, IList<string> labels, double timeUsed)
    {
      ToResultData(result, labels, timeUsed).MatchExpects();
    }

    /// <summary>
    ///   Match the expects
    /// </summary>
    /// <param name="rd"></param>
    public static void MatchExpects(ResultData rd)
    {
      rd.MatchExpects();
    }

    /// <summary>
    /// Convert an array of doubles to result data.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="labels">The labels.</param>
    /// <param name="timeUsed">The time used.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public ResultData ToResultData(
      IList<double> result, IList<string> labels, double timeUsed)
    {
      var rd = LoadExpects();
      rd.TimeUsed = timeUsed;
      rd.Results[0].Actuals = ToArray(result);
      if (labels != null)
        rd.Results[0].Labels = ToArray(labels);
      return rd;
    }

    public static T[] ToArray<T>(IList<T> list)
    {
      if (list == null)
        return null;
      else if (list is T[])
        return (T[])list;
      else if (list is List<T>)
        return ((List<T>)list).ToArray();

      T[] array = new T[list.Count];
      for (int i = 0; i < array.Length; ++i)
        array[i] = list[i];
      return array;
    }

    public ResultData ToResultData(double[][] columns,
      string[] colNames, double timeUsed)
    {
      int cols = columns.Length;
      var rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }
      for (int i = 0; i < cols; ++i)
      {
        rd.Results[i].Name = colNames[i];
        rd.Results[i].Actuals = columns[i];
      }
      if (timeUsed > 0.0) rd.TimeUsed = timeUsed;
      return rd;
    }

    #endregion

    #region UserInputs
    /// <summary>
    ///   Get user input on day count
    /// </summary>
    /// <param name="dfltValue">default value when no input from user</param>
    /// <returns>daycount</returns>
    protected DayCount Get(DayCount dfltValue)
    {
      return DayCount == DayCount.None ? dfltValue : DayCount;
    }

    protected Frequency Get(Frequency dfltValue)
    {
      return Frequency == Frequency.None ? dfltValue : Frequency;
    }

    protected BDConvention Get(BDConvention dfltValue)
    {
      return Roll == BDConvention.None ? dfltValue : Roll;
    }

    protected Calendar Get(Calendar dfltValue)
    {
      return Calendar == Calendar.None ? dfltValue : Calendar;
    }

    protected Currency Get(Currency dfltValue)
    {
      return Currency == Currency.None ? dfltValue : Currency;
    }

    protected double GetNotional(double dfltValue)
    {
      return Notional == 0 ? dfltValue : Notional;
    }

    protected Copula GetCopula()
    {
      if (CopulaData == null)
        return new Copula();
      object[] objs = Parsers.Parse(new Type[] { typeof(CopulaType), typeof(int), typeof(int) }, CopulaData);
      return new Copula((CopulaType)objs[0], (int)objs[1], (int)objs[2]);
    }

    protected void GetTimeGrid(ref int stepSize, ref TimeUnit stepUnit)
    {
      stepSize = 0; stepUnit = TimeUnit.None;
      if (TimeGrid != null)
      {
        object[] objs = Parsers.Parse(new Type[] { typeof(int), typeof(TimeUnit) }, TimeGrid);
        stepSize = (int)objs[0];
        stepUnit = (TimeUnit)objs[1];
      }
      return;
    }

    /// <summary>
    ///   Parse a comma delimited string
    /// </summary>
    /// <param name="type">Type of elements</param>
    /// <param name="commaDelimted">Comma delimited string</param>
    /// <returns>objects</returns>
    public static ArrayList Parse(Type type, string commaDelimted)
    {
      if (commaDelimted == null || commaDelimted.Length <= 0)
        return null;
      ArrayList list = new ArrayList();
      Regex regex = new Regex(@"\s*,\s*");
      string[] elems = regex.Split(commaDelimted);
      foreach (string elem in elems)
        list.Add(type == typeof(string) ? elem : ParseElement(type, elem));
      return list;
    }

    /// <summary>
    ///   Parse a comma delimited string
    /// </summary>
    /// <param name="type">Type of elements</param>
    /// <param name="commaDelimted">Comma delimited string</param>
    /// <returns>objects</returns>
    public static string[] ParseString(string commaDelimted)
    {
      if (commaDelimted == null || commaDelimted.Length <= 0)
        return null;
      Regex regex = new Regex(@"\s*,\s*");
      string[] elems = regex.Split(commaDelimted);
      return elems;
    }

    /// <summary>
    ///   Parse a comma delimited string
    /// </summary>
    /// <param name="type">Type of elements</param>
    /// <param name="commaDelimted">Comma delimited string</param>
    /// <returns>objects</returns>
    public static double[] ParseDouble(string commaDelimted)
    {
      ArrayList list = Parse(typeof(double), commaDelimted);
      return list == null ? null : (double[])list.ToArray(typeof(double));
    }

    /// <summary>
    ///   Parse a comma delimited string
    /// </summary>
    /// <param name="type">Type of elements</param>
    /// <param name="commaDelimted">Comma delimited string</param>
    /// <returns>objects</returns>
    public static int[] ParseInt(string commaDelimted)
    {
      ArrayList list = Parse(typeof(int), commaDelimted);
      return list == null ? null : (int[])list.ToArray(typeof(int));
    }

    private static object ParseElement(Type type, string elem)
    {
      if (elem == null || elem.Length <= 0)
        return null;
      if (type.IsEnum)
        return Enum.Parse(type, elem);
      if (type == typeof(double))
        return Double.Parse(elem);
      if (type == typeof(Int32))
        return Int32.Parse(elem);
      if (type == typeof(Int64))
        return Int64.Parse(elem);
      if (type == typeof(Int16))
        return Int16.Parse(elem);
      if (type == typeof(bool))
        return Boolean.Parse(elem);
      if (type == typeof(string))
        return elem;
      if (type == typeof(Toolkit.Base.Dt))
        return new Toolkit.Base.Dt(Int32.Parse(elem));
      throw new ArgumentOutOfRangeException("type", String.Format("Don't know how to parse type: {0}", type));
    }

    /// <summary>
    ///    Convert an integer to Dt
    /// </summary>
    /// <param name="date">date as integer</param>
    /// <returns>date as Dt</returns>
    public static Dt ToDt(int date)
    {
      if (date <= 0)
        return new Dt();
      return new Dt(date);
    }


    /// <summary>
    ///   Select a subset of curves based on names
    /// </summary>
    /// <param name="curves">Curves to select from</param>
    /// <param name="names">Names to select</param>
    /// <returns>selected curves</returns>
    public static SurvivalCurve[] Select(SurvivalCurve[] curves, string[] names)
    {
      SurvivalCurve[] selected = new SurvivalCurve[names.Length];
      System.Collections.Hashtable ht = new System.Collections.Hashtable();
      for (int i = 0; i < names.Length; ++i)
        ht[names[i]] = i;
      foreach (SurvivalCurve c in curves)
        if (c != null)
        {
          if (ht.Contains(c.Name))
            selected[(int)ht[c.Name]] = c;
        }
      return selected;
    }

    #endregion // UserInputs

    #region ConfigurationStack

    // Calibrator Pricer configuration parameters
    //private System.Collections.Stack configStack_ = null;

    internal class Configuration
    {
      public bool BasketPricer_SubstractShortedFromPrincipal = false;
      public bool BasketPricer_UseNaturalSettlement = false;
      public bool BasketPricer_UseCurveRecoveryForBaseCorrelation = false;
      public bool Calibrator_UseCashflowStream = false;
      public bool CashflowModel_DiscountingAccrued = false;
      public bool CashflowStreamModel_DiscountingAccrued = false;
      public bool CashflowFactory_CdsIncludeMaturityAccrual = false;
      public bool CashflowFactory_CdsIncludeMaturityProtection = false;
      public bool CashflowFactory_UseConsistentCashflowEffective = false;
      public bool CashflowStreamFactory_UseConsistentCashflowEffective = false;
      public bool CDXOptionPricer_ForceIntrinsicSurvival = false;
      public bool CDXOptionPricer_ForceCleanValue = false;
      public bool SurvivalCalibrator_UseCdsSpecific = false;
      public bool SurvivalCalibrator_UseNaturalSettlement = false;
      public bool SyntheticCDOPricer_UseOriginalNotionalForFee = false;
      public bool SyntheticCDOPricer_UseHeterogeneousModelAsDefault = false;
      public bool Solver_KeepBracket = false;
    }

    #endregion // ConfigurationStack

    #region Properties

    /// <summary>
    ///  Get the relative path name of the file for the expects data.
    /// </summary>
    public string ExpectsFileName { get; set; }

    /// <summary>
    ///   Use cashflow stream model for calibration
    /// </summary>
    public bool ConfigUseCashflowStream { get; set; } = true;

    /// <summary>
    ///   Do not discount accrued in the cashflow models
    /// </summary>
    public bool ConfigCfModelDiscountingAccrued { get; set; } = true;

    /// <summary>
    ///   Use the natural settlement for curve calibrating
    /// </summary>
    public bool ConfigUseNaturalSettlement { get; set; } = false;

    /// <summary>
    ///   Use the original notional for fee
    /// </summary>
    public bool ConfigCdoUseOriginalNotionalForFee { get; set; } = true;

    public bool ConfigCdsIncludeMaturityAccrual { get; set; } = true;

    public bool ConfigCdsIncludeMaturityProtection { get; set; } = false;

    public bool ConfigUseConsistentCashflowEffective { get; set; } = false;

    public bool ConfigCdxOptionForceIntrinsicSurvival { get; set; } = true;

    public bool ConfigCdxOptionForceCleanValue { get; set; } = true;

    public bool ConfigSubstractShortedFromPrincipal { get; set; } = false;

    public bool ConfigUseCurveRecoveryForBaseCorrelation { get; set; } = false;

    #region Pricing related

    /// <summary>
    ///   Pricing date
    /// </summary>
    public int PricingDate { get; set; } = 0;

    /// <summary>
    ///   Settle date
    /// </summary>
    public int SettleDate { get; set; } = 0;

    /// <summary>
    ///   Product effective date
    /// </summary>
    public int EffectiveDate { get; set; } = 0;

    /// <summary>
    ///   Product maturity date
    /// </summary>
    public int MaturityDate { get; set; } = 0;

    /// <summary>
    ///   Product first copon date
    /// </summary>
    public int FirstPremDate { get; set; } = 0;

    /// <summary>
    ///   Notional
    /// </summary>
    public double Notional { get; set; } = 0;

    /// <summary>
    ///   Day count
    /// </summary>
    public DayCount DayCount { get; set; } = DayCount.None;

    /// <summary>
    ///   Frequency
    /// </summary>
    public Frequency Frequency { get; set; } = Frequency.None;

    /// <summary>
    ///   Business convention
    /// </summary>
    public BDConvention Roll { get; set; } = BDConvention.Following;

    /// <summary>
    ///   Calendar
    /// </summary>
    public Calendar Calendar { get; set; } = Calendar.None;

    /// <summary>
    ///   Currency
    /// </summary>
    public Currency Currency { get; set; } = Currency.None;

    /// <summary>
    ///   Copula type and parameters
    /// </summary>
    /// <remarks>This is a string with format "CopulaType,dfCommon,dfIndividual,others".</remarks>
    public string CopulaData { get; set; } = null;

    /// <summary>
    ///   Time grid
    /// </summary>
    /// <remarks>This is a string with format "stepSize,stepUnit".</remarks>
    public string TimeGrid { get; set; } = null;

    /// <summary>
    ///   Quadrature points
    /// </summary>
    public int QuadraturePoints { get; set; } = 0;

    /// <summary>
    ///   Accuracy
    /// </summary>
    public double Accuracy { get; set; } = 1E-6;

    /// <summary>
    ///   Simulation sample size
    /// </summary>
    public int SampleSize { get; set; } = 0;

    /// <summary>
    ///   Fee settle date.
    /// </summary>
    public int FeeSettleDate { get; set; } = 0;

    /// <summary>
    ///   Upfront fee.
    /// </summary>
    public double Fee { get; set; } = 0;

    // configuration
    protected ToolkitConfigSettings Settings => ToolkitConfigurator.Settings;

    #endregion

    #endregion Properties

    #region Interfaces to load/generate expects

    public ResultData LoadExpects()
    {
      var test = TestContext.CurrentContext.Test;
      if (test == null)
      {
        throw new ToolkitException("Test case not specified");
      }
      var results = _results;
      if (results == null)
      {
        if (string.IsNullOrEmpty(ExpectsFilePath))
          throw new ToolkitException("Expects file not specified");
        results = _results = LoadExpectsCollection(ExpectsFilePath);
      }

      return ResultData.LoadExpects(test.Name, 
        BaseEntityContext.IsGeneratingExpects, results);
    }

    public string ExpectsFilePath => _expectsFilePath ?? (
      _expectsFilePath = GetExpectsFilePath());

    private string _expectsFilePath;

    private string GetExpectsFilePath()
    {
      if (string.IsNullOrEmpty(ExpectsFileName))
      {
        return "";
      }
      var file = NormalizeFileName(ExpectsFileName);
      return System.IO.Path.Combine(BaseEntityContext.InstallDir, "toolkit", "test", file);
    }

    private static IList<ResultData> LoadExpectsCollection(string path)
    {
      var result = new List<ResultData>();
      if (System.IO.File.Exists(path))
      {
        var data = (ResultCollection)XmlLoadData(path, typeof(ResultCollection));
        result.AddRange(data.List);
      }
      return result;
    }

    private static void SaveExpectsCollection(
      string path, ResultCollection data)
    {
      XmlSaveData(path, typeof(ResultCollection), data);
    }

    #endregion
  }
}
