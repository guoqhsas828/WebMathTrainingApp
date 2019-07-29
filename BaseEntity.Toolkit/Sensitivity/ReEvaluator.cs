using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Bump;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   Interface for re-evaluating a price measure.
  /// </summary>
  public interface IReEvaluator
  {
    /// <summary>
    ///   Reset and evaluate
    /// </summary>
    /// <returns></returns>
    double ReEvaluate();

    /// <summary>
    ///   Get the base value
    /// </summary>
    double BaseValue { get; }

    /// <summary>
    ///   Get the name of this evaluator
    /// </summary>
    string Name { get; }
  }

  /// <summary>
  ///   Interface for retrive all the curves for bump
  /// </summary>
  public interface ISensitivityCurvesProvider
  {
    /// <summary>
    ///  Get curves used by this evaluator which
    ///  contains the specified bump targets.
    /// </summary>
    /// <param name="bumpTenorTarget">The bump tenor targets</param>
    /// <returns>A list of curves</returns>
    IEnumerable<CalibratedCurve> GetCurves(BumpTarget bumpTenorTarget);
  }

  /// <summary>
  ///  Internal utility class for Sensitivities2
  /// </summary>
  internal class ReEvaluator : Tuple<PricerEvaluator, double>, IDisposable
    , IReEvaluator, ISensitivityCurvesProvider
  {
    #region Constructors

    internal static ReEvaluator Create(PricerEvaluator evaluator)
    {
      return evaluator == null ? null : new ReEvaluator(evaluator);
    }

    internal static ReEvaluator Create(IPricer pricer)
    {
      return pricer == null ? null
        : new ReEvaluator(new PricerEvaluator(pricer));
    }

    private ReEvaluator(PricerEvaluator evaluator)
      : this(evaluator, evaluator.Reset().Evaluate())
    {}

    internal ReEvaluator(PricerEvaluator evaluator, double val)
      : base(evaluator, val)
    {
      if (evaluator == null) return;
      _allPrerequisiteCurves = new[] { evaluator }.GetPrerequisiteCurves();
      var basket = Evaluator.Basket as BaseCorrelationBasketPricer;
      if (basket == null) return;
      _originalRescaleStrikes = basket.RescaleStrike;
    }
    #endregion

    #region Methods
    /// <summary>
    ///   Reset and evaluate
    /// </summary>
    /// <returns></returns>
    public double ReEvaluate()
    {
      Evaluator.Reset(RecoveryChanged, CorrelationChanged);
      return Evaluator.Evaluate();
    }

    /// <summary>
    ///  Get curves used by this evaluator which
    ///  contains the specified bump targets.
    /// </summary>
    /// <param name="bumpTenorTarget">The bump tenor targets</param>
    /// <returns>A list of curves</returns>
    public IEnumerable<CalibratedCurve> GetCurves(BumpTarget bumpTenorTarget)
    {
      return Evaluator.GetCurves(bumpTenorTarget);
    }

    internal bool DependsOn(ICurveTenorSelection selection)
    {
      if (selection == null || selection.Curves == null)
        return false;
      var curves = _allPrerequisiteCurves;
      return curves != null &&
             selection.Curves.FirstOrDefault(curves.Contains) != null;
    }

    internal void SetRescaleStrike(bool rescaleStrike)
    {
      var basket = Evaluator.Basket as BaseCorrelationBasketPricer;
      if (basket == null || basket.RescaleStrike == rescaleStrike)
        return;
      basket.RescaleStrike = rescaleStrike;
      _flags |= CorrelationChangedFlag;
    }

    #endregion

    #region Properties

    internal PricerEvaluator Evaluator
    {
      get { return Item1; }
    }

    public double BaseValue
    {
      get { return Item2; }
    }

    public string Name
    {
      get { return Evaluator.Product.Description; }
    }

    private bool RecoveryChanged
    {
      get { return (_flags & RecoveryChangedFlag) != 0; }
    }

    private bool CorrelationChanged
    {
      get { return (_flags & CorrelationChangedFlag) != 0; }
    }

    #endregion

    #region Data

    private readonly IList<CalibratedCurve> _allPrerequisiteCurves;
    private readonly bool _originalRescaleStrikes;
    private int _flags;
    private const int RecoveryChangedFlag = 1;
    private const int CorrelationChangedFlag = 2;

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      SetRescaleStrike(_originalRescaleStrikes);
      Evaluator.Reset(RecoveryChanged, CorrelationChanged);
    }

    #endregion
  }

  internal class ReEvaluatorList : List<ReEvaluator>, IDisposable
  {
    #region IDisposable Members

    public void Dispose()
    {
      for (int i = 0, n = Count; i < n; ++i)
        this[i].Dispose();
    }

    #endregion
  }

  internal class AggregateEvaluator : IReEvaluator
  {
    #region Constructor

    internal AggregateEvaluator(IList<PricerEvaluator> evaluators,
      string name)
    {
      _evaluators = evaluators;
      _baseValue = ReEvaluate();
      _name = name;
    }

    #endregion

    #region Data

    private readonly IList<PricerEvaluator> _evaluators;
    private readonly double _baseValue;
    private readonly string _name;

    #endregion

    #region IReEvaluator Members

    public double ReEvaluate()
    {
      return _evaluators == null
        ? 0.0 : _evaluators.Select(p => p.Reset().Evaluate()).Average();
    }

    public double BaseValue
    {
      get { return _baseValue; }
    }

    public string Name
    {
      get { return _name; }
    }
    #endregion
  }
}
