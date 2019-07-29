// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;
using InputType = BaseEntity.Toolkit.Models.Simulations.Native.Simulator.InputType;

namespace BaseEntity.Toolkit.Ccr
{
  public static partial class Simulations
  {
    #region Utils

    private static int[] PricerDependencyGraph(IEnumerable<IPricer> pricers, IEnumerable<CalibratedCurve> curves)
    {
      return pricers.Select((p, i) => new Tuple<int, PricerEvaluator>(i, new PricerEvaluator(p))).Where(p => curves.Any(p.Item2.DependsOn)).Select(
        p => p.Item1).ToArray();
    }

    private static ReferenceData ToReferenceData(this object reference, Simulator simulator)
    {
      if (reference == null)
        return null;
      Tuple<CalibratedCurve, InputType, int> info;
      simulator.Map.TryGetValue(reference, out info);
      if (info == null)
        throw new ArgumentException("Reference not found in the MarketEnvironment");
      return new ReferenceData(reference, info.Item1, info.Item2, info.Item3);
    }

    internal static string ReferenceName(object reference)
    {
      var cc = reference as CalibratedCurve;
      if (cc != null)
        return cc.Name;
      var spot = reference as ISpot;
      if (spot != null)
        return spot.Name;
      return String.Empty;
    }

    private static IEnumerable<CurveTenor> ReferenceTenors(object reference) 
    {
      var cc = reference as CalibratedCurve;
      if (cc != null)
        return cc.Tenors;
      return null;
    }

    private static Dt AsOfDate(object reference)
    {
      var cc = reference as CalibratedCurve;
      if (cc != null)
        return cc.AsOf;
      var spot = reference as ISpot;
      if (spot != null)
        return spot.Spot;
      return Dt.Empty;
    }

    private static CalibratedCurve ToCalibratedCurve(this ISpot spot)
    {
      return new VolatilityCurve(spot.Spot, spot.Value) {Name = spot.Name};
    }

    private static CalibratedCurve ReferenceAsCurve(this ReferenceData data)
    {
      var cc = data.Reference as CalibratedCurve;
      if (cc != null)
        return cc;
      var spot = data.Reference as ISpot;
      if (spot != null)
        return spot.ToCalibratedCurve();
      throw new ArgumentException("Reference type not handled");
    }

    private static ReferenceData[] GetReferenceData(this IEnumerable<object> references, Simulator simulator)
    {
      return (references == null) ? null : references.Select(r => r.ToReferenceData(simulator)).Where(r => r != null).ToArray();
    }

    private static bool GenerateFactorBumps(int n, bool bumpDown, bool bumpRelative, ref double[] bump)
    {
      if (bump == null || bump.Length == 0)
      {
        double blip = bumpDown ? -0.05 : 0.05;
        bump = ArrayUtil.Generate(n, i => blip);
        return true;
      }
      if (bump.Length == 1)
      {
        double blip = bump[0];
        if (bumpDown)
          blip = -blip;
        if (bumpRelative && blip < 0.0)
          blip = blip / (1.0 - blip); //make sure bump is less than -100%
        else if (!bumpRelative && Math.Abs(blip) > 1.0)//1 = 1%
          blip *= 1e-2;
        bump = ArrayUtil.Generate(n, i => blip);
        return bumpRelative;
      }
      if (bump.Length == n)
      {
        for (int i = 0; i < n; ++i)
        {
          double blip = bump[i];
          if (bumpDown)
            blip = -blip;
          if (bumpRelative && blip < 0.0)
            blip = blip / (1.0 - blip); //make sure bump is less than -100%
          else if (!bumpRelative && Math.Abs(blip) > 1.0)
            blip *= 1e-2;
          bump[i] = blip;
        }
        return bumpRelative;
      }
      throw new ArgumentException(String.Format("Bumps expected of size 1 or {0}", n));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="originalCurve"></param>
    /// <param name="bump"></param>
    /// <param name="bumpRelative"></param>
    /// <param name="bumpDown"></param>
    /// <param name="targetQuoteType"></param>
    /// <param name="bumpTenors"></param>
    /// <param name="fastClone"></param>
    /// <returns></returns>
    public static CalibratedCurve PerturbTermStructure(CalibratedCurve originalCurve, double bump, bool bumpRelative, bool bumpDown, QuotingConvention targetQuoteType, string[] bumpTenors, bool fastClone = true)
    {
      BumpFlags flags = bumpRelative ? BumpFlags.BumpRelative : 0;
      if (bumpDown)
        flags |= BumpFlags.BumpDown;
      var retVal = fastClone ? originalCurve.CloneObjectGraph(CloneMethod.FastClone) : originalCurve.Clone() as CalibratedCurve; 
      retVal.DependentCurves = new Dictionary<long, CalibratedCurve>(); //empty the dependent curve list
      new[] {retVal}.BumpQuotes(bumpTenors, targetQuoteType, bump, flags | BumpFlags.RefitCurve);
      return retVal;
    }

    private static double[] PerturbFactorLoadings(double[,] fl, int tenor, double[] bump, bool bumpRelative)
    {
      return CalibrationUtils.PerturbFactorLoadings(fl, tenor, bump, bumpRelative);
    }

    private static Tuple<CalibratedCurve[], int[], InputType[]> PerturbTermStructure(ReferenceData[] references, double bump,
                                                                                                                     bool bumpRelative, bool bumpDown,
                                                                                                                     QuotingConvention targetQuoteType,
                                                                                                                     string[] bumpTenors)
    {
      int n = references.Length;
      var bumpedCurves = new CalibratedCurve[n];
      var inputIndex = new int[n];
      var inputType = new InputType[n];
      for (int i = 0; i < references.Length; ++i)
      {
        var r = references[i];
        var original = r.ReferenceAsCurve();
        bumpedCurves[i] = PerturbTermStructure(original, bump, bumpRelative, bumpDown, targetQuoteType, bumpTenors);
        inputIndex[i] = r.Index;
        inputType[i] = r.InputType;
      }
      return new Tuple<CalibratedCurve[], int[], InputType[]>(bumpedCurves, inputIndex, inputType);
    }

    private static Tuple<VolatilityCurve[], int[], int[], InputType[]> PerturbVolatility(
      VolatilityCollection originalVolatilities, IEnumerable<ReferenceData> references, int[] tenIndex, double bump, bool bumpRelative, bool bumpDown)
    {
      BumpFlags flags = bumpRelative ? BumpFlags.BumpRelative : 0;
      if (bumpDown)
        flags |= BumpFlags.BumpDown;
      var bumpedCurves = new List<VolatilityCurve>();
      var inputIndex = new List<int>();
      var tenorIndex = new List<int>();
      var inputType = new List<InputType>();
      foreach (var r in references)
      {
        var v = originalVolatilities.GetVolsAt(r.Reference);
        if (v == null)
          continue;
        int idx = r.Index;
        var type = r.InputType;
        for (int j = 0; j < Math.Min(tenIndex.Length, v.Length); ++j)
        {
          int tenor = Math.Min(tenIndex[j], v.Length - 1);
          var vj = v[tenor];
          var bumped = new CalibratedCurve[]{vj.CloneObjectGraph(CloneMethod.FastClone)};
          bumped.BumpQuotes(new string[0], QuotingConvention.None, bump, flags | BumpFlags.RefitCurve);
          bumpedCurves.Add((VolatilityCurve)bumped[0]);
          inputIndex.Add(idx);
          tenorIndex.Add(tenor);
          inputType.Add(type);
        }
      }
      return new Tuple<VolatilityCurve[], int[], int[], InputType[]>(bumpedCurves.ToArray(), inputIndex.ToArray(), tenorIndex.ToArray(),
                                                                               inputType.ToArray());
    }

    private static Tuple<double[,], int[], int[], InputType[]> PerturbFactorLoadings(FactorLoadingCollection originalFactors, IEnumerable<ReferenceData> references, int[] tenIndex, double[] bump,
                                                                                                  bool bumpRelative)
    {
      var bumpedFactors = new List<double[]>();
      var inputIndex = new List<int>();
      var tenorIndex = new List<int>();
      var inputType = new List<InputType>();
      foreach (var r in references)
      {
        var fl = originalFactors.GetFactorsAt(r.Reference);
        if (fl == null)
          continue;
        int idx = r.Index;
        var type = r.InputType;
        for (int j = 0; j < Math.Min(tenIndex.Length, fl.GetLength(0)); ++j)
        {
          int tenor = Math.Min(tenIndex[j], fl.GetLength(0) - 1);
          var bumped = PerturbFactorLoadings(fl, tenor, bump, bumpRelative);
          bumpedFactors.Add(bumped);
          inputIndex.Add(idx);
          tenorIndex.Add(tenor);
          inputType.Add(type);
        }
      }
      var bumpedFactorMatrix = new double[bumpedFactors.Count,originalFactors.FactorCount];
      for (int i = 0; i < bumpedFactorMatrix.GetLength(0); ++i)
        for (int j = 0; j < bumpedFactorMatrix.GetLength(1); ++j)
          bumpedFactorMatrix[i, j] = bumpedFactors[i][j];
      return new Tuple<double[,], int[], int[], InputType[]>(bumpedFactorMatrix, inputIndex.ToArray(), tenorIndex.ToArray(), inputType.ToArray());
    }

    private class ReferenceData
    {
      public readonly CalibratedCurve DependentTermStructure;
      public readonly int Index;
      public readonly InputType InputType;
      public readonly object Reference;

      public ReferenceData(object reference, CalibratedCurve dependentTermStructure, InputType inputType, int index)
      {
        Reference = reference;
        DependentTermStructure = dependentTermStructure;
        Index = index;
        InputType = inputType;
      }
    }

    #endregion

    #region Perturbation

    /// <summary>
    /// Perturbed input base class
    /// </summary>
    public abstract class Perturbation
    {
      #region Data

      /// <summary>
      /// Name of item being bumped
      /// </summary>
      public string Id { get; internal set; }
      /// <summary>
      /// Tenor being bumped
      /// </summary>
      public string Tenor { get; internal set; }

      internal readonly object[] References;
      #endregion

      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      ///<param name="id">Perturbed process id</param>
      ///<param name="ten">Perturbed tenor id</param>
      /// <param name="references">Perturbed object references</param>
      internal Perturbation(string id, string ten, object[] references)
      {
        Id = id;
        Tenor = ten;
        References = references;
      }

      #endregion

      #region Methods

      /// <summary>
      /// Generate perturbations
      /// </summary>
      /// <param name="references">Reference objects</param>
      /// <param name="upBump">Up bump</param>
      /// <param name="downBump">Down bump</param>
      /// <param name="bumpRelative">Bump is relative</param>
      /// <param name="bumpType">Bump type</param>
      /// <param name="targetQuoteType">Target quote type</param>
      /// <param name="bumpTenors">Tenors to bump</param>
      /// <param name="calcGamma">True to calculate gamma</param>
      /// <returns>Perturbations</returns>
      internal static Tuple<Perturbation[], bool> GenerateTermStructurePerturbations(object[] references,
                                                                                        double upBump,
                                                                                        double downBump,
                                                                                        bool bumpRelative,
                                                                                        BumpType bumpType,
                                                                                        QuotingConvention
                                                                                          targetQuoteType,
                                                                                        string[] bumpTenors,
                                                                                        bool calcGamma) 
      {
        return TermStructurePerturbation.Generate(references, upBump, downBump, bumpRelative, bumpType, targetQuoteType, bumpTenors, calcGamma);
      }

      /// <summary>
      /// Generate perturbations
      /// </summary>
      /// <param name="references">Reference objects</param>
      /// <param name="originalFactors">Originial factor loadings</param>
      /// <param name="upBump">Up bump</param>
      /// <param name="downBump">Down bump</param>
      /// <param name="bumpRelative">Bump is relative</param>
      /// <param name="bumpType">Bump type</param>
      /// <param name="calcGamma">True to calculate gamma</param>
      /// <returns>Perturbations</returns>
      /// <remarks>Default bump is 5% relative</remarks>
      internal static Tuple<Perturbation[], bool> GenerateFactorPerturbations(object[] references,
                                                                                 FactorLoadingCollection originalFactors,
                                                                                 double[] upBump,
                                                                                 double[] downBump,
                                                                                 bool bumpRelative,
                                                                                 BumpType bumpType,
                                                                                 bool calcGamma)
      {
        return FactorPerturbation.Generate(references, originalFactors, upBump, downBump, bumpRelative, bumpType, calcGamma);
      }

      /// <summary>
      /// Generate perturbations
      /// </summary>
      /// <param name="references">Reference objects</param>
      /// <param name="originalVols">Original volatilities</param>
      /// <param name="upBump">Up bump</param>
      /// <param name="downBump">Down bump</param>
      /// <param name="bumpRelative">Bump is relative</param>
      /// <param name="bumpType">Bump type</param>
      /// <param name="calcGamma">True to calculate gamma</param>
      /// <returns>Perturbations</returns>
      internal static Tuple<Perturbation[], bool> GenerateVolatilityPerturbations(object[] references,
                                                                                     VolatilityCollection originalVols,
                                                                                     double upBump,
                                                                                     double downBump,
                                                                                     bool bumpRelative,
                                                                                     BumpType bumpType,
                                                                                     bool calcGamma)
      {
        return VolatilityPerturbation.Generate(references, originalVols, upBump, downBump, bumpRelative, bumpType, calcGamma);
      }

      /// <summary>
      /// Perturb the original simulator
      /// </summary>
      /// <param name="simulator">Base simulator</param>
      /// <param name="pricers">Portfolio</param>
      /// <returns>[Perturbed simulator, Pricer dependency graph]</returns>
      internal abstract Tuple<Simulator, int[]> GetPerturbedSimulator(Simulator simulator, params IPricer[] pricers);

      /// <summary>
      /// Return a simulator for the perturbed scenario
      /// </summary>
      public abstract Simulator PerturbSimulator(Simulator simulator);

      #endregion
    }

    #endregion

    #region FactorPerturbation

    /// <summary>
    /// Perturb factor loadings
    ///</summary>
    public class FactorPerturbation : Perturbation
    {
      #region Data

      private readonly double[] bump_;
      private readonly bool bumpRelative_;
      private readonly FactorLoadingCollection originalFactors_;
      private readonly int[] tenorIndex_;

      #endregion

      #region Properties

      /// <summary>
      /// size of bump for each factor loading
      /// </summary>
      public double[] Bump
      {
        get { return bump_; }
      }

      /// <summary>
      /// Are the bumps relative or absolute
      /// </summary>
      public bool IsRelative
      {
        get { return bumpRelative_; }
      }

      #endregion


      #region Constructor

      private FactorPerturbation(string id, string ten, object[] references, FactorLoadingCollection originalFactors, int[] tenorIndex, double[] bump, bool bumpRelative)
        :
          base(id, ten, references)
      {
        originalFactors_ = originalFactors;
        tenorIndex_ = tenorIndex;
        bump_ = bump;
        bumpRelative_ = bumpRelative;
      }

      #endregion

      #region Methods
      private static Perturbation Uniform(FactorLoadingCollection originalFactors, object[] references, double[] bump, bool bumpRelative)
      {
        if (originalFactors == null || !references.Any(r => originalFactors.References.Contains(r)))
          return null;
        return new FactorPerturbation(String.Concat(references.First().GetType(), "-Factors"), "All", references, originalFactors,
                                      ArrayUtil.Generate(originalFactors.TenorCount, i => i), bump, bumpRelative);
      }

      private static Perturbation Parallel(FactorLoadingCollection originalFactors, object reference, double[] bump, bool bumpRelative)
      {
        if (originalFactors == null || !originalFactors.References.Contains(reference))
          return null;
        return new FactorPerturbation(FactorLoadingCollection.GetId(reference), "All", new[] { reference }, originalFactors,
                                         ArrayUtil.Generate(originalFactors.TenorCount, i => i), bump, bumpRelative);
      }

      private static Perturbation ByTenor(FactorLoadingCollection originalFactors, object reference, int tenor, double[] bump, bool bumpRelative)
      {
        if (originalFactors == null || !originalFactors.References.Contains(reference))
          return null;
        return new FactorPerturbation(String.Concat(FactorLoadingCollection.GetId(reference), "-Factors"), originalFactors.Tenors[tenor].ToString(),
                                         new[] {reference}, originalFactors, new[] {tenor}, bump, bumpRelative);
      }

      internal static Tuple<Perturbation[], bool> Generate(object[] references, FactorLoadingCollection originalFactors, double[] upBump,
                                                           double[] downBump, bool bumpRelative, BumpType bumpType,
                                                           bool calcGamma)
      {
        bumpRelative = GenerateFactorBumps(originalFactors.FactorCount, false, bumpRelative, ref upBump);
        calcGamma = calcGamma && (downBump != null && downBump.Length != 0);
        if (calcGamma)
          bumpRelative &= GenerateFactorBumps(originalFactors.FactorCount, true, bumpRelative, ref downBump);
        var retVal = new List<Perturbation>();
        if (bumpType == BumpType.Uniform)
        {
          var p = Uniform(originalFactors, references, upBump, bumpRelative);
          if (p != null)
          {
            retVal.Add(p);
            if (calcGamma)
              retVal.Add(Uniform(originalFactors, references, downBump, bumpRelative));
          }
        }
        else if (bumpType == BumpType.Parallel)
        {
          foreach (var r in references)
          {
            var p = Parallel(originalFactors, r, upBump, bumpRelative);
            if (p != null)
            {
              retVal.Add(p);
              if (calcGamma)
                retVal.Add(Parallel(originalFactors, r, downBump, bumpRelative));
            }
          }
        }
        else if (bumpType == BumpType.ByTenor)
        {
          foreach (var r in references)
          {
            for (int i = 0; i < originalFactors.TenorCount; ++i)
            {
              var p = ByTenor(originalFactors, r, i, upBump, bumpRelative);
              if (p != null)
              {
                retVal.Add(p);
                if (calcGamma)
                  retVal.Add(ByTenor(originalFactors, r, i, downBump, bumpRelative));
              }
            }
          }
        }
        else throw new ArgumentException("BumpType not supported");
        return new Tuple<Perturbation[], bool>(retVal.ToArray(), calcGamma);
      }

      internal override Tuple<Simulator, int[]> GetPerturbedSimulator(Simulator simulator, params IPricer[] pricers)
      {
        var refData = References.GetReferenceData(simulator);
        int[] dependencyGraph = null;
        if (pricers != null)
        {
          dependencyGraph = refData.Any(r => r.InputType == InputType.DiscountRateInput && r.Index == 0)
                              ? pricers.Select((p, i) => i).ToArray()
                              : PricerDependencyGraph(pricers, refData.Select(r => r.DependentTermStructure));
        }
        var fl = PerturbFactorLoadings(originalFactors_, refData, tenorIndex_, bump_, bumpRelative_);
        var perturbedSimulator = Simulator.PerturbFactors(simulator, fl.Item1, fl.Item2, fl.Item3, fl.Item4);
        return new Tuple<Simulator, int[]>(perturbedSimulator, dependencyGraph);
      }

      /// <summary>
      /// Perturb Simulator
      /// </summary>
      /// <param name="simulator">simulator</param>
      /// <returns></returns>
      public override Simulator PerturbSimulator(Simulator simulator)
      {
        var refData = References.GetReferenceData(simulator);
        var fl = PerturbFactorLoadings(originalFactors_, refData, tenorIndex_, bump_, bumpRelative_);
        var perturbedSimulator = Simulator.PerturbFactors(simulator, fl.Item1, fl.Item2, fl.Item3, fl.Item4);
        return perturbedSimulator;
      }

      #endregion
    }

    #endregion

    #region VolatilityPerturbation

    /// <summary>
    /// Volatility Perturbation 
    /// </summary>
    public class VolatilityPerturbation : Perturbation
    {
      #region Data

      private readonly double bump_;
      private readonly bool bumpDown_;
      private readonly bool bumpRelative_;
      private readonly VolatilityCollection originalVolatility_;
      private readonly int[] tenorIndex_;

      #endregion

      #region Properties

      /// <summary>
      /// Size of bump
      /// </summary>
      public double Bump
      {
        get { return bump_;  }
      }

      /// <summary>
      /// Is bump direction down
      /// </summary>
      public bool IsDownBump
      {
        get { return bumpDown_; }
      }

      /// <summary>
      /// Is bump amount relative or absolute
      /// </summary>
      public bool IsRelative
      {
        get { return bumpRelative_; }
      }

      #endregion

      #region Constructor

      private VolatilityPerturbation(string id, string ten, object[] references, VolatilityCollection originalVolatility, int[] tenorIndex, double bump,
                                     bool bumpRelative, bool bumpDown) :
                                       base(id, ten, references)
      {
        tenorIndex_ = tenorIndex;
        originalVolatility_ = originalVolatility;
        bump_ = bump;
        bumpRelative_ = bumpRelative;
        bumpDown_ = bumpDown;
      }

      #endregion

      #region Methods

      private static Perturbation Uniform(VolatilityCollection originalVols, object[] references, double bump, bool bumpRelative, bool bumpDown)
      {
        if (originalVols == null || !references.Any(r => originalVols.References.Contains(r)))
          return null;
        return new VolatilityPerturbation(string.Concat(references.First().GetType(), "-Vol"), "All", references, originalVols,
                                             ArrayUtil.Generate(originalVols.TenorCount, i => i), bump, bumpRelative, bumpDown);
      }

      private static Perturbation Parallel(VolatilityCollection originalVols, object reference,
                                           double bump, bool bumpRelative, bool bumpDown)
      {
        if (originalVols == null || !originalVols.References.Contains(reference))
          return null;
        return new VolatilityPerturbation(String.Concat(VolatilityCollection.GetId(reference), "-Vol"), "All", new[] { reference }, originalVols,
                                             ArrayUtil.Generate(originalVols.TenorCount, i => i), bump, bumpRelative, bumpDown);
      }

      private static Perturbation ByTenor(VolatilityCollection originalVols, object reference,
                                          int tenor, double bump, bool bumpRelative, bool bumpDown)
      {
        if (originalVols == null || !originalVols.References.Contains(reference))
          return null;
        return new VolatilityPerturbation(String.Concat(VolatilityCollection.GetId(reference), "-Vol"), originalVols.Tenors[tenor].ToString(),
                                             new[] {reference}, originalVols, new[] {tenor}, bump, bumpRelative, bumpDown);
      }

      internal static Tuple<Perturbation[], bool> Generate(object[] references, VolatilityCollection originalVols, double upBump,
                                                           double downBump, bool bumpRelative, BumpType bumpType,
                                                           bool calcGamma)
      {
        calcGamma = calcGamma && (downBump > 0.0);
        var retVal = new List<Perturbation>();
        if (bumpType == BumpType.Uniform)
        {
          Perturbation p = Uniform(originalVols, references, upBump, bumpRelative, false);
          if (p != null)
          {
            retVal.Add(p);
            if (calcGamma)
              retVal.Add(Uniform(originalVols, references, downBump, bumpRelative, true));
          }
        }
        else if (bumpType == BumpType.Parallel)
        {
          foreach (var r in references)
          {
            Perturbation p = Parallel(originalVols, r, upBump, bumpRelative, false);
            if (p != null)
            {
              retVal.Add(p);
              if (calcGamma)
                retVal.Add(Parallel(originalVols, r, downBump, bumpRelative, true));
            }
          }
        }
        else if (bumpType == BumpType.ByTenor)
        {
          foreach (var r in references)
          {
            for (int i = 0; i < originalVols.TenorCount; ++i)
            {
              Perturbation p = ByTenor(originalVols, r, i, upBump, bumpRelative, false);
              if (p != null)
              {
                retVal.Add(p);
                if (calcGamma)
                  retVal.Add(ByTenor(originalVols, r, i, downBump, bumpRelative, true));
              }
            }
          }
        }
        else throw new ArgumentException("BumpType not supported");
        return new Tuple<Perturbation[], bool>(retVal.ToArray(), calcGamma);
      }

      internal override Tuple<Simulator, int[]> GetPerturbedSimulator(Simulator simulator, params IPricer[] pricers)
      {
        var refData = References.GetReferenceData(simulator);
        int[] dependencyGraph = null;
        if (pricers != null)
        {
          dependencyGraph = refData.Any(r => r.InputType == InputType.DiscountRateInput && r.Index == 0)
                              ? pricers.Select((p, i) => i).ToArray()
                              : PricerDependencyGraph(pricers, refData.Select(r => r.DependentTermStructure));
        }
        var vols = PerturbVolatility(originalVolatility_, refData, tenorIndex_, bump_, bumpRelative_, bumpDown_);
        var perturbedSimulator = Simulator.PerturbVolatilities(simulator, vols.Item1, vols.Item2, vols.Item3, vols.Item4);
        return new Tuple<Simulator, int[]>(perturbedSimulator, dependencyGraph);
      }

      /// <summary>
      /// Perturb simulator
      /// </summary>
      /// <param name="simulator">simulator</param>
      /// <returns></returns>
      public override Simulator PerturbSimulator(Simulator simulator)
      {
        var refData = References.GetReferenceData(simulator);
        var vols = PerturbVolatility(originalVolatility_, refData, tenorIndex_, bump_, bumpRelative_, bumpDown_);
        var perturbedSimulator = Simulator.PerturbVolatilities(simulator, vols.Item1, vols.Item2, vols.Item3, vols.Item4);
        return perturbedSimulator;
      }

      #endregion
    }

    #endregion

    #region TermStructurePerturbation

    /// <summary>
    /// Perturbation for a curve
    /// </summary>
    public class TermStructurePerturbation : Perturbation
    {
      #region Data

      private readonly double bump_;
      private readonly bool bumpDown_;
      private readonly bool bumpRelative_;
      private readonly string[] bumpTenors_;
      private readonly QuotingConvention targetQuoteType_;

      #endregion

      #region Properties

      /// <summary>
      /// Size of bump
      /// </summary>
      public double Bump
      {
        get { return bump_; }
      }

      /// <summary>
      /// Direction
      /// </summary>
      public bool IsDownBump
      {
        get { return bumpDown_; }
      }

      /// <summary>
      /// Relative or absolute
      /// </summary>
      public bool IsRelative
      {
        get { return bumpRelative_; }
      }

      #endregion

      #region Constructor

      private TermStructurePerturbation(string id, string ten, object[] references, string[] bumpTenors, double bump,
                                        bool bumpRelative, bool bumpDown, QuotingConvention targetQuoteType) :
                                          base(id, ten, references)
      {
        bump_ = bump;
        bumpRelative_ = bumpRelative;
        bumpDown_ = bumpDown;
        targetQuoteType_ = targetQuoteType;
        bumpTenors_ = bumpTenors;
      }

      #endregion

      #region Methods

      private static Perturbation Uniform(object[] references, double bump, bool bumpRelative,
                                          bool bumpDown, QuotingConvention targetQuoteType)
      {
        if(references == null || references.Length == 0)
          return null;
        return new TermStructurePerturbation(references[0].GetType().ToString(), "All", references, new string[0], bump, bumpRelative, bumpDown, targetQuoteType);
      }

      private static Perturbation Parallel(object reference, double bump, bool bumpRelative,
                                           bool bumpDown, QuotingConvention targetQuoteType)
      {
        return new TermStructurePerturbation(ReferenceName(reference), "All", new[] {reference}, new string[0], bump, bumpRelative, bumpDown, targetQuoteType);
      }

      private static Perturbation ByTenor(object reference, string tenor, double bump,
                                          bool bumpRelative, bool bumpDown, QuotingConvention targetQuoteType)
      {
        return ByTenor(reference, tenor, new[] {tenor}, bump, bumpRelative, bumpDown, targetQuoteType);
      }

      private static Perturbation ByTenor(object reference, string bucket, string[] tenors, double bump,
                                          bool bumpRelative, bool bumpDown, QuotingConvention targetQuoteType)
      {
        return new TermStructurePerturbation(ReferenceName(reference), bucket, new[] {reference}, tenors, bump, bumpRelative, bumpDown,
                                                targetQuoteType);
      }

      internal static Tuple<Perturbation[], bool> Generate(object[] references, double upBump, double downBump, bool bumpRelative,
                                                           BumpType bumpType, QuotingConvention targetQuoteType, string[] bumpTenors,
                                                           bool calcGamma)
      {
        var retVal = new List<Perturbation>();
        calcGamma = calcGamma && (downBump > 0.0);
        if (bumpType == BumpType.Uniform)
        {
          retVal.Add(Uniform(references, upBump, bumpRelative, false, targetQuoteType));
          if (calcGamma)
            retVal.Add(Uniform(references, downBump, bumpRelative, true, targetQuoteType));
        }
        else if (bumpType == BumpType.Parallel)
        {
          foreach (var r in references)
          {
            retVal.Add(Parallel(r, upBump, bumpRelative, false, targetQuoteType));
            if (calcGamma)
              retVal.Add(Parallel(r, downBump, bumpRelative, true, targetQuoteType));
          }
        }
        else if (bumpType == BumpType.ByTenor)
        {
          foreach (var r in references)
          {
            var tenors = ReferenceTenors(r);
            if (tenors == null)
            {
              retVal.Add(Parallel(r, upBump, bumpRelative, false, targetQuoteType));
              if (calcGamma)
                retVal.Add(Parallel(r, downBump, bumpRelative, true, targetQuoteType));
              continue;
            }
            if (bumpTenors == null || bumpTenors.Length == 0)
            {
              foreach (string ten in tenors.Select(t => t.Name))
              {
                retVal.Add(ByTenor(r, ten, upBump, bumpRelative, false, targetQuoteType));
                if (calcGamma)
                  retVal.Add(ByTenor(r, ten, downBump, bumpRelative, true, targetQuoteType));
              }
            }
            else
            {
              Dt asOf = AsOfDate(r);
              Dt start = asOf;
              foreach (string ten in bumpTenors)
              {
                Dt end =  (r is SurvivalCurve)? Dt.CDSMaturity(asOf, ten) :  Dt.Add(asOf, ten);
                var curveTenors = (from t in ReferenceTenors(r)
                                   where t.Maturity > start && t.Maturity <= end
                                   select t.Name).ToArray();

                if (!curveTenors.Any())
                  continue;
                retVal.Add(ByTenor(r, ten, curveTenors, upBump, bumpRelative, false, targetQuoteType));
                if (calcGamma)
                  retVal.Add(ByTenor(r, ten, curveTenors, downBump, bumpRelative, true, targetQuoteType));
                start = end;
              }
            }
          }
        }
        else
        {
          throw new ArgumentException("BumpType not supported");
        }
        return new Tuple<Perturbation[], bool>(retVal.ToArray(), calcGamma);
      }

      internal override Tuple<Simulator, int[]> GetPerturbedSimulator(Simulator simulator, params IPricer[] pricers)
      {
        var refData = References.GetReferenceData(simulator);
        int[] dependencyGraph = null;
        if (pricers != null)
        {
          dependencyGraph = refData.Any(r => r.InputType == InputType.DiscountRateInput && r.Index == 0)
                              ? pricers.Select((p, i) => i).ToArray()
                              : PricerDependencyGraph(pricers, refData.Select(r => r.DependentTermStructure));
        }
        var ts = PerturbTermStructure(refData, bump_, bumpRelative_, bumpDown_, targetQuoteType_, bumpTenors_);
        var perturbedSimulator = Simulator.PerturbTermStructures(simulator, ts.Item1, ts.Item2, ts.Item3);
        return new Tuple<Simulator, int[]>(perturbedSimulator, dependencyGraph);
      }

      /// <summary>
      /// Perturb simulator
      /// </summary>
      /// <param name="simulator">simulator</param>
      /// <returns></returns>
      public override Simulator PerturbSimulator(Simulator simulator)
      {
        var refData = References.GetReferenceData(simulator);
        var ts = PerturbTermStructure(refData, bump_, bumpRelative_, bumpDown_, targetQuoteType_, bumpTenors_);
        var perturbedSimulator = Simulator.PerturbTermStructures(simulator, ts.Item1, ts.Item2, ts.Item3);
        return perturbedSimulator;
      }
      #endregion
    }

    #endregion
  }
}