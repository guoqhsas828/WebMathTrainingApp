//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture("TestCurveQuoteInputs-InputValidation1a")]
  [TestFixture("TestCurveQuoteInputs-InputValidation1b")]
  [TestFixture("TestCurveQuoteInputs-InputValidation1c")]
  [TestFixture("TestCurveQuoteInputs-InputValidation2a")]
  [TestFixture("TestCurveQuoteInputs-InputValidation2b")]
  [TestFixture("TestCurveQuoteInputs-InputValidation2c")]
  [TestFixture("TestCurveQuoteInputs-InputValidation3a")]
  [TestFixture("TestCurveQuoteInputs-InputValidation3b")]
  [TestFixture("TestCurveQuoteInputs-InputValidation3c")]
  [TestFixture("TestCurveQuoteInputs-InputValidation3d")]
  [TestFixture("TestCurveQuoteInputs-InputValidation4a")]
  [TestFixture("TestCurveQuoteInputs-InputValidation4b")]
  [TestFixture("TestCurveQuoteInputs-InputValidation4c")]
  [TestFixture("TestCurveQuoteInputs-InputValidation5a")]
  [TestFixture("TestCurveQuoteInputs-InputValidation5b")]
  [TestFixture("TestCurveQuoteInputs-InputValidation5c")]
  [TestFixture("TestCurveQuoteInputs-InputValidation6a")]
  [TestFixture("TestCurveQuoteInputs-InputValidation6b")]
  [TestFixture("TestCurveQuoteInputs-InputValidation6c")]
  [TestFixture("TestCurveQuoteInputs-InputValidation7a")]
  [TestFixture("TestCurveQuoteInputs-InputValidation7b")]
  [Smoke]
  public class TestCurveQuoteInputs : ToolkitTestBase
  {
    public TestCurveQuoteInputs(string name) : base(name)
    {}

    #region Tests
    [Test, Smoke]
    public void CdsCurve()
    {
      try
      {
        SurvivalCurve[] curves = CreateCurves(false);
        if (ExpectException)
          Assert.IsTrue(false, "Expected exception on curve ");
      }
      catch(ArgumentException e)
      {
        if (!ExpectException)
          throw e;
      }
    }

    [Test, Smoke]
    public void LcdsCurve()
    {
      try
      {
        SurvivalCurve[] curves = CreateCurves(true);
        if (ExpectException)
          Assert.IsTrue(false, "Expected exception on curve ");
      }
      catch (ArgumentException e)
      {
        if (!ExpectException)
          throw e;
      }
    }
    #endregion Tests

    /// <summary>
    ///   Create two curves:
    ///    the first with upfront fees and actual running premiums;
    ///    the second with zero upfronts and quivalent all-running premiums.
    /// </summary>
    /// <param name="withRefinance">True if fit Lcds curves</param>
    /// <returns>Two curves</returns>
    public SurvivalCurve[] CreateCurves(bool withRefinance)
    {
      Dt asOf = PricingDate != 0 ? new Dt(this.PricingDate) : Dt.Today();
      Currency ccy = this.Currency;
      DayCount cdsDayCount = this.DayCount;
      Frequency cdsFrequency = this.Frequency;
      BDConvention cdsRoll = this.Roll;
      Calendar cdsCalendar = this.Calendar;
      DiscountCurve dc = new DiscountCurve(asOf, 0.03);

      SurvivalCurve refi = null;
      double corr = 0.0;

      if (withRefinance)
      {
        refi = new SurvivalCurve(asOf, 0.01);
        corr = 0.3;
      }
      SurvivalCurve curve0 = SurvivalCurve.FitLCDSQuotes(
          asOf, ccy, "Original", cdsDayCount, cdsFrequency, cdsRoll, cdsCalendar,
          interpMethod, extrapMethod, nspTreatment, dc,
          tenorNames_, tenorDates_, fees_, premiums_,
          recoveries_, recoveryDisp_, forceFit_, eventDates_, refi, corr);
       
      
      // done!
      return new SurvivalCurve[] { curve0 };
    }


    #region Properties
    public string[] TenorNames
    {
      set { tenorNames_ = value; }
    }
    public Dt[] TenorDates
    {
      set { tenorDates_ = value; }
    }
    public double[] Fees
    {
      set { fees_ = value; }
    }
    public double[] Premiums
    {
      set { premiums_ = value; }
    }
    public double[] Recoveries
    {
      set { recoveries_ = value; }
    }
    public double RecoveryDispersions
    {
      set { recoveryDisp_ = value; }
    }
    public bool ForceFit
    {
      set { forceFit_ = value; }
    }
    public Dt[] EventDates
    {
      set { eventDates_ = value; }
    }

    public bool ExpectException { get; set; }

    public bool OnlyTestConstruction { get; set; }

    #endregion Properties

    #region Data
    
    string[] tenorNames_;
    Dt[] tenorDates_;
    double[] fees_;
    double[] premiums_;
    double[] recoveries_ ;
    double recoveryDisp_;
    bool forceFit_;
    Dt[] eventDates_;

    InterpMethod interpMethod = InterpMethod.Weighted;
    ExtrapMethod extrapMethod = ExtrapMethod.Const;
    NegSPTreatment nspTreatment = NegSPTreatment.Allow;

    const double tol = 1.0E-9;
    #endregion Data
  }
} 
