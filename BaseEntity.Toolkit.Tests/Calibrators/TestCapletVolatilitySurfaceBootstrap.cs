// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture("TestCapletAtmVolsBootstrapLogNormal1612")]
  [TestFixture("TestCapletAtmVolsBootstrapNormal1612")]
  [TestFixture("TestCapletVolsBootstrapLogNormal")]
  [TestFixture("TestCapletVolsBootstrapLogNormal1612")]
  [TestFixture("TestCapletVolsBootstrapNormal1612")]
  [TestFixture("MultipleCurvesLogNormal")]
  [TestFixture("MultipleCurvesNormal")]
  [TestFixture("MultipleCurvesSABRLogNormal")]
  [TestFixture("MultipleCurvesSABRNormal")]
  [TestFixture("TestCapletVolsSabrLogNormal")]
  [TestFixture("TestCapletVolsSABRLogNormal1612")]
  [TestFixture("TestCapletVolsSABRNormal1612")]

  public class TestCapletVolatilitySurfaceBootstrap : ToolkitTestBase
  {
    public TestCapletVolatilitySurfaceBootstrap(string name): base(name)
    {}

    #region data

    private string irDataFile_;
    private Dt asOf_;
    private Dt settle_;
    private Dt[] capMaturities_;
    private double[] capStrikes_;
    private double[] capVols_;
    private double[] lambdasCaps_;
    private RateVolatilityCube cube_;
    private VolatilityType volatilityType_;
    private bool useSabr_;
    private bool calibrateAtm_;
    private double[] capAtmVols_;
    private bool multipleCurve_;
    #endregion 

    [OneTimeSetUp]
    public void Initialize()
    {

      var discountCurve = LoadDiscountCurve(irDataFile_);
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None, DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
      DiscountCurve[] projectionCurves = null;
      InterestRateIndex[] projectionIndex = null;
      Func<Dt, InterestRateIndex> indexSelector =
        dt => (projectionIndex == null) ? rateIndex : (Dt.Years(asOf_, dt, DayCount.Actual365Fixed) < 3.0) ? projectionIndex[1] : projectionIndex[0];
      Func<Dt, DiscountCurve> curveSelector =
        dt => (projectionCurves == null) ? discountCurve : (Dt.Years(asOf_, dt, DayCount.Actual365Fixed) < 3.0) ? projectionCurves[1] : projectionCurves[0];
      capStrikes_ = capStrikes_ ?? new[]{0.0};
      if (multipleCurve_)
      {
        var rateIndex6M = new InterestRateIndex("LIBOR", Tenor.Parse("6M"), Currency.None, DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
        projectionIndex = new[] {rateIndex6M, rateIndex};
        var libor3MCurve = new DiscountCurve(discountCurve.AsOf);
        for (int i = 0; i < discountCurve.Count; ++i)
          libor3MCurve.Add(discountCurve.GetDt(i), 1.025 * discountCurve.GetVal(i));
        var libor6MCurve = new DiscountCurve(discountCurve.AsOf);
        for (int i = 0; i < discountCurve.Count; ++i)
          libor6MCurve.Add(discountCurve.GetDt(i), 1.05 * discountCurve.GetVal(i));
        projectionCurves = new[] {libor6MCurve, libor3MCurve};
      }
      if (calibrateAtm_)
      {
        var factor = (volatilityType_ == Toolkit.Base.VolatilityType.LogNormal) ? 1.0 : 0.0001;
        var atmStrikes = capMaturities_.Select(dt => RateVolatilityCalibrator.CalculateSwapRate(discountCurve, asOf_, dt, rateIndex)).ToArray();
        var vols = capAtmVols_.Select(v => v * factor).ToArray();
        var atmCapCalibrator = new RateVolatilityATMCapCalibrator(asOf_, asOf_, discountCurve, indexSelector, curveSelector, atmStrikes, capMaturities_, vols,
                                                                  lambdasCaps_.First(), volatilityType_);
        cube_ = new RateVolatilityCube(atmCapCalibrator);
        cube_.Fit();
      }
      else
      {
        var factor = (volatilityType_ == Toolkit.Base.VolatilityType.LogNormal) ? 1.0 : 0.0001;
        var capVolatilities = new double[capMaturities_.Length,capStrikes_.Length];
        for (int i = 0, idx = 0; i < capStrikes_.Length; i++)
        {
          for (int j = 0; j < capMaturities_.Length; j++, idx++)
            capVolatilities[j, i] = capVols_[idx] * factor;
        }
        // Setup
        if (useSabr_)
        {
          var sabrCalibrator = new RateVolatilityCapSabrCalibrator(asOf_, settle_, discountCurve, indexSelector, curveSelector, volatilityType_,
                                                                   new Dt[] {}, new double[] {},
                                                                   new double[] {}, null, null, capMaturities_, capStrikes_,
                                                                   capVolatilities, new double[] {}, lambdasCaps_,
                                                                   VolatilityBootstrapMethod.PiecewiseQuadratic,
                                                                   new[] {0.001, -0.9, 0.001},
                                                                   new[] {0.1, 0.5, 0.7}, new Curve(settle_, 0.35),
                                                                   null, null, null);
          cube_ = new RateVolatilityCube(sabrCalibrator);
          cube_.Fit();
        }
        else
        {
          var bootstrapCalibrator = new RateVolatilityCapBootstrapCalibrator(asOf_, settle_, discountCurve, indexSelector, curveSelector, volatilityType_,
                                                                             new Dt[0], new double[0], new double[0], null, null, capMaturities_, capStrikes_,
                                                                             capVolatilities, new double[0], lambdasCaps_,
                                                                             VolatilityBootstrapMethod.PiecewiseQuadratic);
          cube_ = new RateVolatilityCube(bootstrapCalibrator);
          cube_.Fit();
        }
      }
    }

    [Test]
    public void SabrBeta()
    {
      Curve curve;
      if(useSabr_)
      {
        var calibrator = (RateVolatilityCapSabrCalibrator) cube_.RateVolatilityCalibrator;
        curve = calibrator.Beta;
      }else
      {
        curve = new Curve(settle_,0.0);
      }
      ToResultData(curve, 0.0);
    }

    [Test]
    public void SabrAlpha()
    {
      Curve curve;
      if (useSabr_)
      {
        var calibrator = (RateVolatilityCapSabrCalibrator)cube_.RateVolatilityCalibrator;
        curve = calibrator.Alpha;
      }
      else
      {
        curve = new Curve(settle_, 0.0);
      }
      ToResultData(curve, 0.0);
    }

    [Test]
    public void SabrRho()
    {
      Curve curve;
      if (useSabr_)
      {
        var calibrator = (RateVolatilityCapSabrCalibrator)cube_.RateVolatilityCalibrator;
        curve = calibrator.Rho;
      }
      else
      {
        curve = new Curve(settle_, 0.0);
      }
      ToResultData(curve, 0.0); 
    }

    [Test]
    public void SabrNu()
    {
      Curve curve;
      if (useSabr_)
      {
        var calibrator = (RateVolatilityCapSabrCalibrator)cube_.RateVolatilityCalibrator;
        curve = calibrator.Nu;
      }
      else
      {
        curve = new Curve(settle_, 0.0);
      }
      ToResultData(curve, 0.0); 
    }


    [Test]
    public void CapletVolatilities()
    {
      var dt = new DataTable("caplet volatilities");
      dt.Columns.Add(new DataColumn("Expiry", typeof(Dt)));
      for (int i = 0; i < capStrikes_.Length; i++)
        dt.Columns.Add(new DataColumn(capStrikes_[i].ToString(), typeof(double)));
      var timer = new Timer();
      timer.Start();
      foreach (var mat in capMaturities_)
      {
        DataRow row = dt.NewRow();
        row[0] = mat;
        for (int j = 0; j < capStrikes_.Length; j++)
          row[j + 1] = cube_.CapletVolatility(mat, capStrikes_[j]);
        dt.Rows.Add(row);
      }
      timer.Stop();
      ToResultData(dt, timer.Elapsed);
    }

    [Test]
    public void CalibratedCapVolatilities()
    {
      var dt = new DataTable("cap volatilities");
      dt.Columns.Add(new DataColumn("Expiry", typeof(Dt)));
      for (int i = 0; i < capStrikes_.Length; i++)
        dt.Columns.Add(new DataColumn(capStrikes_[i].ToString(), typeof(double)));
      var timer = new Timer();
      timer.Start();
      foreach (var mat in capMaturities_)
      {
        DataRow row = dt.NewRow();
        row[0] = mat;
        var cap = new Cap(Dt.Add(settle_, "3M"), mat, Currency.USD, CapFloorType.Cap, 0.0, DayCount.Actual360,
                          Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
        for (int j = 0; j < capStrikes_.Length; j++)
        {
          cap.Strike = capStrikes_[j];
          row[j + 1] = cube_.CapVolatility(cube_.RateVolatilityCalibrator.RateProjectionCurve, cap);
        }
        dt.Rows.Add(row);
      }
      timer.Stop();
      ToResultData(dt, timer.Elapsed);
    }

    public void ToResultData(DataTable table, double timeUsed)
    {
      int rows = table.Rows.Count;
      int cols = table.Columns.Count;
      var maturityDate = new double[rows];
      var vol = new double[cols - 1][];
      for (int i = 0; i < vol.Length; ++i)
        vol[i] = new double[rows];
      for (int i = 0; i < rows; i++)
      {
        DataRow row = table.Rows[i];
        maturityDate[i] = (double)((Dt)row["Expiry"]).ToInt();
        for (int j = 0; j < vol.Length; ++j)
          vol[j][i] = (double)row[j + 1];
      }

      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }
      rd.Results[0].Name = table.Columns[0].ColumnName;
      rd.Results[0].Actuals = maturityDate;
      for (int i = 1; i < cols; ++i)
      {
        rd.Results[i].Name = table.Columns[i].ColumnName;
        rd.Results[i].Actuals = vol[i - 1];
      }
      MatchExpects(rd);
    }

    public void ToResultData(Curve curve,double timeUsed)
    {
      int rows = curve.Count;
      const int cols = 2;
      var dates = new double[rows];
      var values = new double[rows];

      for(int i=0;i<curve.Count;i++)
      {
        dates[i] = curve.GetDt(i).ToInt();
        values[i] = curve.GetVal(i);
      }
      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }
      int idx = 0;
      rd.Results[idx].Name = "Dates";
      rd.Results[idx].Actuals = dates;
      idx++;
      rd.Results[idx].Name = "Values";
      rd.Results[idx].Actuals = values;
      rd.TimeUsed = timeUsed;
      MatchExpects(rd);
    }

    
    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public String LiborDataFile
    {
     set { irDataFile_ = value; }
    }

    public String AsOf
    {
      set { asOf_ = Dt.FromStr(value, "%Y%m%d"); }
    }
    
    public String Settle
    {
      set { settle_ = Dt.FromStr(value, "%Y%m%d"); }
    }

    public String CapStrikes
    {
      set
      {
        var strikesArray = value.Split(new char[] { ',' });
        capStrikes_ = new double[strikesArray.Length];
        for (int i = 0; i < strikesArray.Length; i++)
          capStrikes_[i] = Double.Parse(strikesArray[i]);
      }
    }

    public String CapMaturities
    {
      set
      {
        var capMaturitiesArray = value.Split(new char[] { ',' });
        capMaturities_ = new Dt[capMaturitiesArray.Length];
        for (int i = 0; i < capMaturitiesArray.Length; i++)
          capMaturities_[i] = Dt.FromStr(capMaturitiesArray[i], "%Y%m%d");
      }
    }

    public String CapVolatilities
    {
      set
      {
        var capVolatilitiesArray = value.Split(new char[] {','});
        capVols_ = new double[capVolatilitiesArray.Length];
        for (int i = 0; i < capVolatilitiesArray.Length; i++)
          capVols_[i] = Double.Parse(capVolatilitiesArray[i]);
      }
    }

    public String LambdaCaps
    {
      set
      {
        var lambdaCapsArray = value.Split(new char[] {','});
        lambdasCaps_ = new double[lambdaCapsArray.Length];
        for (int i = 0; i < lambdaCapsArray.Length; i++)
          lambdasCaps_[i] = Double.Parse(lambdaCapsArray[i]);
      }
    }

    public String VolatilityType
    {
      set { volatilityType_ = (VolatilityType)Enum.Parse(typeof(VolatilityType), value); ;}
    }

    public bool UseSabr
    {
      set{ useSabr_ = value;}
    }

    public bool CalibrateAtm
    {
      set { calibrateAtm_ = value; }
    }

    public String CapAtmVolatilities
    {
      set
      {
        var capVolatilitiesArray = value.Split(new[]{ ',' });
        capAtmVols_ = new double[capVolatilitiesArray.Length];
        for (int i = 0; i < capVolatilitiesArray.Length; i++)
          capAtmVols_[i] = Double.Parse(capVolatilitiesArray[i]);
      }
    }

    public bool MultipleCurve
    {
      set { multipleCurve_ = value; }
    }
  }
}
