/*
 *  -2015. All rights reserved.
 *
 */
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///   A simple wrapper, delegate every thing to <see cref="CcrPricer"/>.
  /// </summary>
  internal class CcrPricerWrapper : IPvEvaluator
  {
    private readonly CcrPricer _pricer;
    private CcrPricerWrapper(CcrPricer pricer)
    {
      _pricer = pricer;
    }

    internal static IPvEvaluator Get(CcrPricer pricer)
    {
      return new CcrPricerWrapper(pricer);
    }

    public double FastPv(int exposureIndex, Dt exposureDate)
    {
      return _pricer.FastPv(exposureDate);
    }

    public Currency Ccy
    {
      get { return _pricer.Ccy; }
    }

    public double FastPv(Dt settle)
    {
      return _pricer.FastPv(settle);
    }

    public bool DependsOn(object marketObject)
    {
      return _pricer.DependsOn(marketObject);
    }

    public Dt[] ExposureDates
    {
      get { return _pricer.ExposureDates; }
      set { _pricer.ExposureDates = value; }
    }
  }

}
