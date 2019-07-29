using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;


namespace BaseEntity.Toolkit.Pricers
{
  #region IAmericanMonteCarloAdapter

  /// <summary>
  ///   Provider interface for IAmericanMonteCarloAdapter
  /// </summary>
  public interface IAmericanMonteCarloAdapterProvider
  {
    /// <summary>
    ///   Get an instance of IAmericanMonteCarloAdapterProvider
    /// </summary>
    /// <returns>IAmericanMonteCarloAdapterProvider</returns>
    IAmericanMonteCarloAdapter GetAdapter();
  }

  /// <summary>
  /// Interface for pricers with American/Bermudan optionality to be plugged in the LeastSquaresMonteCarloEngine
  /// </summary>
  public interface IAmericanMonteCarloAdapter
  {
    /// <summary>
    ///   Gets the valuation currency
    /// </summary>
    Currency ValuationCurrency { get; }

    /// <summary>
    /// Underlying funding curves
    /// </summary>
    IEnumerable<DiscountCurve> DiscountCurves { get; }

    /// <summary>
    /// Underlying projection curves
    /// </summary>
    IEnumerable<CalibratedCurve> ReferenceCurves { get; }

    /// <summary>
    /// Underlying survival curves
    /// </summary>
    IEnumerable<SurvivalCurve> SurvivalCurves { get; }

    /// <summary>
    /// Underlying FX rates
    /// </summary>
    IEnumerable<FxRate> FxRates { get; }

    /// <summary>
    /// Cashflow of the underlier paid out until the first of call/put date
    /// </summary>
    IList<ICashflowNode> Cashflow { get; }

    /// <summary>
    /// Contingent claim upon call exercise  
    /// </summary>
    ExerciseEvaluator CallEvaluator { get; }

    /// <summary>
    /// Contingent claim upon put exercise
    /// </summary>
    ExerciseEvaluator PutEvaluator { get; }

    /// <summary>
    /// Explanatory variables for regression algorithm. 
    /// </summary>
    BasisFunctions Basis { get; } 
    
    /// <summary>
    /// Notional 
    /// </summary>
    double Notional { get; }

    /// <summary>
    /// True if instance of Product requires American Monte Carlo (i.e. a Swap with no call features).
    /// </summary>
    bool Exotic { get; }

		/// <summary>
		/// Significant dates for exposure profile of this pricer
		/// </summary>
		Dt[] ExposureDates { get; set; }
  }

  #endregion
}
