/*
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Models.HullWhiteShortRates;
using BaseEntity.Toolkit.Curves;
using InputType = BaseEntity.Toolkit.Models.Simulations.Native.Simulator.InputType;

namespace BaseEntity.Toolkit.Models.Simulations
{
  using static MarketEnvironment;

  public class HullWhiteSimulatorFactory : Native.HullWhiteSimulatorFactory, ISimulatorFactory
  {
    public HullWhiteSimulatorFactory(Simulator simulator)
    {
      Simulator = simulator;
    }

    public Simulator Simulator { get; }

    #region Methods to add market objects to simulate

    public void AddDomesticDiscount(DiscountCurve discountCurve,
      VolatilityCurve[] volatilityCurves, double[,] factorLoadings, bool active)
    {
      if (Map.ContainsKey(discountCurve))
        return;
      var index = AddDomesticShortRateProcess(Simulator,
        NoCorrectiveOverlay(discountCurve),
        HullWhiteShortRateVolatility.GetMeanReversion(volatilityCurves),
        HullWhiteShortRateVolatility.GetVolatility(volatilityCurves),
        factorLoadings, active);
      Map.Add(discountCurve, new Tuple<CalibratedCurve, InputType, int>(
        discountCurve, InputType.DiscountRateInput, index));
    }

    public void AddDiscount(DiscountCurve discountCurve,
      VolatilityCurve[] volatilityCurves, double[,] factorLoadings,
      FxRate fxRate, VolatilityCurve[] fxVolatility, double[,] fxFactorLoadings,
      bool active)
    {
      if (Map.ContainsKey(discountCurve))
        return;
      int index = AddForeignShortRateProcess(Simulator,
        NoCorrectiveOverlay(discountCurve),
        HullWhiteShortRateVolatility.GetMeanReversion(volatilityCurves),
        HullWhiteShortRateVolatility.GetVolatility(volatilityCurves),
        factorLoadings, fxRate.Value, (fxRate.ToCcy == discountCurve.Ccy),
        fxVolatility[0], fxFactorLoadings, active);
      Map.Add(discountCurve, new Tuple<CalibratedCurve, InputType, int>(
        discountCurve, InputType.DiscountRateInput, index));
      Map.Add(fxRate, new Tuple<CalibratedCurve, InputType, int>(
        discountCurve, InputType.FxRateInput, index));
    }

    public void AddSurvival(SurvivalCurve survivalCurve,
      VolatilityCurve[] volatilityCurves, double[,] factorLoadings,
      bool active)
    {
      if (Map.ContainsKey(survivalCurve))
        return;
      int index = AddSurvivalProcess(Simulator,
        NoCorrectiveOverlay(survivalCurve),
        volatilityCurves[0], factorLoadings, active);
      Map.Add(survivalCurve, new Tuple<CalibratedCurve, InputType, int>(
        survivalCurve, InputType.CreditInput, index));
    }

    public void AddForward(CalibratedCurve forwardCurve,
      VolatilityCurve[] volatilityCurves, double[,] factorLoadings, bool active)
    {
      if (Map.ContainsKey(forwardCurve)) return;
      int index = AddForwardProcess(Simulator,
        NoCorrectiveOverlay(forwardCurve),
        ValidateTenorConsistency(volatilityCurves), factorLoadings,
        forwardCurve is DiscountCurve, active);
      Map.Add(forwardCurve, new Tuple<CalibratedCurve, InputType, int>(
        forwardCurve, InputType.ForwardPriceInput, index));
    }

    public void AddSpot(ISpot spot, CalibratedCurve referenceCurve,
      IVolatilityProcessParameter volatility, double[,] factorLoadings,
      Func<Dt, Dt, double> carryCostAdjustment,
      IList<Tuple<Dt, double>> dividendSchedule, bool active)
    {
      if (Map.ContainsKey(spot))
        return;
      var volatilityCurves = (volatility as StaticVolatilityCurves)?.Curves;
      if (volatilityCurves == null)
      {
        throw new ArgumentException("Invalid volatility parameters");
      }
      var dividends = (dividendSchedule == null || dividendSchedule.Count == 0)
        ? new double[0]
        : SimulationDates.Select((dt, i) => dividendSchedule
          .Where(p => (p.Item1 > ((i == 0) ? AsOf : SimulationDates[i - 1]) && p.Item1 <= dt))
          .Sum(p => p.Item2)).ToArray();
      var carryAdj = (carryCostAdjustment == null)
        ? new double[0]
        : SimulationDates.Select((dt, i) => carryCostAdjustment(
          i == 0 ? AsOf : SimulationDates[i - 1], dt)).ToArray();
      int index = AddSpotProcess(Simulator, spot.Value, (int)spot.Ccy,
        volatilityCurves[0], factorLoadings, carryAdj, dividends, active);
      Map.Add(spot, new Tuple<CalibratedCurve, InputType, int>(
        referenceCurve, InputType.SpotPriceInput, index));
    }

    public void AddRadonNykodim(
      int[] cptyIndex,
      SurvivalCurve[] cptyCreditCurves,
      double defaultTimeCorrelation)
    {
      AddCrediKernelProcess(Simulator,
        cptyIndex, cptyCreditCurves,
        defaultTimeCorrelation/100);
    }

    private Curve[] ValidateTenorConsistency(VolatilityCurve[] volatilities)
    {
      if ((Simulator.Tenors.Length != volatilities.Length)
        && (volatilities.Length != 1))
      {
        throw new ArgumentException(
          $"volatilities expected of size {Simulator.Tenors.Length} or 1");
      }
      return volatilities;
    }

    #endregion

    private Dictionary<object, Tuple<CalibratedCurve, InputType, int>> Map => Simulator.Map;
    private Dt[] SimulationDates => Simulator.SimulationDates;
    private Dt AsOf => Simulator.AsOf;
  }

}