/*
 * TestFxSensitivities.cs
 *
 * Sensitivity tests on all FX product
 *
 * Copyright (c) 2005-2012,   . All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;    
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics; //contains interp extrap methods

using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{
  [TestFixture]
  public class TestFxSensitivities : SensitivityTest
  {
    #region Testing Data

    private Dictionary<Currency,  DiscountCurve> discountCurves_;
    private Dictionary<string, FxCurve> fxFwdCurves_;
    private Dictionary<string, FxCurve> fxBasisCurves_;
    private FxForwardPricer fwdFxPricer_;
    private FxNonDeliverableForwardPricer ndfFxPricer_;
    private FxSwapPricer swapFxPricer_;
    private FxForwardPricer fwdFxPricer1_;
    private FxNonDeliverableForwardPricer ndfFxPricer1_;
    private FxSwapPricer swapFxPricer1_;

    #endregion

    [OneTimeSetUp]
    public void Initialize()
    {
      PricingDate = 20120717;
      Dt asOf = GetAsOf();
      Dt settle = GetSettle();
      Dt valueDate = Dt.Add(settle, "1Y");

      discountCurves_ = new Dictionary<Currency, DiscountCurve>();
      discountCurves_.Add(Currency.USD, CreateIRCurve(
        new InterestRateIndex("USD_LIBOR", Frequency.Quarterly, Currency.USD, DayCount.Actual360,Calendar.LNB,2), DayCount.Thirty360,
        Frequency.SemiAnnual));
      discountCurves_.Add(Currency.GBP, CreateIRCurve(
        new InterestRateIndex("GBP_LIBOR", Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0), DayCount.Actual365Fixed,
        Frequency.Annual));

      var fxFwd = new FxForward(valueDate, Currency.GBP, Currency.USD, 1.50) {Description ="Fx Fwd"};
      fwdFxPricer_ = new FxForwardPricer(fxFwd, asOf, settle, 1E6, Currency.USD, discountCurves_[Currency.USD], 
        getForwardBasedFxCurve(Currency.USD, Currency.GBP, fxFwd.FxRate), null);

      var fxFwd1 = new FxForward(valueDate, Currency.USD, Currency.GBP, 1.0/1.5) {Description = "Fx Fwd(1)"};
      fwdFxPricer1_ = new FxForwardPricer(fxFwd1, asOf, settle, 1E6, Currency.GBP, discountCurves_[Currency.GBP],
       null, getBasisBasedFxCurve(Currency.USD, Currency.GBP, fxFwd1.FxRate, 0.0015));

      var ndf = new FxNonDeliverableForward(valueDate, Currency.USD, Currency.GBP, 1.0/1.5, true, Dt.AddDays(valueDate, -2, Calendar.LNB))
        {Description = "Fx NDF"};
      ndfFxPricer_ = new FxNonDeliverableForwardPricer(ndf, asOf, settle, 1E6, Currency.USD,
                                                       discountCurves_[Currency.USD],
                                                       null,
                                                       getForwardBasedFxCurve(Currency.USD, Currency.GBP, ndf.FxRate));

      var ndf1 = new FxNonDeliverableForward(valueDate, Currency.GBP, Currency.USD, 1.5, true, Dt.AddDays(valueDate, -2, Calendar.LNB))
        {Description = "Fx NDF(1)"};
      ndfFxPricer1_ = new FxNonDeliverableForwardPricer(ndf1, asOf, settle, 1E6, Currency.GBP,
                                                       discountCurves_[Currency.GBP],
                                                       getBasisBasedFxCurve(Currency.GBP, Currency.USD, ndf.FxRate, 0.0015), null);

      var fxSwap = new FxSwap(valueDate, Currency.USD, Currency.GBP, 1.0 / 1.5, Dt.Add(valueDate, "1Y"), 1.0 / 1.6)
        {Description = "Fx Swap"};
      swapFxPricer_ = new FxSwapPricer(fxSwap, asOf, settle, 1E6*1.5, Currency.USD, discountCurves_[Currency.USD],
                                       getForwardBasedFxCurve(Currency.USD, Currency.GBP, fxSwap.NearFxRate), null);

      var fxSwap1 = new FxSwap(valueDate, Currency.GBP, Currency.USD, 1.5, Dt.Add(valueDate, "1Y"), 1.6)
        {Description ="Fx Swap(1)"};
      swapFxPricer1_ = new FxSwapPricer(fxSwap1, asOf, settle, 1E6/1.5, Currency.GBP, discountCurves_[Currency.GBP],null,
                                       getBasisBasedFxCurve(Currency.GBP, Currency.USD, fxSwap1.NearFxRate, 0.0015));
    }


    #region FxCurve testing

    [Test, Smoke]
    public void TestFxCurveSensitivity()
    {
      var timer = new Timer();
      timer.Start();
      var dt = Sensitivities.FxCurve(new IPricer[] {fwdFxPricer_, ndfFxPricer_, swapFxPricer_}, "Pv", 0.0, 4.0, 4.0, true, false,
                                          BumpType.ByTenor, null, true, false, null, true,
                                          SensitivityMethod.FiniteDifference,
                                          null);
      timer.Stop();
      ToResultData(dt, timer.Elapsed);
    }


    [Test, Smoke]
    public void TestFxBasisSensitivity()
    {
      var timer = new Timer();
      timer.Start();
      var dt = Sensitivities.BasisAdjustment(new IPricer[] { fwdFxPricer1_, ndfFxPricer1_, swapFxPricer1_ }, "Pv", 0.0, 4.0, 4.0, true, false,
                                          BumpType.ByTenor, null, true, false, null, true,
                                          SensitivityMethod.FiniteDifference,
                                          null);
      timer.Stop();
      ToResultData(dt, timer.Elapsed);
    }

    #endregion

    #region Helper Methods

    public void ToResultData(DataTable table, double timeUsed)
    {
      int rows = table.Rows.Count;
      int cols = 1;

      var fxDeltas = new double[rows];
      var labels = new string[rows];

      for (int i = 0; i < rows; i++)
      {
        DataRow row = table.Rows[i];
        fxDeltas[i] = Convert.ToDouble(row["Delta"]);
        labels[i] = String.Format("{0}.{1}.{2}", row["Element"].ToString(), row["Curve Tenor"].ToString(),row["Pricer"].ToString());
      }

      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; j++)
        {
          rd.Results[j] = new ResultData.ResultSet();
        }
      }
      else if (rd.Results[0].Expects.Length != rows)
      {
        throw new Exception(String.Format("Number of generated sensitivities[{0}] doesn't match expected[{1}]",
                                          rows, rd.Results[0].Expects.Length));
      }

      rd.Results[0].Name = "By-tenor fx curve sensitivity:";
      rd.Results[0].Labels = labels;
      rd.Results[0].Actuals = fxDeltas;

      rd.TimeUsed = timeUsed;

      MatchExpects(rd);
    }

    private Dt GetAsOf()
    {
      return PricingDate == 0 ? Dt.Today() : ToDt(PricingDate);
    }

    private Dt GetSettle()
    {
      Dt asOf = GetAsOf();
      return SettleDate == 0 ? Dt.Add(asOf, 1, TimeUnit.Days) : ToDt(SettleDate);
    }

    /// <summary>
    ///   Create a calibrated IR curve
    /// </summary>
    /// <returns>Discount curve</returns>
    private DiscountCurve CreateIRCurve(InterestRateIndex rateIndex, DayCount swapDc, Frequency swapFixedPayFreq)
    {
      Dt asOf = GetAsOf();
      Dt effective = Dt.AddDays(asOf, rateIndex.SettlementDays, rateIndex.Calendar);
      var mmTenors = new string[] {"6 Month", "1 Year"};
      var mmRates = new double[] {0.0369, 0.0386};
      var mmMaturities = new Dt[mmTenors.Length];
      for (int i = 0; i < mmTenors.Length; i++)
        mmMaturities[i] = Dt.Roll(Dt.Add(effective, mmTenors[i]), rateIndex.Roll, rateIndex.Calendar);
      var mmDayCount = rateIndex.DayCount;

      var swapTenors = new string[] {"2 Year", "3 Year", "5 Year", "7 Year", "10 Year"};
      var swapRates = new double[] {0.0399, 0.0407, 0.0417, 0.0426, 0.044};

      var swapMaturities = new Dt[swapTenors.Length];
      for (int i = 0; i < swapTenors.Length; i++)
        swapMaturities[i] = Dt.Add(effective, swapTenors[i]);
      var swapDayCount = swapDc;

      //IR curve is calibrated to market so need bootstrap calibrator
      var calibrator = new DiscountCurveFitCalibrator(asOf, rateIndex, new CalibratorSettings());

      var curve = new DiscountCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      curve.Ccy = rateIndex.Currency;
      curve.Category = "None";
      curve.Name = rateIndex.IndexName;

      // Add MM rates
      for (int i = 0; i < mmTenors.Length; i++)
        if (mmRates[i] > 0.0)
          curve.AddMoneyMarket(mmTenors[i], mmMaturities[i], mmRates[i], mmDayCount);

      // Add swap rates
      for (int i = 0; i < swapTenors.Length; i++)
        if (swapRates[i] > 0.0)
          curve.AddSwap(swapTenors[i], swapMaturities[i], swapRates[i], swapDayCount,
                        swapFixedPayFreq, rateIndex.Roll, rateIndex.Calendar);

      curve.Fit();

      return curve;
    }

    /// <summary>
    /// Construct Fx curve between a ccy pair using fx fwds
    /// </summary>
    /// <param name="payCcy">Pay ccy</param>
    /// <param name="rcvCcy">Rcv ccy</param>
    /// <param name="spotRate">Spot fx rate</param>
    /// <returns>Fx curve</returns>
    private FxCurve getForwardBasedFxCurve(Currency payCcy, Currency rcvCcy, double spotRate)
    {
      if (fxFwdCurves_ == null)
        fxFwdCurves_ = new Dictionary<string, FxCurve>();

      string fxPairKey = payCcy.ToString() + rcvCcy.ToString();
      string fxReversePairKey = rcvCcy.ToString() + payCcy.ToString();
      if (fxFwdCurves_.ContainsKey(fxPairKey))
        return fxFwdCurves_[fxPairKey];
      else if (fxFwdCurves_.ContainsKey(fxReversePairKey))
        return fxFwdCurves_[fxReversePairKey];
      else // build one on the fly and add into cache
      {
        var fd1 = new FxData
        {
          AsOf = GetAsOf(),
          Name = fxPairKey,
          FromCcy = payCcy,
          ToCcy = rcvCcy,
          FromCcyCalendar = Calendar.LNB,
          ToCcyCalendar = Calendar.NYB,
          SpotFx = spotRate,
          TenorNames = new string[] { "1Y", "10Y" },
          FwdFx = new double[] { spotRate * 1.1, spotRate * 1.2 }
        };
        fxFwdCurves_.Add(fd1.Name, fd1.GetFxCurve());
        return fxFwdCurves_[fd1.Name];
      }
    }

    /// <summary>
    /// Construct Fx curve between a ccy pair using fx fwds
    /// </summary>
    /// <param name="payCcy">Pay ccy</param>
    /// <param name="rcvCcy">Rcv ccy</param>
    /// <param name="spotRate">Spot fx rate</param>
    /// <param name="basis">Basis spread </param>
    /// <returns>Fx curve</returns>
    private FxCurve getBasisBasedFxCurve(Currency payCcy, Currency rcvCcy, double spotRate, double basis)
    {
      if (fxBasisCurves_ == null)
        fxBasisCurves_ = new Dictionary<string, FxCurve>();

      string fxPairKey = payCcy.ToString() + rcvCcy.ToString();
      string fxReversePairKey = rcvCcy.ToString() + payCcy.ToString();
      if (fxBasisCurves_.ContainsKey(fxPairKey))
        return fxBasisCurves_[fxPairKey];
      else if (fxBasisCurves_.ContainsKey(fxReversePairKey))
        return fxBasisCurves_[fxReversePairKey];
      else // build one on the fly and add into cache
      {
        var fxBasis = CalibrateBasisFxCurve(GetAsOf(), new string[] {"1Y", "10Y"}, new double[] {basis, basis},
                                           Calendar.LNB, Calendar.NYB, spotRate, fxPairKey,
                                           payCcy, rcvCcy, discountCurves_[payCcy], discountCurves_[rcvCcy]);
        fxBasisCurves_.Add(fxBasis.Name, fxBasis);
        return fxBasisCurves_[fxBasis.Name];
      }
    }

    public FxCurve CalibrateBasisFxCurve(Dt asOf, string[] tenorNames, double[] basisQuotes, Calendar calFromCcy, 
      Calendar calToCcy, double spotFx, string name, Currency fromCcy, Currency toCcy, DiscountCurve discFromCcy,
      DiscountCurve discToCcy)
    {
      Dt[] dates = new Dt[tenorNames.Length];
      var spotDate = FxUtil.FxSpotDate(asOf, 2, calFromCcy, calToCcy);
      for (int i = 0; i < tenorNames.Length; i++)
      {
        dates[i] = Dt.Add(spotDate, tenorNames[i]);
      }
      return new FxCurve(asOf, spotDate, fromCcy, toCcy, spotFx, ArrayUtil.NewArray(dates.Length, InstrumentType.BasisSwap), tenorNames,
        dates, basisQuotes, calFromCcy, BasisSwapSide.Ccy1, discFromCcy, discToCcy ,null, name);

    }

    #endregion
  }
}
