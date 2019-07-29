/*
 * Loan.cs
 *
 *   2010. All rights reserved.
 *
 * Created by rsmulktis on 1/12/2010 1:02:57 PM
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Factor class for Loan Pricers.
  /// </summary>
  public static class LoanPricerFactory
  {
    /// <summary>
    /// Creates a new Loan Pricer based on variable parameters for prepayment and model calibration.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="useDrawnNotional">Use drawn notional </param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="prepayment">The prepayment assumption (curve or target WAL)</param>
    /// <param name="volCurve">Volatility curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="calibrateModel">Whether to calibrate the model or apply a known market spread</param>
    /// <param name="knownMarketSpread">The known market spread to apply in stead of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="intPeriods">Collection of current interest periods</param>
    /// <param name="rateVol">Rate volatility </param>
    /// <param name="disableToolkitSpreadCalibrate">If true, turn off the toolkit Market Spread calculation</param>
    /// <returns>Loan Pricer</returns>
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      object prepayment,
      VolatilityCurve volCurve, 
      double refiCost,
      double price,
      CalibrationType calType,
      bool calibrateModel, 
      double knownMarketSpread, 
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods,
      double rateVol,
      bool disableToolkitSpreadCalibrate)
    {
      // Validate params
      if (calibrateModel && survivalCurve == null && calType != CalibrationType.SurvivalCurve)
        throw new ToolkitException("If you are not calibrating the SurvivalCurve then you must specify a SurvivalCurve!");

      // Create pricer
      LoanPricer pricer = new LoanPricer(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, referenceCurve,
                                         survivalCurve,
                                         recoveryCurve, null, volCurve, refiCost, curLevel, usages, endProb,
                                         new MarketQuote(price, QuotingConvention.FlatPrice),
                                         calType, (calibrateModel ? 0 : knownMarketSpread), rateVol);

      // Add interest periods
      if (loan.IsFloating)
        CollectionUtil.Add(pricer.InterestPeriods, intPeriods);

      // Calibrate to market
      if (settle < loan.Maturity)
      {
        // Calibrate prepayment curve
        if(prepayment != null && prepayment is SurvivalCurve)
          pricer.PrepaymentCurve = (SurvivalCurve) prepayment;
        else if(prepayment != null && prepayment is double)
          pricer.PrepaymentCurve = pricer.ImpliedFlatPrepaymentCurve((double) prepayment);

        // Calibrate survival curve if necessary
        if (calibrateModel && survivalCurve == null && calType == CalibrationType.SurvivalCurve)
          pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve();
        // Calibrate to market if necessary
        else if (calibrateModel && calType != CalibrationType.None && !disableToolkitSpreadCalibrate)
          pricer.Calibrate();
      }

      // Validate config
      pricer.Validate();

      // Done
      return pricer;
    }

    /// <summary>
    /// Creates a new Loan Pricer from all required parameters.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="intPeriods">Collection of current interest periods</param>
    /// 
    /// <returns>Loan Pricer</returns>
    /// 
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      SurvivalCurve prepaymentCurve,
      double refiCost,
      double price,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods)
    {
      return New(loan, asOf, settle, commitment, drawn, true, discountCurve, referenceCurve, survivalCurve, recoveryCurve, prepaymentCurve,
                 refiCost, price, calType, curLevel, usages, endProb, intPeriods);
    }

    /// <summary>
    /// Creates a new Loan Pricer from all required parameters.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="useDrawnNotional">Use drawn notional</param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="intPeriods">Collection of current interest periods</param>
    /// 
    /// <returns>Loan Pricer</returns>
    /// 
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      SurvivalCurve prepaymentCurve,
      double refiCost,
      double price,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods)
    {
      return New(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, 
        referenceCurve, survivalCurve, recoveryCurve,
       prepaymentCurve, refiCost, price, QuotingConvention.FlatPrice,
        calType, curLevel, usages, endProb, intPeriods, 0);
    }


    private static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      SurvivalCurve prepaymentCurve,
      double refiCost,
      double price,
      QuotingConvention quoteType,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods,
      double rateVol)
    {
      // Validate params
      if (survivalCurve == null && calType != CalibrationType.SurvivalCurve)
        throw new ToolkitException("If you are not calibrating the SurvivalCurve then you must specify a SurvivalCurve!");

      // Create pricer
      LoanPricer pricer = new LoanPricer(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, referenceCurve,
                                         survivalCurve,
                                         recoveryCurve, prepaymentCurve, null, refiCost, curLevel, usages, endProb,
                                         new MarketQuote(price, quoteType),
                                         calType, 0, rateVol);

      // Add interest periods
      if (loan.IsFloating)
        CollectionUtil.Add(pricer.InterestPeriods, intPeriods);

      // Calibrate to market
      if (settle < loan.Maturity)
      {
        // Calibrate survival curve if necessary
        if (survivalCurve == null && calType == CalibrationType.SurvivalCurve)
          pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve();
        // Calibrate to market if necessary
        else if (survivalCurve != null && calType != CalibrationType.None)
          pricer.Calibrate();
      }

      // Validate config
      pricer.Validate();

      // Done
      return pricer;
    }

    /// <summary>
    /// Creates a new Loan Pricer from all required parameters.
    /// </summary>
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="useDrawnNotional">Use drawn notional </param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="volatilityCurve">Volatility curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="intPeriods">Collection of current interest periods</param>
    /// <param name="rateVol">Rate volatility </param>
    /// <returns>Loan Pricer</returns>
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      SurvivalCurve prepaymentCurve, 
      VolatilityCurve volatilityCurve,
      double refiCost,
      double price,
      QuotingConvention quoteType,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods,
      double rateVol)
    {
      // Create pricer
      LoanPricer pricer = new LoanPricer(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, referenceCurve,
                                         survivalCurve,
                                         recoveryCurve, prepaymentCurve, volatilityCurve, refiCost, curLevel, usages, endProb,
                                         new MarketQuote(price, quoteType),
                                         calType, 0, rateVol);

      // Add interest periods
      if (loan.IsFloating)
        CollectionUtil.Add(pricer.InterestPeriods, intPeriods);

      // Calibrate to market
      if (settle < loan.Maturity)
      {
        // Calibrate survival curve if necessary
        if (survivalCurve == null && calType == CalibrationType.SurvivalCurve)
          pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve();
        // Calibrate to market if necessary
        else if (survivalCurve != null && calType != CalibrationType.None)
          pricer.Calibrate();
      }

      // Validate config
      pricer.Validate();

      // Done
      return pricer;
    }

    /// <summary>
    /// Creates a new Loan Pricer and calibrates its prepayment curve.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="prepayTargetWAL">Target WAL for calibrating a Prepayment curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="intPeriods">Collection of current interest periods</param>
    /// 
    /// <returns>Loan Pricer</returns>
    /// 
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      double prepayTargetWAL,
      double refiCost,
      double price,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods)
    {
      return New(loan, asOf, settle, commitment, drawn, true, discountCurve, referenceCurve, survivalCurve, recoveryCurve,
                  prepayTargetWAL, refiCost, price, calType, curLevel, usages, endProb, intPeriods);
    }

    /// <summary>
    /// Creates a new Loan Pricer and calibrates its prepayment curve.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="useDrawnNotional">Use drawn notioal </param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="prepayTargetWAL">Target WAL for calibrating a Prepayment curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="intPeriods">Collection of current interest periods</param>
    /// 
    /// <returns>Loan Pricer</returns>
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      double prepayTargetWAL,
      double refiCost,
      double price,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods)
    {
      return New(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, referenceCurve, survivalCurve, recoveryCurve,
       prepayTargetWAL, refiCost, price, calType, curLevel, usages, endProb, intPeriods, 0);
    }

    private static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      double prepayTargetWAL,
      double refiCost,
      double price,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods,
      double rateVol)
    {
      // Validate params
      if (survivalCurve == null && calType != CalibrationType.SurvivalCurve)
        throw new ToolkitException("If you are not calibrating the SurvivalCurve then you must specify a SurvivalCurve!");

      // Create pricer
      LoanPricer pricer = new LoanPricer(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, referenceCurve,
                                         survivalCurve,
                                         recoveryCurve, null, null, refiCost, curLevel, usages, endProb,
                                         new MarketQuote(price, QuotingConvention.FlatPrice),
                                         calType, 0, rateVol);

      // Add interest periods
      if (loan.IsFloating)
        CollectionUtil.Add(pricer.InterestPeriods, intPeriods);

      // Calibrate to market
      if (settle < loan.Maturity)
      {
        // Calibrate prepayment curve
        pricer.PrepaymentCurve = pricer.ImpliedFlatPrepaymentCurve(prepayTargetWAL);

        // Calibrate survival curve if necessary
        if (survivalCurve == null && calType == CalibrationType.SurvivalCurve)
          pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve();
        // Calibrate to market if necessary
        else if (calType != CalibrationType.None)
          pricer.Calibrate();
      }

      // Validate config
      pricer.Validate();

      // Done
      return pricer;
    }

    /// <summary>
    /// Creates a new Loan Pricer and calibrates its prepayment curve.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="useDrawnNotional">Use drawn notional </param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="prepayTargetWAL">Target WAL for calibrating a Prepayment curve</param>
    /// <param name="volatilityCurve">Volatility curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="intPeriods">Collection of current interest periods</param>
    /// <param name="rateVol">Rate volatility </param>
    /// <returns>Loan Pricer</returns>
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      RecoveryCurve recoveryCurve,
      double prepayTargetWAL, 
      VolatilityCurve volatilityCurve,
      double refiCost,
      double price,
      QuotingConvention quoteType,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      ICollection<InterestPeriod> intPeriods,
      double rateVol)
    {
      // Create pricer
      LoanPricer pricer = new LoanPricer(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, referenceCurve,
                                         survivalCurve,
                                         recoveryCurve, null, volatilityCurve, refiCost, curLevel, usages, endProb,
                                         new MarketQuote(price, quoteType),
                                         calType, 0, rateVol);

      // Add interest periods
      if (loan.IsFloating)
        CollectionUtil.Add(pricer.InterestPeriods, intPeriods);

      // Calibrate to market
      if (settle < loan.Maturity)
      {
        // Calibrate prepayment curve
        pricer.PrepaymentCurve = pricer.ImpliedFlatPrepaymentCurve(prepayTargetWAL);

        // Calibrate survival curve if necessary
        if (survivalCurve == null && calType == CalibrationType.SurvivalCurve)
          pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve();
        // Calibrate to market if necessary
        else if (calType != CalibrationType.None)
          pricer.Calibrate();
      }

      // Validate config
      pricer.Validate();

      // Done
      return pricer;
    }

    /// <summary>
    /// Creates a new Loan Pricer for a Loan with no pricing grid from the most basic assumptions.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">The commitment drawn</param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="lastReset">Last interest rate fixing (assumes current draw, 1 interest period)</param>
    /// 
    /// <returns>Loan Pricer</returns>
    /// 
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      double recoveryRate,
      SurvivalCurve prepaymentCurve,
      double refiCost,
      double price,
      CalibrationType calType,
      double lastReset)
    {
      // Create 
      return New(loan, asOf, settle, commitment, drawn, true, discountCurve, referenceCurve,
                                   survivalCurve, recoveryRate, prepaymentCurve, refiCost,
                                   price, calType, Loan.DefaultPerformanceLevel, null, null,
                                   lastReset);
    }

    /// <summary>
    /// Creates a new Loan Pricer with a Pricing Grid from the most basic parameters.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="lastReset">Last interest rate fixing (assumes current draw, 1 interest period)</param>
    /// 
    /// <returns>Loan Pricer</returns>
    /// 
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      double recoveryRate,
      SurvivalCurve prepaymentCurve,
      double refiCost,
      double price,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      double lastReset)
    {
      return New(loan, asOf, settle, commitment, drawn, true, discountCurve, referenceCurve, survivalCurve,
                 recoveryRate, prepaymentCurve, refiCost, price, calType, curLevel, usages, endProb, lastReset);
    }

    /// <summary>
    /// Creates a new Loan Pricer with a Pricing Grid from the most basic parameters.
    /// </summary>
    /// 
    /// <param name="loan">The Loan</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commitment">Total commitment</param>
    /// <param name="drawn">Drawn commitment</param>
    /// <param name="useDrawnNotional">Use drawn notioal </param>
    /// <param name="discountCurve">Interest rate curve for discounting</param>
    /// <param name="referenceCurve">Interest rate curve for forward rates</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="refiCost">Cost of refinancing</param>
    /// <param name="price">Market flat price</param>
    /// <param name="calType">Method of calibration</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="usages">Array of expected draw for each performance level</param>
    /// <param name="endProb">Array of probabilities of performance at maturity for each level</param>
    /// <param name="lastReset">Last interest rate fixing (assumes current draw, 1 interest period)</param>
    /// 
    /// <returns>Loan Pricer</returns>
    public static LoanPricer New(
      Loan loan,
      Dt asOf,
      Dt settle,
      double commitment,
      double drawn,
      bool useDrawnNotional,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      double recoveryRate,
      SurvivalCurve prepaymentCurve,
      double refiCost,
      double price,
      CalibrationType calType,
      string curLevel,
      double[] usages,
      double[] endProb,
      double lastReset)
    {
      // Setup recovery curve
      RecoveryCurve recoveryCurve = new RecoveryCurve(asOf, recoveryRate);
      recoveryCurve.Name = loan.Description + "_Recovery";

      // Setup interest periods
      IList<InterestPeriod> interestPeriods = new List<InterestPeriod>();
      interestPeriods.Add(InterestPeriodUtil.DefaultInterestPeriod(settle, loan, lastReset, drawn / commitment));

      // Create 
      return New(loan, asOf, settle, commitment, drawn, useDrawnNotional, discountCurve, referenceCurve,
                                   survivalCurve, recoveryCurve, prepaymentCurve, refiCost,
                                   price, QuotingConvention.FlatPrice, calType, curLevel, usages, endProb, interestPeriods, 0);
    }

    /// <summary>
    ///  Creates a collection of interest periods for a Loan.
    /// </summary>
    /// <param name="method">The way to create</param>
    /// <param name="loan">The Loan</param>
    /// <param name="knownPeriods">Known periods</param>
    /// <param name="asOf">Pricing Date</param>
    /// <param name="curPricing">Current floating rate spread</param>
    /// <param name="drawnPct">Percentage of the Loan drawn</param>
    /// <param name="projectionCurve">Forward rate curve</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <returns></returns>
    public static IList<InterestPeriod> CreateInterestPeriods(LoanNextCouponTreatment method, Dt asOf, Dt settle, Loan loan, double curPricing, double drawnPct, DiscountCurve projectionCurve, IList<InterestPeriod> knownPeriods)
    {
      IList<InterestPeriod> interestPeriods = new List<InterestPeriod>();
      var schedParams = loan.GetScheduleParams();
      Schedule schedule = new Schedule(settle, schedParams.AccrualStartDate, schedParams.FirstCouponDate,
                                       schedParams.NextToLastCouponDate, schedParams.Maturity, schedParams.Frequency,
                                       schedParams.Roll, schedParams.Calendar, schedParams.CycleRule,
                                       schedParams.CashflowFlag);
      // Get next coupon date
      Dt nextCoupon = schedule.GetNextCouponDate(settle);

      if (method == LoanNextCouponTreatment.StubRate)
      {
        // Get current rate fixing from forward curve, floored if appropriate
        double cpn = ProjectedRate(loan, projectionCurve, nextCoupon) + curPricing;

        // Setup period
        var ip = InterestPeriodUtil.DefaultInterestPeriod(settle, loan, cpn, drawnPct);

        // Add
        interestPeriods.Add(ip);
      }
      else if (method == LoanNextCouponTreatment.CurrentFixing)
      {
        // Get current fixing end date
        Dt periodEnd = Dt.Add(asOf, loan.Frequency, loan.CycleRule == CycleRule.EOM);

        // Get current rate fixing from forward curve, floored if appropriate
        double cpn = ProjectedRate(loan, projectionCurve, periodEnd) + curPricing;

        // Setup period
        var ip = InterestPeriodUtil.DefaultInterestPeriod(settle, loan, cpn, drawnPct);
        // Add
        interestPeriods.Add(ip);
      }
      else if (method == LoanNextCouponTreatment.None)
      {
        // Setup period
        var ip = InterestPeriodUtil.DefaultInterestPeriod(settle, loan, curPricing, drawnPct);
        // Add
        interestPeriods.Add(ip);
      }
      else if (method == LoanNextCouponTreatment.InterestPeriods)
      {
        interestPeriods = knownPeriods;
      }
      else
        throw new ToolkitException(String.Format("The NextCouponTreatment of [{0}] is not supported!", method));

      // Done
      return interestPeriods;
    }

    private static double ProjectedRate(Loan loan, DiscountCurve projectionCurve, Dt periodEnd)
    {
      double r = projectionCurve.R(periodEnd);
      if (loan.LoanFloor.HasValue)
        r = Math.Max(r, loan.LoanFloor.Value);
      return r;
    }
  }
}
