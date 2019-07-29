/*
 * ABSBasketPricer.cs
 *
 */
using System;
using System.Collections.Generic;
using System.Collections;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  /// <summary>
  ///   Basket pricer for portfolios of credits with time varying principals and/or recoveries.
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  ///
  [Serializable]
  public class ABSBasketPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(HeterogeneousBasketPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start calculating losses</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Notionals or weights by names</param>
    /// <param name="cashflowStreams">ABS cashflow streams</param>
    /// <param name="dates">Dates representing pricing time grids</param>
    /// <param name="copula">Copula for the correlation structure</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    public ABSBasketPricer(
      Dt portfolioStart,
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      CashflowStream[] cashflowStreams,
      Copula copula,
      Correlation correlation,
      Dt[] dates,
      Array lossLevels
      )
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves,
        principals, copula, correlation, 50, TimeUnit.Years, lossLevels)
    {
      logger.DebugFormat("Creating ABS Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);

      this.PortfolioStart = portfolioStart;

      if (survivalCurves.Length != cashflowStreams.Length)
        throw new ArgumentException( String.Format("Number of assets ({0}) and cashflow generatorss ({1}) not match",
          survivalCurves.Length, cashflowStreams.Length));

      this.cashflowStreams_ = cashflowStreams;
      this.dates_ = dates;

      this.lossDistribution_ = null;
      this.amorDistribution_ = null;

      this.GridSize = 0.005;

      logger.Debug("Basket created");
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      ABSBasketPricer obj = (ABSBasketPricer)base.Clone();

      obj.cashflowStreams_ = new CashflowStream[cashflowStreams_.Length];
      for (int i = 0; i < cashflowStreams_.Length; ++i)
        obj.cashflowStreams_[i] = (CashflowStream)cashflowStreams_[i].clone();

      obj.dates_ = new Dt[dates_.Length];
      for (int i = 0; i < dates_.Length; ++i)
        obj.dates_[i] = dates_[i];

      obj.lossDistribution_ = lossDistribution_ == null ? null : lossDistribution_.clone();
      obj.amorDistribution_ = amorDistribution_ == null ? null : amorDistribution_.clone();

      return obj;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // No. of survival curves must match the no. of cashflowstreams
      if (SurvivalCurves.Length != cashflowStreams_.Length)
        InvalidValue.AddError(errors, this, String.Format("Surv. Curves no must match no. of cashflows"));

      return;
    }


    /// <summary>
    ///   Compute the whole distribution, save the result for later use
    /// </summary>
    private void
    ComputeAndSaveDistribution(bool calcAmorDistribution)
    {
      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing distribution for Base Correlation basket");

      // Initialize distributions and basket analyzer
      Dt start = this.PortfolioStart.IsEmpty() ? this.Settle : this.PortfolioStart;
      Dt[] dates = checkDates(start, this.dates_);
      Analyzer analyzer = new Analyzer(calcAmorDistribution, dates, this.SurvivalCurves,
        this.RecoveryCurves, this.Principals, this.cashflowStreams_);
      Curve2D dist = InitializeDistribution(false, start, dates, this.CookedLossLevels);
      if (calcAmorDistribution)
        this.amorDistribution_ = dist;
      else
        this.lossDistribution_ = dist;

      // Get correlation
      CorrelationTermStruct corr = this.CorrelationTermStruct;
      Copula copula = this.Copula;

      // Calculate distributions
      ABSBasketModel.ComputeDistributions(false, this.Count, this.TotalPrincipal,
        copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
        corr.Correlations, corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
        this.IntegrationPointsFirst, this.IntegrationPointsSecond,
        analyzer.Probabilities, analyzer.DefaultValues, analyzer.SurvivalValues,
        this.GridSize, dist);

      timer.stop();
      logger.DebugFormat("Completed basket distribution in {0} seconds", timer.getElapsed());

      return;
    }


    /// <summary>
    ///   Compute the cumulative loss distribution
    /// </summary>
    ///
    /// <remarks>
    ///   The returned array may have three columns, the first of which contains the
    ///   loss levels, the second and the third columns contain the cumulative
    ///   probabilities or expected base losses corresponding to attachment or
    ///   detachment correlations.
    /// </remarks>
    ///
    /// <param name="wantProbability">If true, return probabilities; else, return expected base losses</param>
    /// <param name="date">The date at which to calculate the distribution</param>
    /// <param name="lossLevels">Array of lossLevels (should be between 0 and 1)</param>
    public override double[,]
    CalcLossDistribution(
      bool wantProbability,
      Dt date, double[] lossLevels)
    {
      throw new System.NotImplementedException("ABSBasketPricer.CalcLossDistribution() method is not implemented yet");
    }


    ///
    /// <summary>
    ///   Compute the accumulated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    ///
    public override double
    AccumulatedLoss(
        Dt date,
        double trancheBegin,
        double trancheEnd)
    {
      if (lossDistribution_ == null)
        ComputeAndSaveDistribution(false);

      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);
      loss += lossDistribution_.Interpolate(0, date, trancheBegin, trancheEnd) / TotalPrincipal;

      //logger.DebugFormat("Computed Loss for {0}-{1} @{2} as {3}", trancheBegin, trancheEnd, date, loss );

      return loss;
    }


    ///
    /// <summary>
    ///   Compute the amortized amount on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    ///
    public override double
    AmortizedAmount(
        Dt date,
        double trancheBegin,
        double trancheEnd)
    {
      if (amorDistribution_ == null)
        ComputeAndSaveDistribution(true);

      double amortized = 0;
      double tBegin = 1 - trancheEnd;
      double tEnd = 1 - trancheBegin;
      AdjustTrancheLevels(true, ref tBegin, ref tEnd, ref amortized);
      amortized += AmorDistribution.Interpolate(0, date, tBegin, tEnd) / TotalPrincipal;

      //logger.DebugFormat("Computed Amortization for {0}-{1} @{2} as {3}", trancheBegin, trancheEnd, date, amort );

      return amortized;
    }


    ///
    /// <summary>
    ///   Reset the pricer such that in the next request for AccumulatedLoss()
    ///   or AmortizedAmount(), it recompute everything.
    /// </summary>
    ///
    public override void Reset()
    {
      lossDistribution_ = null;
      amorDistribution_ = null;
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Computed distribution for basket
    /// </summary>
    public Curve2D LossDistribution
    {
      get { return lossDistribution_; }
      set { lossDistribution_ = value; }
    }

    /// <summary>
    ///   Computed distribution for basket
    /// </summary>
    public Curve2D AmorDistribution
    {
      get { return amorDistribution_; }
      set { amorDistribution_ = value; }
    }
    #endregion // Properties

    #region Data

    private CashflowStream[] cashflowStreams_;
    private Dt[] dates_;

    private Curve2D lossDistribution_;
    private Curve2D amorDistribution_;
    #endregion Data

    #region Helper_Functions
    /// <summary>
    ///   Check date array is legitimate, re-organize it if necessary
    /// </summary>
    ///
    /// <remarks>This functions makes sure the first date in the array is the start date
    ///  and every subsequent dates are in proper order.</remarks>
    ///
    /// <param name="start">portfolio start date</param>
    /// <param name="dates">Dates array</param>
    /// <returns>Dates array start with settle</returns>
    private static Dt[] checkDates(Dt start, Dt[] dates)
    {
      if (dates == null || dates.Length == 0)
        throw new ArgumentException("dates array is null or empty");
      if (dates.Length > 1)
        for (int i = 1; i < dates.Length; ++i)
          if (Dt.Cmp(dates[i - 1], dates[i]) >= 0)
            throw new ArgumentException(String.Format( "date {0} is out of order", i));
      int startIndex = 0;
      while (Dt.Cmp(start, dates[startIndex]) > 0)
      {
        if (++startIndex >= dates.Length)
          throw new ArgumentException("All dates are before settle");
      }
      int add = (Dt.Cmp(start, dates[startIndex]) < 0) ? 1 : 0;
      if (add > 0 || startIndex != 0)
      {
        int N = dates.Length - startIndex;
        Dt[] tmp = new Dt[N + add];
        if (add > 0)
          tmp[0] = start;
        for (int i = 0; i < N; ++i)
          tmp[i + add] = dates[i + startIndex];
        dates = tmp;
      }
      return dates;
    }

    /// <summary>
    ///   Get initial notional from cash flow generator
    /// </summary>
    /// <param name="cg">Cashflow generators</param>
    /// <returns></returns>
    private static double[] getStreamNotional(CashflowStream[] cg)
    {
      double[] principals = new double[cg.Length];
      for(int i = 0; i < principals.Length; ++i)
        principals[i] = cg[i].GetNotional(0);
      return principals;
    }

    /// <summary>
    ///   Initialize the distribution surface
    /// </summary>
    public static Curve2D InitializeDistribution(
      bool wantProbability,
      Dt asOf,
      Dt[] dates,
      IList<double> lossLevels)
    {
      dates = checkDates(asOf, dates);
      int nDates = dates.Length;
      int nLevels = lossLevels.Count;
      Curve2D distribution = new Curve2D();
      distribution.Initialize(nDates, nLevels, 1);
      distribution.SetAsOf(asOf);
      for (int i = 0; i < nDates; ++i)
        distribution.SetDate(i, dates[i]);
      for (int i = 0; i < nLevels; ++i)
      {
        distribution.SetLevel(i, lossLevels[i]);
        distribution.SetValue(i, 0.0);
      }
      if (wantProbability)
      {
        // start with the probability of no default equals to 1
        distribution.SetValue(0, 1.0);
      }
      return distribution;
    }

    /// <summary>
    ///   Initialize distributions
    /// </summary>
    private void initDistributions()
    {
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      lossDistribution_ = InitializeDistribution(false, start, dates_, this.CookedLossLevels);
      amorDistribution_ = InitializeDistribution(false, start, dates_, this.CookedLossLevels);
    }
    #endregion // Helper_Functions

    #region Helper_Classes
    /// <summary>
    ///   Simple Analyzer: need to be extended
    /// </summary>
    /// <exclude />
    class Analyzer
    {
      #region Methods
      /// <summary>
      ///   Constructor
      /// </summary>
      /// <param name="amortize">for amortizations</param>
      /// <param name="dates">dates</param>
      /// <param name="survivalCurves">survival curves</param>
      /// <param name="recoveryCurves">recovery curves</param>
      /// <param name="principals">principals</param>
      /// <param name="cashflowStreams">generators</param>
      /// <exclude />
      public Analyzer(
        bool amortize,
        Dt[] dates,
        SurvivalCurve[] survivalCurves,
        RecoveryCurve[] recoveryCurves,
        double[] principals,
        CashflowStream[] cashflowStreams
        )
      {
        int basketSize = survivalCurves.Length;
        int nDates = dates.Length;
        int dataLen = nDates * basketSize;
        probabilities_ = new double[dataLen];
        defaultValues_ = new double[dataLen];
        survivalValues_ = new double[dataLen];
        for (int i = 0; i < basketSize; ++i)
          initialize(amortize, dates, i * nDates,
            survivalCurves[i], recoveryCurves[i], principals[i],
            cashflowStreams[i]);
        return;
      }

      /// <summary>
      ///   Calculate losses and amortized amounts given default
      /// </summary>
      /// <param name="amortize"></param>
      /// <param name="dates"></param>
      /// <param name="nameIndex"></param>
      /// <param name="survivalCurve"></param>
      /// <param name="recoveryCurve"></param>
      /// <param name="principal"></param>
      /// <param name="cashflow"></param>
      /// <exclude />
      private Dt initialize(
        bool amortize,
        Dt[] dates, int nameIndex,
        SurvivalCurve survivalCurve,
        RecoveryCurve recoveryCurve,
        double principal,
        CashflowStream cashflow
        )
      {
        lossStartIndex_ = 0; amorStartIndex_ = 0;
        probabilities_[nameIndex] = 0.0;
        defaultValues_[nameIndex] = 0.0;
        survivalValues_[nameIndex] = 0.0;

        Dt asOf = dates[0];
        Dt lastDate = asOf;
        double startSurvProb = survivalCurve.Interpolate(asOf);
        if (startSurvProb > 1.0E-9)
          startSurvProb = 1.0;
        else
          startSurvProb = 0.0;
        for (int i = 1, idx = nameIndex + 1; i < dates.Length; ++idx, ++i)
        {
          Dt date = dates[i];
          probabilities_[idx] = startSurvProb * Math.Max(1.0 - survivalCurve.Interpolate(asOf, date), 0.0);

          double amor = amortize ? getPrepay(asOf, date, survivalCurve, cashflow) : 0.0;
          survivalValues_[idx] = amortize ? amor * principal : 0.0;

          double lgd = getLGD(asOf, date, survivalCurve, recoveryCurve, cashflow);
          defaultValues_[idx] = principal * (amortize ? (1.0 - lgd) : lgd);
          if (Math.Abs(lgd) > 1E-9 || Math.Abs(amor - 1.0) > 1E-9)
            lastDate = date;
        }
        return lastDate;
      }

      /// <summary>
      ///   Find the value of loss given default on a date
      /// </summary>
      /// <param name="asOf">asOf date</param>
      /// <param name="date">date</param>
      /// <param name="survivalCurve">survival curve</param>
      /// <param name="recoveryCurve">recovery curve</param>
      /// <param name="cashflow">cashflow</param>
      /// <returns>loss given default</returns>
      /// <exclude />
      private double getLGD(
        Dt asOf, Dt date,
        SurvivalCurve survivalCurve,
        RecoveryCurve recoveryCurve,
        CashflowStream cashflow )
      {
        int count = cashflow.Count;
        int start = lossStartIndex_;
        if (start >= count)
          return 0.0;

        Dt startDt = cashflow.GetDate(start);
        while (Dt.Cmp(startDt, asOf) < 0)
        {
          // In case part of the cashflow shift to before the asOf,
          // for example, when doing theta calculation.
          if (start == count - 1)
            return 0.0;
          lossStartIndex_ = ++start;
          startDt = cashflow.GetDate(start);
        }

        if (Dt.Cmp(startDt, date) >= 0)
        {
          double notional = start > 0 ? cashflow.GetNotional(start - 1) : cashflow.GetNotional(start);
          double rate = 1.0 - recoveryCurve.Interpolate(date);
          return rate * notional;
        }
        else if (start == count - 1)
        {
          // This is the case where startDt is the last date in the cashflow and date is later than it.
          // Obviously, date is after the maturity date of this name and no default is possible.
          return 0.0;
        }

        double sumP = 0.0;
        double sumL = 0.0;
        double p0 = survivalCurve.Interpolate(asOf, startDt);
        for (++start; start < count; ++start)
        {
          startDt = cashflow.GetDate(start);
          if (Dt.Cmp(startDt, date) >= 0)
          {
            double notional = start > 0 ? cashflow.GetNotional(start - 1) : cashflow.GetNotional(start);
            double rate = 1.0 - recoveryCurve.Interpolate(date);
            double prob = Math.Max(p0 - survivalCurve.Interpolate(asOf, date), 0.0);
            sumP += prob;
            sumL += prob * rate * notional;
            break;
          }
          else
          {
            double rate = 1.0 - recoveryCurve.Interpolate(date);
            double p1 = survivalCurve.Interpolate(asOf, startDt);
            double prob = Math.Max(p0 - p1, 0.0);
            sumP += prob;
            sumL += prob * rate * cashflow.GetNotional(start);
            p0 = p1;
          }
        }
        lossStartIndex_ = start;

        if (sumP > 1.0E-14)
          return sumL / sumP;
        else
          return 0.0;
      }

      /// <summary>
      ///   Find the value of prepayment on a date
      /// </summary>
      /// <param name="asOf">asOf date</param>
      /// <param name="date">date</param>
      /// <param name="survivalCurve">survival curve</param>
      /// <param name="cashflow">cashflow</param>
      /// <returns>prepayment on survival</returns>
      /// <exclude />
      private double getPrepay(
        Dt asOf, Dt date,
        SurvivalCurve survivalCurve,
        CashflowStream cashflow)
      {
        int count = cashflow.Count;
        int start = amorStartIndex_;
        if (start >= count)
          return 1.0;

        Dt startDt = cashflow.GetDate(start);
        while (Dt.Cmp(startDt, asOf) < 0)
        {
          // In case part of the cashflow shift to before the asOf,
          // for example, when doing theta calculation.
          if (start == count - 1)
            return 1.0; // all paid
          amorStartIndex_ = ++start;
          startDt = cashflow.GetDate(start);
        }

        for (; start < count; ++start)
        {
          startDt = cashflow.GetDate(start);
          if (Dt.Cmp(startDt, date) >= 0)
          {
            double notional = start > 0 ? cashflow.GetNotional(start - 1) : cashflow.GetNotional(start);
            if (this.continuousPayment_)
            {
              // If in the middle of two payment dates, use the weight average of
              // the remaining notionals.
              double sp = survivalCurve.Interpolate(date, startDt);
              notional += sp * (cashflow.GetNotional(start) - notional);
            }
            amorStartIndex_ = start;
            return 1.0 - notional;
          }
        }
        amorStartIndex_ = start;

        if (start >= count || Dt.Cmp(startDt, date) >= 0)
          return 1.0;
        return 1.0 - cashflow.GetNotional(start);
      }

      #endregion // Methods

      #region Properties
      /// <summary>
      ///   Probability array
      /// </summary>
      /// <exclude />
      public double[] Probabilities
      {
        get { return probabilities_; }
      }

      /// <summary>
      ///   Payments if default occurs
      /// </summary>
      /// <exclude />
      public double[] DefaultValues
      {
        get { return defaultValues_; }
      }

      /// <summary>
      ///   Principal payments if survival
      /// </summary>
      /// <exclude />
      public double[] SurvivalValues
      {
        get { return survivalValues_; }
      }

      #endregion // Properties

      #region Data
      //private Dt[] dates_;
      private double[] probabilities_;
      private double[] defaultValues_;
      private double[] survivalValues_;
      private bool continuousPayment_ = false;

      private int lossStartIndex_; // cached index for efficient search
      private int amorStartIndex_; // cached index for efficient search
      #endregion // Data

      #region OLD_METHODS
#if Not_yet
      private static ABSData AnalyzeData(
        int basketSize,
        double totalPrincipal,
        Dt[] dates,
        double[] probabilities,
        double[] defaultValues,
        double[] survivalValues
        )
      {
        Dt[] dts = GetSignificantDates
          (basketSize, totalPrincipal, dates,
          probabilities, defaultValues, survivalValues);
        ABSData ad = new ABSData();
        int newDataLen = dts.Length * basketSize;
        if (newDataLen == probabilities.Length)
        {
          ad.Dates = dates;
          ad.Probabilities = probabilities;
          ad.DefaultValues = defaultValues;
          ad.SurvivalValues = survivalValues;
          return ad;
        }
        ad.Dates = dts;
        ad.Probabilities = new double[newDataLen];
        ad.DefaultValues = new double[newDataLen];
        ad.SurvivalValues = new double[newDataLen];
        for (int i = 0; i < basketSize; ++i)
          AggregateData(basketSize, i, dates, probabilities, defaultValues, survivalValues, ad);
        return ad;
      }

      private static void AggregateData(
        int basketSize,
        int nameIndex,
        Dt[] dates,
        double[] probabilities,
        double[] defaultValues,
        double[] survivalValues,
        ABSData ad
        )
      {
        int start = nameIndex * dates.Length;
        int start0 = nameIndex * ad.Dates.Length;
        double sumP = 0;
        double sumD = 0;
        double sumS = 0;
        int len = ad.Dates.Length;
        for (int i = 0, idx = 0; i < len; )
        {
          double prob = probabilities[start + idx];
          sumP += prob;
          sumD += prob * defaultValues[start + idx];
          sumS += prob * survivalValues[start + idx];
          if (Dt.Cmp(dates[idx], ad.Dates[i]) == 0)
          {
            ad.Probabilities[start0 + i] = sumP;
            ad.DefaultValues[start0 + i] = sumP > 1.0E-9 ? (sumD / sumP) : 0.0;
            ad.SurvivalValues[start0 + i] = sumP > 1.0E-9 ? (sumS / sumP) : 0.0;
            ++i;
            sumP = 0;
            sumD = 0;
            sumS = 0;
          }
          ++idx;
        }
        return;
      }

      private static Dt[] GetSignificantDates(
        int basketSize,
        double totalPrincipal,
        Dt[] dates,
        double[] probabilities,
        double[] defaultValues,
        double[] survivalValues
        )
      {
        const double epsilon = 1.0E-5;
        double tolerance = epsilon * totalPrincipal;
        System.Collections.ArrayList dateList = new System.Collections.ArrayList();
        AddDate(dateList, dates[0]);
        for (int i = 0; i < basketSize; ++i)
          AddDates(basketSize, tolerance, dates, i, probabilities, defaultValues, survivalValues, dateList);
        AddDate(dateList, dates[dates.Length - 1]);
        return (Dt[])dateList.ToArray(typeof(Dt));
      }

      private static void AddDates(
        int basketSize,
        double tolerance,
        Dt[] dates,
        int nameIndex,
        double[] probabilities,
        double[] defaultValues,
        double[] survivalValues,
        System.Collections.ArrayList dateList
        )
      {
        int nDates = dates.Length;
        int start = nameIndex * nDates;
        double dvalue0 = defaultValues[start];
        double svalue0 = survivalValues[start];
        int stop = start + nDates;
        for (int i = start + 1, idx = 0; i < stop; ++idx, ++i)
        {
          if (Math.Abs(defaultValues[i] - dvalue0) > tolerance)
          {
            AddDate(dateList, dates[idx]);
            dvalue0 = defaultValues[i];
            svalue0 = survivalValues[i];
          }
          else if (Math.Abs(survivalValues[i] - svalue0) > tolerance)
          {
            AddDate(dateList, dates[idx]);
            dvalue0 = defaultValues[i];
            svalue0 = survivalValues[i];
          }
        }
        return;
      }

      private static void AddDate(
        System.Collections.ArrayList array, Dt date)
      {
        int pos = array.BinarySearch(date);
        if (pos < 0)
          array.Insert(~pos, date);
        return;
      }
#endif
      #endregion // OLD_METHOD
    };
    #endregion // Helper_Classes

  } // class ABSBasketPricer

}
