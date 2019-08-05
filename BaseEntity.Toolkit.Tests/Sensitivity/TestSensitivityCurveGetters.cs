using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{

  [TestFixture]
  public class TestSensitivityCurveGetter
  {
    #region Reference Curves

    // It should be okay for pricer to return null array or curve
    [Test]
    public void RateAnInflationCurves()
    {
      var pricer = new DummyPricer() as IPricer;
      var curves = new[] { pricer }.GetRateAndInflationCurves();
      curves.IsExpected(To.Be.Empty);
    }

    // DependsOn should work with pricer with no ReferenceCurve property
    [Test]
    public void PricerDependsOn()
    {
      var pricer = new DummyPricer() as IPricer;
      var test = new PricerEvaluator(pricer).DependsOn(
        new DiscountCurve(Dt.Today()));
      test.IsExpected(To.Be.False);
    }

    public class DummyPricer : IPricer
    {
      public DiscountCurve DiscountCurve => null;

      #region stubs

      public double Pv()
      {
        throw new NotImplementedException();
      }

      public double Accrued()
      {
        throw new NotImplementedException();
      }

      public BaseEntity.Toolkit.Cashflows.Cashflow GenerateCashflow(BaseEntity.Toolkit.Cashflows.Cashflow cashflow, Toolkit.Base.Dt from)
      {
        throw new NotImplementedException();
      }

      public void Reset()
      {
        throw new NotImplementedException();
      }

      public Toolkit.Base.Dt AsOf
      {
        get
        {
          throw new NotImplementedException();
        }
        set
        {
          throw new NotImplementedException();
        }
      }

      public Toolkit.Base.Dt Settle
      {
        get
        {
          throw new NotImplementedException();
        }
        set
        {
          throw new NotImplementedException();
        }
      }

      public Toolkit.Products.IProduct Product
      {
        get { throw new NotImplementedException(); }
      }

      public IPricer PaymentPricer
      {
        get { throw new NotImplementedException(); }
      }

      public Toolkit.Base.Currency ValuationCurrency
      {
        get { throw new NotImplementedException(); }
      }

      public object Clone()
      {
        throw new NotImplementedException();
      }
      
      #endregion
    }
    #endregion
  }
}
