//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Models.HullWhiteShortRates
{
  using NUnit.Framework;

  [TestFixture]
  public class CcrTests
  {
    [Test]
    public void Test()
    {
      var data = (object[]) XmlSerialization.ReadXmlFile(
        @"toolkit\test\data\hw-ccr-data-set-01.xml".GetFullPath(true));
      var fvs = (FactorizedVolatilitySystem) data[0];
      var simuDates = ((object[]) data[1])
        .Select(d => Dt.FromExcelDate((double) d))
        .ToArray();
      var cptyCurves = data.OfType<SurvivalCurve>().ToArray();
      Assert.AreEqual(2, cptyCurves.Length);
      var pricers = data.OfType<IPricer>().ToArray();
      Assert.AreEqual(2, pricers.Length);
      var liborCurves = pricers.Select(p => p.GetDiscountCurve())
        .Distinct().ToArray();

      var asOf = pricers[0].AsOf;
      var choice = new SimulationModelChoice(
        MultiStreamRng.Type.MersenneTwister,
        SimulationModels.HullWhiteModel);
      var nettingSets = pricers.Select(p => p.Product.Description).ToArray();
      var calc = BaseEntity.Toolkit.Ccr.Simulations.SimulateCounterpartyCreditRisks(
        choice,
        asOf,
        pricers,
        nettingSets,
        cptyCurves,
        new[] {0.4, 0.4},
        Array.ConvertAll(fvs.Volatilities.Tenors, t => Dt.Add(asOf, t)),
        liborCurves,
        null,
        null,
        null,
        fvs.Volatilities,
        fvs.FactorLoadings,
        -100,
        1000,
        simuDates,
        Tenor.SixMonths, // grid size
        false);
      calc.Execute();

      string[] groups = nettingSets.Distinct().ToArray();
      string[] superGroups = Array.ConvertAll(groups, i => "all");
      var engine = new CCRExposures(calc, groups, superGroups, null);
      /*
    cva	-71813.4861697763	double
    dva	91615.596762057568	double
    ec	-328787.46144026885	double
       */
      var cva = engine.GetMeasure(CCRMeasure.CVA, Dt.Empty, 1);
      var dva = engine.GetMeasure(CCRMeasure.DVA, Dt.Empty, 1);
      var ec = engine.GetMeasure(CCRMeasure.EC, Dt.Empty, 0.99);

      return;
    }

    #region Backward compatible serialization helper

    static readonly ISimpleXmlSerializer VolatilitySerializer
      = new VolatilityParameterSerializer();

    static CcrTests()
    {
      CustomSerializers.Register(VolatilitySerializer);
    }

    class VolatilityParameterSerializer : ISimpleXmlSerializer
    {
      public bool CanHandle(Type type)
      {
        return type == typeof(IVolatilityProcessParameter);
      }

      public object ReadValue(XmlReader reader, SimpleXmlSerializer settings, Type type)
      {
        Debug.Assert(string.IsNullOrEmpty(reader.GetAttribute("type")));
        var curves = (VolatilityCurve[])SimpleXmlSerializationUtility.ReadValue(
          reader, settings, typeof(VolatilityCurve[]));
        return new StaticVolatilityCurves(curves);
      }

      public void WriteValue(XmlWriter writer, SimpleXmlSerializer settings, object data)
      {
        throw new NotImplementedException();
      }
    }

    #endregion
  }


  ///<summary>
  /// Holds the cube of simulated forward exposures plus additional netting information
  ///</summary>
  [Serializable]
  public class CCRExposures : ISimulationData, ICCRMeasureSource
  {
    #region Implementation of ISimulatedValues

    /// <summary>
    ///   Number of dates on each path.
    /// </summary>
    int ISimulatedValues.DateCount => Exposures.SimulatedValues.DateCount;

    /// <summary>
    /// Exposure dates
    /// </summary>
    Dt[] ISimulatedValues.ExposureDates => Exposures.SimulatedValues.ExposureDates;

    /// <summary>
    ///   Number of paths.
    /// </summary>
    int ISimulatedValues.PathCount => Exposures.SimulatedValues.PathCount;

    MultiStreamRng.Type ISimulatedValues.RngType => Exposures.SimulatedValues.RngType;

    /// <summary>
    /// Netting group information
    /// </summary>
    Dictionary<string, int> ISimulatedValues.NettingMap => Exposures.SimulatedValues.NettingMap;

    /// <summary>
    ///   Simulated realizations.
    /// </summary>
    IEnumerable<ISimulatedPathValues> ISimulatedValues.Paths => Exposures.SimulatedValues.Paths;

    /// <summary>
    /// Max simulation step size
    /// </summary>
    Tenor ISimulatedValues.GridSize => Exposures.SimulatedValues.GridSize;

    #endregion

    ///<summary>
    /// Exposure calculation engine combined with netting information
    ///</summary>
    ///<param name="exposures">Exposure calculation engine</param>
    ///<param name="nettingGroups">Netting group IDs</param>
    ///<param name="nettingSupergroups">Netting supergroups IDs</param>
    ///<param name="collateral">collateral rules</param>
    public CCRExposures(ICounterpartyCreditRiskCalculations exposures, string[] nettingGroups,
                        string[] nettingSupergroups, ICollateralMap[] collateral)
    {
      Exposures = exposures;
      NettingGroups = nettingGroups;
      NettingSupergroups = nettingSupergroups;
      Collateral = collateral;
    }

    ///<summary>
    /// Exposure engine
    ///</summary>
    public ICounterpartyCreditRiskCalculations Exposures { get; }

    ///<summary>
    /// Netting super-set IDs
    ///</summary>
    public string[] NettingSupergroups { get; }

    /// <summary>
    /// Collateral rule 
    /// </summary>
    public ICollateralMap[] Collateral { get; }

    ///<summary>
    /// Netting set IDs
    ///</summary>
    public string[] NettingGroups { get; }

    /// <summary>
    /// Netting data
    /// </summary>
    public Netting Netting => new Netting(NettingGroups, NettingSupergroups, Collateral);

    #region ICCRMeasureSource Members

    ///<summary>
    ///</summary>
    /// <param name="measure">Measure enum constant</param>
    /// <param name="date">Future date (required only for time-bucketed measures)</param>
    /// <param name="confidenceLevel">Confidence level (required only for tail measures)</param>
    public double GetMeasure(CCRMeasure measure, Dt date, double confidenceLevel)
    {
      return Exposures.GetMeasure(measure, Netting, date, confidenceLevel);
    }

    ///<summary>
    ///</summary>
    /// <param name="measure">Measure enum constant</param>
    /// <param name="date">Future date (required only for time-bucketed measures)</param>
    /// <param name="confidenceLevel">Confidence level (required only for tail measures)</param>
    double[] ICCRMeasureSource.GetMeasureMarginal(CCRMeasure measure, Dt date, double confidenceLevel)
    {
      throw new NotImplementedException();
    }

    #endregion

    #region ISimulationData Members

    /// <summary>
    /// Parallels NettingGroups list and allows further netting to be specified across a super set of groups. 
    /// </summary>
    IList<string> ISimulationData.NettingSupergroups => NettingSupergroups;

    /// <summary>
    /// Collateral rule for counterparty exposure 
    /// </summary>
    IList<ICollateralMap> ISimulationData.Collateral => Collateral;

    /// <summary>
    /// Identifies the lowest level netting group (MasterAgreement or individual Trade). Collateral thresholds also apply at this level.
    /// </summary>
    IList<string> ISimulationData.NettingGroups => NettingGroups;

    #endregion
  }


  /// <exclude></exclude>
  public interface ISimulationData : ISimulatedValues
  {
    /// <summary>
    /// Identifies the lowest level netting group (MasterAgreement or individual Trade). Collateral thresholds also apply at this level.
    /// </summary>
    IList<string> NettingGroups { get; }

    /// <summary>
    /// Parallels NettingGroups list and allows further netting to be specified across a super set of groups. 
    /// </summary>
    IList<string> NettingSupergroups { get; }

    /// <summary>
    /// Collateral agreements
    /// </summary>
    IList<ICollateralMap> Collateral { get; }
  }

}
