/*
 * Scenario.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Base
{

  ///
  /// <summary>
  ///  CPDO Bond Path class
  /// </summary>
  ///
  ///
  public class CPDOBondPath
  {

    #region Constructors


    /// <summary>
    ///   Create a Cpdo Bond Path
    /// </summary>
    ///
    /// <param name="cpdo">Cpdo Product</param>
    /// <param name="asOf">AsOf</param>
    /// <param name="settle">Settle</param>
    /// <param name="discountCurve">Discount Curve</param>
    /// <param name="referenceCurve">Reference Curve</param>
    /// <param name="lastResetFloatingBond">Last Reset</param>
    /// <param name="freq">Eval Grid Frequency</param>
    ///
    ///
    /// <returns>Cpdo Bond Path</returns>
    ///
    public CPDOBondPath(CPDO cpdo,
                         Dt asOf, Dt settle,
                         DiscountCurve discountCurve,
                         DiscountCurve referenceCurve,
                         double lastResetFloatingBond,
                         Frequency freq)
    {

      cpdo_ = cpdo;
      asOf_ = asOf;
      settle_ = settle;
      lastResetFloatingBond_ = lastResetFloatingBond;
      referenceCurve_ = referenceCurve;
      discountCurve_ = discountCurve;
      freq_ = freq;

      // fill in bond dates, accruals, values, roll dates, cpdoSpreads

      // create evaluation grid and fill in eval dates and spreads list
      Dt firstCoupon = CDS.GetDefaultFirstPremiumDate(Settle, Cpdo.Maturity);
      //while (firstCouponDate.Month != (int)Month.March && firstCouponDate.Month != (int)Month.September)
      //  firstCouponDate = CDS.GetDefaultFirstPremiumDate(firstCouponDate, Cpdo.Maturity);
      Schedule cpdoEvalSched = new Schedule(Settle, Cpdo.Effective, firstCoupon,
                                              Cpdo.Maturity, Cpdo.Maturity,
                                              freq, Cpdo.BDConvention,
                                              Cpdo.Calendar, false, false, false);

      evalDates_ = new List<Dt>();
      evalDates_.Add(Settle);

      for (int i = 0; i < cpdoEvalSched.Count; ++i)
      {
        Dt paymentDate = cpdoEvalSched.GetPaymentDate(i);
        if (!evalDates_.Contains(paymentDate))
          evalDates_.Add(paymentDate);
      }

      // Calculate bond payment dates and accrual amounts
      Bond floatingRateBond = cpdo.FloatingRateBond;
      Bond cashInBarrier = (Bond)cpdo.FloatingRateBond.Clone();
      cashInBarrier.Coupon += cpdo.AdminFee;


      // create riskless survival curve for the floating rate bond
      SurvivalCurve floatingBondRisklessSC = new SurvivalCurve(AsOf, 0);

      BondPricer floatingBondPricer, cashInBarrierPricer;
      floatingBondPricer = new BondPricer(floatingRateBond, AsOf, Settle, DiscountCurve,
                                                  floatingBondRisklessSC, 0, TimeUnit.None, 1.0);
      cashInBarrierPricer = new BondPricer(cashInBarrier, AsOf, Settle, DiscountCurve,
                                                  floatingBondRisklessSC, 0, TimeUnit.None, 1.0);

      floatingBondPricer.Notional = Cpdo.Notional;
      cashInBarrierPricer.Notional = Cpdo.Notional;

      // Set floating rate curve if required
      if (ReferenceCurve == null || LastResetFloatingBond <= 0.0)
        throw new ArgumentException("Must specify the reference curve and last reset rate for floating rate bond");

      floatingBondPricer.ReferenceCurve = ReferenceCurve;
      cashInBarrierPricer.ReferenceCurve = ReferenceCurve;
      // path reset date to bondpricer
      floatingBondPricer.CurrentRate = LastResetFloatingBond;
      cashInBarrierPricer.CurrentRate = LastResetFloatingBond + Cpdo.AdminFee;

      // Create the bond payments schedule. 
      // floatingRateBond.FirstCoupon = CDS.GetDefaultFirstPremiumDate(floatingRateBond.Effective, floatingRateBond.Maturity);
      var floatingBondCashflow = new CashflowAdapter(
        floatingBondPricer.GetPaymentSchedule(null, AsOf));

      // Extract bond dates and accruals
      floatingBondDates_ = new List<Dt>();
      floatingBondAccruals_ = new List<double>();

      // on settle date we price the bond
      floatingBondDates_.Add(Settle);
      floatingBondAccruals_.Add(0);


      for (int i = 0; i < floatingBondCashflow.Count; ++i)
      {
        Dt floatingBondDate = floatingBondCashflow.GetDt(i);
        double accrued = floatingBondCashflow.GetAccrued(i);
        floatingBondAccruals_.Add(accrued);
        if (!evalDates_.Contains(floatingBondDate))
          evalDates_.Add(floatingBondDate);
        if (!floatingBondDates_.Contains(floatingBondDate))
          floatingBondDates_.Add(floatingBondDate);
      }
      evalDates_.Sort();
      floatingBondDates_.Sort();

      // create bond prices and values
      floatingBondForwardFullPrices_ = new List<double>();
      floatingBondForwardValues_ = new List<double>();
      double pv = floatingBondPricer.ProductPv();
      floatingBondForwardFullPrices_.Add(pv * 100 / Cpdo.Notional);
      floatingBondForwardValues_.Add(pv);

      // create cashInBarrier prices and values
      cashInBarrierForwardFullPrices_ = new List<double>();
      cashInBarrierForwardValues_ = new List<double>();
      pv = cashInBarrierPricer.ProductPv();
      cashInBarrierForwardFullPrices_.Add(pv * 100 / Cpdo.Notional);
      cashInBarrierForwardValues_.Add(pv);

      for (int i = 1; i < evalDates_.Count; i++)
      {
        floatingBondPricer.MarketQuote = floatingBondForwardFullPrices_[0]/100;
        floatingBondPricer.QuotingConvention = QuotingConvention.FullPrice;
        pv = floatingBondPricer.FwdValue(EvalDates[i]);
        floatingBondForwardValues_.Add(pv);
        floatingBondForwardFullPrices_.Add(pv * 100 / Cpdo.Notional);

        cashInBarrierPricer.MarketQuote = cashInBarrierForwardFullPrices_[0]/100;
        cashInBarrierPricer.QuotingConvention = QuotingConvention.FullPrice;
        pv = cashInBarrierPricer.FwdValue(EvalDates[i]);
        cashInBarrierForwardValues_.Add(pv);
        cashInBarrierForwardFullPrices_.Add(pv * 100 / Cpdo.Notional);
      }

      // set values at maturity to par
      floatingBondForwardFullPrices_[EvalDates.Count - 1] = 100;
      floatingBondForwardValues_[EvalDates.Count - 1] = Cpdo.Notional;
      cashInBarrierForwardFullPrices_[EvalDates.Count - 1] = 100.0;
      cashInBarrierForwardValues_[EvalDates.Count - 1] = Cpdo.Notional;

      // Generate Roll Dates
      rollDates_ = new List<Dt>();
      Dt firstCouponDate = CDS.GetDefaultFirstPremiumDate(Settle, Cpdo.Maturity);
      while (firstCouponDate.Month != (int)Month.March && firstCouponDate.Month != (int)Month.September)
        firstCouponDate = CDS.GetDefaultFirstPremiumDate(firstCouponDate, Cpdo.Maturity);

      Schedule cpdoRollSched = new Schedule(Settle, Cpdo.Effective, firstCouponDate,
                                             Cpdo.Maturity, Cpdo.Maturity,
                                             Frequency.SemiAnnual, Cpdo.BDConvention,
                                             Cpdo.Calendar, false, false, false);
      for (int i = 0; i < cpdoRollSched.Count; ++i)
      {
        Dt rollDate = cpdoRollSched.GetPaymentDate(i);
        if (!rollDates_.Contains(rollDate))
          rollDates_.Add(rollDate);
        if (!evalDates_.Contains(rollDate))
          evalDates_.Add(rollDate);
      }
    }

    #endregion

    #region Methods

    #endregion

    #region Properties

    /// <summary>
    ///  As of Date
    /// </summary>
    public Dt AsOf
    {
      get
      {
        return asOf_;
      }
    }

    /// <summary>
    ///  Settle Date
    /// </summary>
    public Dt Settle
    {
      get
      {
        return settle_;
      }
    }


    /// <summary>
    ///  Cpdo product
    /// </summary>
    public CPDO Cpdo
    {
      get
      {
        return cpdo_;
      }
    }

    /// <summary>
    ///  Cpdo Eval frequency
    /// </summary>
    public Frequency Freq
    {
      get
      {
        return freq_;
      }
    }

    /// <summary>
    ///  Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get
      {
        return discountCurve_;
      }
    }

    /// <summary>
    ///  Reference curve
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get
      {
        return referenceCurve_;
      }
    }

    /// <summary>
    ///  Last Reset Floating Rate Bond
    /// </summary>
    public double LastResetFloatingBond
    {
      get
      {
        return lastResetFloatingBond_;
      }
    }

    /// <summary>
    ///  List of floating Bond Coupon dates
    /// </summary>
    public List<Dt> FloatingBondDates
    {
      get
      {
        return floatingBondDates_;
      }
    }

    /// <summary>
    ///  Globoxx roll dates
    /// </summary>
    public List<Dt> RollDates
    {
      get
      {
        return rollDates_;
      }
    }

    /// <summary>
    ///  Globoxx eval dates
    /// </summary>
    public List<Dt> EvalDates
    {
      get
      {
        return evalDates_;
      }
    }

    /// <summary>
    ///  Floating Bonds Accruals
    /// </summary>
    public List<double> FloatingBondAccruals
    {
      get
      {
        return floatingBondAccruals_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Values
    /// </summary>
    public List<double> FloatingBondForwardValues
    {
      get
      {
        return floatingBondForwardValues_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Full Prices
    /// </summary>
    public List<double> FloatingBondForwardFullPrices
    {
      get
      {
        return floatingBondForwardFullPrices_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Full Prices + Admin Fees = Cash In Barrier
    /// </summary>
    public List<double> CashInBarrierForwardFullPrices
    {
      get
      {
        return cashInBarrierForwardFullPrices_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Values
    /// </summary>
    public List<double> CashInBarrierForwardValues
    {
      get
      {
        return cashInBarrierForwardValues_;
      }
    }

    #endregion

    #region Data

    private CPDO cpdo_;
    private Dt asOf_;
    private Dt settle_;
    private double lastResetFloatingBond_;
    private Frequency freq_;
    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private List<Dt> floatingBondDates_;
    private List<Dt> rollDates_;
    private List<Dt> evalDates_;
    private List<double> floatingBondAccruals_;
    private List<double> floatingBondForwardValues_;
    private List<double> floatingBondForwardFullPrices_;
    private List<double> cashInBarrierForwardFullPrices_;
    private List<double> cashInBarrierForwardValues_;

    #endregion //Data

  } // class CPDOBondPath


  ///
  /// <summary>
  ///  CPDO Scenario engine class
  /// </summary>
  ///
  ///
  public class CPDOMonteCarloModel
  {

    #region Constructors


    /// <summary>
    ///   Create a Cpdo Path
    /// </summary>
    ///
    ///
    /// <param name="cpdo">Cpdo Product</param>
    /// <param name="cpdoBondPath">Cpdo Bond Path</param>
    /// <param name="asOf">AsOf</param>
    /// <param name="settle">Settle</param>
    /// <param name="discountCurve">Discount Curve</param>
    /// <param name="referenceCurve">Reference Curve</param>
    /// <param name="spreadCurve">Spreads Curve</param>
    /// <param name="recovery">Recovery</param>
    /// <param name="lastResetFloatingBond">Last Reset</param>
    /// <param name="cushion">Cushion</param>
    /// <param name="lossFactor">Loss Factor</param>
    /// <param name="rollCost">Roll Cost</param>
    /// <param name="compChange">Composition Change spread adjustment</param>
    /// <param name="rollDown">Roll Down spread ajustment</param>
    /// <param name="freq">Eval Grid Frequency</param>
    /// <param name="upDeltas">Bid-Ask Costs</param>
    ///
    ///
    /// <returns>Cpdo Path</returns>
    ///
    public CPDOMonteCarloModel(CPDO cpdo,
                     CPDOBondPath cpdoBondPath,
                     Dt asOf, Dt settle,
                     DiscountCurve discountCurve,
                     DiscountCurve referenceCurve,
                     Curve spreadCurve,
                     double recovery,
                     double lastResetFloatingBond,
                     double cushion,
                     double lossFactor,
                     double rollCost,
                     double compChange, //adjust spread at roll dates
                     double rollDown, // adjust spread at roll dates
                     Frequency freq,
                     double[] upDeltas)
    {

      cpdo_ = cpdo;
      cpdoBondPath_ = cpdoBondPath;
      asOf_ = asOf;
      settle_ = settle;
      cushion_ = cushion;
      lossFactor_ = lossFactor;
      lastResetFloatingBond_ = lastResetFloatingBond;
      referenceCurve_ = referenceCurve;
      discountCurve_ = discountCurve;
      spreadCurve_ = spreadCurve;
      recovery_ = recovery;
      rollCost_ = rollCost;
      compChange_ = compChange;
      rollDown_ = rollDown;
      frequency_ = freq;
      floatingBondDates_ = cpdoBondPath.FloatingBondDates;
      rollDates_ = cpdoBondPath.RollDates;
      evalDates_ = cpdoBondPath.EvalDates;
      floatingBondAccruals_ = cpdoBondPath.FloatingBondAccruals;
      floatingBondForwardValues_ = cpdoBondPath.FloatingBondForwardValues;
      floatingBondForwardFullPrices_ = cpdoBondPath.FloatingBondForwardFullPrices;
      cashInBarrierForwardFullPrices_ = cpdoBondPath.CashInBarrierForwardFullPrices;
      cashInBarrierForwardValues_ = cpdoBondPath.CashInBarrierForwardValues; ;
      upDeltas_ = upDeltas;

      // fill in spreads
      spreads_ = new List<double>();
      for (int i = 0; i < cpdoBondPath.EvalDates.Count; ++i)
      {
        spreads_.Add(spreadCurve.Interpolate(cpdoBondPath.EvalDates[i]) * (1 + RollDown) * (1 + CompChange));
      }
      // Allocate space for path dictionary
      pathDictionary_ = new Dictionary<Dt, CPDOMCPathStorage>();
    }

    #endregion

    #region Methods


    /// <summary>
    ///   Return Path DataTable
    /// </summary>
    ///
    /// <returns>Path DataTable</returns>
    public DataTable FillPathDataTable()
    {
      double pathPv = GetPathPv();
      DataTable pathDataTable = new DataTable("Path Data Table");
      pathDataTable.Columns.Add(new DataColumn("Date", typeof(Dt)));
      pathDataTable.Columns.Add(new DataColumn("Globoxx Notional", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("GloboxxCDS Premium", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Globoxx Spread", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Globoxx MTM", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Carry", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Cash Account", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Fee", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("NAV", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Target Bond Value", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("CashIn Barrier", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Fee Forward Pv", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Shortfall", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Target Notional", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Max Notional", typeof(double)));
      pathDataTable.Columns.Add(new DataColumn("Leverage", typeof(double)));

      // do loop till pathDictionary.Count
      for (int i = 0; i < PathDictionary.Count; ++i)
      {
        DataRow row = pathDataTable.NewRow();

        row["Date"] = EvalDates[i];
        row["Globoxx Notional"] = PathDictionary[EvalDates[i]].GloboxxNotional;
        row["GloboxxCDS Premium"] = PathDictionary[EvalDates[i]].GloboxxCdsFwdPremium;
        row["Globoxx Spread"] = Spreads[i];
        row["Globoxx MTM"] = PathDictionary[EvalDates[i]].GloboxxCdsFwdMTM;
        row["Carry"] = PathDictionary[EvalDates[i]].GloboxxCarry;
        row["Cash Account"] = PathDictionary[EvalDates[i]].CashAccount;
        row["Fee"] = PathDictionary[EvalDates[i]].Fee;
        row["NAV"] = PathDictionary[EvalDates[i]].Nav;
        row["Target Bond Value"] = PathDictionary[EvalDates[i]].TargetBondValue;
        row["CashIn Barrier"] = PathDictionary[EvalDates[i]].CashInBarrierValue;
        row["Fee Forward Pv"] = PathDictionary[EvalDates[i]].FeeForwardPv;
        row["Shortfall"] = PathDictionary[EvalDates[i]].Shortfall;
        row["Target Notional"] = PathDictionary[EvalDates[i]].TargetNotional;
        row["Max Notional"] = Cpdo.MaxLeverage * Cpdo.Notional;
        row["Leverage"] = PathDictionary[EvalDates[i]].Leverage;

        pathDataTable.Rows.Add(row);
      }
      return pathDataTable;
    }

    /// <summary>
    ///   Return Path Pv
    /// </summary>
    ///
    /// <returns>Path Pv</returns>
    public double GetPathPv()
    {

      // Initialize Tracked Values (PathStorage values)
      CPDOMCPathStorage pathStorage = new CPDOMCPathStorage();

      // construct CDS survival curve, product and pricer for MTM, and other calculations.
      // -----------------------------------------------------------------------------------------

      // construct flat hazard rate survival curve
      SurvivalFitCalibrator fit = new SurvivalFitCalibrator(AsOf, AsOf, Recovery, DiscountCurve);
      SurvivalCurve flatSurvivalCurve = new SurvivalCurve(fit);
      flatSurvivalCurve.AddCDS("EvalDtToMaturity", Dt.CDSMaturity(AsOf, "5 Years"), Spreads[0] / 10000.0,
                      Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);
      flatSurvivalCurve.Fit();

      double riskyDuration = CurveUtil.ImpliedDuration(flatSurvivalCurve, AsOf, Cpdo.Maturity, Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);

      // needed for Carry Calculations
      double[] survProbDeltaT = new double[EvalDates.Count];
      survProbDeltaT[0] = 1 - flatSurvivalCurve.DefaultProb(EvalDates[1]);

      // construct CDS product and Pricer
      CDS globoxxCds = new CDS(AsOf, Dt.CDSMaturity(AsOf, "5 Year"), Cpdo.Ccy, Cpdo.Premium / 10000.0,
                   Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);

      CDSCashflowPricer globoxxCdsPricer = new CDSCashflowPricer(globoxxCds, AsOf, AsOf, DiscountCurve, flatSurvivalCurve,
                                         null, 0, 0, TimeUnit.None);
      globoxxCdsPricer.Notional = Cpdo.Notional * Cpdo.InitialLeverage;
      //------------------------------------------------------------------------------------------


      // 1. targetBondValues and floatingBondAccruals are path independent (assuming static IR curve)

      pathStorage.TargetBondValue = FloatingBondForwardValues[0];
      pathStorage.TargetBondPrice = FloatingBondForwardFullPrices[0];
      pathStorage.CashInBarrierValue = CashInBarrierForwardValues[0];
      pathStorage.CashInBarrierPrice = CashInBarrierForwardFullPrices[0];

      pathStorage.FeeForwardPv = pathStorage.CashInBarrierValue - pathStorage.TargetBondValue;

      // 2. Globoxx Fwd Spreads
      pathStorage.GloboxxCdsFwdFairSpread = Cpdo.Premium;// cpdo (simulated) forward spreads
      pathStorage.GloboxxCdsFwdPremium = Cpdo.Premium;// forward Globoxx CDS Premium

      // 3. Globoxx Fwd Pv
      pathStorage.GloboxxCdsFwdMTM = globoxxCdsPricer.ProductPv() - globoxxCdsPricer.Accrued();

      // 4. Globboxx MTM changes (over each eval period)
      pathStorage.GloboxxMTMDelta = 0;// keep track of MTM changes over each period

      // 5. Globboxx Carry  (over each eval period
      pathStorage.GloboxxCarry = 0;// keep track of MTM changes over each period

      // 6. Cash Acount - Roll(Bid-Ask) Costs (whenever you're buying/selling the index  
      pathStorage.CashAccount = Cpdo.Notional;
      pathStorage.CashAccount -= Math.Abs(UpDeltas[(int)Cpdo.Premium]) * globoxxCdsPricer.Notional;

      // 7. Nav  
      pathStorage.Nav = pathStorage.CashAccount;

      // 8. Shortfall  
      pathStorage.Shortfall = pathStorage.CashInBarrierValue * (1 + Cushion) - pathStorage.Nav;

      // 9. Leverage  
      pathStorage.Leverage = Cpdo.InitialLeverage;

      // 10. Target Notional  
      pathStorage.TargetNotional = pathStorage.Leverage * Cpdo.Notional;

      // 11. Globox Notional 
      pathStorage.GloboxxNotional = Math.Min(pathStorage.TargetNotional, Cpdo.Notional * Cpdo.MaxLeverage);

      // 12. Floating Coupons Pv
      pathStorage.FloatingCouponPv = 0;

      // 13. Fee Pv
      pathStorage.Fee = 0;

      // 14. CpdoPv
      pathStorage.CpdoPathFullPrice = FloatingBondForwardFullPrices[0];
      pathStorage.CpdoPathValue = FloatingBondForwardValues[0];


      PathDictionary.Add(EvalDates[0], pathStorage);
      Dt cdsEffectiveDate = Cpdo.Effective;
      IEnumerator rollDtEnum = RollDates.GetEnumerator(); rollDtEnum.MoveNext();
      IEnumerator floatingBondCpnDateEnum = FloatingBondDates.GetEnumerator();
      floatingBondCpnDateEnum.MoveNext(); floatingBondCpnDateEnum.MoveNext();
      IEnumerator floatingBondAccrualEnum = FloatingBondAccruals.GetEnumerator();
      floatingBondAccrualEnum.MoveNext(); floatingBondAccrualEnum.MoveNext();


      for (int i = 1; i < EvalDates.Count; ++i)
      {
        // initialize path variables

        Dt previousEvalDate = EvalDates[i - 1];
        Dt currentEvalDate = EvalDates[i];
        CPDOMCPathStorage previousPathStorage = PathDictionary[previousEvalDate];
        CPDOMCPathStorage currentPathStorage = new CPDOMCPathStorage();
        Dt rollDate = (Dt)(rollDtEnum.Current);
        // Dt previousRollDt
        Dt floatingBondCpnDate = (Dt)floatingBondCpnDateEnum.Current;
        double floatingBondAccrual = (double)floatingBondAccrualEnum.Current;


        //0. Path Discount Factor 
        double df = DiscountCurve.DiscountFactor(currentEvalDate);

        //1. Target Bond Values (Cash-In Barrier)
        currentPathStorage.TargetBondValue = FloatingBondForwardValues[i];
        currentPathStorage.CashInBarrierValue = CashInBarrierForwardValues[i];
        currentPathStorage.FeeForwardPv = currentPathStorage.CashInBarrierValue - currentPathStorage.TargetBondValue;

        //2. Fair Fwd Premiums: To be used in Carry Calculations (constant between coupon periods)
        //   in fact constant between rebalancing periods

        if (currentEvalDate == rollDate)
        {
          currentPathStorage.GloboxxCdsFwdPremium = Spreads[i];// forward Globoxx CDS Premium   
          cdsEffectiveDate = rollDate;
        }
        else
        {
          currentPathStorage.GloboxxCdsFwdPremium = previousPathStorage.GloboxxCdsFwdPremium;
        }

        // Globbox Fair Fws spreads
        currentPathStorage.GloboxxCdsFwdFairSpread = Spreads[i];

        //3-5. Calculate Forward MTMs (PVs), FeePv's,ProtectionPv's, MTM's changes
        Dt forwardSettle = currentEvalDate;

        // construct CDS product and pricer for MTM, FeePv, ProtPv calculations.
        // -----------------------------------------------------------------------------------------

        // construct flat hazard rate survival curve with cpdo maturity

        fit = new SurvivalFitCalibrator(forwardSettle, forwardSettle, Recovery, DiscountCurve);
        flatSurvivalCurve.Clear();
        flatSurvivalCurve = new SurvivalCurve(fit);

        flatSurvivalCurve.AddCDS("EvalDtToMaturity", Dt.CDSMaturity(forwardSettle, "5 Year"), Spreads[i] / 10000,
                            Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);
        flatSurvivalCurve.Fit();

        // construct CDS product and Pricer: Needed for MTM calculations
        globoxxCds = new CDS(cdsEffectiveDate, Dt.CDSMaturity(cdsEffectiveDate, "5 Years"), Cpdo.Ccy, currentPathStorage.GloboxxCdsFwdPremium / 10000.0,
                     Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);

        globoxxCdsPricer = new CDSCashflowPricer(globoxxCds, forwardSettle, forwardSettle, DiscountCurve, flatSurvivalCurve,
                                           0, TimeUnit.None);
        globoxxCdsPricer.Notional = previousPathStorage.GloboxxNotional;


        if (currentEvalDate != Cpdo.Maturity)
        {
          riskyDuration = CurveUtil.ImpliedDuration(flatSurvivalCurve, forwardSettle, Cpdo.Maturity, Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);
          currentPathStorage.GloboxxCdsFwdMTM = globoxxCdsPricer.ProductPv() - globoxxCdsPricer.Accrued();
          currentPathStorage.GloboxxMTMDelta = currentPathStorage.GloboxxCdsFwdMTM - previousPathStorage.GloboxxCdsFwdMTM;
        }
        else
        {
          currentPathStorage.GloboxxCdsFwdMTM = previousPathStorage.GloboxxCdsFwdMTM;
          currentPathStorage.GloboxxMTMDelta = currentPathStorage.GloboxxCdsFwdMTM - previousPathStorage.GloboxxCdsFwdMTM;
        }

        // 5. Calculate Globboxx Carry  
        double deltaT = Dt.Fraction(previousEvalDate, currentEvalDate, Cpdo.DayCount);
        currentPathStorage.GloboxxCarry = previousPathStorage.GloboxxNotional * deltaT * LossFactor * previousPathStorage.GloboxxCdsFwdFairSpread / 10000.0;//*deltaT  - defaults*deltaT!!!!
        if ((i < EvalDates.Count - 1) && (currentEvalDate == rollDate))
          survProbDeltaT[i] = (1 - flatSurvivalCurve.DefaultProb(EvalDates[i + 1]));
        else if (i < EvalDates.Count - 1)
          survProbDeltaT[i] = survProbDeltaT[i - 1] * (1 - flatSurvivalCurve.DefaultProb(EvalDates[i + 1]));
        else
          survProbDeltaT[i] = 1; // over last period [T-1, T]

        // 6. Cash Acount : Previous cashAcccount value + accrued on cash + carry  + MTMDelta 
        double interestOnCash = DiscountCurve.F(previousEvalDate, currentEvalDate, DayCount.Actual365Fixed, Frequency.Continuous);
        double accrualOnCashAccount = deltaT * interestOnCash;
        currentPathStorage.CashAccount = previousPathStorage.CashAccount * (1 + accrualOnCashAccount) + currentPathStorage.GloboxxCarry;

        // subtract gap fee
        currentPathStorage.CashAccount -= deltaT * Cpdo.GapFee * globoxxCdsPricer.Notional;

        // subtract admin fee
        currentPathStorage.CashAccount -= deltaT * Cpdo.AdminFee * Cpdo.Notional;
        currentPathStorage.Fee = deltaT * Cpdo.GapFee * globoxxCdsPricer.Notional
                               + deltaT * Cpdo.AdminFee * Cpdo.Notional;

        if (currentEvalDate == floatingBondCpnDate)
        {
          // subtract (bond) coupon
          currentPathStorage.CashAccount -= floatingBondAccrual * Cpdo.Notional;

          // need to keep track of discounted floating coupons;
          currentPathStorage.FloatingCouponPv = previousPathStorage.FloatingCouponPv + floatingBondAccrual * Cpdo.Notional * df;
        }
        else
          currentPathStorage.FloatingCouponPv = previousPathStorage.FloatingCouponPv;


        // 7. Nav : globoxxMTM + Carry + Cash Account 
        currentPathStorage.Nav = currentPathStorage.GloboxxCdsFwdMTM + currentPathStorage.GloboxxCarry + currentPathStorage.CashAccount;


        // 8. Shortfall  
        currentPathStorage.Shortfall = currentPathStorage.CashInBarrierValue * (1 + Cushion) - currentPathStorage.Nav;

        // 9. Target Notional  (only if rebalancing conditions are met !!!!! )
        if (currentEvalDate != Cpdo.Maturity)
          currentPathStorage.TargetNotional = (currentPathStorage.Shortfall / (riskyDuration * (currentPathStorage.GloboxxCdsFwdFairSpread / 10000) * Cpdo.Notional * LossFactor)) * Cpdo.Notional;
        else
          currentPathStorage.TargetNotional = previousPathStorage.TargetNotional;

        if (currentEvalDate == rollDate)
        {
          if (currentEvalDate != Cpdo.Maturity)
          {
            currentPathStorage.GloboxxNotional = Math.Min(currentPathStorage.TargetNotional, Cpdo.MaxLeverage * Cpdo.Notional);
            double newNotional = currentPathStorage.GloboxxNotional;

            double buyProtectionCost = Math.Abs(UpDeltas[(int)Spreads[i]]) * globoxxCdsPricer.Notional;
            currentPathStorage.CashAccount -= buyProtectionCost;
            currentPathStorage.Nav -= buyProtectionCost;
            currentPathStorage.Fee += buyProtectionCost;

            globoxxCdsPricer.Notional = newNotional;
            double sellProtectionCost = buyProtectionCost;
            currentPathStorage.CashAccount -= sellProtectionCost;
            currentPathStorage.Nav -= sellProtectionCost;
            currentPathStorage.Fee += sellProtectionCost;

          }
          else
          {

            currentPathStorage.GloboxxNotional = previousPathStorage.GloboxxNotional;
            // adjust nav for bid cost (you're unwinding your exposure at maturity)

            double buyProtectionCost = Math.Abs(UpDeltas[(int)Spreads[i]]) * globoxxCdsPricer.Notional;
            currentPathStorage.CashAccount -= buyProtectionCost;
            currentPathStorage.Nav -= buyProtectionCost;
            currentPathStorage.Fee += buyProtectionCost;
          }
        }
        // no roll date, but rebalancing trigger hit
        else if ((currentEvalDate != Cpdo.Maturity) && (currentPathStorage.TargetNotional - globoxxCdsPricer.Notional) / globoxxCdsPricer.Notional >= Cpdo.RebalTarget
                  && currentPathStorage.TargetNotional <= Cpdo.MaxLeverage * Cpdo.Notional)
        {
          double offsetNotional = (Math.Min(currentPathStorage.TargetNotional, Cpdo.MaxLeverage * Cpdo.Notional) - globoxxCdsPricer.Notional);
          // adjust nav for bid-ask spread (roll cost)
          double oldNotional = globoxxCdsPricer.Notional;
          double newNotional = oldNotional + offsetNotional;

          // calculate transaction cost on the offsetNotional
          if (offsetNotional > 0)
          {

            globoxxCdsPricer.Notional = offsetNotional;
            double sellProtectionCost = Math.Abs(UpDeltas[(int)Spreads[i]]) * globoxxCdsPricer.Notional; ;
            currentPathStorage.CashAccount -= sellProtectionCost;
            currentPathStorage.Nav -= sellProtectionCost;
            currentPathStorage.Fee += sellProtectionCost;

          }
          else
          {
            globoxxCdsPricer.Notional = offsetNotional;
            double buyProtectionCost = Math.Abs(UpDeltas[(int)Spreads[i]]) * globoxxCdsPricer.Notional; ;
            currentPathStorage.CashAccount -= buyProtectionCost;
            currentPathStorage.Nav -= buyProtectionCost;
            currentPathStorage.Fee += Math.Abs(buyProtectionCost);
          }

          globoxxCdsPricer.Notional = newNotional;
          currentPathStorage.GloboxxNotional = newNotional;
        }
        else
        {
          currentPathStorage.GloboxxNotional = previousPathStorage.GloboxxNotional;
        }

        // 11. Leverage  
        currentPathStorage.Leverage = currentPathStorage.GloboxxNotional / Cpdo.Notional;

        //-----------------------------------------------------------------------
        //                              Cash-In condition
        //-----------------------------------------------------------------------
        if (currentPathStorage.Nav >= currentPathStorage.CashInBarrierValue)
        {
          currentPathStorage.CpdoPathValue = FloatingBondForwardValues[0]; // initial bond value
          currentPathStorage.Nav = currentPathStorage.CashInBarrierValue; // initial bond value                    
          PathDictionary.Add(currentEvalDate, currentPathStorage);
          break;
        }

        //-----------------------------------------------------------------------
        //                              Cash-Out condition
        //-----------------------------------------------------------------------
        if (currentPathStorage.Nav < Cpdo.Notional * Cpdo.CashOutTarget)
        {
          currentPathStorage.CpdoPathValue = currentPathStorage.FloatingCouponPv + currentPathStorage.Nav * df;
          PathDictionary.Add(currentEvalDate, currentPathStorage);
          break;
        }

        // update path value
        currentPathStorage.CpdoPathValue = currentPathStorage.FloatingCouponPv + currentPathStorage.Nav * df;


        // update roll dates, coupon dates path storage for current eval date
        PathDictionary.Add(currentEvalDate, currentPathStorage);

        if (currentEvalDate == rollDate)
          rollDtEnum.MoveNext();

        if (currentEvalDate == floatingBondCpnDate)
        {
          floatingBondCpnDateEnum.MoveNext();
          floatingBondAccrualEnum.MoveNext();
        }

      }

      int dictCount = PathDictionary.Count;
      CPDOMCPathStorage lastPathStorage = PathDictionary[EvalDates[dictCount - 1]];
      double nav = lastPathStorage.Nav;
      bool cashOut = (lastPathStorage.Nav < Cpdo.Notional * Cpdo.CashOutTarget);

      // fill in path table to maturity
      for (int i = dictCount; i < EvalDates.Count; ++i)
      {
        // initialize path variables
        Dt currentEvalDate = EvalDates[i];
        CPDOMCPathStorage previousPathStorage = PathDictionary[EvalDates[dictCount - 1]];
        CPDOMCPathStorage currentPathStorage = new CPDOMCPathStorage();

        currentPathStorage.GloboxxNotional = 0;
        currentPathStorage.GloboxxCdsFwdFairSpread = 0;
        currentPathStorage.GloboxxCdsFwdMTM = 0;
        currentPathStorage.GloboxxCarry = 0;
        currentPathStorage.CashAccount = 0;
        currentPathStorage.Fee = 0;
        if (cashOut)
          currentPathStorage.Nav = nav;
        else
          currentPathStorage.Nav = previousPathStorage.CashInBarrierValue;

        currentPathStorage.TargetBondValue = FloatingBondForwardValues[i];
        currentPathStorage.CashInBarrierValue = CashInBarrierForwardValues[i];
        currentPathStorage.Nav = CashInBarrierForwardValues[i];
        currentPathStorage.FeeForwardPv = 0;
        currentPathStorage.Shortfall = 0;
        currentPathStorage.TargetNotional = 0;
        currentPathStorage.Leverage = 0;
        currentPathStorage.GloboxxMTMDelta = 0;

        PathDictionary.Add(currentEvalDate, currentPathStorage);
      }
      return Math.Min(lastPathStorage.CpdoPathValue, FloatingBondForwardValues[0]);
    }

    #endregion

    #region Properties

    /// <summary>
    ///  As of Date
    /// </summary>
    public Dt AsOf
    {
      get
      {
        return asOf_;
      }
    }

    /// <summary>
    ///  Settle Date
    /// </summary>
    public Dt Settle
    {
      get
      {
        return settle_;
      }
    }


    /// <summary>
    ///  Cpdo product
    /// </summary>
    public CPDO Cpdo
    {
      get
      {
        return cpdo_;
      }
    }

    /// <summary>
    ///  Cpdo Bond Path 
    /// </summary>
    public CPDOBondPath CpdoBondPath
    {
      get
      {
        return cpdoBondPath_;
      }
    }

    /// <summary>
    ///  Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get
      {
        return discountCurve_;
      }
    }

    /// <summary>
    ///  Reference curve
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get
      {
        return referenceCurve_;
      }
    }

    /// <summary>
    ///  Spreads Curve 
    /// </summary>
    public Curve SpreadCurve
    {
      get
      {
        return spreadCurve_;
      }
    }

    /// <summary>
    ///  Recoveries
    /// </summary>
    public double Recovery
    {
      get
      {
        return recovery_;
      }
    }

    /// <summary>
    ///  Shortfall cushion
    /// </summary>
    public double Cushion
    {
      get
      {
        return cushion_;
      }
    }

    /// <summary>
    ///  Loss Factor
    /// </summary>
    public double LossFactor
    {
      get
      {
        return lossFactor_;
      }
    }

    /// <summary>
    ///  Last Reset Floating Rate Bond
    /// </summary>
    public double LastResetFloatingBond
    {
      get
      {
        return lastResetFloatingBond_;
      }
    }

    /// <summary>
    ///  Roll Cost
    /// </summary>
    public double RollCost
    {
      get
      {
        return rollCost_;
      }
    }


    /// <summary>
    ///  List of floating Bond Coupon dates
    /// </summary>
    public List<Dt> FloatingBondDates
    {
      get
      {
        return floatingBondDates_;
      }
    }

    /// <summary>
    ///  Globoxx roll dates
    /// </summary>
    public List<Dt> RollDates
    {
      get
      {
        return rollDates_;
      }
    }

    /// <summary>
    ///  Globoxx eval dates
    /// </summary>
    public List<Dt> EvalDates
    {
      get
      {
        return evalDates_;
      }
    }

    /// <summary>
    ///  Floating Bonds Accruals
    /// </summary>
    public List<double> FloatingBondAccruals
    {
      get
      {
        return floatingBondAccruals_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Values
    /// </summary>
    public List<double> FloatingBondForwardValues
    {
      get
      {
        return floatingBondForwardValues_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Full Prices
    /// </summary>
    public List<double> FloatingBondForwardFullPrices
    {
      get
      {
        return floatingBondForwardFullPrices_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Full Prices + Admin Fees = Cash In Barrier
    /// </summary>
    public List<double> CashInBarrierForwardFullPrices
    {
      get
      {
        return cashInBarrierForwardFullPrices_;
      }
    }

    /// <summary>
    ///  Floating Bonds Forward Values
    /// </summary>
    public List<double> CashInBarrierForwardValues
    {
      get
      {
        return cashInBarrierForwardValues_;
      }
    }

    /// <summary>
    ///  Cpdo Spreads
    /// </summary>
    public List<double> Spreads
    {
      get
      {
        return spreads_;
      }
    }

    /// <summary>
    ///  Saved Delta's (upbump)
    /// </summary>
    public double[] UpDeltas
    {
      get
      {
        return upDeltas_;
      }
    }

    /// <summary>
    ///  Roll Down Cost
    /// </summary>
    public double RollDown
    {
      get
      {
        return rollDown_;
      }
    }

    /// <summary>
    ///  Composition Change
    /// </summary>
    public double CompChange
    {
      get
      {
        return compChange_;
      }
    }

    /// <summary>
    ///  Evaluation-grid frequency
    /// </summary>
    public Frequency Frequency
    {
      get
      {
        return frequency_;
      }
    }

    /// <summary>
    ///  Path Dictionary
    /// </summary>
    public Dictionary<Dt, CPDOMCPathStorage> PathDictionary
    {
      get { return pathDictionary_; }
      set { pathDictionary_ = value; }
    }

    #endregion

    #region Data

    private CPDO cpdo_;
    private CPDOBondPath cpdoBondPath_;
    private double recovery_;
    private Dt asOf_;
    private Dt settle_;
    private Frequency frequency_;
    private double lastResetFloatingBond_;
    private double cushion_;
    private double lossFactor_;
    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private Curve spreadCurve_;

    // Fees
    private double rollCost_; //bps

    // Spread adjustments at roll dates
    private double compChange_;
    private double rollDown_;

    // Path storage 
    private Dictionary<Dt, CPDOMCPathStorage> pathDictionary_;
    private List<Dt> floatingBondDates_;
    private List<Dt> rollDates_;
    private List<Dt> evalDates_;
    private List<double> floatingBondAccruals_;
    private List<double> floatingBondForwardValues_;
    private List<double> floatingBondForwardFullPrices_;
    private List<double> spreads_;

    private List<double> cashInBarrierForwardFullPrices_;
    private List<double> cashInBarrierForwardValues_;
    private double[] upDeltas_;

    #endregion //Data

  } // class CPDOPath

  ///
  /// <summary>
  ///  CPDO Path Storage 
  /// </summary>
  ///
  ///
  public class CPDOMCPathStorage
  {
    #region Constructors

    /// <summary>
    ///   Default Constructor
    /// </summary>
    public CPDOMCPathStorage()
    {

      targetBondValue_ = 0;
      targetBondPrice_ = 0;
      cashInBarrierValue_ = 0;
      cashInBarrierPrice_ = 0;
      globoxxCdsFwdPremium_ = 0;
      globoxxCdsFwdFairSpread_ = 0;// cpdoSpreads
      globoxxCdsFwdMTM_ = 0;
      globoxxMTMDelta_ = 0;
      globoxxCarry_ = 0;
      cashAccount_ = 0;
      nav_ = 0;
      shortfall_ = 0;
      leverage_ = 0;
      targetNotional_ = 0;
      globoxxNotional_ = 0;
      cpdoPathValue_ = 0;
      cpdoPathFullPrice_ = 0;

    }

    /// <summary>
    ///   Constructor for a Cpdo Path Storage (structure)
    /// </summary>
    ///
    /// <param name="targetBondValue">Target Bond value (Cash-In value barrier) </param>
    /// <param name="targetBondPrice">Target Bond price (Cash-In Price barrier)</param>
    /// <param name="cashInBarrierValue">Target Bond value (Cash-In value barrier) </param>
    /// <param name="cashInBarrierPrice">Target Bond price (Cash-In Price barrier)</param>        
    /// <param name="globoxxCdsFwdPremium">Globoxx forward 5Y premium</param>
    /// <param name="globoxxCdsFwdFairSpread">Globoxx forward 5Y spread</param>
    /// <param name="globoxxCdsFwdMTM">Globoxx Forward Pv </param>
    /// <param name="globoxxMTMDelta">Globoxx forward Delta</param>
    /// <param name="globoxxCarry">Globoxx forward Carry</param>
    /// <param name="cashAccount">Forward value of cash account</param>
    /// <param name="nav">Forward Nav (net asset value) </param>
    /// <param name="shortfall">Forward Shortfall</param>
    /// <param name="leverage">Currency of premium and recovery payments</param>
    /// <param name="targetNotional">Forward target notional</param>
    /// <param name="globoxxNotional">Forward Globoxx Notional </param>
    /// <param name="cpdoPathValue">Forward Cpdo Path Value</param>
    /// <param name="cpdoPathFullPrice">Forward Cpdo Full Price</param>
    ///
    public CPDOMCPathStorage(double targetBondValue, double targetBondPrice,
                           double cashInBarrierValue, double cashInBarrierPrice,
                           double globoxxCdsFwdPremium,
                           double globoxxCdsFwdFairSpread, double globoxxCdsFwdMTM, double globoxxMTMDelta,
                           double globoxxCarry, double cashAccount, double nav, double shortfall, double leverage,
                           double targetNotional, double globoxxNotional, double cpdoPathValue, double cpdoPathFullPrice)
    {

      targetBondValue_ = targetBondValue;
      targetBondPrice_ = targetBondPrice;
      cashInBarrierValue_ = cashInBarrierValue;
      cashInBarrierPrice_ = cashInBarrierPrice;

      globoxxCdsFwdPremium_ = globoxxCdsFwdPremium;
      globoxxCdsFwdFairSpread_ = globoxxCdsFwdFairSpread;// cpdoSpreads
      globoxxCdsFwdMTM_ = globoxxCdsFwdMTM;
      globoxxMTMDelta_ = globoxxMTMDelta;
      globoxxCarry_ = globoxxCarry;
      cashAccount_ = cashAccount;
      nav_ = nav;
      shortfall_ = shortfall;
      leverage_ = leverage;
      targetNotional_ = targetNotional;
      globoxxNotional_ = globoxxNotional;
      cpdoPathValue_ = cpdoPathValue;
      cpdoPathFullPrice_ = cpdoPathFullPrice;

    }


    #endregion

    #region Methods

    #endregion

    #region Properties

    /// <summary>
    ///   Target Bond Value (takes notional into account).
    /// </summary>
    ///
    public double TargetBondValue
    {
      get { return targetBondValue_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be < 0, Not {0}", value));
        targetBondValue_ = value;
      }
    }

    /// <summary>
    ///   Target Bond Price (par price = 100).
    /// </summary>
    ///
    public double TargetBondPrice
    {
      get { return targetBondPrice_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        targetBondPrice_ = value;
      }
    }

    /// <summary>
    ///   CashInBarrier Value (takes notional into account).
    /// </summary>
    ///
    public double CashInBarrierValue
    {
      get { return cashInBarrierValue_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be < 0, Not {0}", value));
        cashInBarrierValue_ = value;
      }
    }

    /// <summary>
    ///   CashInBarrier Price (par price = 100).
    /// </summary>
    ///
    public double CashInBarrierPrice
    {
      get { return cashInBarrierPrice_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        cashInBarrierPrice_ = value;
      }
    }

    /// <summary>
    ///   Forward CDS Premium of 5Y (Leveraged) Index.
    /// </summary>
    ///
    public double GloboxxCdsFwdPremium
    {
      get { return globoxxCdsFwdPremium_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        globoxxCdsFwdPremium_ = value;
      }
    }

    /// <summary>
    ///   Forward CDS (fair) Spread of 5Y (Leveraged) Index.
    /// </summary>
    ///
    public double GloboxxCdsFwdFairSpread
    {
      get { return globoxxCdsFwdFairSpread_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        globoxxCdsFwdFairSpread_ = value;
      }
    }

    /// <summary>
    ///   Forward Pv of 5Y (Leveraged) Index.
    /// </summary>
    ///
    public double GloboxxCdsFwdMTM
    {
      get { return globoxxCdsFwdMTM_; }
      set
      {
        globoxxCdsFwdMTM_ = value;
      }
    }

    /// <summary>
    ///   Forward Pv Delta of 5Y (Leveraged) Index. (between t-1 and t, 2 consecutive eval dates)
    /// </summary>
    ///
    public double GloboxxMTMDelta
    {
      get { return globoxxMTMDelta_; }
      set
      {
        globoxxMTMDelta_ = value;
      }
    }

    /// <summary>
    ///   Forward Carry of 5Y (Leveraged) Index. (between t-1 and t, 2 consecutive eval dates)
    /// </summary>
    ///
    public double GloboxxCarry
    {
      get { return globoxxCarry_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        globoxxCarry_ = value;
      }
    }

    /// <summary>
    ///   Forward Cash Account value
    /// </summary>
    ///
    public double CashAccount
    {
      get { return cashAccount_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        cashAccount_ = value;
      }
    }

    /// <summary>
    ///   Forward Nav (net asset value)
    /// </summary>
    ///
    public double Nav
    {
      get { return nav_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        nav_ = value;
      }
    }

    /// <summary>
    ///   Forward shortfall        
    /// </summary>
    ///
    public double Shortfall
    {
      get { return shortfall_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        shortfall_ = value;
      }
    }

    /// <summary>
    ///   Forward Leverage        
    /// </summary>
    ///
    public double Leverage
    {
      get { return leverage_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        leverage_ = value;
      }
    }

    /// <summary>
    ///   Forward Target Notional        
    /// </summary>
    ///
    public double TargetNotional
    {
      get { return targetNotional_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        targetNotional_ = value;
      }
    }

    /// <summary>
    ///   Forward Globoxx Notional        
    /// </summary>
    ///
    public double GloboxxNotional
    {
      get { return globoxxNotional_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        globoxxNotional_ = value;
      }
    }

    /// <summary>
    ///  Coupons Pv
    /// </summary>
    public double FloatingCouponPv
    {
      get { return floatingCouponPv_; }
      set
      {
        floatingCouponPv_ = value;
      }

    }

    /// <summary>
    ///  Fee Pv
    /// </summary>
    public double Fee
    {
      get { return fee_; }
      set
      {
        fee_ = value;
      }
    }

    /// <summary>
    ///  Fee Forward Pv
    /// </summary>
    public double FeeForwardPv
    {
      get { return feeForwardPv_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        feeForwardPv_ = value;
      }
    }

    /// <summary>
    ///   Forward Cpdo Path Value        
    /// </summary>
    ///
    public double CpdoPathValue
    {
      get { return cpdoPathValue_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        cpdoPathValue_ = value;
      }
    }

    /// <summary>
    ///   Forward Cpdo Path Full price        
    /// </summary>
    ///
    public double CpdoPathFullPrice
    {
      get { return cpdoPathFullPrice_; }
      set
      {
        if (value < 0.0)
          throw new ArgumentException(String.Format("Invalid premium. Must be between 0, Not {0}", value));
        cpdoPathFullPrice_ = value;
      }
    }

    #endregion

    #region Data

    private double targetBondValue_;
    private double targetBondPrice_;
    private double cashInBarrierValue_;
    private double cashInBarrierPrice_;

    private double globoxxCdsFwdPremium_;
    private double globoxxCdsFwdFairSpread_;// cpdoSpreads
    private double globoxxCdsFwdMTM_;
    private double globoxxMTMDelta_;
    private double globoxxCarry_;
    private double cashAccount_;
    private double nav_;
    private double shortfall_;
    private double leverage_;
    private double targetNotional_;
    private double globoxxNotional_;
    private double cpdoPathValue_;
    private double cpdoPathFullPrice_;

    private double floatingCouponPv_;
    private double feeForwardPv_;
    private double fee_;


    #endregion //Data

  } // class CPDOPathStorage
}
