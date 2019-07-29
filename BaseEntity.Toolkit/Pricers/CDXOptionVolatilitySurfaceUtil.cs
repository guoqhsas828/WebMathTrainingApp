// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///  This class encapsulates CDX Option Volatility Surface input data.
  ///  The format of this input data may need to be converted before constructing a CalibratedVolatilitySurface
  ///  object from it which in turn will be passed into CDXOptionPricer
  /// </summary>
  public class CDXOptionVolatilitySurfaceInput
  {
    /// <summary>Constructor</summary>
    public CDXOptionVolatilitySurfaceInput(OptionStyle style, CDXOptionQuoteType quoteType, CDXOptionStrikeFormat strikeFormat,
                                           IList<double> strikes, IList<Dt> expiries, double? underlyingCDXQuote)
    {
      // NOTE: expiries are expected to be sorted here.
      PriceStrikes = new List<double>();
      SpreadStrikes = new List<double>();
      Expiries = new List<Dt>();
      Style = style;
      CDXOptionQuoteType = quoteType;
      StrikeFormat = strikeFormat;
      if (strikes == null || strikes.Count == 0 || expiries == null || expiries.Count == 0)
        return; // Invalid object
      if (StrikeFormat == CDXOptionStrikeFormat.PriceStrike) // Strikes are specified in the input as Price Strikes
        PriceStrikes = new List<double>(strikes);
      else // Otherwise assume Spread Strike
        SpreadStrikes = new List<double>(strikes);

      Expiries = new List<Dt>(expiries);
      PayerQuotes = new double[NumberOfStrikes,Expiries.Count]; // initialized to 0, which is an invalid vol. value
      ReceiverQuotes = new double[NumberOfStrikes,Expiries.Count]; // initialized to 0, which is an invalid vol. value
      AverageQuotes = new double[NumberOfStrikes,Expiries.Count]; // initialized to 0, which is an invalid vol. value
      UnderlyingCDXQuote = underlyingCDXQuote;
    }

    /// <summary>Option Style</summary>
    public OptionStyle Style { get; internal set; }

    /// <summary>CDX Option Quote type</summary>
    public CDXOptionQuoteType CDXOptionQuoteType { get; internal set; }

    /// <summary>CDX Option  strike format</summary>
    public CDXOptionStrikeFormat StrikeFormat { get; internal set; }

    /// <summary>Price strikes; these are input if the input strike format is price, or computed otherwise. Expressed in percent, i.e. we will have 80 instead of 0.8</summary>
    public List<double> PriceStrikes { get; internal set; }

    /// <summary>Spread strikes; these are input if the input strike format is spread, or computed otherwise. Expressed in B.P, i.e. we will have 80 instead of 0.008</summary>
    public List<double> SpreadStrikes { get; internal set; }

    /// <summary>Unique expiries (sorted)</summary>
    public List<Dt> Expiries { get; internal set; }

    /// <summary>Quotes for payer options; item (i, j) corresponds to strike i and expiry j</summary>
    public double[,] PayerQuotes { get; internal set; }

    /// <summary>Quotes for receiver options; item (i, j) corresponds to strike i and expiry j</summary>
    public double[,] ReceiverQuotes { get; internal set; }

    /// <summary>Average of payer and receiver quotes (computed); item (i, j) corresponds to strike i and expiry j</summary>
    public double[,] AverageQuotes { get; internal set; }

    /// <summary>Underlying CDX quote (spread or price)</summary>
    public double? UnderlyingCDXQuote { get; internal set; }

    /// <summary>Total number of strikes (price strikes or spread strikes, depending on input strike format)</summary>
    public int NumberOfStrikes
    {
      get { return (StrikeFormat == CDXOptionStrikeFormat.PriceStrike ? PriceStrikes.Count : SpreadStrikes.Count); }
    }

    /// <summary>Price or Spread Strikes, depending on StrikeFormat</summary>
    public List<double> Strikes
    {
      get { return (StrikeFormat == CDXOptionStrikeFormat.PriceStrike ? PriceStrikes : SpreadStrikes); }
    }

    /// <summary>Set a quote given strike, expiry and option type</summary>
    public void SetQuote(double strike, Dt expiry, PayerReceiver optType, double vol)
    {
      if (!CDXOptionVolatilitySurfaceUtil.IsValidVolValue(vol))
        return; // Perhaps throw an exception
      int strikeInd = (StrikeFormat == CDXOptionStrikeFormat.PriceStrike ? FindIndexOfStrike(PriceStrikes, strike) : FindIndexOfStrike(SpreadStrikes, strike));
      if (strikeInd < 0) return; // Perhaps throw an exception
      int expiryInd = Expiries.IndexOf(expiry);
      if (expiryInd < 0) return; // Perhaps throw an exception
      if (optType == PayerReceiver.Payer)
        PayerQuotes[strikeInd, expiryInd] = vol;
      else
        ReceiverQuotes[strikeInd, expiryInd] = vol;
    }

    private static int FindIndexOfStrike(IList<double> strikes, double strk)
    {
      if (strikes == null || strikes.Count == 0) return -1;
      for (int i = 0; i < strikes.Count; i++)
      {
        if (strk.ApproximatelyEqualStrike(strikes[i])) return i;
      }
      return -1;
    }

    /// <summary>Get a quote given strike, expiry and option type</summary>
    public double GetQuote(double strike, Dt expiry, PayerReceiver optType)
    {
      int strikeInd = (StrikeFormat == CDXOptionStrikeFormat.PriceStrike ? FindIndexOfStrike(PriceStrikes, strike) : FindIndexOfStrike(SpreadStrikes, strike));
      if (strikeInd < 0) return 0.0; // Invalid vol. value
      int expiryInd = Expiries.IndexOf(expiry);
      if (expiryInd < 0) return 0.0; // Invalid vol. value
      if (optType == PayerReceiver.Payer)
        return PayerQuotes[strikeInd, expiryInd]; // May again return 0, if the value was not set
      else
        return ReceiverQuotes[strikeInd, expiryInd];
    }

    /// <summary>
    ///  This function will return the average of payer and receiver quotes, if both are present.
    ///  Otherwise, if only payer or only receiver is available, will return that.
    /// </summary>
    internal double GetQuote(double strike, Dt expiry)
    {
      int strikeInd = (StrikeFormat == CDXOptionStrikeFormat.PriceStrike ? FindIndexOfStrike(PriceStrikes, strike) : FindIndexOfStrike(SpreadStrikes, strike));
      if (strikeInd < 0) return 0.0; // Invalid vol. value
      int expiryInd = Expiries.IndexOf(expiry);
      if (expiryInd < 0) return 0.0; // Invalid vol. value

      if (CDXOptionVolatilitySurfaceUtil.IsValidVolValue(PayerQuotes[strikeInd, expiryInd]) &&
          CDXOptionVolatilitySurfaceUtil.IsValidVolValue(ReceiverQuotes[strikeInd, expiryInd]))
        return (PayerQuotes[strikeInd, expiryInd] + ReceiverQuotes[strikeInd, expiryInd]) * 0.5;
      else if (CDXOptionVolatilitySurfaceUtil.IsValidVolValue(PayerQuotes[strikeInd, expiryInd]))
        return PayerQuotes[strikeInd, expiryInd];
      else
        return ReceiverQuotes[strikeInd, expiryInd];
    }

    /// <summary>Is the object valid</summary>
    public bool IsValid()
    {
      return (NumberOfStrikes > 0 && Expiries.Count > 0 && PayerQuotes != null && ReceiverQuotes != null);
    }

    /// <summary>Are all implied volatility values invalid (0 or NaN) ?</summary>
    public bool AreAllAverageValuesInvalid()
    {
      if (!IsValid()) return false;
      for (int i = 0; i < NumberOfStrikes; i++)
        for (int j = 0; j < Expiries.Count; j++)
        {
          if (CDXOptionVolatilitySurfaceUtil.IsValidVolValue(this.AverageQuotes[i, j]))
            return false;
        }
      return true;
    }

    /// <summary>Clone the object</summary>
    public CDXOptionVolatilitySurfaceInput Clone()
    {
      CDXOptionVolatilitySurfaceInput ret = (CDXOptionVolatilitySurfaceInput)this.MemberwiseClone();
      ret.PriceStrikes = new List<double>(PriceStrikes);
      ret.SpreadStrikes = new List<double>(SpreadStrikes);
      ret.Expiries = new List<Dt>(Expiries);
      if (PayerQuotes != null)
        ret.PayerQuotes = CloneUtil.Clone(PayerQuotes);
      if (ReceiverQuotes != null)
        ret.ReceiverQuotes = CloneUtil.Clone(ReceiverQuotes);
      if (AverageQuotes != null)
        ret.AverageQuotes = CloneUtil.Clone(AverageQuotes);
      ret.UnderlyingCDXQuote = UnderlyingCDXQuote;
      return ret;
    }

    /// <summary>Compute the average of payer and receiver quotes</summary>
    internal void SetAverageQuotes()
    {
      if (!IsValid())
        return;
      int i, j;
      if (AverageQuotes == null)
        AverageQuotes = new double[NumberOfStrikes,Expiries.Count];
      for (i = 0; i < NumberOfStrikes; i++)
        for (j = 0; j < Expiries.Count; j++)
        {
          if (CDXOptionVolatilitySurfaceUtil.IsValidVolValue(PayerQuotes[i, j]) && CDXOptionVolatilitySurfaceUtil.IsValidVolValue(ReceiverQuotes[i, j]))
            AverageQuotes[i, j] = (PayerQuotes[i, j] + ReceiverQuotes[i, j]) * 0.5;
          else if (CDXOptionVolatilitySurfaceUtil.IsValidVolValue(PayerQuotes[i, j]))
            AverageQuotes[i, j] = PayerQuotes[i, j];
          else
            AverageQuotes[i, j] = ReceiverQuotes[i, j];
        }
    }
  }

  /// <summary>Utility functions to convert the format of CDX Option Volatility Surface input</summary>
  public static class CDXOptionVolatilitySurfaceUtil
  {
    /// <summary>Is the value a valid volatility or price quote of a CDX option?</summary>
    public static bool IsValidVolValue(double v)
    {
      if (Utils.IsFinite(v) && v > 0.0) return true;
      return false;
    }

    /// <summary>
    /// Convert the format of input CDX option quotes to be able to construct calibrated volatility surface. 
    /// See the word document attached in FB 34929 for an explanation of why we need to convert this format. See also Excel example CDX Option Pricer
    /// (Volatility Surface page) whose functionality we are trying to replicate here.
    /// </summary>
    public static CDXOptionVolatilitySurfaceInput ConvertCDXOptionVolatilitySurfaceInput(CDXOptionVolatilitySurfaceInput orig,
                                                                                         CDXOptionQuoteType newQuoteType, CDXOptionStrikeFormat newStrikeFormat,
                                                                                         Dt asOf, Dt settle, DiscountCurve dc,
                                                                                         bool isQuotedInPrice, CDX note,
                                                                                         bool useModifiedBlackForImpliedSpreadVol = false,
                                                                                         double recoveryRate = DefaultRecoveryRate)
    {
      // When converting from price to spread vol. use ModelType = CDXOptionModelType.ModifiedBlack when useModifiedBlackForImpliedSpreadVol = true,
      // and use ModelType = CDXOptionModelType.Black if useModifiedBlackForImpliedSpreadVol = false.
      // When converting from price to price vol., use ModelType = CDXOptionModelType.BlackPrice
      if (orig == null || !orig.IsValid() || note == null)
        return null;
      // NOTE: in this function we assume that orig.CDXOptionQuoteType is not None and newQuoteType is not None

      CDXOptionVolatilitySurfaceInput ret = orig.Clone();
      if (newQuoteType == orig.CDXOptionQuoteType && newStrikeFormat == orig.StrikeFormat)
      {
        ret.SetAverageQuotes();
        return ret; // Do not need to convert.
      }

      if (!orig.UnderlyingCDXQuote.HasValue)
        throw new ToolkitException("Need a valid underlying CDX quote to convert price volatility to spread volatility");
      double cdxQuote = orig.UnderlyingCDXQuote.Value;

      // In the new object, we will need to populate PriceStrikes if SpreadStrikes were originally specified,
      // and, vice versa, SpreadStrikes if PriceStrikes were originally specified, even if the strike format does
      // not change, because we need both price strikes and spread strikes in order to convert from spread vol to price vol
      // and vice versa.
      var cdxPricer = new CDXPricer(note, asOf, settle, dc, 0.01);
      cdxPricer.MarketRecoveryRate = recoveryRate;
      double x;
      int i, j;
      if (orig.StrikeFormat == CDXOptionStrikeFormat.PriceStrike)
      {
        // Convert price strike to spread strike. Reference: qCDXPriceToSpread() function.
        cdxPricer.QuotingConvention = QuotingConvention.FlatPrice;
        ret.SpreadStrikes = new List<double>(orig.PriceStrikes);
        for (i = 0; i < orig.PriceStrikes.Count; i++)
        {
          x = cdxPricer.PriceToSpread(orig.PriceStrikes[i] / 100) * 10000;
          ret.SpreadStrikes[i] = x;
        }
      }
      else
      {
        // Convert spread strike to price strike. Reference: qCDXSpreadToPrice() function.
        cdxPricer.QuotingConvention = QuotingConvention.CreditSpread;
        ret.PriceStrikes = new List<double>(orig.SpreadStrikes);
        for (i = 0; i < orig.SpreadStrikes.Count; i++)
        {
          x = cdxPricer.SpreadToPrice(orig.SpreadStrikes[i] / 10000.0) * 100;
          ret.PriceStrikes[i] = x;
        }
      }
      ret.StrikeFormat = newStrikeFormat;
      // Now convert the actual quotes, if needed.
      if (newQuoteType == orig.CDXOptionQuoteType)
      {
        ret.SetAverageQuotes();
        return ret;
      }
      ret.CDXOptionQuoteType = newQuoteType;
      if (orig.CDXOptionQuoteType == CDXOptionQuoteType.Price) // Case 1: compute implied vol. from option price (price vol or spread vol)
      {
        // Reference: qCDXOptionCalcImpliedVolatilityTable() function.
        // Calc. implied vol for payer and receiver sides separately.
        // Will produce the Spread Vol or price vol - whichever is needed.
        // Then take average of payer and receiver quote.

        var modelData = new CDXOptionModelData();
        CDXOptionModelType modelType = CDXOptionModelType.BlackPrice;
        if (newQuoteType == CDXOptionQuoteType.SpreadVol && useModifiedBlackForImpliedSpreadVol)
          modelType = CDXOptionModelType.ModifiedBlack;
        else if (newQuoteType == CDXOptionQuoteType.SpreadVol && !useModifiedBlackForImpliedSpreadVol)
          modelType = CDXOptionModelType.Black;
        // CDX price expressed in %, while spread in B.P.
        var cdxMarketQuote = new MarketQuote(cdxQuote, isQuotedInPrice ? QuotingConvention.FlatPrice : QuotingConvention.CreditSpread);

        // ret.AverageQuotes = new double[ret.NumberOfStrikes, ret.Expiries.Count];  // all set to 0
        for (i = 0; i < orig.NumberOfStrikes; i++)
          for (j = 0; j < orig.Expiries.Count; j++)
          {
            // Init all values to invalid value:
            ret.PayerQuotes[i, j] = 0.0;
            ret.ReceiverQuotes[i, j] = 0.0;
            ret.AverageQuotes[i, j] = 0.0;
            double strk = (orig.StrikeFormat == CDXOptionStrikeFormat.PriceStrike
                                                  ? (orig.PriceStrikes[i] * 0.01)
                                                  : (orig.SpreadStrikes[i] * 0.0001));
            // Do payer and receiver side separately
            if (IsValidVolValue(orig.PayerQuotes[i, j]))
            {
              CDXOption cdxo = new CDXOption(note.Effective, orig.Expiries[j], note.Effective, note.Maturity,
                                             Currency.None, note.Premium, note.DayCount, note.Freq, note.BDConvention,
                                             note.Calendar, PayerReceiver.Payer, orig.Style,
                                             strk, (orig.StrikeFormat == CDXOptionStrikeFormat.PriceStrike));

              ICreditIndexOptionPricer pricer = cdxo.CreatePricer(asOf, settle, dc, cdxMarketQuote, Dt.Empty, recoveryRate, 0,
                null, modelType, modelData, null, 1.0, null);

              try
              {
                ret.PayerQuotes[i, j] = pricer.ImplyVolatility(orig.PayerQuotes[i, j] * 0.0001);                
              }
              catch (Exception ex)
              {
                throw new ToolkitException("Error computing implied volatility for a CDX option quote: expiration " + orig.Expiries[j] + ", strike " + strk + ", payer quote " + orig.PayerQuotes[i, j] + " Error:\n" + ex.Message);
              }
            }
            if (IsValidVolValue(orig.ReceiverQuotes[i, j]))
            {
              CDXOption cdxo = new CDXOption(note.Effective, orig.Expiries[j], note.Effective, note.Maturity,
                                             Currency.None, note.Premium, note.DayCount, note.Freq, note.BDConvention,
                                             note.Calendar, PayerReceiver.Receiver, orig.Style,
                                             strk, (orig.StrikeFormat == CDXOptionStrikeFormat.PriceStrike));

              ICreditIndexOptionPricer pricer = cdxo.CreatePricer(asOf, settle, dc, cdxMarketQuote, Dt.Empty, recoveryRate, 0,
                null, modelType, modelData, null, 1.0, null);

              try
              {
                ret.ReceiverQuotes[i, j] = pricer.ImplyVolatility(orig.ReceiverQuotes[i, j] * 0.0001);
              }
              catch (Exception ex)
              {
                throw new ToolkitException("Error computing implied volatility for a CDX option quote: expiration " + orig.Expiries[j] + ", strike " + strk + ", receiver quote " + orig.ReceiverQuotes[i, j] + " Error:\n" + ex.Message);
              }
            }
          }
        ret.SetAverageQuotes();
        return ret;
      }
      else if (orig.CDXOptionQuoteType == CDXOptionQuoteType.PriceVol && newQuoteType == CDXOptionQuoteType.SpreadVol) // Case 2: convert price vol to spread vol
      {
        // Reference: qPriceVolToSpreadVol()
        ret.SetAverageQuotes();
        for (i = 0; i < orig.NumberOfStrikes; i++)
          for (j = 0; j < orig.Expiries.Count; j++)
          {
            if (IsValidVolValue(ret.AverageQuotes[i, j]))
            {
              double iv = PriceVolToSpreadVol(note, dc, ret.AverageQuotes[i, j], ret.SpreadStrikes[i], ret.PriceStrikes[i], orig.Expiries[j],
                                              cdxQuote, isQuotedInPrice, recoveryRate);
              ret.AverageQuotes[i, j] = iv;
            }
          }
        return ret;
      }
      else if (orig.CDXOptionQuoteType == CDXOptionQuoteType.SpreadVol && newQuoteType == CDXOptionQuoteType.PriceVol) // Case 3: convert spread vol to price vol
      {
        // Reference: qSpreadVolToPriceVol()
        ret.SetAverageQuotes();
        for (i = 0; i < orig.NumberOfStrikes; i++)
          for (j = 0; j < orig.Expiries.Count; j++)
          {
            if (IsValidVolValue(ret.AverageQuotes[i, j]))
            {
              double iv = SpreadVolToPriceVol(note, dc, ret.AverageQuotes[i, j], ret.SpreadStrikes[i], ret.PriceStrikes[i], orig.Expiries[j],
                                              cdxQuote, isQuotedInPrice, recoveryRate);
              ret.AverageQuotes[i, j] = iv;
            }
          }
        return ret;
      }
      /* Do not need this case for now ...
      else  // Case 4: convert spread vol or price vol to option price
      {
      }
      */
      return null; // to satisfy compiler - should not happen in reality ...
    }

    /// <summary>
    /// Convert an object of type CDXOptionVolatilitySurfaceInput to CalibratedVolatilitySurface format, which can be passed into the toolkit CDX option pricer.
    /// </summary>
    public static CalibratedVolatilitySurface ConvertToCalibratedVolatilitySurface(Dt asOf, CDXOptionVolatilitySurfaceInput inp, Interp strikeInterp,
                                                                                   Interp timeInterp)
    {
      // Reference: qVolatilitySurface()
      if (inp == null || !inp.IsValid())
        return null; // Perhaps throw?
      var tenors = new List<PlainVolatilityTenor>();
      List<double> strikes = (inp.StrikeFormat == CDXOptionStrikeFormat.PriceStrike ? inp.PriceStrikes : inp.SpreadStrikes);
      for (int t = 0; t < inp.Expiries.Count; ++t)
      {
        if (t > 0 && inp.Expiries[t - 1] >= inp.Expiries[t])
        {
          throw new ArgumentException("Expiries are out of order.");
        }
        var stks = new List<double>();
        var vols = new List<double>();
        for (int i = 0; i < strikes.Count; ++i)
        {
          if (strikes[i] < 0 || !IsValidVolValue(inp.AverageQuotes[i, t])) continue; // replace volatilities[i, t] ... with inp.AverageQuotes[i, t]
          stks.Add(inp.StrikeFormat == CDXOptionStrikeFormat.PriceStrike ? strikes[i] * 0.01 : strikes[i] * 0.0001);
          vols.Add(inp.AverageQuotes[i, t]);
        }
        if (vols.Count <= 0) continue;
        string name = Tenor.FromDateInterval(asOf, inp.Expiries[t]).ToString();
        tenors.Add(new PlainVolatilityTenor(name, inp.Expiries[t])
                   {
                     Strikes = stks.ToArray(),
                     Volatilities = vols.ToArray()
                   });
      }
      if (tenors.Count == 0)
      {
        throw new ArgumentException("No valid volatility/strike pair.");
      }
      if (strikeInterp == null)
      {
        strikeInterp = InterpScheme.InterpFromName("TensionC1", ExtrapMethod.Smooth, 0.0, 10.0);
      }
      if (timeInterp == null)
      {
        timeInterp = InterpScheme.InterpFromName("Linear", ExtrapMethod.Smooth, 0.0, 10.0);
      }
      var vinterp = new VolatilityPlainInterpolator(strikeInterp, timeInterp);
      return new CalibratedVolatilitySurface(asOf, tenors.ToArray(), null, vinterp);
    }

    /// <summary>Convert spread volatility to price volatility</summary>
    public static double SpreadVolToPriceVol(
      CDX note,
      DiscountCurve dc,
      double spreadVol,
      double spreadStrike,
      double priceStrike,
      Dt expiry,
      double underlyingCDXQuote,
      bool isCDXQuotedInPrice,
      double recoveryRate = DefaultRecoveryRate
      )
    {
      // Convert spread vol to price vol. Copy code from qSpreadVolToPriceVol().
      Dt asOf = expiry;
      Dt settle = Dt.AddDays(expiry, 1, (note.Calendar != Calendar.None) ? note.Calendar : Calendar.NYB);
      //double qt = (isCDXQuotedInPrice ? underlyingCDXQuote * 0.01 : underlyingCDXQuote * 0.0001);
      double qt = underlyingCDXQuote;
      var cdxPricer2 = new CDXPricer(note, asOf, settle, dc, qt);
      if (isCDXQuotedInPrice)
        cdxPricer2.QuotingConvention = QuotingConvention.FlatPrice;
      else
        cdxPricer2.QuotingConvention = QuotingConvention.CreditSpread;
      cdxPricer2.MarketRecoveryRate = recoveryRate;
      if (priceStrike > 0)
      {
        double pv01 = cdxPricer2.RiskyDuration();
        return spreadVol * pv01 * spreadStrike / priceStrike / 100;
      }
      return -1;
    }

    /// <summary>Convert price volatility to spread volatility</summary>
    public static double PriceVolToSpreadVol(
      CDX note,
      DiscountCurve dc,
      double priceVol,
      double spreadStrike,
      double priceStrike,
      Dt expiry,
      double underlyingCDXQuote,
      bool isCDXQuotedInPrice,
      double recoveryRate = DefaultRecoveryRate
      )
    {
      // Convert price vol to spread vol. Copy code from qPriceVolToSpreadVol().
      Dt asOf = expiry;
      Dt settle = Dt.AddDays(expiry, 1, (note.Calendar != Calendar.None) ? note.Calendar : Calendar.NYB);
      //double qt = (isCDXQuotedInPrice ? underlyingCDXQuote * 0.01 : underlyingCDXQuote * 0.0001);
      double qt = underlyingCDXQuote;
      var cdxPricer2 = new CDXPricer(note, asOf, settle, dc, qt);
      if (isCDXQuotedInPrice)
        cdxPricer2.QuotingConvention = QuotingConvention.FlatPrice;
      else
        cdxPricer2.QuotingConvention = QuotingConvention.CreditSpread;
      cdxPricer2.MarketRecoveryRate = recoveryRate;
      if (priceStrike > 0)
      {
        double pv01 = cdxPricer2.RiskyDuration();
        return 100 * priceVol * priceStrike / spreadStrike / pv01;
      }
      return -1;
    }

    /// <summary>
    ///  Check if strike x can be assumed to be equal to y
    /// </summary>
    public static bool ApproximatelyEqualStrike(this double x, double y)
    {
      // Note: the ApproximatelyEqualsTo function is not sufficient because it reports two numbers as distict with very small difference, for example 1.0675000000000001 vs 1.0675
      return Math.Abs(x - y) < StrikeComparisonPrecision;
    }

    /// <summary>
    ///  Check if a CDX option strike can be assumed to be equal to another strike
    /// </summary>
    public const double StrikeComparisonPrecision = 1.0e-14;

    /// <summary>Default recovery rate for CDX; may be passed as a parameter</summary>
    public const double DefaultRecoveryRate = 0.4;
  }
}