/*
 * CDOCashflowGenerator.cs
 *
 *   2007-2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  /// Cashflow generator for synthetic CDOs.
  /// </summary>
	[Serializable]
	public class CDOCashflowGenerator
	{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="cdo"></param>
    /// <param name="settle"></param>
    /// <param name="discountCurve"></param>
    /// <param name="rateResets"></param>
    public CDOCashflowGenerator(
			SyntheticCDO cdo, Dt settle,
			DiscountCurve discountCurve,
			List<RateReset> rateResets)
		{
			cdo_ = cdo;
			discountCurve_ = discountCurve;
			rateResets_ = rateResets;
			settle_ = settle;
		}

		/// <summary>
		///   Generate cashflows given a sequence of defaults
		/// </summary>
		/// <param name="defaultDates">Array of default dates</param>
		/// <param name="losses">Array of default losses per unit total basket principal</param>
		/// <param name="recovers">Array of default recveries per unit total basket principal</param>
		/// <returns>CashflowStream with tranche losses infomation</returns>
    public CashflowStream GenerateCashflow(
			Dt[] defaultDates, double[] losses, double[] recovers)
		{
			if (scheduledCashflow_ == null)
				GenerateScheduledCashflow();
			int count = scheduledCashflow_.Count;
			if (count == 0) return null;
      if (defaultDates == null || defaultDates.Length == 0)
        return ToCashflowStream(scheduledCashflow_);

		  double cumuBasketLoss = 0, cumuBasketAmor = 0,
				prevTrancheLoss = 0, prevTrancheAmor = 0,
				prevBalance = 1.0;

			Dt prevDate;
			CashflowStream cf = new CashflowStream();
			List<Pair<Dt, double>> trancheLosses = cf.DefaultLosses;
			cf.Effective = prevDate = scheduledCashflow_.Effective;
			cf.AccruedPaidOnDefault = scheduledCashflow_.AccruedPaidOnDefault;
			cf.DayCount = scheduledCashflow_.DayCount;
			for (int t = 0, d = 0; t < count; ++t)
			{
				double lastBalance = prevBalance;
				Dt lastDate = prevDate;
				double cumuBalance = 0.0;

				Dt dfltDate = Dt.Empty;
				Dt date = scheduledCashflow_.GetDt(t);
				while (d < defaultDates.Length && defaultDates[d] <= date)
				{
					dfltDate = defaultDates[d];
					cumuBasketLoss += losses[d];
					double trancheLoss = CumulativeTrancheLoss(
						cdo_.Attachment, cdo_.Detachment, cumuBasketLoss);
					double loss = trancheLoss - prevTrancheLoss;
					if (loss > 0)
						trancheLosses.Add(new Pair<Dt, double>(dfltDate, loss));
					prevTrancheLoss = trancheLoss;

					cumuBasketAmor += recovers[d];
					double trancheAmor = CumulativeTrancheAmor(
						cdo_.Attachment, cdo_.Detachment, cumuBasketAmor);
					double amor = trancheAmor - prevTrancheAmor;
					double balance = 1.0 - trancheAmor - trancheLoss;
					if (amor > 0)
						cf.Add(dfltDate, amor, 0.0, 0.0, balance);
					prevTrancheAmor = trancheAmor;

					cumuBalance += lastBalance * Dt.Fraction(lastDate, dfltDate, cdo_.DayCount);
					lastBalance = balance;
					lastDate = dfltDate;
					d++;
				}
				cumuBalance += lastBalance * Dt.Fraction(lastDate, date, cdo_.DayCount);
				double avgBalance = cumuBalance / Dt.Fraction(prevDate, date, cdo_.DayCount);
				double interest = (cumuBalance <= 0 ? 0.0 : (scheduledCashflow_.GetAccrued(t) * avgBalance));

				// Set cashflows
				if (cf.Count == 0 || cf.GetDate(cf.Count-1) < date)
					cf.Add(date, 0.0, 0.0, interest, lastBalance);
				else
				{
					int last = cf.Count - 1;
					cf.Set(last, cf.GetPrincipal(last), cf.GetAccrual(last), cf.GetInterest(last) + interest, lastBalance);
				}

				// Handle last period
				if (t == count - 1 && (cdo_.CdoType == CdoType.FundedFloating || cdo_.CdoType == CdoType.FundedFixed))
				{
					int last = cf.Count - 1;
					cf.Set(last, cf.GetPrincipal(last) + lastBalance, cf.GetAccrual(last), cf.GetInterest(last), 0);
				}

				// next loop
				prevDate = date;
				prevBalance = lastBalance;
			}

			return cf;
		}

    //TODO: The following conversion should be removed once CashflowCDO no longer uses CashflowStream.
    private static CashflowStream ToCashflowStream(Cashflow cf)
    {
      CashflowStream cfs = new CashflowStream();
      cfs.Effective = cf.Effective;
      cfs.AccruedPaidOnDefault = cf.AccruedPaidOnDefault;
      cfs.DayCount = cf.DayCount;
      int count = cf.Count;
      for (int t = 0; t < count; ++t)
        cfs.Add(cf.GetDt(t), cf.GetAmount(t),
          cf.GetAccrued(t), cf.GetPrincipalAt(t));
      return cfs;
    }

		private double CumulativeTrancheLoss(
			double attachment, double detachment, double cumulativeBasketLoss)
		{
			if (cumulativeBasketLoss <= attachment) return 0;
			if (cumulativeBasketLoss >= detachment) return 1;// detachment - attachment;
			return (cumulativeBasketLoss - attachment) / (detachment - attachment);
		}

		private double CumulativeTrancheAmor(
			double attachment, double detachment, double cumulativeBasketAmor)
		{
			double a = 1 - detachment;
			if (cumulativeBasketAmor <= a) return 0;
			double d = 1 - attachment;
			if (cumulativeBasketAmor >= d) return 1;// d - a;
			return (cumulativeBasketAmor - a) / (d - a);
		}

		private void GenerateScheduledCashflow()
		{
			scheduledCashflow_ = PriceCalc.GenerateCashflowForFee(
				settle_, cdo_.Premium,
				cdo_.Effective, cdo_.FirstPrem, cdo_.Maturity,
				cdo_.Ccy, cdo_.DayCount, cdo_.Freq, cdo_.BDConvention, cdo_.Calendar, null,
				cdo_.CdoType == CdoType.FundedFloating || cdo_.CdoType == CdoType.IoFundedFloating,
				cdo_.CdoType == CdoType.FundedFixed || cdo_.CdoType == CdoType.FundedFloating,
				discountCurve_, rateResets_);
		}

		private SyntheticCDO cdo_;
		private Dt settle_;
		private Cashflow scheduledCashflow_;

		// The following two fields are used by floating cdos only
		private DiscountCurve discountCurve_;
		private List<RateReset> rateResets_;
	}
}
