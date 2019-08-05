//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Curves
{
  class BufferedCsvReader : IDisposable
  {
    private readonly CsvReader reader_;
    private string[] line_;

    public BufferedCsvReader(string path)
    {
      reader_ = new CsvReader(path);
    }

    public string[] GetCsvLine()
    {
      if (line_ != null)
      {
        string[] x = line_;
        line_ = null;
        return x;
      }
      return reader_.GetCsvLine();
    }

    public void PushBack(string[] line)
    {
      if(line_!=null)
        throw new CsvReaderException("Cannot push back a line.");
      line_ = line;
    }

    public void Dispose()
    {
      reader_.Dispose();
    }
  }

  class RateCurveData
  {
    private readonly Dictionary<string, int> curveDataMap_
      = new Dictionary<string, int>();
    private readonly Dictionary<string, string[][]> instrument_
      = new Dictionary<string, string[][]>();

    public static RateCurveData LoadFromCsvFile(string path)
    {
      var data = new RateCurveData();
      using (var reader = new BufferedCsvReader(GetTestFilePath(path)))
      {
        string[] line;
        while ((line = reader.GetCsvLine()) != null)
        {
          if (line.Length == 0 || String.IsNullOrEmpty(line[2])) continue;
          for (int i = 2; i < line.Length; ++i)
          {
            if (String.IsNullOrEmpty(line[i])) continue;
            data.curveDataMap_.Add(line[i], i);
          }
          break;
        }

        while ((line = reader.GetCsvLine()) != null)
        {
          if (line.Length == 0 || String.IsNullOrEmpty(line[0])) continue;
          var name = line[0].Replace(" ", "").ToUpper();
          if (name.StartsWith("MM")) name = "MM";
          else if (name.StartsWith("EDFUT")) name = "EDFUT";
          else if (name.StartsWith("SWAP")) name = "SWAP";
          else if (name.StartsWith("OIS")) name = "OIS";
          else if (name.StartsWith("BASIS")) name = "BASIS";
          else if (name.Contains("MARKETENV")) name = "MMENV";

          var list = new List<string[]>();
          while ((line = reader.GetCsvLine()) != null)
          {
            if (line.Length == 0 || String.IsNullOrEmpty(line[1]))
            {
              reader.PushBack(line);
              break;
            }
            bool nonempty = false;
            for (int i = 2; i < line.Length; ++i)
            {
              if (!String.IsNullOrEmpty(line[i]))
              {
                nonempty = true;
                break;
              }
            }
            if (nonempty) list.Add(line);
          }
          if (list.Count > 0)
            data.instrument_.Add(name, list.ToArray());
        }
      }
      if (data.instrument_.Count > 0) return data;
      return null;
    }

    public DiscountCurve CalibrateBasisProjectionCurve(string curveName, Dt tradeDate, string discountTerms,
      CurveTerms targetTerms, CurveFitMethod fitMethod, InterpScheme interpScheme, DiscountCurve discount,
      DiscountCurve projection, ReferenceIndex targetIndex, ReferenceIndex projectionIndex)
    {
      if (this.curveDataMap_ == null || this.instrument_ == null)
      {
        throw new Exception("Null or invalid rate curve data");
      }

      CurveTerms drcTerms = RateCurveTermsUtil.CreateDefaultCurveTerms(
        discountTerms);


      string[] instrumentTypes, tenorNames;
      double[] quotes;
      if (!GetMarketQuotes(targetTerms.Name,
                           new[] {"BASIS"},
                           out instrumentTypes, out tenorNames, out quotes))
      {
        throw new Exception(String.Format("No rate data for curve name {0}",
                                          targetTerms.Name));
      }
      targetTerms = targetTerms.Merge(instrumentTypes, drcTerms, false);
      var types = CollectionUtil.ConvertAll(instrumentTypes, t => RateCurveTermsUtil.ConvertInstrumentType(t, true, InstrumentType.None));
      var dayCounts = types.Select((t, i) => RateCurveTermsUtil.GetAssetDayCount(targetTerms, t, instrumentTypes[i])).ToArray();
      var rolls = types.Select((t, i) => RateCurveTermsUtil.GetAssetBDConvention(targetTerms, t, instrumentTypes[i])).ToArray();
      var cals = types.Select((t, i) => RateCurveTermsUtil.GetAssetCalendar(targetTerms, t, instrumentTypes[i])).ToArray();
      var freqs = new Frequency[types.Length,2];
      for (int i = 0; i < types.Length; i++)
      {
        if (types[i] != InstrumentType.None)
        {
          BasisSwapAssetCurveTerm bsTerms;
          if (targetTerms.TryGetInstrumentTerm(types[i], out bsTerms))
          {
            freqs[i, 0] = bsTerms.RecFreq;
            freqs[i, 1] = bsTerms.PayFreq;
          }
          else
          {
            freqs[i, 0] = RateCurveTermsUtil.GetAssetPaymentFrequency(targetTerms, types[i], "");
            freqs[i, 1] = Frequency.None;
          }
        }
      }
      CurveFitSettings fitSettings;
      {
        Dt spot = Dt.AddDays(tradeDate, drcTerms.ReferenceIndex.SettlementDays,
                             drcTerms.ReferenceIndex.Calendar);
        fitSettings = new CurveFitSettings(spot);
        //{
        //  InterpScheme = interpScheme,
        //  Method = fitMethod
        //};
      }
      var calibratorSettings = new CalibratorSettings(fitSettings);
      var paymentSettings = instrumentTypes.Select((t, i) => RateCurveTermsUtil.GetPaymentSettings(targetTerms, t)).ToArray();
      return ProjectionCurveFitCalibrator.ProjectionCurveFit(curveName, calibratorSettings, discount, projection,
                                                             new [] { targetIndex}, projectionIndex, drcTerms.Ccy, "", quotes,
                                                             types, null, null,
                                                             tenorNames, null, dayCounts, freqs, rolls, cals,
                                                             paymentSettings);

    }

    public DiscountCurve CalibrateDiscountCurve(
      string curveName,
      Dt tradeDate,
      string discountTerms,
      string projectTerms,
      CurveFitMethod fitMethod,
      InterpScheme interpScheme,
      params InstrumentType[] overlapTreatmentOrder)
    {
      if (this.curveDataMap_ == null || this.instrument_==null)
      {
        throw new Exception("Null or invalid rate curve data");
      }

      CurveTerms drcTerms = RateCurveTermsUtil.CreateDefaultCurveTerms(
        discountTerms);

      string[] instrumentTypes, tenorNames;
      double[] quotes;
      if (!GetMarketQuotes(drcTerms.Ccy.ToString(),
        new[] { "MM", "EDFUT", "SWAP" },
        out instrumentTypes, out tenorNames, out quotes))
      {
        throw new Exception(String.Format("No rate data for currecy {0}",
                                          drcTerms.Ccy));
      }
      CurveTerms prcTerms = projectTerms == null ? null
        : RateCurveTermsUtil.CreateDefaultCurveTerms(projectTerms);
      var terms = drcTerms.Merge(instrumentTypes, prcTerms, true);
      CurveFitSettings fitSettings;
      {
        int spotDays = drcTerms.ReferenceIndex.SettlementDays;
        var spotCal = drcTerms.ReferenceIndex.Calendar;
        Dt spot = Dt.AddDays(tradeDate, spotDays, spotCal);
        fitSettings = new CurveFitSettings(spot)
                        {
                          CurveSpotDays = spotDays,
                          CurveSpotCalendar = spotCal,
                          InterpScheme = interpScheme,
                          Method = fitMethod
                        };
        if(overlapTreatmentOrder != null && overlapTreatmentOrder.Length != 0)
        fitSettings.OverlapTreatmentOrder = overlapTreatmentOrder;
      }
      var calibratorSettings = new CalibratorSettings(fitSettings)
      {
        Tolerance = 1e-14
      };

      return DiscountCurveFitCalibrator.DiscountCurveFit(tradeDate, terms, curveName, quotes, instrumentTypes, tenorNames,
        calibratorSettings);

#if Extended

      var assetTypes = RateCurveTermsUtil.SpecialConvertTypeStrings(
        instrumentTypes, true, InstrumentType.None);
      var dayCounts = RateCurveTermsUtil.GetDayCountArray(drcTerms, assetTypes);
      var rolls = RateCurveTermsUtil.GetBDConventionArray(drcTerms, assetTypes);
      var cals = RateCurveTermsUtil.GetCalendarArray(drcTerms, assetTypes);

      Frequency[,] freqs = new Frequency[assetTypes.Length, 2];
      for (int i = 0; i < assetTypes.Length; i++)
      {
        if (assetTypes[i] != InstrumentType.None)
        {
          if (assetTypes[i] == InstrumentType.BasisSwap && prcTerms != null)
          {
            freqs[i, 0] = RateCurveTermsUtil.GetAssetPaymentFrequency(
              prcTerms, assetTypes[i]);
            freqs[i, 1] = freqs[i, 0];
          }
          else
          {
            freqs[i, 0] = RateCurveTermsUtil.GetAssetPaymentFrequency(
              drcTerms, assetTypes[i]);
            freqs[i, 1] = Frequency.None;
          }
        }
      }

      PaymentSettings[] paymentSettings = RateCurveTermsUtil.GetPaymentSettings(
        drcTerms, assetTypes); //Default payment settings

      var settles = new Dt[instrumentTypes.Length];
      for (int i = 0; i < settles.Length; i++)
      {
        if (settles[i].IsEmpty() && assetTypes[i] != InstrumentType.None)
        {
          settles[i] = RateCurveTermsUtil.GetTenorSettlement(drcTerms,
            instrumentTypes[i], tradeDate, tenorNames[i]);
        }
      }

      return DiscountCurveFitCalibrator.DiscountCurveFit(calibratorSettings,
        drcTerms.RateIndex,
        prcTerms == null ? drcTerms.RateIndex : prcTerms.RateIndex,
        curveName, drcTerms.Ccy, category,
        quotes, assetTypes, settles, null /*maturities*/, tenorNames,
        null /*weights*/, dayCounts, freqs, rolls, cals, paymentSettings);
#endif
    }

    private bool GetMarketQuotes(
      string curveKey,  // either curve currency or index name
      string[] instruments,
      out string[] instrumentTypes,
      out string[] tenorNames,
      out double[] quotes)
    {
      instrumentTypes = null;
      tenorNames = null;
      quotes = null;

      int idx = -1;
      if (!curveDataMap_.TryGetValue(curveKey, out idx)) return false;

      var dict = instrument_;
      var types = new List<string>();
      var names = new List<string>();
      var values = new List<double>();
      foreach (var inst in instruments)
      {
        string[][] list = null;
        if (!dict.TryGetValue(inst, out list)) continue;
        for (int i = 0; i < list.Length; ++i)
        {
          double quote = 0;
          if (!Double.TryParse(list[i][idx], out quote)) continue;
          values.Add(quote);
          names.Add(list[i][0]);
          types.Add(list[i][1]);
        }
      }
      if (values.Count == 0) return false;
      instrumentTypes = types.ToArray();
      tenorNames = names.ToArray();
      quotes = values.ToArray();
      return true;
    }
  }
}
