using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Shared;

using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  public static class QuoteUtil
  {
    /// <summary>
    ///   Load basket quotes from a file 
    /// </summary>
    /// <param name="filename">filename</param>
    /// <returns>basket quotes</returns>
    public static BasketQuotes LoadBasketQuotes(string filename)
    {
      filename = GetTestFilePath(filename);
      BasketQuotes bq = (BasketQuotes)XmlLoadData(
        filename, typeof(BasketQuotes));
      return bq;
    }

    /// <summary>
    ///   Create credit curves from quotes
    /// </summary>
    /// <param name="quotes"></param>
    /// <param name="discountCurve"></param>
    /// <returns></returns>
    public static SurvivalCurve[] CreateSurvivalCurves(
      CDSQuote[] quotes, DiscountCurve discountCurve,
      bool includeUnsettled, bool normalize)
    {
      SurvivalCurve[] curves = new SurvivalCurve[quotes.Length];
      for (int i = 0; i < quotes.Length; ++i)
        curves[i] = CreateCreditCurve(quotes[i], discountCurve,
          includeUnsettled, normalize);
      return curves;
    }

    /// <summary>
    ///   Create Index caling calibrator
    /// </summary>
    /// <param name="indexTerm"></param>
    /// <param name="indexQuotes"></param>
    /// <param name="survivalCurves"></param>
    /// <param name="discountCurve"></param>
    /// <returns></returns>
    public static IndexScalingCalibrator CreateScalingCalibrator(
      IndexTerm indexTerm, IndexQuote[] indexQuotes,
      SurvivalCurve[] survivalCurves, DiscountCurve discountCurve,
      CDXScalingMethod scalingMethod,
      bool relativeScaling, bool scaleOnHazardRate,
      string[] useTenors)
    {
      if (indexQuotes == null || indexQuotes.Length <= 0)
        return null;
      int tenorCount = indexQuotes.Length;
      Dt asOf = new Dt(indexQuotes[0].Date);
      Dt settle = Dt.Add(asOf, 1);
      CDX[] cdx = new CDX[tenorCount];
      string[] tenors = new string[tenorCount];
      double[] quotes = new double[tenorCount];
      CDXScalingMethod[] scalingMethods = new CDXScalingMethod[tenorCount];
      bool isLCDX = indexTerm.IndexName.Contains("LCDX");
      int quoteStatus = 0;
      for (int i = 0; i < indexQuotes.Length; ++i)
      {
        IndexTerm.Term t = indexTerm.Terms[i];
        tenors[i] = t.TenorName;
        if (!UseTenor(tenors[i], useTenors)) continue;
        IndexQuote q = indexQuotes[i];
        cdx[i] = isLCDX
          ? new LCDX(new Dt(t.Effective), new Dt(t.Maturity),
            Currency.None, t.DealPremium, DayCount.Actual360, Frequency.Quarterly,
            BDConvention.Following, Calendar.NYB)
           :
           new CDX(new Dt(t.Effective), new Dt(t.Maturity),
           Currency.None, t.DealPremium, DayCount.Actual360, Frequency.Quarterly,
           BDConvention.Following, Calendar.NYB);
        if (t.FirstPrem != 0)
          cdx[i].FirstPrem = new Dt(t.FirstPrem);
        //cdx[i].Weights = ArrayUtil.NewArray<double>(survivalCurves.Length, 1.0 / survivalCurves.Length);
        if (!Double.IsNaN(q.Spread) && quoteStatus >= 0)
        {
          quotes[i] = q.Spread;
          quoteStatus = 1;
        }
        else if (!Double.IsNaN(q.Price) && quoteStatus <= 0)
        {
          quotes[i] = q.Price / 100;
          quoteStatus = -1;
        }
        scalingMethods[i] = scalingMethod;
      }

      IndexScalingCalibrator cal = new IndexScalingCalibrator(asOf, settle,
        cdx, tenors, quotes, quoteStatus < 0, scalingMethods,
        relativeScaling, scaleOnHazardRate, discountCurve, survivalCurves,
        ArrayUtil.Generate<bool>(survivalCurves.Length, delegate(int i) { return true; }),
        isLCDX ? 0.7 : 0.4);
      cal.Name = indexTerm.IndexName;
      return cal;
    }

    private static bool UseTenor(string tenor, string[] useTenors)
    {
      if (useTenors == null || useTenors.Length == 0) return true;
      foreach (string t in useTenors)
        if (String.Compare(t, tenor, true) == 0)
          return true;
      return false;
    }

    /// <summary>
    ///   Calibrate base correlation term structure
    /// </summary>
    public static BaseCorrelationTermStruct CreateBaseCorrelation(
      IndexScalingCalibrator index,
      IndexTrancheQuote[][] trancheQuotes,
      BaseCorrelationParam paramObj)
    {
      CDX[] cdx = index.Indexes;
      if (cdx.Length != trancheQuotes.Length)
        throw new ArgumentException(String.Format(
          "Number of index tenors [{0}] and tranche quotes [{1}] not match",
          cdx.Length, trancheQuotes.Length));
      int tenorCount = cdx.Length;

      double[] dp = GetDetachments(trancheQuotes);
      if (dp == null)
        throw new ArgumentException("No tranche quote available");
      int trancheCount = dp.Length;

      Dt[] maturities = null;
      double[,] runningPrem = new double[1,1];
      bool[] useTenors = new bool[tenorCount];
      double[,] quotes = new double[trancheCount, tenorCount];
      for (int t = 0; t < tenorCount; ++t)
      {
        double[] q = GetQuotes(trancheQuotes[t], dp);
        if (q != null)
        {
          for (int i = 0; i < q.Length; ++i)
            quotes[i, t] = q[i];
          useTenors[t] = true;
          runningPrem[0,0] = GetRunningPremium(trancheQuotes[t]);
        }
        else
          useTenors[t] = false;
      }

      if (paramObj == null) paramObj = new BaseCorrelationParam();
      return BaseCorrelationFactory.BaseCorrelationFromMarketQuotes(
        paramObj.CalibrationMethod, paramObj.MappingMethod,
        index, runningPrem, dp, quotes, cdx, maturities, useTenors, null,
        paramObj);
    }

    /// <summary>
    ///   Create CDO tranches from quotes
    /// </summary>
    /// <param name="index"></param>
    /// <param name="trancheQuotes"></param>
    /// <returns></returns>
    public static SyntheticCDO[][] CreateSyntheticCDOs(
      IndexScalingCalibrator index, IndexTrancheQuote[][] trancheQuotes)
    {
      CDX cdx = null;
      foreach (CDX c in index.Indexes)
        if (c != null) { cdx = c; break; }
      int tenorCount = trancheQuotes.Length;

      double[] dp = GetDetachments(trancheQuotes);
      int trancheCount = dp.Length;

      List<SyntheticCDO[]> cdos = new List<SyntheticCDO[]>();
      for (int t = 0; t < tenorCount; ++t)
      {
        SyntheticCDO[] q = GetCDOs(cdx, trancheQuotes[t], dp);
        if (q != null)
          cdos.Add(q);
      }
      return cdos.ToArray();
    }


    private static double GetRunningPremium(IndexTrancheQuote[] tquotes)
    {
      if (tquotes != null)
      {
        foreach (IndexTrancheQuote tq in tquotes)
          if (tq != null && tq.IndexName != null)
            return tq.IndexName.Contains("LCDX.") || tq.IndexName.Contains("HY.") ? 0 : 500;
      }
      return 500;
    }

    private static double[] GetQuotes(IndexTrancheQuote[] tquotes, double[] dp)
    {
      double[] result = null;
      if (tquotes != null)
      {
        foreach (IndexTrancheQuote tq in tquotes)
          if (tq != null && !Double.IsNaN(tq.Detachment))
          {
            int idx = Array.BinarySearch(dp, tq.Detachment);
            if (idx < 0)
              throw new ArgumentException("Internal error");
            else if (tq.Attachment != (idx == 0 ? 0.0 : dp[idx-1]))
              throw new ArgumentException("Corrupted tranche quotes");
            double q = tq.Mid;
            if (Double.IsNaN(q))
              q = (tq.Ask + tq.Bid) / 2;
            if (Double.IsNaN(q))
              q = tq.Ask;
            if (Double.IsNaN(q))
              q = tq.Bid;
            if (result == null)
              result = new double[dp.Length];
            if (tq.QuoteType == "Spread")
              result[idx] = q * 10000;
            else
              result[idx] = q;
          }
      }
      return result;
    }

    private static SyntheticCDO[] GetCDOs(CDX cdx, IndexTrancheQuote[] tquotes, double[] dp)
    {
      SyntheticCDO[] cdos = null;
      if (tquotes != null)
      {
        foreach (IndexTrancheQuote tq in tquotes)
          if (tq != null && !Double.IsNaN(tq.Detachment))
          {
            int idx = Array.BinarySearch(dp, tq.Detachment);
            if (idx < 0)
              throw new ArgumentException("Internal error");
            else if (tq.Attachment != (idx == 0 ? 0.0 : dp[idx - 1]))
              throw new ArgumentException("Corrupted tranche quotes");
            double q = tq.Mid;
            if (Double.IsNaN(q))
              q = (tq.Ask + tq.Bid) / 2;
            if (Double.IsNaN(q))
              q = tq.Ask;
            if (Double.IsNaN(q))
              q = tq.Bid;
            if (cdos == null)
            {
              cdos = new SyntheticCDO[dp.Length];
            }

            double fee = 0, premium = 0;
            if (tq.QuoteType == "Spread")
              premium = q;
            else
            {
              fee = q;
              premium = tq.IndexName.Contains("LCDX.") || tq.IndexName.Contains("HY.") ? 0 : 0.05;
            }

            cdos[idx] = new SyntheticCDO(cdx.Effective, new Dt(tq.Maturity),
              cdx.Ccy, cdx.DayCount, cdx.Freq, cdx.BDConvention, cdx.Calendar,
              premium, fee, idx == 0 ? 0 : dp[idx - 1], dp[idx]);
            cdos[idx].Description = String.Format("{0} {1}~{2}%",
              tq.IndexName, tq.Attachment * 100, tq.Detachment * 100);
          }
      }
      return cdos;
    }

    private static double[] GetDetachments(IndexTrancheQuote[][] trancheQuotes)
    {
      if (trancheQuotes==null) return null;
      UniqueSequence<double> dps = new UniqueSequence<double>();
      foreach (IndexTrancheQuote[] qs in trancheQuotes)
        if(qs!=null)
      {
        foreach (IndexTrancheQuote q in qs)
          if (q != null && !Double.IsNaN(q.Detachment))
            dps.Add(q.Detachment);
      }
      if (dps.Count==0) return null;
      return dps.ToArray();
    }
      

    /// <summary>
    ///   Create a credit curve from cds quotes
    /// </summary>
    /// <param name="quote"></param>
    /// <param name="discountCurve"></param>
    /// <returns></returns>
    private static SurvivalCurve CreateCreditCurve(
      CDSQuote quote, DiscountCurve discountCurve,
      bool includeUnsettled, bool normalize)
    {
      Dt pricingDate = new Dt(quote.Date);
      Currency ccy = (Currency) Enum.Parse(typeof(Currency), quote.Currency);
      string category = null;
      DayCount cdsDayCount = DayCount.Actual360;
      Frequency cdsFreq = Frequency.Quarterly;
      BDConvention cdsRoll = BDConvention.Following;
      Calendar cdsCalendar = Calendar.NYB;
      InterpMethod interpMethod = InterpMethod.Weighted;
      ExtrapMethod extrapMethod = ExtrapMethod.Const;
      NegSPTreatment nspTreatment = NegSPTreatment.Allow;

      int[] indices = GetIncludedQuotes(quote.Quotes, normalize);
      int tenorCount = indices.Length;
      string[] tenorNames = new string[tenorCount];
      Dt[] tenorDates = new Dt[tenorCount];
      double[] fees = new double[tenorCount];
      double[] premiums = new double[tenorCount];
      for (int i = 0; i < tenorCount; ++i)
      {
        Quote q = quote.Quotes[indices[i]];
        tenorNames[i] = q.Tenor;
        tenorDates[i] = CDSMaturity(pricingDate, q.Tenor, cdsCalendar);
        if (q.Type=="Spread")
          premiums[i] = q.Value*10000;
        else
          fees[i] = q.Value;
      }
      double[] recoveries = new double[] { quote.Recovery };
      double recoveryDisp = 0.0;
      bool forceFit = true;
      Dt[] defaultDates = null;
      if (quote.DefaultInfo != null && quote.DefaultInfo.DefaultDate > 0)
      {
        Dt defaultDate = new Dt(quote.DefaultInfo.DefaultDate);
        if (defaultDate >= pricingDate)
          defaultDates = null;
        else if (quote.DefaultInfo.NotSettled && includeUnsettled)
        {
          Dt dfltSettle = quote.DefaultInfo.SettleDate > 0 ?
            new Dt(quote.DefaultInfo.SettleDate) : Dt.Add(pricingDate, 2);
          defaultDates = new Dt[] { defaultDate, dfltSettle };
        }
        else
          defaultDates = new Dt[] { defaultDate };
      }

      SurvivalCurve curve;
      if (quote.Refinance != null)
      {
        // Build Refinancing curve
        SurvivalCurve refinanceCurve = CreateRefinancingCurve(
          pricingDate, quote.Refinance.AnnualRate);

        // build LCDS SurvivalCurve 
        curve = SurvivalCurve.FitLCDSQuotes(pricingDate,
          ccy, category, cdsDayCount, cdsFreq, cdsRoll, cdsCalendar,
          interpMethod, extrapMethod, nspTreatment, discountCurve,
          tenorNames, tenorDates, fees, premiums,
          recoveries, recoveryDisp, forceFit, defaultDates,
          refinanceCurve, quote.Refinance.Correlation);
      }
      else
      {
        curve = SurvivalCurve.FitCDSQuotes(pricingDate,
          ccy, category, cdsDayCount, cdsFreq, cdsRoll, cdsCalendar,
          interpMethod, extrapMethod, nspTreatment, discountCurve,
          tenorNames, tenorDates, fees, premiums,
          recoveries, recoveryDisp, forceFit, defaultDates);
      }
      curve.Name = quote.Ticker;
      return curve;
    }
    private static int[] GetIncludedQuotes(Quote[] quotes, bool normalize)
    {
      if (quotes == null || quotes.Length == 0)
        return new int[0];
      if (!normalize)
      {
        return ArrayUtil.Generate<int>(quotes.Length,
          delegate(int i) { return i; });
      }
      List<int> index = new List<int>();
      int start = 0;
      foreach (string tenor in stdCdsTenors)
      {
        for (int i = start; i < quotes.Length; ++i)
          if (quotes[i].Tenor == tenor) index.Add(i);
      }
      return index.ToArray();
    }
    private static readonly string[] stdCdsTenors = new string[]{
      "1Y","2Y","3Y","5Y","7Y","10Y","15Y","20Y","30Y"
    };

    private static Dt CDSMaturity(Dt asOf, string tenor, Calendar calendar)
    {
      Dt dt = Dt.CDSMaturity(asOf, tenor);
      return (calendar == Calendar.None) ? dt : Dt.Roll(dt, BDConvention.Following, calendar);
    }

    /// <summary>
    ///   Create a refinancing curve
    /// </summary>
    ///<exclude/>
    private static SurvivalCurve CreateRefinancingCurve(Dt asOf, double rate)
    {
      // Single annual refinancing probability
      Dt[] tenorDates = tenorDates = new Dt[] { Dt.Add(asOf, 1, TimeUnit.Years) };
      string[] tenorNames = tenorNames = new string[] { "1Y" };
      double[] nonRefiProbs = new double[] { 1.0 - rate };
      return SurvivalCurve.FromProbabilitiesWithBond(asOf, Currency.None, null,
        InterpMethod.Weighted, ExtrapMethod.Const,
        tenorDates, nonRefiProbs, tenorNames, null, null, null, 0);
    }

  } // class QuoteUtil

}
