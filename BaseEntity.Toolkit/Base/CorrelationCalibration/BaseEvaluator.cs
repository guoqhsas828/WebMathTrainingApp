/*
 * BaseEvaluator.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System.Collections.Generic;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
	internal class BaseEvaluator : BaseEntityObject
  {
    #region Constructors

    protected BaseEvaluator()
    {
    }

    public BaseEvaluator(
      SyntheticCDO cdoTerms,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double detachment,
      double totalPrincipal,
      Dt[] tenorDates)
    {
      SyntheticCDO cdo = (SyntheticCDO)cdoTerms.Clone();
      cdo.Attachment = 0;
      cdo.Detachment = detachment;
      cdo.Premium = 1;
      cdo.Fee = 1;
      cdo.FeeSettle = Dt.Add((cdo.Effective > basket.Settle) ? cdo.Effective : basket.Settle, 1);
      string[] names = basket.EntityNames;

      basket = basket.Duplicate();
      basket.RawLossLevels =
        new UniqueSequence<double>(0.0, detachment);
      if (basket.MaximumAmortizationLevel() <= BasketPricerFactory.MinimumAmortizationLevel(cdo))
        basket.NoAmortization = true;
#if Backward_Compatible
      // Hack for backward compatibility
      if (!(basket is SemiAnalyticBasketPricer))
#endif
        basket.AddGridDates(tenorDates);

      basket.Maturity = tenorDates[tenorDates.Length - 1];
      basket.Correlation = new CorrelationTermStruct(
        names, new double[1], new Dt[1] { basket.Maturity });
      basket.Reset(basket.OriginalBasket); // force reset default settlements

      pricer_ = new SyntheticCDOPricer(cdo, basket, discountCurve, 1.0, false, null);

      tenorDates_ = tenorDates;
      tenorIndex_ = 0;
      notional_ = totalPrincipal * detachment;
      cache_ = new SortedList<double, TrancheValue[]>(10);
    }

    #endregion // Constructors

    #region Methods

    public double evaluate(double factor,
      int tenorIndex, double premium, double fee)
    {
      TrancheValue v = GetValue(factor, tenorIndex);
      double pv = (v.ProtectionPv + premium * v.FlatFeePv
        + fee * v.UpfrontFee) * notional_;
      return pv;
    }

    public double Solve(double target,
      int tenorIndex, double premium, double fee,
      double toleranceF, double toleranceX,
      double min, double max)
    {
      tenorIndex_ = tenorIndex;
      premium_ = premium;
      fee_ = fee;
      CorrelationSolver solver = new CorrelationSolver();
      UnivariateFunction fn = this.evaluate;
      solver.BracketBaseTranche(fn, target, toleranceF, min, max);
      solver.RefineBaseTrancheBracket(fn,
        target, cache_.Keys, toleranceF);
      double factor = solver.BrentSolve(fn,
        target, toleranceF, toleranceX);
      return factor * factor;
    }

    public double DetachmentScalingFactor(
      BaseCorrelationStrikeMethod strikeMethod)
    {
      BasketPricer basket = pricer_.Basket.Duplicate();
      basket.Maturity = tenorDates_[tenorIndex_];
      return BaseEntity.Toolkit.Base.BaseCorrelation.DetachmentScalingFactor(
        strikeMethod, basket, pricer_.DiscountCurve);
    }

    public double CalcStrike(double corr,
      BaseEntity.Toolkit.Base.BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      BaseCorrelationStrikeMethod strikeMethod)
    {
      pricer_.Basket.Maturity = tenorDates_[tenorIndex_];
      return BaseEntity.Toolkit.Base.BaseCorrelation.Strike(pricer_,
        strikeMethod, strikeEvaluator, corr);
    }

    protected virtual double evaluate(double factor)
    {
      TrancheValue v = GetValue(factor, tenorIndex_);
      double pv = (v.ProtectionPv + premium_ * v.FlatFeePv
        + fee_ * v.UpfrontFee) * notional_;
      return pv;
    }

    private TrancheValue GetValue(double factor, int t)
    {
      TrancheValue[] values = null;
      if (!cache_.TryGetValue(factor, out values))
      {
        values = ComputeValues(factor);
        cache_[factor] = values;
      }
      return values[t];
    }

    private TrancheValue[] ComputeValues(double factor)
    {
      BasketPricer basket = pricer_.Basket;
      basket.SetFactor(factor);
      basket.Reset();

      TrancheValue[] values = new TrancheValue[tenorDates_.Length];

      for (int j = 0; j < tenorDates_.Length; ++j)
      {
        pricer_.CDO.Maturity = tenorDates_[j];
#if Backward_Compatible
        if (basket is SemiAnalyticBasketPricer)
        {
          basket.Maturity = tenorDates_[j];
          ((SemiAnalyticBasketPricer)basket).SetRecalculationStartDate(
            j > 0 ? tenorDates_[j - 1] : Dt.Empty, true);
          basket.Reset();
        }
#endif
        pricer_.UpdateEffectiveNotional();
        values[j] = new TrancheValue(pricer_.ProtectionPv(),
          pricer_.FlatFeePv(1.0), pricer_.UpFrontFeePv());
      }
      return values;
    }

    #endregion // Methods

    #region Properties

    public int TenorIndex
    {
      get { return tenorIndex_; }
      set { tenorIndex_ = value; }
    }

    public double Premium
    {
      get { return premium_; }
      set { premium_ = value; }
    }

    public double Fee
    {
      get { return fee_; }
      set { fee_ = value; }
    }

    public double Notional
    {
      get { return notional_; }
      set { notional_ = value; }
    }

    #endregion // Properties

    #region Data

    private double premium_;
    private double fee_;
    private double notional_;

    private int tenorIndex_;
    private Dt[] tenorDates_;

    private SyntheticCDOPricer pricer_;
    private SortedList<double, TrancheValue[]> cache_;

    #endregion // Data
  } // class BaseEvaluator
}
