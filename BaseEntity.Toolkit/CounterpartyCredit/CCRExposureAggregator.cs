using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{

  /// <summary>
  /// Utility to parameterize and construct an aggregator
  /// </summary>
  public class AggregatorFactory
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="discountExposures">discount exposures to present value when reporting expectations</param>
    /// <param name="wrongWayRisk">adjust for correlation between default prob and exposure</param>
    /// <param name="fundingCostNoDefault">report FCA without discounting for default risk</param>
    /// <param name="fundingBenefitNoDefault">report FBA without discounting for default risk</param>
    /// <param name="applyNetting">net exposures of trades in same netting set</param>
    /// <param name="applyCollateral">model collateralization for each netting set</param>
    /// <param name="allocateExposures">allocate marginal exposures for each trade</param>
    
    /// <param name="modelOvercollateralization">excess collateral contributes to exposure</param>
    /// <param name="pykhtinRosenMethodology">use old methodology for allocation of marginal values</param>
    public AggregatorFactory(
      bool unilateral = false,
      bool discountExposures = false,
      bool wrongWayRisk = true,
      bool fundingCostNoDefault = false,
      bool fundingBenefitNoDefault = false,
      bool applyNetting = true,
      bool applyCollateral = true,
      bool allocateExposures = true,
      bool modelOvercollateralization = false,
      bool pykhtinRosenMethodology = false
      )
    {
      Unilateral = unilateral;
      DiscountExposures = discountExposures;
      WrongWayRisk = wrongWayRisk;
      FundingCostNoDefault = fundingCostNoDefault;
      FundingBenefitNoDefault = fundingBenefitNoDefault;
      ApplyNetting = applyNetting;
      ApplyCollateral = applyCollateral;
      AllocateExposures = allocateExposures;
      ModelOvercollateralization = modelOvercollateralization;
      PykhtinRosenMethodology = pykhtinRosenMethodology; 
    }
    
    /// <summary>
    /// treat default unilaterally or jointly (first-to-default) 
    /// </summary>
    public bool Unilateral { get; set; }

    /// <summary>
    /// discount exposures to present value when reporting expectations
    /// </summary>
    public bool DiscountExposures { get; set; }

    /// <summary>
    /// adjust for correlation between default prob and exposure
    /// </summary>
    public bool WrongWayRisk { get; set; }

    /// <summary>
    /// report FCA without discounting for default risk
    /// </summary>
    public bool FundingCostNoDefault { get; set; }

    /// <summary>
    /// report FBA without discounting for default risk
    /// </summary>
    public bool FundingBenefitNoDefault { get; set; }

    /// <summary>
    /// Net exposures for trades in same netting set
    /// </summary>
    public bool ApplyNetting { get; set; }

    /// <summary>
    /// Model collateralization for each netting set
    /// </summary>
    public bool ApplyCollateral { get; set; }

    /// <summary>
    /// allocate marginal exposures for each trade
    /// </summary>
    public bool AllocateExposures { get; set; }

    /// <summary>
    /// excess collateral contributes to exposure
    /// </summary>
    public bool ModelOvercollateralization { get; set; }

    /// <summary>
    /// use old methodology for allocation of marginal values
    /// </summary>
    public bool PykhtinRosenMethodology { get; set; }

    /// <summary>
    /// Construct an aggregator matching the supplied parameters
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="marketData"></param>
    /// <param name="exposures"></param>
    /// <param name="exposureDts"></param>
    /// <param name="netting"></param>
    /// <param name="binaryLoggingEnabled">enable ObjectLogger</param>
    public CCRExposureAggregator Create(Dt asOf,
                                        PrecalculatedMarketData marketData,
                                        PrecalculatedExposures exposures,
                                        Dt[] exposureDts, 
                                        Netting netting,
                                        bool binaryLoggingEnabled = false)
    {
      if (!ApplyNetting && !ApplyCollateral)
      {
        // check that each exposure is in a distinct netting group
        if (exposures.NettingGroups.Distinct().Count() != exposures.Count)
        {
          if (PykhtinRosenMethodology)
            return new PykhtinRosenExposureAggregator(asOf, marketData, exposures, netting, exposureDts, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, AllocateExposures, binaryLoggingEnabled, ModelOvercollateralization);
          else
            return new CollateralizedExposureAggregator(asOf, marketData, exposures, netting, exposureDts, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, AllocateExposures, binaryLoggingEnabled, ModelOvercollateralization);

        }
        return new NoNetExposureAggregator(asOf, marketData, exposures, exposureDts, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, binaryLoggingEnabled, ModelOvercollateralization);
      }
      if (PykhtinRosenMethodology)
        return new PykhtinRosenExposureAggregator(asOf, marketData, exposures, netting, exposureDts, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, AllocateExposures, binaryLoggingEnabled, ModelOvercollateralization);

      return new CollateralizedExposureAggregator(asOf, marketData, exposures, netting, exposureDts, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, AllocateExposures, binaryLoggingEnabled, ModelOvercollateralization);
    }


    /// <summary>
    /// Construct a native aggregator matching the supplied parameters. 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="pathCount"></param>
    /// <param name="storeAsFloat">exposures are stored as single precision float</param>
    /// <param name="tradeExposures">pointer to exposure data for each trade or exposure set</param>
    /// <param name="tradeExposureDates">pointer to exposure dates for each trade or exposure set</param>
    /// <param name="tradeExposureDateCounts">number of exposure dates for each trade or exposure set</param>
    /// <param name="nettingGroups">Name of netting group for each trade or exposure set</param>
    /// <param name="netting">Contains collateral mappings for each netting group</param>
    /// <param name="binaryLoggingEnabled">Enable binary logging if it is supported</param>
    /// <param name="exposureDts">dates to aggregate over</param>
    /// <param name="discountFactors">pointer to discount data</param>
    /// <param name="radonNikodyms">pointer to measure change data for each path</param>
    /// <param name="integrationKernels">default kernels</param>
    /// <param name="recoveryRates">recovery assumptions for XVA</param>
    /// <param name="cptyCurve">The Counterparty Curve required to compute RWA</param>
    public CCRExposureAggregator CreateNative(Dt asOf,
      int pathCount,
      bool storeAsFloat, 
      IList<IntPtr> tradeExposures,
      IList<IntPtr> tradeExposureDates,
      IList<int> tradeExposureDateCounts,
      IList<string> nettingGroups,
      Netting netting,
      Dt[] exposureDts,
      IntPtr discountFactors,
      IntPtr[] radonNikodyms,
      IList<Tuple<Dt[], double[]>> integrationKernels,
      double[] recoveryRates,
      bool binaryLoggingEnabled = false,
      SurvivalCurve cptyCurve = null
    )
    {
      ExposureAggregatorNative nativeImpl;
      if (!ApplyNetting)
      {
        // check that each exposure is in a distinct netting group
        if (nettingGroups.Distinct().Count() != tradeExposures.Count)
        {
          var collateralizedExposureAggregatorNative =
            new CollateralizedExposureAggregatorNative(asOf.ToDouble(), pathCount, BaseEntity.Toolkit.Concurrency.ParallelSupport.Enabled, storeAsFloat);
          foreach (var nettingCollateralMap in netting.CollateralMaps.OfType<INativeCollateralMap>())
          {
            nettingCollateralMap.AddNativeCollateralMap(collateralizedExposureAggregatorNative);
          }
          collateralizedExposureAggregatorNative.addNetting(new StringVector(netting.NettingGroups), new StringVector(netting.NettingSuperGroups));
          nativeImpl = collateralizedExposureAggregatorNative;
        }
        else
        {
          nativeImpl = new NoNetAggregatorNative(asOf.ToDouble(), pathCount, BaseEntity.Toolkit.Concurrency.ParallelSupport.Enabled, storeAsFloat);
        }
      }
      else
      {
        var collateralizedExposureAggregatorNative =
          new CollateralizedExposureAggregatorNative(asOf.ToDouble(), pathCount, BaseEntity.Toolkit.Concurrency.ParallelSupport.Enabled, storeAsFloat);
        foreach (var nettingCollateralMap in netting.CollateralMaps.OfType<INativeCollateralMap>())
        {
          nettingCollateralMap.AddNativeCollateralMap(collateralizedExposureAggregatorNative);
        }
        collateralizedExposureAggregatorNative.addNetting(new StringVector(netting.NettingGroups), new StringVector(netting.NettingSuperGroups));
        nativeImpl = collateralizedExposureAggregatorNative;
      }


      foreach (var integrationKernel in integrationKernels)
      {
        nativeImpl.addIntegrationKernel(integrationKernel.Item1.Select(dt => dt.ToDouble()).ToArray(), integrationKernel.Item2);
      }
      nativeImpl.addPreCalculatedMarketData(exposureDts.Select(dt => dt.ToDouble()).ToArray(), discountFactors, radonNikodyms, recoveryRates);

      if (cptyCurve != null)
      {
        nativeImpl.addCptyCurve(cptyCurve);
      }

      for (var i = 0; i < tradeExposures.Count; i++)
      {
        var exposureSet = tradeExposures[i];
        var exposureSetDts = tradeExposureDates[i];
        var nettingGroup = nettingGroups[i];
        var dtCount = tradeExposureDateCounts[i];
        nativeImpl.addExposures(exposureSet, exposureSetDts,  dtCount, nettingGroup);
      }

      nativeImpl.createAggregator(exposureDts.Select(dt => dt.ToDouble()).ToArray(), Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, ModelOvercollateralization, binaryLoggingEnabled);

      var wrapper = new CCRExposureAggregatorNativeWrapper(nativeImpl, asOf, exposureDts, integrationKernels, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, binaryLoggingEnabled);
      return wrapper;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="pathCount"></param>
    /// <param name="storeAsFloat"></param>
    /// <param name="netting"></param>
    /// <param name="exposureDts"></param>
    /// <param name="discountFactors"></param>
    /// <param name="radonNikodyms"></param>
    /// <param name="integrationKernels"></param>
    /// <param name="recoveryRates"></param>
    /// <param name="precalculatedExposuresPtr"></param>
    /// <param name="noNetting"></param>
    /// <param name="cptyCurve"></param>
    /// <param name="binaryLoggingEnabled"></param>
    /// <returns></returns>
    public CCRExposureAggregator CreateNative(Dt asOf, int pathCount, bool storeAsFloat, Netting netting, Dt[] exposureDts, IntPtr discountFactors, IntPtr[] radonNikodyms, IList<Tuple<Dt[], double[]>> integrationKernels, double[] recoveryRates, IntPtr precalculatedExposuresPtr, bool noNetting, SurvivalCurve cptyCurve = null, bool binaryLoggingEnabled = false)
    {
      ExposureAggregatorNative nativeImpl;
      if (noNetting)
      {
        nativeImpl = new NoNetAggregatorNative(asOf.ToDouble(), pathCount, BaseEntity.Toolkit.Concurrency.ParallelSupport.Enabled, storeAsFloat);
      }
      else
      {
        var collateralizedExposureAggregatorNative =
          new CollateralizedExposureAggregatorNative(asOf.ToDouble(), pathCount, BaseEntity.Toolkit.Concurrency.ParallelSupport.Enabled, storeAsFloat);
        foreach (var nettingCollateralMap in netting.CollateralMaps.OfType<INativeCollateralMap>())
        {
          nettingCollateralMap.AddNativeCollateralMap(collateralizedExposureAggregatorNative);
        }
        collateralizedExposureAggregatorNative.addNetting(new StringVector(netting.NettingGroups), new StringVector(netting.NettingSuperGroups));
        nativeImpl = collateralizedExposureAggregatorNative;
      }

      foreach (var integrationKernel in integrationKernels)
      {
        nativeImpl.addIntegrationKernel(integrationKernel.Item1.Select(dt => dt.ToDouble()).ToArray(), integrationKernel.Item2);
      }
      nativeImpl.addPreCalculatedMarketData(exposureDts.Select(dt => dt.ToDouble()).ToArray(), discountFactors, radonNikodyms, recoveryRates);
      if (cptyCurve != null)
      {
        nativeImpl.addCptyCurve(cptyCurve);
      }
      nativeImpl.createAggregator(exposureDts.Select(dt => dt.ToDouble()).ToArray(), precalculatedExposuresPtr, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, ModelOvercollateralization, binaryLoggingEnabled);

      var wrapper = new CCRExposureAggregatorNativeWrapper(nativeImpl, asOf, exposureDts, integrationKernels, Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, binaryLoggingEnabled);
      return wrapper;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="marketData"></param>
    /// <param name="exposures"></param>
    /// <param name="incrementalExposures"></param>
    /// <param name="exposureDts"></param>
    /// <param name="netting"></param>
    /// <param name="binaryLoggingEnabled"></param>
    /// <returns></returns>
    public IIncrementalExposureAggregator CreateIncrementalExposureAggregatorManaged(Dt asOf,
      PrecalculatedMarketData marketData,
      PrecalculatedExposures exposures,
      PrecalculatedExposures incrementalExposures,
      Dt[] exposureDts,
      Netting netting,
      bool binaryLoggingEnabled = false)
    {
        return new IncrementalCCRExposureAggregator(asOf, marketData, exposures, incrementalExposures, netting, exposureDts, Unilateral, DiscountExposures,
          WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, binaryLoggingEnabled, ModelOvercollateralization);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="pathCount"></param>
    /// <param name="storeAsFloat"></param>
    /// <param name="tradeExposures"></param>
    /// <param name="tradeExposureDates"></param>
    /// <param name="tradeExposureDateCounts"></param>
    /// <param name="nettingGroups"></param>
    /// <param name="postTradeExposures"></param>
    /// <param name="postTradeExposureDates"></param>
    /// <param name="postTradeExposureDateCounts"></param>
    /// <param name="postNettingGroups"></param>
    /// <param name="netting"></param>
    /// <param name="exposureDates"></param>
    /// <param name="integrationKernels"></param>
    /// <param name="discountFactors"></param>
    /// <param name="radonNikodyms"></param>
    /// <param name="recoveryRates"></param>
    /// <param name="binaryLoggingEnabled"></param>
    /// <returns></returns>
    public IIncrementalExposureAggregator CreateIncrementalExposureAggregatorNative(Dt asOf, 
      int pathCount, 
      bool storeAsFloat,
      IList<IntPtr> tradeExposures,
      IList<IntPtr> tradeExposureDates,
      IList<int> tradeExposureDateCounts,
      IList<string> nettingGroups,
      IList<IntPtr> postTradeExposures,
      IList<IntPtr> postTradeExposureDates,
      IList<int> postTradeExposureDateCounts,
      IList<string> postNettingGroups,
      Netting netting,
      Dt[] exposureDates,
      IList<Tuple<Dt[], double[]>> integrationKernels,
      IntPtr discountFactors,
      IntPtr[] radonNikodyms,
      double[] recoveryRates,
      bool binaryLoggingEnabled = false)
    {
      var nativeImp = new IncrementalCCRExposureAggregatorNative(asOf.ToDouble(), pathCount, BaseEntity.Toolkit.Concurrency.ParallelSupport.Enabled, storeAsFloat);
      foreach (var nettingCollateralMap in netting.CollateralMaps.OfType<INativeCollateralMap>())
      {
        nettingCollateralMap.AddIncrementalNativeCollateralMap(nativeImp);
      }
      nativeImp.addNetting(new StringVector(netting.NettingGroups), new StringVector(netting.NettingSuperGroups));
      foreach (var kernel in integrationKernels)
      {
        nativeImp.addIntegrationKernel(kernel.Item1.Select(dt => dt.ToDouble()).ToArray(), kernel.Item2);
      }
      
      nativeImp.addPreCalculatedMarketData(exposureDates.Select(dt => dt.ToDouble()).ToArray(), discountFactors, radonNikodyms, recoveryRates);

      for (var i = 0; i < tradeExposures.Count; i++)
      {
        nativeImp.addExposures(tradeExposures[i], tradeExposureDates[i], tradeExposureDateCounts[i], nettingGroups[i]);
      }
      for (var i = 0; i < postTradeExposures.Count; i++)
      {
        nativeImp.addPostIncrementalExposures(postTradeExposures[i], postTradeExposureDates[i], postTradeExposureDateCounts[i], postNettingGroups[i]);
      }
      nativeImp.createAggregator(exposureDates.Select(dt => dt.ToDouble()).ToArray(), Unilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, ModelOvercollateralization, binaryLoggingEnabled);
      var wrapper = new IncrementalCCRExposureAggregatorNativeWrapper(nativeImp, asOf, exposureDates, Unilateral, integrationKernels, binaryLoggingEnabled);
      return wrapper;
    }
  }

  /// <summary>
  /// 
  /// </summary>
  public interface IIncrementalExposureAggregator
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="ci"></param>
    void AddMeasureAccumulator(CCRMeasure measure, double ci);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    double GetIncrementalMeasure(CCRMeasure measure, int t, double ci);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    double GetIncrementalMeasure(CCRMeasure measure, Dt dt, double ci);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    double GetTotalMeasure(CCRMeasure measure, int t, double ci);

    /// <summary>
    /// 
    /// </summary>
    void Reduce();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="kernels"></param>
    /// <returns></returns>
    IIncrementalExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels);

    /// <summary>
    /// 
    /// </summary>
    Dt MaxExposureDate { get; }

    /// <summary>
    /// The positive and negative exposures per path, for each exposure date along 
    /// with the Collateral Payments which were posted and received over the exposure dates 
    /// </summary>
    DataTable DiagnosticsTable { get; }

    /// <summary>
    /// Datatable of the Integration Kernals used to aggregate the trade level exposures
    /// </summary>
    DataTable IntegrationKernelsDataTable { get; }
  }

  /// <summary>
  /// 
  /// </summary>
  public class IncrementalCCRExposureAggregatorNativeWrapper : IIncrementalExposureAggregator, IDisposable
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="nativeImpl"></param>
    /// <param name="asOf"></param>
    /// <param name="exposureDates"></param>
    /// <param name="isUnilateral"></param>
    /// <param name="integrationKernels"></param>
    /// <param name="binaryLoggingEnabled"></param>
    public IncrementalCCRExposureAggregatorNativeWrapper(IncrementalCCRExposureAggregatorNative nativeImpl, Dt asOf, Dt[] exposureDates, bool isUnilateral, IList<Tuple<Dt[], double[]>> integrationKernels = null, bool binaryLoggingEnabled = false)
    {
      NativeImpl = nativeImpl;
      _asOf = asOf;
      _exposureDates = exposureDates;
      _isUnilateral = isUnilateral;
      _integrationKernels =  integrationKernels;
      _binaryLoggingEnabled = binaryLoggingEnabled;
    }

    public bool StoreExpAsFloat => NativeImpl.storeAsFloat();

    private IncrementalCCRExposureAggregatorNative NativeImpl { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="ci"></param>
    public void AddMeasureAccumulator(CCRMeasure measure, double ci)
    {
      NativeImpl.addMeasureAccumulator((int)measure, ci);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public double GetIncrementalMeasure(CCRMeasure measure, int t, double ci)
    {
      if (_overrideIntegrationKernels && t <= 0)
        ChangeNativeIntegrationKernels(_alternativeIntegrationKernels);
      var result = NativeImpl.getIncrementalMeasure((int)measure, t, ci);
      if (_overrideIntegrationKernels && t <= 0)
        ChangeNativeIntegrationKernels(_originalIntegrationKernels);
      return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public double GetIncrementalMeasure(CCRMeasure measure, Dt dt, double ci)
    {
      if (_overrideIntegrationKernels && dt.IsEmpty())
        ChangeNativeIntegrationKernels(_alternativeIntegrationKernels);
      var result = NativeImpl.getIncrementalMeasure((int)measure, dt.ToDouble(), ci);
      if (_overrideIntegrationKernels && dt.IsEmpty())
        ChangeNativeIntegrationKernels(_originalIntegrationKernels);
      return result;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="groupCount">Number of netting groups.</param>
    /// <returns></returns>
    /// <remarks>We take groupCount as an argument because where we call this from has easier access to it than we do from here</remarks>
    public byte[] GetNetExposures(int groupCount)
    {
      // declare and pin array
      var bytesPerExp = NativeImpl.storeAsFloat() ? sizeof(float) : sizeof(double);

      byte[] exposures = new byte[_exposureDates.Length * PathCount * bytesPerExp * groupCount];
      var _gcHandleExposures = GCHandle.Alloc(exposures, GCHandleType.Pinned);

      var x = NativeImpl.getNetExposures(_gcHandleExposures.AddrOfPinnedObject());

      // unpin array
      _gcHandleExposures.Free();
      if (x == false)
        return null;
      return exposures;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public double GetTotalMeasure(CCRMeasure measure, int t, double ci)
    {
      if (_overrideIntegrationKernels && t <= 0)
        ChangeNativeIntegrationKernels(_alternativeIntegrationKernels);
      var result = NativeImpl.getTotalMeasure((int)measure, t, ci);
      if (_overrideIntegrationKernels && t <= 0)
        ChangeNativeIntegrationKernels(_originalIntegrationKernels);
      return result;
    }

    /// <summary>
    /// Number of paths
    /// </summary>
    public int PathCount => NativeImpl.pathCount();

    /// <summary>
    /// 
    /// </summary>
    public void Reduce()
    {
      NativeImpl.reduce();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="kernels"></param>
    /// <returns></returns>
    public IIncrementalExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels)
    {
      var clone = new IncrementalCCRExposureAggregatorNativeWrapper(NativeImpl, _asOf, _exposureDates, _isUnilateral, kernels, _binaryLoggingEnabled);
      var transformed = CCRExposureAggregator.TransformIntegrationKernels(kernels, _asOf, _exposureDates, _isUnilateral);
      clone._overrideIntegrationKernels = true;
      clone._alternativeIntegrationKernels = transformed;
      clone._originalIntegrationKernels = _integrationKernels;
      return clone;
    }

    private void ChangeNativeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels)
    {
      for (var i = 0; i < kernels.Count; i++)
      {
        var kernel = kernels[i];
        NativeImpl.changeIntegrationKernel(kernel.Item1.Select(dt => dt.ToDouble()).ToArray(), kernel.Item2, i);
      }
    }

    /// <summary>
    /// Number of exposure dates
    /// </summary>
    public int DateCount => _exposureDates.Length;

    public Dt[] ExposureDates => _exposureDates;

    /// <summary>
    /// The positive and negative exposures per path, for each exposure date along 
    /// with the Collateral Payments which were posted and received over the exposure dates 
    /// </summary>
    public DataTable DiagnosticsTable
    {
      get
      {
        if (_diagnosticTable == null)
        {
          _diagnosticTable = new DataTable();
          _diagnosticTable.Columns.Add("PathId", typeof(int));
          _diagnosticTable.Columns.Add("Key", typeof(string));
          foreach (var date in _exposureDates)
          {
            _diagnosticTable.Columns.Add(date.ToString(), typeof(double));
          }
          for (var pathId = 0; pathId < PathCount; ++pathId)
          {
            for (var i = 0; i < 4; ++i)
            {
              var row = _diagnosticTable.NewRow();
              row["PathId"] = pathId;
              row["Key"] = GetKey(i);
              var rowElements = NativeImpl.diagnosticTable(pathId, i);
              for (var j = 0; j < rowElements.Length; ++j)
              {
                row[j + 2] = rowElements[j];
              }
              _diagnosticTable.Rows.Add(row);
            }
          }
        }
        return _diagnosticTable;
      }
    }

    /// <summary>
    /// Datatable of the Integration Kernals used to aggregate the trade level exposures
    /// </summary>
    public DataTable IntegrationKernelsDataTable
    {
      get
      {
        if (!_binaryLoggingEnabled || _integrationKernels.Count == 0)
        {
          return null;
        }
        var dataTable = new DataTable();
        dataTable.Columns.Add("Kernels", typeof(string));

        foreach (var date in _integrationKernels[0].Item1)
        {
          dataTable.Columns.Add(date.ToString(), typeof(double));
        }

        for (var i = 0; i < _integrationKernels.Count; ++i)
        {
          var row = dataTable.NewRow();
          row["Kernels"] = GetIndex(i);
          for (var j = 0; j < _integrationKernels[i].Item2.Length; ++j)
          {
            row[j + 1] = _integrationKernels[i].Item2[j];
          }

          dataTable.Rows.Add(row);
        }

        return dataTable;
      }
    }

    /// <summary>
    /// The Diagnostic Table Reference Keys
    /// </summary>
    /// <param name="keyId"></param>
    /// <returns></returns>
    protected virtual string GetKey(int keyId)
    {
      switch (keyId)
      {
        case 0: return "PE";
        case 1: return "NE";
        case 2: return "Received";
        case 3: return "Posted";
      }
      throw new ArgumentException($"Invalid Key Id: {keyId}");
    }

    /// <summary>
    /// The Integration Kernal Diagnostic Table Reference Keys
    /// </summary>
    /// <param name="keyId"></param>
    /// <returns></returns>
    protected virtual string GetIndex(int keyId)
    {
      switch (keyId)
      {
        case 0: return "Cpty Default";
        case 1: return "Own Default";
        case 2: return "Survival";
        case 3: return "Ignore Default";
      }
      throw new ArgumentException($"Invalid Key Id: {keyId}");
    }

    /// <summary>
    /// 
    /// </summary>
    public Dt MaxExposureDate => new Dt(NativeImpl.getMaxDate());

    private readonly Dt _asOf;
    private readonly Dt[] _exposureDates;
    private readonly bool _isUnilateral;
    private DataTable _diagnosticTable;
    private readonly IList<Tuple<Dt[], double[]>> _integrationKernels;
    private readonly bool _binaryLoggingEnabled;
    private bool _overrideIntegrationKernels = false;
    private IList<Tuple<Dt[], double[]>> _alternativeIntegrationKernels;
    private IList<Tuple<Dt[], double[]>> _originalIntegrationKernels;


    /// <inheritdoc />
    public void Dispose()
    {
      Dispose(true);
    }

    /// <inheritdoc />
    protected virtual void Dispose(bool dispose)
    {
      if (!dispose || NativeImpl == null || _overrideIntegrationKernels)
      {
        return;
      }
      NativeImpl.Dispose();
      NativeImpl = null;
    }

  }

  /// <summary>
  ///  
  /// </summary>
  public abstract class IncrementalCCRExposureAggregatorManaged : IIncrementalExposureAggregator
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="ci"></param>
    public abstract void AddMeasureAccumulator(CCRMeasure measure, double ci);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public abstract double GetIncrementalMeasure(CCRMeasure measure, int t, double ci);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public abstract double GetIncrementalMeasure(CCRMeasure measure, Dt dt, double ci);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public abstract double GetTotalMeasure(CCRMeasure measure, int t, double ci);

    /// <summary>
    /// 
    /// </summary>
    public abstract void Reduce();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="kernels"></param>
    /// <returns></returns>
    public abstract IIncrementalExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public virtual Dt MaxExposureDate { get; }

    /// <summary>
    /// The positive and negative exposures per path, for each exposure date along 
    /// with the Collateral Payments which were posted and received over the exposure dates 
    /// </summary>
    public abstract DataTable DiagnosticsTable { get; }

    /// <summary>
    /// Datatable of the Integration Kernals used to aggregate the trade level exposures
    /// </summary>
    public abstract DataTable IntegrationKernelsDataTable { get; }
  }

  /// <summary>
  /// Wrapper for native implementation of CCRExposureAggregator
  /// </summary>
  public class CCRExposureAggregatorNativeWrapper : CCRExposureAggregator, IDisposable
  {
    private Dt[] _exposureDates;

    /// <summary>
    /// Construct an aggregator that wraps a native implementation
    /// </summary>
    /// <param name="nativeImpl"></param>
    /// <param name="exposureDts"></param>
    /// <param name="integrationKernels"></param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="asOf"></param>
    /// <param name="discountExposures">discount exposures to present value when reporting expectations</param>
    /// <param name="wrongWayRisk">adjust for correlation between default prob and exposure</param>
    /// <param name="fundingCostNoDefault">report FCA without discounting for default risk</param>
    /// <param name="fundingBenefitNoDefault">report FBA without discounting for default risk</param>
    /// <param name="binaryLoggingEnabled">indicates if binary logging is enabled for Aggregation Statistics</param>
    public CCRExposureAggregatorNativeWrapper(ExposureAggregatorNative nativeImpl, 
                                              Dt asOf,
                                              Dt[] exposureDts,
                                              IList<Tuple<Dt[], double[]>> integrationKernels,
                                              bool unilateral = false,
                                              bool discountExposures = false,
                                              bool wrongWayRisk = true,
                                              bool fundingCostNoDefault = false,
                                              bool fundingBenefitNoDefault = false,
                                              bool binaryLoggingEnabled = false)
    {
      NativeImpl = nativeImpl;
      _exposureDates = exposureDts;
      AsOf = asOf;
      IsUnilateral = unilateral;
      DiscountExposures = discountExposures;
      WrongWayRisk = wrongWayRisk;
      FundingCostNoDefault = fundingCostNoDefault;
      FundingBenefitNoDefault = fundingBenefitNoDefault;
      IntegrationKernels = integrationKernels;
      _binaryLoggingEnabled = binaryLoggingEnabled;
    }

    #region Properties

    /// <summary>
    /// Diagnostics table will be populated 
    /// </summary>
    public override bool DiagnosticsSupported => _binaryLoggingEnabled;

    /// <inheritdoc />
    public override Dt[] ExposureDates
    {
      get { return _exposureDates; }
      protected set { _exposureDates = value; }
    }

    /// <summary>
    /// Number of exposure dates
    /// </summary>
    public override int DateCount => ExposureDates.Length;

    /// <summary>
    /// Number of paths
    /// </summary>
    public override int PathCount => NativeImpl.pathCount();

    /// <summary>
    /// Number of trades or exposure sets
    /// </summary>
    public override int TradeCount => NativeImpl.tradeCount();

    public bool StoreExpAsFloat => NativeImpl.storeExpAsFloat();


    /// <summary>
    /// The positive and negative exposures per path, for each exposure date along 
    /// with the Collateral Payments which were posted and received over the exposure dates 
    /// </summary>
    public override DataTable DiagnosticsTable
    {
      get
      {
        if (_diagnosticTable == null)
        {
          _diagnosticTable = new DataTable();
          _diagnosticTable.Columns.Add("PathId", typeof(int));
          _diagnosticTable.Columns.Add("Key", typeof(string));
          foreach (var date in ExposureDates)
          {
            _diagnosticTable.Columns.Add(date.ToString(), typeof(double));
          }
          for (var pathId = 0; pathId < PathCount; ++pathId)
          {
            for (var i = 0; i < 4; ++i)
            {
              var row = _diagnosticTable.NewRow();
              row["PathId"] = pathId;
              row["Key"] = GetKey(i);
              var rowElements = NativeImpl.diagnosticTable(pathId, i);
              for (var j = 0; j < rowElements.Length; ++j)
              {
                row[j + 2] = rowElements[j];
              }
              _diagnosticTable.Rows.Add(row);
            }
          }
        }
        return _diagnosticTable;
      }
    }

    /// <summary>
    /// Datatable if the Integration Kernals
    /// </summary>
    public override DataTable IntegrationKernelsDataTable
    {
      get
      {
        if (!_binaryLoggingEnabled || IntegrationKernels.Count == 0)
        {
          return null;
        }
        var dataTable = new DataTable();
        dataTable.Columns.Add("Kernels", typeof(string));

        foreach (var date in IntegrationKernels[0].Item1)
        {
          dataTable.Columns.Add(date.ToString(), typeof(double));
        }

        for (var i = 0; i < IntegrationKernels.Count; ++i)
        {
          var row = dataTable.NewRow();
          row["Kernels"] = GetIndex(i);
          for (var j = 0; j < IntegrationKernels[i].Item2.Length; ++j)
          {
            row[j + 1] = IntegrationKernels[i].Item2[j];
          }

          dataTable.Rows.Add(row);
        }

        return dataTable;
      }
    }

    private ExposureAggregatorNative NativeImpl { get; set; }

    #endregion

    #region Methods

    /// <inheritdoc />
    public override void AddMeasureAccumulator(CCRMeasure measure, double ci)
    {
      NativeImpl.addMeasureAccumulator((int)measure, ci);
    }

    /// <inheritdoc />
    public override double GetMeasure(CCRMeasure measure, int t, double ci)
    {
      if (_overrideIntegrationKernels && t <= 0)
        ChangeNativeIntegrationKernels(_alternativeIntegrationKernels);
      var result = NativeImpl.getMeasure((int) measure, t, ci);
      if (_overrideIntegrationKernels && t <= 0)
        ChangeNativeIntegrationKernels(_originalIntegrationKernels);
      return result;

    }

    /// <inheritdoc />
    public override double GetMeasure(CCRMeasure measure, Dt dt, double ci)
    {
      if (_overrideIntegrationKernels && dt.IsEmpty())
        ChangeNativeIntegrationKernels(_alternativeIntegrationKernels);
      var result = NativeImpl.getMeasure((int)measure, dt.ToDouble(), ci);
      if (_overrideIntegrationKernels && dt.IsEmpty())
        ChangeNativeIntegrationKernels(_originalIntegrationKernels);
      return result;
    }

    /// <inheritdoc />
    public override double[] GetMeasureMarginal(CCRMeasure measure, Dt dt, double ci)
    {
      var results = new double[TradeCount];
      if (_overrideIntegrationKernels && dt.IsEmpty())
        ChangeNativeIntegrationKernels(_alternativeIntegrationKernels);
      NativeImpl.getMeasureMarginal((int)measure, dt.ToDouble(), ci, results);
      if (_overrideIntegrationKernels && dt.IsEmpty())
        ChangeNativeIntegrationKernels(_originalIntegrationKernels);
      return results; 
    }

    /// <inheritdoc />
    public override double[] GetMeasureMarginal(CCRMeasure measure, int t, double ci)
    {
      var results = new double[TradeCount];
      if (_overrideIntegrationKernels && t == 0)
        ChangeNativeIntegrationKernels(_alternativeIntegrationKernels);
      NativeImpl.getMeasureMarginal((int)measure, t, ci, results);
      if (_overrideIntegrationKernels && t == 0)
        ChangeNativeIntegrationKernels(_originalIntegrationKernels);
      return results;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="groupCount">Number of netting groups.</param>
    /// <returns></returns>
    /// <remarks>We take groupCount as an argument because where we call this from has easier access to it than we do from here</remarks>
    public byte[] GetNetExposures(int groupCount)
    {
      var agg = NativeImpl as CollateralizedExposureAggregatorNative;

      // currently only support using native aggregators for this
      if (agg == null)
        return null;

      // declare and pin array
      var bytesPerExp = agg.storeAsFloat() ? sizeof(float) : sizeof(double);
      byte[] exposures = new byte[_exposureDates.Length * PathCount * bytesPerExp * groupCount];
      var _gcHandleExposures = GCHandle.Alloc(exposures, GCHandleType.Pinned);

      var x = agg.getNetExposures(_gcHandleExposures.AddrOfPinnedObject());

      // unpin array
      _gcHandleExposures.Free();
      if (x == false)
        return null;
      return exposures;
    }

    private void ChangeNativeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels)
    {
      for (var i = 0; i < kernels.Count; i++)
      {
        var kernel = kernels[i];
        NativeImpl.changeIntegrationKernel(kernel.Item1.Select(dt => dt.ToDouble()).ToArray(), kernel.Item2, i);
      }
    }

    /// <inheritdoc />
    public override CCRExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels)
    {
      var clone = new CCRExposureAggregatorNativeWrapper(NativeImpl, AsOf, ExposureDates, IntegrationKernels, IsUnilateral, DiscountExposures, WrongWayRisk, FundingCostNoDefault, FundingBenefitNoDefault, _binaryLoggingEnabled);
      var transformed = TransformIntegrationKernels(kernels, AsOf, ExposureDates, IsUnilateral);
      clone._overrideIntegrationKernels = true;
      clone._alternativeIntegrationKernels = transformed;
      clone._originalIntegrationKernels = IntegrationKernels;
      return clone;
    }

    /// <inheritdoc />
    public override void Reduce()
    {
      NativeImpl.reduce();
    }

    /// <inheritdoc />
    public void Dispose()
    {
      Dispose(true);
    }

    /// <inheritdoc />
    protected virtual void Dispose(bool dispose)
    {
      if (!dispose || NativeImpl == null || _overrideIntegrationKernels)
      {
        return;
      }
      NativeImpl.Dispose();
      NativeImpl = null;
    }
    
    /// <summary>
    /// Latest exposure date for trade
    /// </summary>
    public override Dt MaxExposureDate(int tradeIdx)
    {
      return new Dt(NativeImpl.getMaxDate(tradeIdx));
    }

    #endregion

    #region Data

    private DataTable _diagnosticTable;
    private readonly bool _binaryLoggingEnabled;
    private bool _overrideIntegrationKernels = false;
    private IList<Tuple<Dt[], double[]>> _alternativeIntegrationKernels;
    private IList<Tuple<Dt[], double[]>> _originalIntegrationKernels;

    #endregion
  }



  /// <summary>
  /// 
  /// </summary>
  public abstract class CCRExposureAggregatorManaged : CCRExposureAggregator
  {

    #region Properties
    
    /// <summary>
    /// Delegate to compute pathwise counterparty exposure
    /// </summary>
    public PathWiseExposure CounterpartyExposure { get; protected set; }

    /// <summary>
    /// Delegate to compute pathwise own exposure
    /// </summary>
    public PathWiseExposure BookingEntityExposure { get; protected set; }

    /// <summary>
    /// Precalculated pvs
    /// </summary>
    public PrecalculatedExposures PrecalculatedPvs { get; protected set; }
    

    /// <summary>
    /// Diagnostics table will be populated 
    /// </summary>
    public override bool DiagnosticsSupported => false;

    /// <summary>
    /// Number of exposure dates
    /// </summary>
    public override int DateCount => ExposureDates.Length;

    /// <summary>
    ///   Number of paths.
    /// </summary>
    public override int PathCount => PrecalculatedPvs.PathCount;

    /// <summary>
    /// Number of trades or exposure sets
    /// </summary>
    public override int TradeCount => PrecalculatedPvs.Count;
    
    /// <summary>
    /// 
    /// </summary>
    public virtual PrecalculatedMarketData PrecalculatedMarketData { get; protected set; }

    /// <summary>
    /// Counterparty Curve used to compute RWA
    /// </summary>
    protected SurvivalCurve CptyCurve { get; set; }

    #endregion

    #region


    /// <summary>
    /// Convert requested measure to toolkit enum value based on configuration
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected CCRMeasure ConvertMeasure(CCRMeasure input)
        {
      CCRMeasure measure = input;
      switch (input)
      {
        case CCRMeasure.CVA:
          if (!WrongWayRisk)
            measure = CCRMeasure.CVA0;
          break;
        case CCRMeasure.DVA:
          if (!WrongWayRisk)
            measure = CCRMeasure.DVA0;
          break;
        case CCRMeasure.FCA:
          if (!WrongWayRisk)
            measure = CCRMeasure.FCA0;
          if (FundingCostNoDefault)
            measure = CCRMeasure.FCANoDefault;
          break;
        case CCRMeasure.FBA:
          if (!WrongWayRisk)
            measure = CCRMeasure.FBA0;
          if (FundingBenefitNoDefault)
            measure = CCRMeasure.FBANoDefault;
          break;
        case CCRMeasure.RWA:
          if (!WrongWayRisk)
            measure = CCRMeasure.RWA0;
          break;
        case CCRMeasure.EE:
          if (!WrongWayRisk)
          {
            measure = CCRMeasure.EE0;
            if (DiscountExposures)
              measure = CCRMeasure.DiscountedEE0;
          }
          if (DiscountExposures)
            measure = CCRMeasure.DiscountedEE;
          break;
        case CCRMeasure.NEE:
          if (!WrongWayRisk)
          {
            measure = CCRMeasure.NEE0;
            if (DiscountExposures)
              measure = CCRMeasure.DiscountedNEE0;
          }
          if (DiscountExposures)
            measure = CCRMeasure.DiscountedNEE;
          break;
        case CCRMeasure.PFE:
          if (!WrongWayRisk)
          {
            measure = CCRMeasure.PFE0;
            if (DiscountExposures)
              measure = CCRMeasure.DiscountedPFE0;
          }
          if (DiscountExposures)
            measure = CCRMeasure.DiscountedPFE;
          break;
        case CCRMeasure.PFNE:
          if (!WrongWayRisk) // TODO: Add PFNE0 and DiscountedPFNE0 measures 
          {
            measure = CCRMeasure.PFNE;
            if (DiscountExposures)
              measure = CCRMeasure.DiscountedPFNE;
          }
          if (DiscountExposures)
            measure = CCRMeasure.DiscountedPFNE;
          break;
        case CCRMeasure.StdErrEE:
          if (DiscountExposures)
            measure = CCRMeasure.StdErrDiscountedEE;
          break;
        case CCRMeasure.StdErrNEE:
          if (DiscountExposures)
            measure = CCRMeasure.StdErrDiscountedNEE;
          break;
      }
      return measure;
    }

    /// <summary>
    /// Latest exposure date for trade
    /// </summary>
    public override Dt MaxExposureDate(int tradeIdx)
    {
      return PrecalculatedPvs.MaxExposureDate(tradeIdx);
    }

    #endregion

  }

  /// <summary>
  /// 
  /// </summary>
  public abstract class CCRExposureAggregator : ICCRMeasureSource
  {
    /// <summary>
    /// Construct a specialized instance of CCRExposureAggregator based on parameters
    /// </summary>
    public static CCRExposureAggregator Create(Dt asOf,
      PrecalculatedMarketData marketData,
      PrecalculatedExposures exposures,
      Netting netting,
      Dt[] exposureDts,
      bool unilateral = false,
      bool discountExposures = false,
      bool wrongWayRisk = true,
      bool fundingCostNoDefault = false,
      bool fundingBenefitNoDefault = false,
      bool applyNetting = true,
      bool applyCollateral = true,
      bool allocateExposures = true,
      bool binaryLoggingEnabled = false,
      bool modelOvercollateralization = false,
      bool pykhtinRosenMethodology = false,
      SurvivalCurve cptyCurve = null
      )
    {
      if (!applyNetting && !applyCollateral)
      {
        // check that each exposure is in a distinct netting group
        if (exposures.NettingGroups.Distinct().Count() != exposures.Count)
        {
          if (pykhtinRosenMethodology)
            return new PykhtinRosenExposureAggregator(asOf, marketData, exposures, netting, exposureDts, unilateral, discountExposures, wrongWayRisk, fundingCostNoDefault, fundingBenefitNoDefault, allocateExposures, binaryLoggingEnabled, modelOvercollateralization, cptyCurve);
          else
            return new CollateralizedExposureAggregator(asOf, marketData, exposures, netting, exposureDts, unilateral, discountExposures, wrongWayRisk, fundingCostNoDefault, fundingBenefitNoDefault, allocateExposures, binaryLoggingEnabled, modelOvercollateralization, cptyCurve);

        }
        return new NoNetExposureAggregator(asOf, marketData, exposures, exposureDts, unilateral, discountExposures, wrongWayRisk, fundingCostNoDefault, fundingBenefitNoDefault, binaryLoggingEnabled, modelOvercollateralization, cptyCurve);
      }
      if (pykhtinRosenMethodology)
        return new PykhtinRosenExposureAggregator(asOf, marketData, exposures, netting, exposureDts, unilateral, discountExposures, wrongWayRisk, fundingCostNoDefault, fundingBenefitNoDefault, allocateExposures, binaryLoggingEnabled, modelOvercollateralization, cptyCurve);

      return new CollateralizedExposureAggregator(asOf, marketData, exposures, netting, exposureDts, unilateral, discountExposures, wrongWayRisk, fundingCostNoDefault, fundingBenefitNoDefault, allocateExposures, binaryLoggingEnabled, modelOvercollateralization, cptyCurve);
    }


    #region Properties
    /// <summary>
    /// Time zero, As of date
    /// </summary>
    public virtual Dt AsOf { get; protected set; }

    /// <summary>
    /// EffectiveSurvival[0] = Probability that counterparty survives to T and that default time of counterparty follows default time of the booking entity  
    /// EffectiveSurvival[1] = Probability that booking entity survives to T and that default time of counterparty precedes default time of the booking entity
    /// </summary>
    public virtual IList<Tuple<Dt[], double[]>> IntegrationKernels
    {
      get;
      protected set; 
    }

    /// <summary>
    /// Netting set for each set of precalculated pvs
    /// </summary>
    public virtual IList<string> NettingSets { get; protected set; }

    /// <summary>
    /// Get unilateral flag
    /// </summary>
    public virtual bool IsUnilateral { get; protected set; }

    /// <summary>
    /// Are exposure measures discounted
    /// </summary>
    public virtual bool DiscountExposures { get; protected set; }

    /// <summary>
    /// Include wrong way risk measure change
    /// </summary>
    public virtual bool WrongWayRisk { get; protected set; }

    /// <summary>
    /// Calculate FCA without default risky discounting
    /// </summary>
    public virtual bool FundingCostNoDefault { get; protected set; }

    /// <summary>
    /// Calculate FBA without default risky discounting
    /// </summary>
    public virtual bool FundingBenefitNoDefault { get; protected set; }

    /// <summary>
    /// Diagnostics for the CCRExposure Aggregator
    /// </summary>
    public virtual DataTable DiagnosticsTable => null;

    /// <summary>
    /// Datatable if the Integration Kernals
    /// </summary>
    public virtual DataTable IntegrationKernelsDataTable
    {
      get
      {
        if (!DiagnosticsSupported || IntegrationKernels.Count == 0)
        {
          return null;
        }
        var dataTable = new DataTable();
        dataTable.Columns.Add("Kernels", typeof(string));

        foreach (var date in IntegrationKernels[0].Item1)
        {
          dataTable.Columns.Add(date.ToString(), typeof(double));
        }

        for (var i = 0; i < IntegrationKernels.Count; ++i)
        {
          var row = dataTable.NewRow();
          row["Kernels"] = GetIndex(i);
          for (var j = 0; j < IntegrationKernels[i].Item2.Length; ++j)
          {
            row[j + 1] = IntegrationKernels[i].Item2[j];
          }

          dataTable.Rows.Add(row);
        }

        return dataTable;
      }
    }

    /// <summary>
    /// Diagnostics table will be populated 
    /// </summary>
    public virtual bool DiagnosticsSupported { get; protected set; }
    
    /// <summary>
    /// Number of exposure dates
    /// </summary>
    public virtual int DateCount { get;  }

    /// <summary>
    /// Exposure dates
    /// </summary>
    public virtual Dt[] ExposureDates { get; protected set; }

    /// <summary>
    ///   Number of paths.
    /// </summary>
    public virtual int PathCount { get; protected set; }

    /// <summary>
    ///   Number of trades or exposure sets.
    /// </summary>
    public abstract int TradeCount{ get;  }

    /// <summary>
    /// Netting group information
    /// </summary>
    public virtual Dictionary<string, int> NettingMap { get; protected set; }

    /// <summary>
    /// The Diagnostic Table Reference Keys
    /// </summary>
    /// <param name="keyId"></param>
    /// <returns></returns>
    protected virtual string GetKey(int keyId)
    {
      switch (keyId)
      {
        case 0: return "PE";
        case 1: return "NE";
        case 2: return "Received";
        case 3: return "Posted";
      }
      throw new ArgumentException($"Invalid Key Id: {keyId}");
    }

    /// <summary>
    /// The Integration Kernal Diagnostic Table Reference Keys
    /// </summary>
    /// <param name="keyId"></param>
    /// <returns></returns>
    protected virtual string GetIndex(int keyId)
    {
      switch (keyId)
      {
        case 0: return "Cpty Default";
        case 1: return "Own Default";
        case 2: return "Survival";
        case 3: return "Ignore Default";
      }
      throw new ArgumentException($"Invalid Key Id: {keyId}");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="ci"></param>
    public abstract void AddMeasureAccumulator(CCRMeasure measure, double ci);

    /// <summary>
    /// Get CCRMeasure for portfolio
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public abstract double GetMeasure(CCRMeasure measure, int t, double ci);

    /// <summary>
    /// Get CCRMeasure for portfolio
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public abstract double GetMeasure(CCRMeasure measure, Dt dt, double ci);



    /// <summary>
    /// Get marginal allocations of CCRMeasure for all trades
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public abstract double[] GetMeasureMarginal(CCRMeasure measure, Dt dt, double ci);

    /// <summary>
    /// Get marginal allocations of CCRMeasure for all trades
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public abstract double[] GetMeasureMarginal(CCRMeasure measure, int t, double ci);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="kernels"></param>
    public abstract CCRExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels);

    /// <summary>
    /// Run simulations and aggregate measures
    /// </summary>
    public abstract void Reduce();

    /// <summary>
    /// Latest exposure date for a trade
    /// </summary>
    /// <param name="tradeIdx"></param>
    public abstract Dt MaxExposureDate(int tradeIdx); 

    #endregion

    #region Private Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <param name="asOf"></param>
    /// <param name="dts"></param>
    /// <param name="isUnilateral"></param>
    /// <returns></returns>
    public static Tuple<Dt[], double[]>[] TransformIntegrationKernels(IList<Tuple<Dt[], double[]>> input, Dt asOf, Dt[] dts, bool isUnilateral)
    {
      // single curve only
      if (input.Count == 3)
      {
        return new[]
        {
            TransformIntegrationKernel(isUnilateral ? input[1] : input[0], asOf, dts) // Cpty default
			  };
      }
      return new[]
      {
          TransformIntegrationKernel(isUnilateral  ? input[1] : input[0], asOf, dts), // Cpty default
					TransformIntegrationKernel(isUnilateral ? input[3] : input[2], asOf, dts), // Own default
					TransformIntegrationKernel(isUnilateral ? input[5] : input[4], asOf, dts), // survival 
				  TransformIntegrationKernel(input[6], asOf, dts) // ignore default
			};
    }

    private static Tuple<Dt[], double[]> TransformIntegrationKernel(Tuple<Dt[], double[]> orig, Dt asOf, Dt[] dts)
    {
      var curve = new Curve(asOf);
      var cumulative = CumulativeSum(orig.Item2).ToArray();
      curve.Add(orig.Item1, cumulative);
      var interpolated = new double[dts.Length];
      interpolated[0] = curve.Interpolate(dts[0]);
      var cum = interpolated[0];
      for (int i = 1; i < interpolated.Length; i++)
      {
        interpolated[i] = curve.Interpolate(dts[i]) - cum;
        cum += interpolated[i];
      }
      var kern = new Tuple<Dt[], double[]>(dts, interpolated);
      return kern;
    }

    private static IEnumerable<double> CumulativeSum(IEnumerable<double> sequence)
    {
      double sum = 0;
      foreach (var item in sequence)
      {
        sum += item;
        yield return sum;
      }
    }
    #endregion
  }
}