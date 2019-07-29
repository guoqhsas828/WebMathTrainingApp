/*
 *  -2012. All rights reserved.
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Curves
{
  ///<summary>
  /// Factory class for predefined standard indices.
  ///</summary>
  public class StandardReferenceIndices
  {
    /// <summary>
    /// Create a standard reference index by name
    /// </summary>
    /// <param name="rateIndexKeyword">the keyword labelling the commonly used interest rate index</param>
    /// <returns>Rate Index</returns>
    public static ReferenceIndex Create(string rateIndexKeyword)
    {
      if (rateIndices_.ContainsKey(rateIndexKeyword.ToUpper()))
        return rateIndices_[rateIndexKeyword.ToUpper()];
      else
        throw new ArgumentException(String.Format("There is no pre-defined index {0}", rateIndexKeyword));
    }


    /// <summary>
    /// Get list of pre-defined reference indices
    /// </summary>
    /// <returns>List of defined indices</returns>
    public static ICollection<string> ListAllPredefinedRateIndexKeys()
    {
      return rateIndices_.Keys;
    }

    private static readonly Dictionary<string, ReferenceIndex> rateIndices_ = new Dictionary<string, ReferenceIndex> {
    {"USDLIBOR_3M", new InterestRateIndex("USDLIBOR", Frequency.Quarterly, Currency.USD, DayCount.Actual360, Calendar.NYB, 2)},
    {"USDLIBOR_6M", new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Actual360, Calendar.NYB, 2)},
    {"USDFUNDING_3M", new InterestRateIndex("USDFUNDING", Frequency.Quarterly, Currency.USD, DayCount.Actual360, Calendar.NYB, 2)},
    {"USDFUNDING_6M", new InterestRateIndex("USDFUNDING", Frequency.SemiAnnual, Currency.USD, DayCount.Actual360, Calendar.NYB, 2)},
    {"USDFEDFUNDS_1D", new InterestRateIndex("USDFEDFUNDS", Frequency.Daily, Currency.USD, DayCount.Actual360, Calendar.NYB, 2)},
    {"FFER", new InterestRateIndex("FFER", Frequency.Daily, Currency.USD, DayCount.Actual360, Calendar.NYB, 2)},
    {"EURIBOR_3M", new InterestRateIndex("EURLIBOR", Frequency.Quarterly, Currency.EUR, DayCount.Actual365Fixed, Calendar.TGT, 2)},
    {"EURIBOR_6M", new InterestRateIndex("EURLIBOR", Frequency.SemiAnnual, Currency.EUR, DayCount.Actual365Fixed, Calendar.TGT, 2)},
    {"EURLIBOR_3M", new InterestRateIndex("EURLIBOR", Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, 2)},
    {"EURLIBOR_6M", new InterestRateIndex("EURLIBOR", Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, 2)},
    {"EURFUNDING_3M", new InterestRateIndex("EURFUNDING", Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, 2)},
    {"EURFUNDING_6M", new InterestRateIndex("EURFUNDING", Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, 2)},
    {"EONIA", new InterestRateIndex("EONIA", Frequency.Daily, Currency.EUR, DayCount.Actual360, Calendar.TGT, 2)},
    {"GBPLIBOR_3M", new InterestRateIndex("GBPLIBOR", Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0)},
    {"GBPLIBOR_6M", new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0)},
    {"GBPFUNDING_3M", new InterestRateIndex("GBPFUNDING", Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0)},
    {"GBPFUNDING_6M", new InterestRateIndex("GBPFUNDING", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0)},
    {"SONIA", new InterestRateIndex("SONIA", Frequency.Daily, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0)},
    {"JPYLIBOR_3M", new InterestRateIndex("JPYLIBOR", Frequency.Quarterly, Currency.JPY, DayCount.Actual360, Calendar.TKB, 2)},
    {"JPYLIBOR_6M", new InterestRateIndex("JPYLIBOR", Frequency.SemiAnnual, Currency.JPY, DayCount.Actual360, Calendar.TKB, 2)},
    {"JPYFUNDING_3M", new InterestRateIndex("JPYFUNDING", Frequency.Quarterly, Currency.JPY, DayCount.Actual360, Calendar.TKB, 2)},
    {"JPYFUNDING_6M", new InterestRateIndex("JPYFUNDING", Frequency.SemiAnnual, Currency.JPY, DayCount.Actual360, Calendar.TKB, 2)},
    {"TONAR", new InterestRateIndex("TONAR", Frequency.Daily, Currency.JPY, DayCount.Actual365Fixed, Calendar.TKB, 2)},
    {"AUDLIBOR_3M", new InterestRateIndex("AUDLIBOR", Frequency.Quarterly, Currency.AUD, DayCount.Actual365Fixed, Calendar.SYB, 2)},
    {"AUDLIBOR_6M", new InterestRateIndex("AUDLIBOR", Frequency.SemiAnnual, Currency.AUD, DayCount.Actual365Fixed, Calendar.SYB, 2)},
    {"BBSW_3M", new InterestRateIndex("BBSW", Frequency.Quarterly, Currency.AUD, DayCount.Actual365Fixed, Calendar.SYB, 2)},
    {"BBSW_6M", new InterestRateIndex("BBSW", Frequency.SemiAnnual, Currency.AUD, DayCount.Actual365Fixed, Calendar.SYB, 2)},
    {"BBSY_3M", new InterestRateIndex("BBSY", Frequency.Quarterly, Currency.AUD, DayCount.Actual365Fixed, Calendar.SYB, 2)},
    {"BBSY_6M", new InterestRateIndex("BBSY", Frequency.SemiAnnual, Currency.AUD, DayCount.Actual365Fixed, Calendar.SYB, 2)},
    {"SARLIBOR_3M", new InterestRateIndex("SARLIBOR", Frequency.Quarterly, Currency.SBD, DayCount.Actual360, Calendar.RIB, 2)},
    {"SARLIBOR_6M", new InterestRateIndex("SARLIBOR", Frequency.SemiAnnual, Currency.SBD, DayCount.Actual360, Calendar.RIB, 2)},
    {"JIBAR_3M", new InterestRateIndex("JIBAR", Frequency.Quarterly, Currency.ZAR, DayCount.Actual365Fixed, Calendar.JOB, 2)},
    {"JIBAR_6M", new InterestRateIndex("JIBAR", Frequency.SemiAnnual, Currency.ZAR, DayCount.Actual365Fixed, Calendar.JOB, 2)},
    {"CADLIBOR_3M", new InterestRateIndex("CADLIBOR", Frequency.Quarterly, Currency.CAD, DayCount.Actual365Fixed, Calendar.TRB, 0)},
    {"CADLIBOR_6M", new InterestRateIndex("CADLIBOR", Frequency.SemiAnnual, Currency.CAD, DayCount.Actual365Fixed, Calendar.TRB, 0)},
    {"CORRA", new InterestRateIndex("CORRA", Frequency.Daily, Currency.CAD, DayCount.Actual365Fixed, Calendar.TRB, 0)},

    {"USDCMS_1Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("1Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_2Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("2Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_3Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("3Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_5Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("5Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_7Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("7Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_10Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("10Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_20Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("20Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_30Y_QUARTERLY", new SwapRateIndex("USDCMS_QUARTERLY", Tenor.Parse("30Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},

    {"USDCMS_1Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("1Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_2Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("2Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_3Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("3Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_5Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("5Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_7Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("7Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_10Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("10Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_20Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("20Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},
    {"USDCMS_30Y_SEMIANNUAL", new SwapRateIndex("USDCMS_SEMIANNUAL", Tenor.Parse("30Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, 
       new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, 2))},

    {"USDCMT_2Y_QUARTERLY", new SwapRateIndex("USDCMT_QUARTERLY", Tenor.Parse("2Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2,  
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_3Y_QUARTERLY", new SwapRateIndex("USDCMT_QUARTERLY", Tenor.Parse("3Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_5Y_QUARTERLY", new SwapRateIndex("USDCMT_QUARTERLY", Tenor.Parse("5Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActual, Calendar.NYB,2))},
    {"USDCMT_7Y_QUARTERLY", new SwapRateIndex("USDCMT_QUARTERLY", Tenor.Parse("7Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActual, Calendar.NYB,2))},
    {"USDCMT_10Y_QUARTERLY", new SwapRateIndex("USDCMT_QUARTERLY", Tenor.Parse("10Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_30Y_QUARTERLY", new SwapRateIndex("USDCMT_QUARTERLY", Tenor.Parse("30Y"), Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_2Y_SEMIANNUAL", new SwapRateIndex("USDCMT_SEMIANNUAL", Tenor.Parse("2Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_3Y_SEMIANNUAL", new SwapRateIndex("USDCMT_SEMIANNUAL", Tenor.Parse("3Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_5Y_SEMIANNUAL", new SwapRateIndex("USDCMT_SEMIANNUAL", Tenor.Parse("5Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_7Y_SEMIANNUAL", new SwapRateIndex("USDCMT_SEMIANNUAL", Tenor.Parse("7Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_10Y_SEMIANNUAL", new SwapRateIndex("USDCMT_SEMIANNUAL", Tenor.Parse("10Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},
    {"USDCMT_30Y_SEMIANNUAL", new SwapRateIndex("USDCMT_SEMIANNUAL", Tenor.Parse("30Y"), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Following, 2, 
      new InterestRateIndex("USDLIBOR", Frequency.SemiAnnual, Currency.USD, DayCount.ActualActualBond, Calendar.NYB,2))},

    {"EURCMS_1Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("1Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_2Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("2Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_3Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("3Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_5Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("5Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_7Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("7Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_10Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("10Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_20Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("20Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_30Y_QUARTERLY", new SwapRateIndex("EURCMS_QUARTERLY", Tenor.Parse("30Y"), Frequency.Quarterly, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},

    {"EURCMS_1Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("1Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_2Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("2Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_3Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("3Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_5Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("5Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_7Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("7Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_10Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("10Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_20Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("20Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},
    {"EURCMS_30Y_SEMIANNUAL", new SwapRateIndex("EURCMS_SEMIANNUAL", Tenor.Parse("30Y"), Frequency.SemiAnnual, Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, 2, 
      new InterestRateIndex("EURLIBOR", Frequency.Annual, Currency.EUR, DayCount.Actual360, Calendar.TGT,2))},

    {"GBPCMS_1Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("1Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_2Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("2Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_3Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("3Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_5Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("5Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_7Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("7Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_10Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("10Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_20Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("20Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_30Y_QUARTERLY", new SwapRateIndex("GBPCMS_QUARTERLY", Tenor.Parse("30Y"), Frequency.Quarterly, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},

    {"GBPCMS_1Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("1Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_2Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("2Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_3Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("3Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_5Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("5Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_7Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("7Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_10Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("10Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_20Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("20Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))},
    {"GBPCMS_30Y_SEMIANNUAL", new SwapRateIndex("GBPCMS_SEMIANNUAL", Tenor.Parse("30Y"), Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, 0, 
      new InterestRateIndex("GBPLIBOR", Frequency.SemiAnnual, Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0))}
  };

  }
}
