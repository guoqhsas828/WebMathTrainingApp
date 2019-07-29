//
// DiscountCurve.cs
//  -2014. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   A term structure of interest rates
  /// </summary>
  /// <remarks>
  ///   <para>Contains a term structure of interest rates. The interface is in terms
  ///   of discount factors (riskless zero coupon bond prices).</para>
  ///   <para>Discount curves can be created by directly specifying discount rates
  ///   or by calibrating to market data. Calibration is performed by
  ///   <see cref="DiscountCalibrator"/>s</para>
  /// </remarks>
  [Serializable]
  public class DiscountCurve : CalibratedCurve
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>Interpolation defaults to flat continuously compounded forward rates.
    ///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    ///   Compounding frequency is Continuous. Settlement defaults to </para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    public
    DiscountCurve(Dt asOf)
      : base(asOf)
    { }

    /// <summary>
    ///   Constructor for a flat forward rate curve
    /// </summary>
    /// <remarks>
    ///   <para>Constructs a simple discount curve based on a constant hazard
    ///   rate.</para>
    ///   <para>Settlement defaults to asOf date</para>
    ///   <para>Interpolation defaults to flat continuously compounded forward rates.
    ///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    ///   Compounding frequency is Continuous.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="forwardRate">Single continuously compounded forward rate</param>
    /// <example>
    /// <code language="C#">
    ///   // Pricing is as of today.
    ///   Dt today = Dt.today();
    ///   // Constant forward rate is 4 percent.
    ///   double forwardRate = 0.04;
    ///   // Construct the discount curve using a constant forward rate.
    ///   DiscountCurve discountCurve = new DiscountCurve( today, forwardRate );
    /// </code>
    /// </example>
    public
    DiscountCurve(Dt asOf, double forwardRate)
      : base(asOf, forwardRate)
    { }


    /// <summary>
    ///   Constructor given calibrator
    /// </summary>
    /// <remarks>
    ///   <para>Interpolation defaults to flat continuously compounded forward rates.
    ///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    ///   Compounding frequency is Continuous.</para>
    /// </remarks>
    /// <param name="calibrator">Calibrator</param>
    /// <example>
    /// <code language="C#">
    ///   // Pricing is as of today and settlement is tomorrow.
    ///   Dt today = Dt.today();
    ///   Dt settle = Dt.add(today, 1);
    ///   // Set up the swap tenor points and quotes.
    ///   string [] swapTenors = new string [] { "1 Year", "5 Year", "7 Year", "10 year" };
    ///   double [] swapRates = new double [] { 0.04, 0.043, 0.045, 0.05 };
    ///
    ///   // Construct the discount curve
    ///   DiscountBootstrapCalibrator fit = new DiscountBootstrapCalibrator( today, settle );
    ///   DiscountCurve discountCurve = new DiscountCurve( fit );
    ///
    ///   // Add the swap tenor points
    ///   for( int i = 0; i &lt; swapTenors.Length; i++ )
    ///   {
    ///     discountCurve.AddSwap( swapTenors[i], Dt.add(today, swapTenors[i]), swapRates[i],
    ///                         DayCount.Actual360, Frequency.SemiAnnual, BDConvention.Modified,
    ///                         Calendar.NYB );
    ///   }
    ///
    ///   // Fit the discount curve
    ///   discountCurve.Fit();
    /// </code>
    /// </example>
    public
    DiscountCurve(Calibrator calibrator)
      : base(calibrator)
    { }


    /// <summary>
    ///   Constructor given calibrator and interpolation
    /// </summary>
    /// <param name="calibrator">Calibrator</param>
    /// <param name="interp">Interpolation method</param>
    /// <param name="dc">Daycount for interpolation</param>
    /// <param name="freq">Compounding frequency for interpolation</param>
    public
    DiscountCurve(Calibrator calibrator, Interp interp, DayCount dc, Frequency freq)
      : base(calibrator, interp, dc, freq)
    { }


    /// <summary>
    /// Constructor of a standard overlay curve
    /// </summary>
    /// <param name="calibrator">Calibrator</param>
    /// <param name="overlay">Overlay curve</param>
    public DiscountCurve(Calibrator calibrator, Curve overlay)
      : base(calibrator.AsOf, Frequency.Continuous, overlay)
    {
      Calibrator = calibrator;
    }

    /// <summary>
    /// Constructor of parametric discount curve
    /// </summary>
    /// <param name="asOf">Asof date</param>
    ///<param name="fn">Parametric form</param>
    public DiscountCurve(Dt asOf, ParametricCurveFn fn)
      : this(asOf)
    {
      Initialize(asOf, fn);
      Add(asOf, 1.0);
    }

    /// <summary>
    /// Constructor of parametric discount curve
    /// </summary>
    /// <param name="calibrator">Calibrator</param>
    ///<param name="fn">Parametric form</param>
    ///<param name="overlay">Overlay curve</param>
    public DiscountCurve(Calibrator calibrator, ParametricCurveFn fn, Curve overlay)
      : base(calibrator.AsOf, Frequency.Continuous, overlay)
    {
      Initialize(calibrator.AsOf, fn);
      Calibrator = calibrator;
      Add(calibrator.AsOf, 1.0);
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Get discount factor from as-of date to given date
    /// </summary>
    ///
    /// <param name="date">Date to interpolate for</param>
    ///
    /// <returns>Discount factor matching date</returns>
    ///
    public double
    DiscountFactor(Dt date)
    {
      return Interpolate(date);
    }

    /// <summary>
    ///   Get discount factor give date
    /// </summary>
    ///
    /// <param name="start">Date to interpolate from</param>
    /// <param name="end">Date to interpolate to</param>
    ///
    /// <returns>Discount factor matching date</returns>
    ///
    public double
    DiscountFactor(Dt start, Dt end)
    {
      return Interpolate(start, end);
    }

    /// <summary>
    ///  Add money market rate to calibration
    /// </summary>
    /// <remarks>
    ///   <para>The money market rates are quoted in terms of simple yields.</para>
    /// </remarks>
    /// <param name="description">Description of money market tenor or null to use maturity date</param>
    /// <param name="maturity">Maturity date of money market rate</param>
    /// <param name="yield">Simple yield</param>
    /// <param name="dayCount">Daycount of yield</param>
    /// <returns>New curve tenor</returns>
    public CurveTenor AddMoneyMarket(string description, Dt maturity, double yield, DayCount dayCount)
    {
      var note = new Note(AsOf, maturity, Ccy, yield, dayCount, Frequency.None, BDConvention.None, Calendar.None) {Description = description};
      note.Validate();
      CurveTenor retVal = Add(note, 0, 0.0, 0.0, 1.0);
      return retVal;
    }

    /// <summary>
    ///   Adds a money market to the curve
    /// </summary>
    /// <param name = "description">Description of the note</param>
    /// <param name = "weight">Calibration weight</param>
    /// <param name = "effective">Effective date</param>
    /// <param name = "maturity">Maturity</param>
    /// <param name = "yield">Coupon</param>
    /// <param name = "dayCount">Daycount</param>
    /// <param name = "freq">Frequency of payment</param>
    /// <param name = "roll">Roll</param>
    /// <param name = "calendar">Calendar</param>
    /// <returns>New curve tenor</returns>
    ///
    public CurveTenor AddMoneyMarket(string description, double weight, Dt effective,
      Dt maturity, double yield, DayCount dayCount, Frequency freq, BDConvention roll, Calendar calendar)
    {
      var note = new Note(effective, maturity, Ccy, yield, dayCount, freq, roll, calendar) {Description = description};
      note.Validate();
      return Add(note, 0, 0.0, 0.0, weight);
    }

    /// <summary>
    ///  Add Eurodollar futures to calibration
    /// </summary>
    /// <remarks>
    ///   <para>The eurodollar futures are quoted as 100-rate.</para>
    /// </remarks>
    /// <param name="description">Description of eurodollar futures tenor or null to use maturity date</param>
    /// <param name="maturity">Maturity date of eurodollar future</param>
    /// <param name="daycount">Daycount</param>
    /// <param name="price">Quoted price</param>
    /// <returns>New curve tenor</returns>
    public CurveTenor AddEDFuture(string description, Dt maturity, DayCount daycount, double price)
    {
      const RateFutureType rateFutureType = RateFutureType.MoneyMarketCashRate;
      var tenor = Tenor.Parse("3M");
      var index = new InterestRateIndex(String.Empty, tenor, Ccy, daycount, Calendar.LNB, BDConvention.Following, 2);
      return this.AddRateFuture(description, 1.0, price, maturity.Month, maturity.Year, index, rateFutureType);
    }

    /// <summary>
    /// Adds a rate future to the curve
    /// </summary>
    /// <param name="description">Futures descritpion</param>
    /// <param name="weight">Calibration weight</param>
    /// <param name="price">Price</param>
    /// <param name="month">Contract month</param>
    /// <param name="year">Contract year</param>
    /// <param name="referenceIndex">Underlying deposit index</param>
    /// <param name="rateFutureType">Rate Future type</param>
    public CurveTenor AddRateFuture(string description, double weight, double price,
      int month, int year, ReferenceIndex referenceIndex, RateFutureType rateFutureType)
    {
      double contractSize, tickSize, tickValue;
      Dt lastTrading, lastDelivery, startAccrual, endAccrual;
      StirFuture.TermsFromType(rateFutureType, referenceIndex.Currency, month, year, referenceIndex.IndexTenor,
        out lastTrading, out lastDelivery, out startAccrual, out endAccrual, out contractSize, out tickSize, out tickValue);
      var future = new StirFuture(rateFutureType, lastDelivery, startAccrual, endAccrual, referenceIndex,
        contractSize, tickSize, tickValue) { Description = description, LastTradingDate = lastTrading };
      future.Validate();
      return Add(future, price, 0.0, 0.0, weight);
    }

    /// <summary>
    /// Add a standard vanilla swap leg to calibration
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="premium">Fixed coupon</param>
    /// <param name="dayCount">Daycount convention</param>
    /// <param name="frequency">Frequency</param>
    /// <param name="roll">Roll</param>
    /// <param name="calendar">Calendar</param>
    /// <returns>New curve tenor</returns>
    public CurveTenor AddSwap(string description, Dt maturity, double premium, DayCount dayCount,
      Frequency frequency, BDConvention roll, Calendar calendar)
    {
      var sl = new SwapLeg(AsOf, maturity, Ccy, premium, dayCount, frequency, roll, calendar, false) {Description = description};
      sl.Validate();
      return Add(sl, 0.0, 0.0, 0.0, 1.0);
    }

    /// <summary>
    /// Add a floating swap leg to calibration
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="premium">Fixed coupon</param>
    /// <param name="frequency">Frequency</param>
    /// <param name="index">Reference index</param>
    /// <param name="settings">Payment settings</param>
    /// <returns>New curve tenor</returns>
    public CurveTenor AddSwap(string description, Dt maturity, double premium, Frequency frequency,
      ReferenceIndex index, PaymentSettings settings)
    {
      var sl = new SwapLeg(AsOf, maturity, frequency, premium, index)
               {
                 ProjectionType = (settings != null) ? settings.RecProjectionType : ProjectionType.SimpleProjection,
                 FinalExchange = (settings != null && settings.PrincipalExchange),
                 CompoundingConvention = (settings != null) ? settings.RecCompoundingConvention : CompoundingConvention.None,
                 CompoundingFrequency = GetCompoundingFrequency(settings, frequency, index, true),
                 Description = description
               };
      sl.Validate();
      return Add(sl, 0.0, 0.0, 0.0, 1.0);
    }

    /// <summary>
    ///   Add a non standard fixed vs floating swap to calibration. 
    /// </summary>
    /// <param name = "description">Description</param>
    /// <param name = "weight">Weight</param>
    /// <param name = "effective">Effective date</param>
    /// <param name = "maturity">Maturity date</param>
    /// <param name = "premium">Premium</param>
    /// <param name = "dayCount">Daycount convention for payer (fixed leg)</param>
    /// <param name="fixedLegFreq">Frequency of payment for payer (fixed leg)</param>
    /// <param name="floatLegFreq">Frequency for receiver (floating leg)</param>
    /// <param name = "roll">Roll convention for payer (fixed leg)</param>
    /// <param name = "calendar">Calendar for payer (fixed leg)</param>
    /// <param name="index">Index referencing the floating leg of the swap</param>
    /// <param name="settings">Payment Settings: at index 0 put payer(fixed leg) setting
    /// and at index 1 set receiver (floating leg) settngs.  </param>
    public CurveTenor AddSwap(string description, double weight, Dt effective, Dt maturity,
      double premium, DayCount dayCount, Frequency fixedLegFreq, Frequency floatLegFreq,
      BDConvention roll, Calendar calendar, ReferenceIndex index, PaymentSettings settings)
    {
      //pay fixed 
      var payer = new SwapLeg(effective, maturity, Ccy, premium, dayCount, fixedLegFreq, roll, calendar, false)
                  {
                    FinalExchange = settings != null && settings.PrincipalExchange,
                    IsZeroCoupon = (fixedLegFreq == Frequency.None),
                    CompoundingFrequency = (settings != null) ? settings.PayCompoundingFreq : Frequency.None
                  };
      var psp = (IScheduleParams)payer.Schedule;
      payer.CycleRule = psp.CycleRule;
      payer.Maturity = psp.Maturity;
      //receive floating
      var freq = (floatLegFreq == Frequency.None) ? index.IndexTenor.ToFrequency() : floatLegFreq;
      var receiver = new SwapLeg(effective, maturity, freq, 0.0, index)
                     {
                       ProjectionType = (settings != null) ? settings.RecProjectionType : ProjectionType.SimpleProjection,
                       CompoundingConvention = (settings != null) ? settings.RecCompoundingConvention : CompoundingConvention.None,
                       FinalExchange = (settings != null && settings.PrincipalExchange),
                       CompoundingFrequency = GetCompoundingFrequency(settings, freq, index, true),
                       ResetLag = new Tenor(index.SettlementDays, TimeUnit.Days)
                     };
      var rsp = (IScheduleParams)receiver.Schedule;
      receiver.CycleRule = rsp.CycleRule;
      receiver.Maturity = rsp.Maturity;
      var swap = new Swap(receiver, payer)
                 {
                   Description = description
                 };
      swap.Validate();
      return Add(swap, 0.0, 0.0, 0.0, weight);
    }

    /// <summary>
    ///   Adds a basis swap to calibration of curve. By default receiver is on target index and payer is on projection index
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="weight">Weight</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="premium">Premium over projected rate</param>
    /// <param name="tgLegFreq">Frequency of payment for leg paying target index(receiver)</param>
    /// <param name="projLegFreq">Frequency of payment for leg paying projection index(payer) </param>
    /// <param name="targetIndex">Reference index for target discount curve.</param>
    /// <param name="projectionIndex">Reference index for provided projection curve</param>
    /// <param name="calendar">Payment calendar</param>
    /// <param name="settings">Payment settings </param>
    public CurveTenor AddSwap(string description, double weight, Dt effective, Dt maturity,
                              double premium, Frequency tgLegFreq, Frequency projLegFreq, ReferenceIndex targetIndex,
                              ReferenceIndex projectionIndex, Calendar calendar, PaymentSettings settings)
    {
      bool spreadOnTarget = false;
      if (settings != null)
        spreadOnTarget = settings.SpreadOnReceiver;
      var recFreq = (tgLegFreq == Frequency.None) ? targetIndex.IndexTenor.ToFrequency() : tgLegFreq;
      var receiver = new SwapLeg(effective, maturity, recFreq , spreadOnTarget ? premium : 0.0, targetIndex)
                       {
                         ProjectionType = (settings != null) ? settings.RecProjectionType : ProjectionType.SimpleProjection,
                         CompoundingConvention = (settings != null) ? settings.RecCompoundingConvention : CompoundingConvention.None,
                         FinalExchange = (settings != null && settings.PrincipalExchange),
                         CompoundingFrequency = GetCompoundingFrequency(settings, recFreq, targetIndex, true),
                       };
      if (calendar != Calendar.None) receiver.Calendar = calendar;
      var rsp = (IScheduleParams)receiver.Schedule;
      receiver.CycleRule = rsp.CycleRule;
      receiver.Maturity = rsp.Maturity;
      receiver.InArrears = (receiver.ProjectionType == ProjectionType.ArithmeticAverageRate ||
                            receiver.ProjectionType == ProjectionType.GeometricAverageRate
                            || receiver.ProjectionType == ProjectionType.TBillArithmeticAverageRate ||
                            receiver.ProjectionType == ProjectionType.CPArithmeticAverageRate);
      receiver.ResetLag = new Tenor(targetIndex.SettlementDays, TimeUnit.Days);
      var payFreq = (projLegFreq == Frequency.None) ? projectionIndex.IndexTenor.ToFrequency() : projLegFreq;
      var payer = new SwapLeg(effective, maturity, payFreq, spreadOnTarget ? 0.0 : premium, projectionIndex)
                    {
                      ProjectionType = (settings != null) ? settings.PayProjectionType : ProjectionType.SimpleProjection,
                      CompoundingConvention = (settings != null) ? settings.PayCompoundingConvention : CompoundingConvention.None,
                      FinalExchange = (settings != null && settings.PrincipalExchange),
                      CompoundingFrequency = GetCompoundingFrequency(settings, payFreq, projectionIndex, false),
                      ResetLag = new Tenor(projectionIndex.SettlementDays, TimeUnit.Days),
                    };
      if (calendar != Calendar.None) payer.Calendar = calendar;
      payer.InArrears = (payer.ProjectionType == ProjectionType.ArithmeticAverageRate ||
                         payer.ProjectionType == ProjectionType.GeometricAverageRate
                         || payer.ProjectionType == ProjectionType.TBillArithmeticAverageRate ||
                         payer.ProjectionType == ProjectionType.CPArithmeticAverageRate);
      var psp = (IScheduleParams)payer.Schedule;
      payer.CycleRule = psp.CycleRule;
      payer.Maturity = psp.Maturity;
      var swap = new Swap(receiver, payer) { Description = description };
      swap.Validate();
      return Add(swap, 0.0, 0.0, 0.0, weight);
    }

    ///<summary>
    /// Add a FRA product to calibration
    ///</summary>
    ///<param name="description">Description</param>
    /// <param name="weight">weight of the tenor</param>
    /// <param name="effective">Effective</param>
    ///<param name="maturity">Maturity date</param>
    ///<param name="fixedrate">Fixed rate</param>
    ///<param name="index">Settlement index</param>
    ///<returns>New curve tenor</returns>
    public CurveTenor AddFRA(string description, double weight, Dt effective, Dt maturity, double fixedrate, ReferenceIndex index)
    {
      Tenor settleTenor, maturityTenor;
      Dt settle;
      FRA fra = null; 
      if (Tenor.TryParseComposite(description, out settleTenor, out maturityTenor))
      {
        fra = new FRA(effective, settleTenor, index.IndexTenor.ToFrequency(), fixedrate, index, maturityTenor, index.Currency,
                         index.DayCount, index.Calendar, index.Roll)
        {
          FixingLag = new Tenor(index.SettlementDays, TimeUnit.Days),
          Description = index.IndexName + "." + Enum.GetName(typeof(InstrumentType), InstrumentType.FRA) + "_" +
            description
        };
 
      }
      else if (Dt.TryFromStrComposite(description, "%d-%b-%Y", out settle, out maturity))
      {
        fra = new FRA(effective, settle, index.IndexTenor.ToFrequency(), fixedrate, index, maturity, index.Currency,
          index.DayCount, index.Calendar, index.Roll)
        {
          FixingLag = new Tenor(index.SettlementDays, TimeUnit.Days),
          Description = index.IndexName + "." + Enum.GetName(typeof(InstrumentType), InstrumentType.FRA) + "_" +
                        description
        };
      }
      else
      {
        throw new ArgumentException("Tenor is required to be in A * B composite format for FRA type instrument");
      }
      
      fra.Validate();
      return Add(fra, fixedrate, 0.0, 0.0, weight);
    }

    /// <summary>
    /// Add a FRA to calibration
    /// </summary>
    /// <param name="targetIndex">Target index</param>
    /// <param name="description">Product descritpion</param>
    /// <param name="weight">Weight</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="premium">Premium</param>
    /// <param name="dayCount">Daycount</param>
    /// <param name="roll">Roll</param>
    /// <param name="calendar">Calendar</param>
    public CurveTenor AddFRA(string description, double weight, Dt effective, Dt maturity, double premium, DayCount dayCount, 
      BDConvention roll, Calendar calendar, ReferenceIndex targetIndex)
    {
      //this is different from the swap, description is composite tenor
      Tenor settleTenor, maturityTenor;
      if (!Tenor.TryParseComposite(description, out settleTenor, out maturityTenor))
      {
        throw new ArgumentException("Tenor is required to be in A * B composite format for FRA type instrument");
      }

      var fra = new FRA(effective, settleTenor, targetIndex.IndexTenor.ToFrequency(), premium, targetIndex, maturityTenor,
                        Ccy, dayCount, calendar, roll) { FixingLag = new Tenor(targetIndex.SettlementDays, TimeUnit.Days) };
      fra.Validate();
      fra.Description = targetIndex.IndexName + "." + Enum.GetName(typeof(InstrumentType), InstrumentType.FRA) + "_" +
                        description;
      return Add(fra, premium, 0.0, 0.0, weight);
    }

    /// <summary>
    /// Add risk-free bond to calibration
    /// </summary>
    /// <param name="description">Bond description</param>
    /// <param name="weight">Weight</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="coupon">Bond coupon</param>
    /// <param name="dayCount">Bond day-count</param>
    /// <param name="roll">Bond bd-convention</param>
    /// <param name="cal">Bond calendar</param>
    /// <param name="freq">Bond payment frequency </param>
    /// <param name="type">Bond type</param>
    /// <param name="targetIndex">Curve target index</param>
    /// <param name="quote">Bond market quote</param>
    /// <param name="qc">Bond market quote convention</param>
    /// <returns>New curve tenor</returns>
    public CurveTenor AddRiskFreeBond(string description, double weight, Dt effective, Dt maturity, double coupon, DayCount dayCount,
      BDConvention roll, Calendar cal, Frequency freq, BondType type, ReferenceIndex targetIndex, double quote, QuotingConvention qc)
    {
      var issue = maturity;
      while (issue > effective)
      {
        issue = Dt.Add(issue, freq, -1, false);
      }

      var bond = new Bond(issue, maturity, Ccy, type, coupon, dayCount, CycleRule.None, freq, roll, cal)
                   {
                     QuotingConvention = qc
                   };
      bond.Validate();
      var psp = (IScheduleParams)bond.Schedule;

      bond.CycleRule = psp.CycleRule;
      bond.Description = targetIndex.IndexName + "." + Enum.GetName(typeof(InstrumentType), InstrumentType.Bond) + "_" +
                        description;
      var curveTenor = Add(bond, quote, 0.0, 0.0, weight);
      var qh = new CurveTenorQuoteHandlers.RiskFreeBondQuoteHandler(quote, qc);
      curveTenor.QuoteHandler = qh;
      curveTenor.OriginalQuote = qh.GetCurrentQuote(curveTenor);
      return curveTenor;
    }

    /// <summary>
    ///  Add zero coupon bond to calibration
    /// </summary>
    /// <remarks>
    ///   <para>The bonds are quoted in terms of a simple yield.</para>
    /// </remarks>
    /// <param name="description">Description of zero coupon bond (for tenor) or null to use maturity date</param>
    /// <param name="maturity">Maturity date of zero coupon bond</param>
    /// <param name="yield">Zero coupon bond yield</param>
    /// <param name="dayCount">Daycount of yield</param>
    /// <param name="freq">Compounding frequency of yield</param>
    /// <returns>New curve tenor</returns>
    public CurveTenor AddZeroYield(string description, Dt maturity, double yield, DayCount dayCount, Frequency freq)
    {
      // Validation done in product
      var note = new Note(AsOf, maturity, Ccy, yield, dayCount, freq, BDConvention.None, Calendar.None) {Description = description};
      note.Validate();
      return Add(note, 0.0, 1.0, 0.0, 0.0);
    }

    /// <summary>
    ///  Add zero coupon bond to calibration
    /// </summary>
    /// <remarks>
    ///   <para>The bonds are quoted in terms of a simple yield.</para>
    /// </remarks>
    /// <param name="maturity">Maturity date of zero coupon bond</param>
    /// <param name="yield">Zero coupon bond yield</param>
    /// <param name="dayCount">Daycount of yield</param>
    /// <param name="freq">Compounding frequency of yield</param>
    /// <returns>New curve tenor</returns>
    public CurveTenor AddZeroYield(Dt maturity, double yield, DayCount dayCount, Frequency freq)
    {
      return AddZeroYield(null, maturity, yield, dayCount, freq);
    }

    /// <summary>
    /// Resolves the overlap in the tenor used for calibration of the discount curve
    /// </summary>
    /// <param name="overlapTreatment">Overlap treatment object</param>
    /// <param name="cloneIndividualTenors">if set to <c>true</c>, clone individual tenors</param>
    public void ResolveOverlap(OverlapTreatment overlapTreatment, bool cloneIndividualTenors = true)
    {
      Tenors = overlapTreatment.ResolveTenorOverlap(Tenors, cloneIndividualTenors);
    }
    #endregion

    #region Util
    private static Frequency GetCompoundingFrequency(PaymentSettings settings, Frequency swapLegFrequency, ReferenceIndex referenceIndex, bool receiver)
    {
      var freq = (settings == null) ? Frequency.None : receiver ? settings.RecCompoundingFreq : settings.PayCompoundingFreq;
      if (freq != Frequency.None)
        return (freq > swapLegFrequency) ? freq : Frequency.None;
      var indexFreq = referenceIndex.IndexTenor.ToFrequency();
      return (indexFreq > swapLegFrequency) ? indexFreq : Frequency.None;
    }
    #endregion

  }
}
