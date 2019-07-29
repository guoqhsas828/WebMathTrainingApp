/*
 *  -2012. All rights reserved.
 */
using System;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

namespace BaseEntity.Toolkit.Curves
{
  ///<summary>
  /// The utility class used to generate commonly-used market terms of products used for calibration 
  ///</summary>
  public static class RateCurveTermsUtil
  {
    #region Methods
    ///<summary>
    /// This method extract the pre-defined market terms of products used for curve calibration for some commonly-used cases
    ///</summary>
    ///<param name="curveTermKeyword">The identifier to locate a curve term in cache</param>
    ///<returns>Rate Curve Term</returns>
    public static CurveTerms CreateDefaultCurveTerms(string curveTermKeyword)
    {
      if (String.IsNullOrEmpty(curveTermKeyword))
        throw new ArgumentException("Invalid pre-defined curve term");
      if (rateCurveTermsCache_.ContainsKey(curveTermKeyword.ToUpper()))
        return rateCurveTermsCache_[curveTermKeyword.ToUpper()];
      throw new ArgumentException(String.Format("There is no pre-defined curve term {0}", curveTermKeyword));
    }

    ///<summary>
    /// This method tries to find the pre-defined market terms of products used for curve calibration without throwing exception when not available
    ///</summary>
    ///<param name="curveTermKeyword">The identifier to locate a curve term in cache</param>
    ///<param name="defaultTerm">Rate Curve Term</param>
    ///<returns>true/false</returns>
    public static bool TryGetDefaultCurveTerms(string curveTermKeyword, out CurveTerms defaultTerm)
    {
      if (rateCurveTermsCache_.TryGetValue(curveTermKeyword, out defaultTerm))
      {
        return true;
      }
      defaultTerm = null;
      return false;
    }

    ///<summary>
    /// Get list of pre-defined rate curve terms
    ///</summary>
    ///<returns>List of defined terms</returns>
    public static ICollection<string> ListAllPredefinedRateCurveTerms()
    {
      return rateCurveTermsCache_.Keys;
    }

    public static Calendar GetAssetCalendar(CurveTerms termsInfo, InstrumentType type, string name)
    {
      var retVal = termsInfo.ReferenceIndex.Calendar;
      switch (type)
      {
        case InstrumentType.BasisSwap:
          BasisSwapAssetCurveTerm bsTerm;
          if (termsInfo.TryGetInstrumentTerm(type, name, out bsTerm))
            retVal = bsTerm.SpotCalendar;
          break;
        case InstrumentType.Swap:
          SwapAssetCurveTerm swapTerm;
          if (termsInfo.TryGetInstrumentTerm(type, name, out swapTerm))
            retVal = swapTerm.Calendar;
          break;
        default:
          AssetRateCurveTerm t;
          if (termsInfo.TryGetInstrumentTerm(type, name, out t))
            retVal = t.Calendar;
          break;
      }
      return retVal;
    }


    public static int GetAssetSpotDays(CurveTerms termsInfo, InstrumentType type, string name)
    {
      int retVal = (type == InstrumentType.FRA) ? termsInfo.ReferenceIndex.SettlementDays : 0;
      switch (type)
      {
        case InstrumentType.BasisSwap:
          BasisSwapAssetCurveTerm bsTerm;
          if (termsInfo.TryGetInstrumentTerm(type, name, out bsTerm))
            return bsTerm.SpotDays;
          break;
        case InstrumentType.Swap:
          SwapAssetCurveTerm swapTerm;
          if (termsInfo.TryGetInstrumentTerm(type, name, out swapTerm))
            return swapTerm.SpotDays;
          break;
        default:
          AssetRateCurveTerm t;
          if (termsInfo.TryGetInstrumentTerm(type, name, out t))
            return t.SpotDays;
          break;
      }
      return retVal;
    }

    public static BDConvention GetAssetBDConvention(CurveTerms termsInfo, InstrumentType type, string name)
    {
      var retVal = termsInfo.ReferenceIndex.Roll;
      switch (type)
      {
        case InstrumentType.Swap:
          SwapAssetCurveTerm swapTerm;
          if (termsInfo.TryGetInstrumentTerm(type, name, out swapTerm))
            return swapTerm.BDConvention;
          break;
        default:
          AssetRateCurveTerm t;
          if (termsInfo.TryGetInstrumentTerm(type, name, out t))
            return t.BDConvention;
          break;
      }
      return retVal;
    }


    public static DayCount GetAssetDayCount(CurveTerms termsInfo, InstrumentType type, string instrumentName)
    {
      var retVal = termsInfo.ReferenceIndex.DayCount;
      switch (type)
      {
        case InstrumentType.Swap:
          SwapAssetCurveTerm swapTerm;
          if (termsInfo.TryGetInstrumentTerm(type, instrumentName, out swapTerm))
           return swapTerm.DayCount;
          break;
        default:
          AssetRateCurveTerm t;
          if (termsInfo.TryGetInstrumentTerm(type, instrumentName, out t))
            return t.DayCount;
          break;
      }
      return retVal;
    }

    public static Frequency GetAssetPaymentFrequency(CurveTerms termsInfo, InstrumentType type, string instrumentName)
    {
      var retVal = termsInfo.ReferenceIndex.IndexTenor.ToFrequency();
      switch (type)
      {
        case InstrumentType.Swap:
          SwapAssetCurveTerm swapTerm;
          if (termsInfo.TryGetInstrumentTerm(type, instrumentName, out swapTerm))
            return swapTerm.PayFreq;
          break;
        default:
          AssetRateCurveTerm t;
          if (termsInfo.TryGetInstrumentTerm(type, instrumentName, out t))
            return t.PayFreq;
          break;
      }
      return retVal;
    }

    /// <summary>
    /// Get reference index/indexes 
    /// </summary>
    /// <param name="termsInfo">Asset terms</param>
    /// <param name="type">Asset type</param>
    /// <param name="instrumentName">Instrument name</param>
    /// <returns></returns>
    public static IEnumerable<ReferenceIndex> GetAssetReferenceIndex(CurveTerms termsInfo, InstrumentType type, string instrumentName)
    {
      switch (type)
      {
        case InstrumentType.Swap:
          SwapAssetCurveTerm swapTerm;
          if (termsInfo.TryGetInstrumentTerm(type, instrumentName, out swapTerm))
            yield return swapTerm.ReferenceIndex;
          break;
        case InstrumentType.BasisSwap:
          BasisSwapAssetCurveTerm bsTerm;
          if (termsInfo.TryGetInstrumentTerm(type, instrumentName, out bsTerm))
          {
            yield return bsTerm.ReceiverIndex;
            yield return bsTerm.PayerIndex;
          }
          break;
        default:
          AssetRateCurveTerm t;
          if (termsInfo.TryGetInstrumentTerm(type, instrumentName, out t))
            yield return t.ReferenceIndex;
          break;
      }
      yield return termsInfo.ReferenceIndex;
    }


    /// <summary>
    /// Get payment settings from market conventions
    /// </summary>
    /// <param name="terms">Market conventions</param>
    /// <param name="instrumentName">Instrument identifier (sub-classification of InstrumentType)</param>
    /// <returns>PaymentSettings</returns>
    public static PaymentSettings GetPaymentSettings(CurveTerms terms, string instrumentName)
    {
      PaymentSettings retVal;
      AssetCurveTerm term;
      if (terms.TryGetInstrumentTerm(ConvertInstrumentType(instrumentName, true, InstrumentType.None), instrumentName, out term))
      {
        switch (term.Type)
        {
          case InstrumentType.Swap:
            var swapTerm = (SwapAssetCurveTerm)term;
            retVal = new PaymentSettings
                     {
                       PayCompoundingFreq = swapTerm.CompoundingFreq,
                       RecProjectionType = swapTerm.ProjectionType,
                       RecCompoundingFreq = swapTerm.FloatCompoundingFreq,
                       RecCompoundingConvention = swapTerm.FloatCompoundingConvention
                     };
            break;
          case InstrumentType.BasisSwap:
            var bsTerm = (BasisSwapAssetCurveTerm)term;
            retVal = new PaymentSettings
                     {
                       RecProjectionType = bsTerm.RecProjectionType,
                       RecCompoundingFreq = bsTerm.RecCompoundingFreq,
                       PayProjectionType = bsTerm.PayProjectionType,
                       PayCompoundingFreq = bsTerm.PayCompoundingFreq,
                       RecCompoundingConvention = bsTerm.RecCompoundingConvention,
                       PayCompoundingConvention = bsTerm.PayCompoundingConvention,
                       SpreadOnReceiver = bsTerm.SpreadOnReceiver
                     };
            break;
          default:
            var defaultTerm = (AssetRateCurveTerm)term;
            retVal = new PaymentSettings
                     {
                       RecProjectionType = defaultTerm.ProjectionType
                     };
            break;
        }
      }
      else
        retVal = new PaymentSettings();
      return retVal;
    }


    /// <summary>
    /// Merge target terms and projection terms (for backward compatibility)
    /// </summary>
    /// <param name="instrumentNames">Calibration instrument identifiers</param>
    /// <param name="targetTerms">Target RateCurveTerms</param>
    /// <param name="projectionTerms">Projection RateCurveTerms</param>
    /// <param name="targetIsFunding">Projection RateCurveTerms</param>
    /// <returns>Merged terms</returns>
    public static CurveTerms Merge(this CurveTerms targetTerms, string[] instrumentNames, CurveTerms projectionTerms, bool targetIsFunding)
    {
      if (targetTerms == null)
        throw new ArgumentNullException("targetTerms");
      if (projectionTerms == null)
        return targetTerms;
      var allTerms = new Dictionary<string, AssetCurveTerm>();
      foreach (var instrumentName in instrumentNames.Where(inst => !String.IsNullOrEmpty(inst)).Distinct())
      {
        AssetCurveTerm term;
        if (targetTerms.TryGetInstrumentTerm(ConvertInstrumentType(instrumentName, true, InstrumentType.None), instrumentName, out term))
        {
          if (allTerms.ContainsKey(term.AssetKey))
            continue;
          var retVal = (AssetCurveTerm)term.ShallowCopy();
          var basisSwapAssetTerm = retVal as BasisSwapAssetCurveTerm;
          if (basisSwapAssetTerm != null && (basisSwapAssetTerm.PayerIndex == null || basisSwapAssetTerm.PayerIndex.IsEqualToAnyOf(projectionTerms.ReferenceIndices)))
          {
            if (targetIsFunding)
            {
              SwapAssetCurveTerm swapTerm;
              if (TryGetMatchingSwapTerm(basisSwapAssetTerm.PayerIndex, projectionTerms.AssetTerms, out swapTerm))
              {
                var swapAssetTerm = (SwapAssetCurveTerm)swapTerm.ShallowCopy();
                if (swapAssetTerm.ReferenceIndex == null)
                  swapAssetTerm.ReferenceIndex = projectionTerms.ReferenceIndex;
                if (basisSwapAssetTerm.PayerIndex == null)
                  basisSwapAssetTerm.PayerIndex = swapAssetTerm.ReferenceIndex;
                basisSwapAssetTerm.PayFreq = swapAssetTerm.FloatPayFreq;
                if (basisSwapAssetTerm.PayCompoundingFreq == Frequency.None)
                  basisSwapAssetTerm.PayCompoundingFreq = swapAssetTerm.FloatCompoundingFreq;
                if (basisSwapAssetTerm.PayCompoundingConvention == CompoundingConvention.None)
                  basisSwapAssetTerm.PayCompoundingConvention = swapAssetTerm.FloatCompoundingConvention;
                if (basisSwapAssetTerm.PayProjectionType == ProjectionType.None)
                  basisSwapAssetTerm.PayProjectionType = swapAssetTerm.ProjectionType;
                allTerms[swapTerm.AssetKey] = swapAssetTerm;
              }
              else
                basisSwapAssetTerm.PayerIndex = projectionTerms.ReferenceIndex;
            }
            else
            {
              BasisSwapAssetCurveTerm projectionBasisSwapTerm;
              if (projectionTerms.TryGetInstrumentTerm(InstrumentType.BasisSwap, out projectionBasisSwapTerm))
              {
                if (basisSwapAssetTerm.PayerIndex == null)
                  basisSwapAssetTerm.PayerIndex = projectionBasisSwapTerm.ReceiverIndex ?? projectionTerms.ReferenceIndex;
                if (basisSwapAssetTerm.PayFreq == Frequency.None)
                  basisSwapAssetTerm.PayFreq = projectionBasisSwapTerm.RecFreq;
                if (basisSwapAssetTerm.PayProjectionType == ProjectionType.None)
                  basisSwapAssetTerm.PayProjectionType = projectionBasisSwapTerm.RecProjectionType;
                if (basisSwapAssetTerm.PayCompoundingFreq == Frequency.None)
                  basisSwapAssetTerm.PayCompoundingFreq = projectionBasisSwapTerm.RecCompoundingFreq;
                if (basisSwapAssetTerm.PayCompoundingConvention == CompoundingConvention.None)
                  basisSwapAssetTerm.PayCompoundingConvention = projectionBasisSwapTerm.RecCompoundingConvention;
              }
              else
              {
                if (basisSwapAssetTerm.PayerIndex == null)
                  basisSwapAssetTerm.PayerIndex = basisSwapAssetTerm.PayerIndex ?? projectionTerms.ReferenceIndex;
                if (basisSwapAssetTerm.PayCompoundingFreq == Frequency.None)
                  basisSwapAssetTerm.PayCompoundingFreq = projectionTerms.ReferenceIndex.IndexTenor.ToFrequency();
              }
            }
          }
          allTerms.Add(retVal.AssetKey, retVal);
        }
      }
      return new CurveTerms(targetTerms.Name, targetTerms.Ccy, targetTerms.ReferenceIndices, allTerms.Values);
    }

    private static bool TryGetMatchingSwapTerm(ReferenceIndex index, AssetTermList assetTerms, out SwapAssetCurveTerm term)
    {
      // find swap asset terms matching on index
      foreach (var curveTerm in assetTerms.Values.OfType<SwapAssetCurveTerm>().Where(curveTerm => curveTerm.ReferenceIndex.IsEqual(index)))
      {
        term = curveTerm;
        return true;
      }

      // backward compatibility - if only one swap terms in list, allow it to match
      var swapTerms = assetTerms.Values.OfType<SwapAssetCurveTerm>().ToList();
      if (swapTerms.Count == 1)
      {
        term = swapTerms[0];
        return true;
      }
      
      term = null;
      return false;
    }

    /// <summary>
    /// Group by instrument type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="origArray"></param>
    /// <param name="origTypes"></param>
    /// <param name="filterType"></param>
    /// <returns></returns>
    public static T[] FilterArrayByInstrumentType<T>(T[] origArray, InstrumentType[] origTypes, InstrumentType filterType)
    {
      return origArray.Where((t, i) => (origTypes[i] == filterType)).ToArray();
    }

    /// <summary>
    /// Get default settlement date for given calibration instrument
    /// </summary>
    /// <param name="termsInfo">Market conventions</param>
    /// <param name="type">Instrument type</param>
    /// <param name="instrumentName">Instrument name</param>
    /// <param name="asOf">As of date</param>
    /// <param name="name">Tenor name</param>
    /// <returns>Default settlement date</returns>
    public static Dt GetTenorSettlement(CurveTerms termsInfo, InstrumentType type, string instrumentName, Dt asOf, string name)
    {
      if (type == InstrumentType.None)
        return Dt.Empty;
      if (type == InstrumentType.FUNDMM)
        return String.Compare(name, "T/N", StringComparison.InvariantCultureIgnoreCase) != 0 ? asOf : Dt.AddDays(asOf, 1, GetAssetCalendar(termsInfo, type, instrumentName));

      return Dt.AddDays(asOf, GetAssetSpotDays(termsInfo, type, instrumentName), GetAssetCalendar(termsInfo, type, instrumentName));
    }

    ///<summary>This method generates the maturity date of a product used in calibration
    ///</summary>
    ///<param name="termsInfo">The market terms of products used for calibration</param>
    ///<param name="type">The product type</param>
    ///<param name="instrumentName">Instrument name</param>
    ///<param name="asOf">Base Date</param>
    ///<param name="tenor">Product label (Tenor/IMM futures code/Constant maturity)  </param>
    ///<param name="bkwdCompatible">Flag to indicate whether the curve is calibrated using old-style process (maturity is not rolled)</param>
    ///<returns>Product maturity date</returns>
    public static Dt GetTenorMaturity(CurveTerms termsInfo, InstrumentType type, string instrumentName, Dt asOf, string tenor, bool bkwdCompatible)
    {
      if (String.IsNullOrEmpty(tenor))
        return Dt.Empty;
      Dt settle = GetTenorSettlement(termsInfo, type, instrumentName, asOf, tenor);
      Dt specifiedMaturity;
      Dt nonRollMaturity = settle;
      Tenor mTenor;
      Tenor extraTenor;
      if (tenor == "O/N" || tenor == "T/N")
        tenor = "1D";
      if (Tenor.TryParse(tenor, out mTenor))
        nonRollMaturity = Dt.Add(settle, mTenor);
      else if (CurveUtil.TryGetFixedMaturityFromString(tenor, out specifiedMaturity))
        return specifiedMaturity;
      else if (Tenor.TryParseComposite(tenor, out mTenor, out extraTenor)) //Composite tenor for FRA
        return Dt.LiborMaturity(settle, mTenor, termsInfo.ReferenceIndex.Calendar, termsInfo.ReferenceIndex.Roll);
      else if (Dt.TryFromStrComposite(tenor, "%d-%b-%Y", out specifiedMaturity, out nonRollMaturity))
        return specifiedMaturity;
      if (type == InstrumentType.None)
        return Dt.Empty;
      if (type == InstrumentType.FUT)
      {
        RateFuturesCurveTerm futureTerm;
        if(termsInfo.TryGetInstrumentTerm(InstrumentType.FUT, out futureTerm) && futureTerm.RateFutureType == RateFutureType.ASXBankBill)
        {
          return bkwdCompatible
                 ? Dt.ImmDate(settle, tenor, CycleRule.IMMAUD)
                 : Dt.Roll(Dt.ImmDate(settle, tenor, CycleRule.IMMAUD), GetAssetBDConvention(termsInfo, type, instrumentName), GetAssetCalendar(termsInfo, type, instrumentName));
        }
        return bkwdCompatible
                 ? Dt.ImmDate(settle, tenor)
                 : Dt.Roll(Dt.ImmDate(settle, tenor), GetAssetBDConvention(termsInfo, type, instrumentName), GetAssetCalendar(termsInfo, type, instrumentName));
      }

      if (mTenor.Units == TimeUnit.Days)
        return Dt.AddDays(settle, mTenor.N, GetAssetCalendar(termsInfo, type, instrumentName));
      return bkwdCompatible
               ? nonRollMaturity
               : Dt.Roll(nonRollMaturity, GetAssetBDConvention(termsInfo, type, instrumentName), GetAssetCalendar(termsInfo, type, instrumentName));

    }

    #endregion

    #region Util functions

    ///<summary>
    /// Way to handle instrument type both from enum string, abbrieved strings and special instances  eg. Swap/Swap_3M
    ///</summary>
    ///<param name="types">Instrument type array in string type</param>
    ///<param name="useBlankValue">Convert blank value</param>
    ///<param name="blankValue">Instrument type for blank string</param>
    ///<returns>Instrument type</returns>
    public static InstrumentType[] SpecialConvertTypeStrings(string[] types, bool useBlankValue, InstrumentType blankValue)
    {
      if (types == null || types.Length == 0)
        return null;
      var retVals = new InstrumentType[types.Length];
      for(int i = 0; i < types.Length; ++i)
      {
        if (!StringUtil.HasValue(types[i]))
        {
          retVals[i] = useBlankValue ? blankValue : InstrumentType.None;
        }
        else if (types[i].IndexOf('_') > 0)
        {
          var splitItems = types[i].Split('_');
          retVals[i] = ConvertInstrumentType(splitItems[0], useBlankValue, blankValue);
        }
        else
        {
          retVals[i] = ConvertInstrumentType(types[i], useBlankValue, blankValue);
        }
      }
      return retVals;
    }

    ///<summary>
    /// This method converts the instrument type from string inputs to enum value
    ///</summary>
    ///<param name="typeStr">instrument type in string type</param>
    ///<param name="useBlankValue">whether blank value is getting converted</param>
    ///<param name="blankOrMissingValue">instrument type for blank string</param>
    ///<returns>Instrument type</returns>
    public static InstrumentType ConvertInstrumentType(string typeStr, bool useBlankValue, InstrumentType blankOrMissingValue)
    {
      // Strip space and bracket in the enum
      var rx = new System.Text.RegularExpressions.Regex(@"\s*\(.*\)\s*|\s+");
      string value = rx.Replace(typeStr, "");
      if (value.Length == 0 && useBlankValue)
        return blankOrMissingValue;
      switch (value.ToUpper())
      {
        case "MONEYMARKET":
          return InstrumentType.MM;
        case "FUTURE":
        case "FUTURES":
        case "EDFUTURES":
        case "EDFUTURE":
          return InstrumentType.FUT;
        case "FUNDINGSWAP"://for backward compatibility only
          return InstrumentType.Swap;
        case "SWAP":
          return InstrumentType.Swap;
        case "BASIS":
          return InstrumentType.BasisSwap;
        case "FUNDINGMM":
          return InstrumentType.FUNDMM;
        default:
          InstrumentType retVal;
          if (Enum.TryParse(value, true, out retVal))
            return retVal;
          return blankOrMissingValue;
      }
    }

    /// <summary>
    /// Create a string-representation of swap floating leg fixing tenor vs swap tenor mapping, 
    /// eg 3M/2Y+3Y+4Y;6M/5Y+7Y+10Y+15Y+20Y means 2Y,3Y and 4Y swap tenors using quarterly-fixing,
    /// while 5Y, 7Y, 10Y, 15Y and 20Y swap tenors  using semi-annual-fixing
    /// </summary>
    /// <param name="swapTenors">Swap tenor name vs fixing tenor mapping</param>
    /// <returns>String representation</returns>
    public static string CreateSwapFixingsFromTenors(IDictionary<string, Tuple<Tenor, Frequency>> swapTenors)
    {
      var reversedMapping = new Dictionary<string, List<string>>();
      foreach (var pair in swapTenors)
      {
        var termKey = pair.Value.Item1.ToString("S", null) + "_" + pair.Value.Item2;
        if (!reversedMapping.ContainsKey(termKey))
        {
          var tenors = new List<string> {pair.Key};
          reversedMapping[termKey] = tenors;
        }
        else
        {
          var tenors = reversedMapping[termKey];
          tenors.Add(pair.Key);
        }
      }

      var retVal = string.Empty;
      foreach (var termKey in reversedMapping.Keys)
      {
        var tenors = reversedMapping[termKey];
        if (tenors == null || tenors.Count==0)
          continue;

        if (string.IsNullOrEmpty(retVal))
          retVal = termKey +"/";
        else
        {
          retVal += ";" + termKey+"/";
        }

        foreach (var tenorName in tenors)
        {
          if (retVal.EndsWith("/"))
            retVal += tenorName;
          else
          {
            retVal += "+" + tenorName;
          }
        }
      }

      return retVal;
    }

    /// <summary>
    /// Create the swap tenor name vs fixing tenor mapping from the excel string value
    /// </summary>
    /// <param name="swapFixings">string-representation of the mapping, like 3M/2Y+3Y+4Y;6M/5Y+7Y+10Y+15Y+20Y means 2Y,3Y and 4Y swap tenors using quarterly-fixing,
    /// while 5Y, 7Y, 10Y, 15Y and 20Y swap tenors  using semi-annual-fixing</param>
    /// <returns></returns>
    public static IDictionary<string, Tuple<Tenor,Frequency>> CreateSwapTenorsFromFixings(string swapFixings)
    {
      var retVal = new Dictionary<string, Tuple<Tenor, Frequency>>();
      Tenor fixingTenor;
      Frequency swapFreq;
      if (StringUtil.HasValue(swapFixings))
      {
        var fixingItems = swapFixings.Split(new[] {',', ';'});
        foreach (var fixingItem in fixingItems)
        {
          var mappingItems = fixingItem.Split('/');
          if (mappingItems.Length ==2 && mappingItems[0].Length > 2)
          {
            if (!Tenor.TryParse(mappingItems[0].Substring(0,2), out fixingTenor))
            {
              throw new ArgumentException(string.Format("Creating swap tenors from swap fixing string failed because the fixing tenor [{0}] is invalid", mappingItems[0]));
            }

            if (!Enum.TryParse(mappingItems[0].Substring(3), true, out swapFreq))
            {
              throw new ArgumentException(String.Format("Creating swap tenors from swap fixing string failed because the fixed swap frequency [{0}] is invalid", mappingItems[0]));
            }

            var swapTenors = mappingItems[1].Split('+');
            foreach (var swapTenorName in swapTenors)
            {
              if (!retVal.ContainsKey(swapTenorName))
                retVal.Add(swapTenorName, new Tuple<Tenor, Frequency>(fixingTenor, swapFreq));
              else
              {
                throw new ArgumentException(string.Format("Duplicate swap tenor [{0}] specified in swap tenor fixings", swapTenorName));
              }
            }
          }
        }
      }
      return retVal;
    }

    #endregion

    #region New functions added 10.3.0
    /// <summary>
    /// Gets the instrument type.
    /// </summary>
    /// <param name="instrumentName">The instrument name.</param>
    /// <param name="terms">The market conventions of calibration products.</param>
    /// <returns>An array of instrument types.</returns>
    /// <remarks></remarks>
    public static InstrumentType GetInstrumentType(
      string instrumentName, CurveTerms terms)
    {
      AssetCurveTerm term;
      if (terms != null && terms.AssetTerms.TryGetValue(instrumentName, out term) && term != null)
        return term.Type;
      // If not found, looks for the type using our old method.
      return ConvertInstrumentType(instrumentName, true, InstrumentType.None);
    }


    /// <summary>
    /// Gets the instrument types.
    /// </summary>
    /// <param name="instrumentNames">The instrument names.</param>
    /// <param name="terms">The market conventions of calibration products.</param>
    /// <returns>An array of instrument types.</returns>
    /// <remarks></remarks>
    public static InstrumentType[] GetInstrumentTypes(
      this IList<string> instrumentNames,
      CurveTerms terms)
    {
      int count = instrumentNames.Count;
      var types = new InstrumentType[count];
      for (int i = 0; i < count; ++i)
      {
        var name = instrumentNames[i];
        // First looks inside the rate terms objects to see
        // if there is any asset term defined by that name.
        AssetCurveTerm term;
        if (terms != null && terms.AssetTerms.TryGetValue(name, out term) && term != null)
        {
          types[i] = term.Type;
          continue;
        }
        // If not found, looks for the type using our old method.
        types[i] = ConvertInstrumentType(name, true, InstrumentType.None);
      }
      return types;
    }

    public static Dictionary<ReferenceIndex, RateResets> ClearHistoricalObservations(
      IEnumerable<ReferenceIndex> referenceIndices)
    {
      var retVal = new Dictionary<ReferenceIndex, RateResets>();
      foreach (var ri in referenceIndices)
      {
        if (ri == null || ri.HistoricalObservations == null || retVal.Keys.Contains(ri))
          continue;
        retVal[ri] = ri.HistoricalObservations;
        ri.HistoricalObservations = null;
      }
      return retVal;
    }

    /// <summary>
    /// Get instrument term for given InstrumentType
    /// </summary>
    /// <typeparam name="T">Type of instrument term</typeparam>
    /// <param name="rateCurveTerms">Market terms</param>
    /// <param name="type">InstrumentType</param>
    /// <param name="term">Market terms for given InstrumentType</param>
    /// <returns>True if terms were found</returns>
    public static bool TryGetInstrumentTerm<T>(this CurveTerms rateCurveTerms,
      InstrumentType type, out T term) where T : AssetCurveTerm
    {
      if (rateCurveTerms != null && rateCurveTerms.AssetTerms != null && rateCurveTerms.AssetTerms.TryGetValue(type, out term))
        return term != null;
      term = null;
      return false;
    }

    /// <summary>
    /// Get instrument term for given InstrumentType
    /// </summary>
    /// <typeparam name="T">Type of instrument term</typeparam>
    /// <param name="rateCurveTerms">Market terms</param>
    /// <param name="type">InstrumentType</param>
    /// <param name="key">Sub-classifc</param>
    /// <param name="term"></param>
    /// <returns></returns>
    public static bool TryGetInstrumentTerm<T>(this CurveTerms rateCurveTerms,
      InstrumentType type, string key, out T term) where  T : AssetCurveTerm
    {
      if (rateCurveTerms != null &&
        rateCurveTerms.AssetTerms != null && (
          rateCurveTerms.AssetTerms.TryGetValue(key, out term) ||
            rateCurveTerms.AssetTerms.TryGetValue(type, out term)))
      {
        return term != null;
      }
      term = null;
      return false;
    }

    #endregion

    #region Data

    private static Dictionary<string, CurveTerms> rateCurveTermsCache_ = new Dictionary<string, CurveTerms>
   {
     // USD
   {"USDLIBOR_3M", new CurveTerms("USDLIBOR", Currency.USD, Calendar.NYB, "USDLIBOR_3M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.Quarterly )},
   {"USDLIBOR_6M", new CurveTerms("USDLIBOR", Currency.USD, Calendar.NYB, "USDLIBOR_6M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.SemiAnnual )},
   {"USDFUNDING_3M", new CurveTerms("USDFUNDING", Currency.USD, Calendar.NYB, "USDFUNDING_3M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.SemiAnnual )},
   {"USDFUNDING_6M", new CurveTerms("USDFUNDING", Currency.USD, Calendar.NYB, "USDFUNDING_6M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.SemiAnnual )},
   
   {"USDFEDFUNDS_1D", new CurveTerms("USDFEDFUNDS", Currency.USD, Calendar.NYB, "USDFEDFUNDS_1D", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.SemiAnnual )
   {SwapProjectionType = ProjectionType.GeometricAverageRate, BasisSwapProjectionType = ProjectionType.ArithmeticAverageRate, BasisSwapOtherProjectionType = ProjectionType.SimpleProjection}},
   {"FFER", new CurveTerms("FFER", Currency.USD, Calendar.NYB, "FFER", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.SemiAnnual )
   {SwapProjectionType = ProjectionType.GeometricAverageRate, BasisSwapProjectionType = ProjectionType.ArithmeticAverageRate, BasisSwapOtherProjectionType = ProjectionType.SimpleProjection}},

   // EUR
   {"EURIBOR_3M", new CurveTerms("EURIBOR", Currency.EUR, Calendar.TGT, "EURIBOR_3M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.Annual, Frequency.Quarterly, Frequency.Annual )},
   {"EURIBOR_6M", new CurveTerms("EURIBOR", Currency.EUR, Calendar.TGT, "EURIBOR_6M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.Annual, Frequency.SemiAnnual, Frequency.Annual )},
   {"EURLIBOR_3M", new CurveTerms("EURLIBOR", Currency.EUR, Calendar.TGT, "EURLIBOR_3M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.Annual, Frequency.Quarterly, Frequency.None )},
   {"EURLIBOR_6M", new CurveTerms("EURLIBOR", Currency.EUR, Calendar.TGT, "EURLIBOR_6M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.Annual, Frequency.SemiAnnual, Frequency.None )},
   {"EURFUNDING_3M", new CurveTerms("EURFUNDING", Currency.EUR, Calendar.TGT, "EURFUNDING_3M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.Annual, Frequency.Quarterly, Frequency.Annual )},
   {"EURFUNDING_6M", new CurveTerms("EURFUNDING", Currency.EUR, Calendar.TGT, "EURFUNDING_6M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Thirty360, Frequency.Annual, Frequency.Quarterly, Frequency.Annual )},

   {"EONIA", new CurveTerms("EONIA", Currency.EUR, Calendar.TGT, "EONIA", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Actual360, Frequency.Annual, Frequency.Annual, Frequency.Annual )
   {SwapProjectionType = ProjectionType.GeometricAverageRate}},

   // GBP
   {"GBPLIBOR_3M", new CurveTerms("GBPLIBOR", Currency.GBP, Calendar.LNB, "GBPLIBOR_3M", BDConvention.Modified, DayCount.Actual365Fixed, 0, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.None )},
   {"GBPLIBOR_6M", new CurveTerms("GBPLIBOR", Currency.GBP, Calendar.LNB, "GBPLIBOR_6M", BDConvention.Modified, DayCount.Actual365Fixed, 0, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.None )},
   {"GBPFUNDING_3M", new CurveTerms("GBPFUNDING", Currency.GBP, Calendar.LNB, "GBPFUNDING_3M", BDConvention.Modified, DayCount.Actual365Fixed, 0, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.SemiAnnual )},
   {"GBPFUNDING_6M", new CurveTerms("GBPFUNDING", Currency.GBP, Calendar.LNB, "GBPFUNDING_6M", BDConvention.Modified, DayCount.Actual365Fixed, 0, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.SemiAnnual )},

   {"SONIA", new CurveTerms("SONIA", Currency.GBP, Calendar.LNB, "SONIA", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.Annual, Frequency.Annual, Frequency.Annual )
   {SwapProjectionType = ProjectionType.GeometricAverageRate}},

   // JPY
   {"JPYLIBOR_3M", new CurveTerms("JPYLIBOR", Currency.JPY, Calendar.TKB, "JPYLIBOR_3M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.None )},
   {"JPYLIBOR_6M", new CurveTerms("JPYLIBOR", Currency.JPY, Calendar.TKB, "JPYLIBOR_6M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.None )},
   {"JPYFUNDING_3M", new CurveTerms("JPYFUNDING", Currency.JPY, Calendar.TKB, "JPYFUNDING_3M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.SemiAnnual )},
   {"JPYFUNDING_6M", new CurveTerms("JPYFUNDING", Currency.JPY, Calendar.TKB, "JPYFUNDING_6M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.SemiAnnual )},

   {"TONAR", new CurveTerms("TONAR", Currency.JPY, Calendar.TKB, "TONAR", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.Annual, Frequency.Annual, Frequency.Annual )
   {SwapProjectionType = ProjectionType.GeometricAverageRate}},
   
   // AUD
   {"AUDLIBOR_3M", new CurveTerms("AUDLIBOR", Currency.AUD, Calendar.SYB, "AUDLIBOR_3M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.None )},
   {"AUDLIBOR_6M", new CurveTerms("AUDLIBOR", Currency.AUD, Calendar.SYB, "AUDLIBOR_6M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.None )},
   {"BBSW_3M", new CurveTerms("BBSW", Currency.AUD, Calendar.SYB, "BBSW_3M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.SemiAnnual )},
   {"BBSW_6M", new CurveTerms("BBSW", Currency.AUD, Calendar.SYB, "BBSW_6M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.SemiAnnual )},

   // SBD
   {"SARLIBOR_3M", new CurveTerms("SARLIBOR", Currency.SBD, Calendar.RIB, "SARLIBOR_3M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Actual360, Frequency.Annual, Frequency.Quarterly, Frequency.None )},
   {"SARLIBOR_6M", new CurveTerms("SARLIBOR", Currency.SBD, Calendar.RIB, "SARLIBOR_6M", BDConvention.Modified, DayCount.Actual360, 2, DayCount.Actual360, Frequency.Annual, Frequency.SemiAnnual, Frequency.None )},
   
   // ZAR
   {"JIBAR_3M", new CurveTerms("JIBAR", Currency.ZAR, Calendar.JOB, "JIBAR_3M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.None )},
   {"JIBAR_6M", new CurveTerms("JIBAR", Currency.ZAR, Calendar.JOB, "JIBAR_6M", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.None )},

   // CAD
   {"CADLIBOR_3M", new CurveTerms("CADLIBOR", Currency.CAD, Calendar.TRB, "CADLIBOR_3M", BDConvention.Modified, DayCount.Actual365Fixed, 0, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.Quarterly, Frequency.None)},
   {"CADLIBOR_6M", new CurveTerms("CADLIBOR", Currency.CAD, Calendar.TRB, "CADLIBOR_6M", BDConvention.Modified, DayCount.Actual365Fixed, 0, DayCount.Actual365Fixed, Frequency.SemiAnnual, Frequency.SemiAnnual, Frequency.None)},
   {"CORRA", new CurveTerms("CORRA", Currency.CAD, Calendar.TRB, "CORRA", BDConvention.Modified, DayCount.Actual365Fixed, 2, DayCount.Actual365Fixed, Frequency.Annual, Frequency.Annual, Frequency.Annual )
   {SwapProjectionType = ProjectionType.GeometricAverageRate}}

};
    #endregion Data
  }
}
