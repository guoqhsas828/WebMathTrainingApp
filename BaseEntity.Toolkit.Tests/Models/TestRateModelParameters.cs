//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Models.BGM;
using Param = BaseEntity.Toolkit.Models.RateModelParameters.Param;
using Process = BaseEntity.Toolkit.Models.RateModelParameters.Process;
using Model = BaseEntity.Toolkit.Models.RateModelParameters.Model;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestRateModelParameters : ToolkitTestBase
  {
    #region Data

    private Dt asOf_;
    private Dt expiry_;
    private Dt maturity_;
    private double[] fwd_;
    
    #endregion

    #region Utils
    private DiscountCurve GetDiscountCurve()
    {
      var dc = new DiscountCurve(asOf_);
      Dt dt = asOf_;
      double z = 1.0;
      for (int i = 0; i < 10; ++i)
      {
        dt = Dt.Add(dt, "1Y");
        z *= Math.Exp(-fwd_[i]);
        dc.Add(dt, z);
      }
      return dc;
    }

    private RateModelParameters GetBgmModelParameters()
    {
      var sigma = new Curve(asOf_, 0.5);
      sigma.Add(Dt.Add(asOf_, "100Y"), 0.1);
      var bgm = new[] {Param.Sigma};
      return new RateModelParameters(Model.BGM, bgm, new IModelParameter[] {sigma}, Tenor.Parse("3M"), Currency.USD);
    }

    private RateModelParameters GetNormalBgmModelParameters()
    {
      var sigma = new Curve(asOf_, 0.05);
      sigma.Add(Dt.Add(asOf_, "100Y"), 0.01);
      var bgm = new[] { Param.Sigma };
      return new RateModelParameters(Model.NormalBGM, bgm, new IModelParameter[] { sigma }, Tenor.Parse("3M"), Currency.USD);
    }
    
    private RateModelParameters GetSabrModelParameters()
    {
      var alpha = new Curve(asOf_, 0.2);
      var beta = new Curve(asOf_, 0.85);
      var rho = new Curve(asOf_, 0.8);
      var nu = new Curve(asOf_, 0.2);
      var sabr = new[] { Param.Alpha, Param.Beta, Param.Nu, Param.Rho };
      return new RateModelParameters(Model.SABR, sabr, new IModelParameter[] {alpha, beta, rho, nu}, Tenor.Parse("3M"), Currency.USD);
    }

    private RateModelParameters GetShiftedLognormalModelParameters()
    {
      var sigma = new Curve(asOf_, 0.5);
      sigma.Add(Dt.Add(asOf_, "100Y"), 0.1);
      var kappa = new Curve(asOf_, 0.02);
      var shiftedBgm = new[] { Param.Sigma, Param.Kappa };
      return new RateModelParameters(Model.ShiftedBGM, shiftedBgm, new IModelParameter[] { sigma, kappa }, Tenor.Parse("3M"), Currency.USD);
    }


    private RateModelParameters GetReplicationModelParametersFromVolCube(DiscountCurve dc)
    {
      var index = new InterestRateIndex("libor3M", Frequency.Quarterly, Currency.USD,
                                        DayCount.Actual360, Calendar.NYB, 2);
      var capVols = ArrayUtil.Generate(10, i => 0.5);
      var capTenors = ArrayUtil.Generate(10, i => Dt.Add(Dt.Add(asOf_, new Tenor(i + 1, TimeUnit.Years)), 2));
      var strikes = ArrayUtil.Generate(10, i => RateVolatilityCalibrator.CalculateSwapRate(dc, asOf_, capTenors[i], index));
      var calibrator = new RateVolatilityATMCapCalibrator(asOf_, asOf_, dc, dt => index, dt => dc, strikes, capTenors, capVols, 100.0, VolatilityType.LogNormal);
      var volCube = new RateVolatilityCube(calibrator);
      volCube.Fit();
      var pars = new[] { volCube as IModelParameter };
      return new RateModelParameters(Model.Replication, new[]{Param.Sigma}, pars, Tenor.Parse("3M"), Currency.USD);
    }

    private Tuple<RateModelParameters, InterestRateIndex, SwapRateIndex> GetReplicationModelParametersFromBgmFwdFwdVols(DiscountCurve dc)
    {
      string[] expiries = {
                            "1M", "2M", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y"
                          };
      string[] tenors = {"1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "20Y", "25Y", "30Y"};
      double[,] data =
        {
          {
            0.430708872, 0.455326866, 0.425945933, 0.384810189, 0.353923112, 0.326822749, 0.305934322,
            0.289860774, 0.276978215, 0.267430958, 0.232805641, 0.220272702, 0.215261865, 0.212760352
          },
          {
            0.439169866, 0.448798476, 0.413919093, 0.374039522, 0.344152407, 0.318057608, 0.297918355,
            0.281849479, 0.269965394, 0.260420518, 0.228052251, 0.216021487, 0.210762142, 0.208011553
          },
          {
            0.451864305, 0.447776664, 0.405398405, 0.3662746, 0.336387182, 0.310990168, 0.290406335,
            0.274840854, 0.263455162, 0.253912903, 0.222801017, 0.211771487, 0.205764049, 0.20276429
          },
          {
            0.523521085, 0.454468615, 0.410319227, 0.363935156, 0.327331448, 0.300939131, 0.280364399,
            0.265472369, 0.253422429, 0.244385291, 0.216288265, 0.205264261, 0.200258403, 0.197260184
          },
          {
            0.583342086, 0.461290897, 0.402919182, 0.349808239, 0.308423626, 0.282846658, 0.263952492,
            0.248900663, 0.236364965, 0.227337328, 0.204266333, 0.195249733, 0.19024876, 0.187752668
          },
          {
            0.475179571, 0.385274819, 0.335897171, 0.296499721, 0.265423109, 0.246865722, 0.232824841,
            0.221298382, 0.212778154, 0.205757998, 0.189726942, 0.183221897, 0.179726536, 0.177234988
          },
          {
            0.373087036, 0.308982317, 0.274898656, 0.24783934, 0.226298109, 0.212768659, 0.203250026,
            0.195237057, 0.188722571, 0.183719551, 0.173207491, 0.168211795, 0.166219789, 0.164230795
          },
          {
            0.295106304, 0.251820555, 0.228775268, 0.21074376, 0.196226296, 0.187715029, 0.180208217,
            0.175196622, 0.17019842, 0.166696239, 0.159698312, 0.156707764, 0.154220856, 0.153232456
          },
          {
            0.241723622, 0.212227595, 0.196704349, 0.184692394, 0.175190458, 0.169186933, 0.164179282,
            0.160183407, 0.157183287, 0.155179903, 0.150193647, 0.147209675, 0.145723979, 0.144238108
          },
          {
            0.174933532, 0.163160533, 0.15616798, 0.150672489, 0.147166427, 0.144674101, 0.142676109,
            0.141174342, 0.140183084, 0.139189444, 0.135708953, 0.133726974, 0.13174423, 0.129759396
          },
          {
            0.134531876, 0.132169004, 0.131185289, 0.129688969, 0.129683882, 0.129197192, 0.130202125,
            0.130206318, 0.130208467, 0.130209263, 0.127734364, 0.12525587, 0.123273902, 0.119790369
          },
          {
            0.128286136, 0.129261767, 0.128756955, 0.128751285, 0.128745934, 0.129252248, 0.12875797,
            0.128761141, 0.128763263, 0.128764698, 0.126784933, 0.123305425, 0.119820759, 0.116330322
          },
          {
            0.128864165, 0.129292501, 0.128791535, 0.12779142, 0.128288892, 0.128794026, 0.129297484,
            0.129799935, 0.129802477, 0.130303779, 0.12732441, 0.122840376, 0.118349159, 0.114356476
          },
          {
            0.130955098, 0.132323138, 0.132822597, 0.132323383, 0.132822831, 0.132828092, 0.132832172,
            0.132835475, 0.132838282, 0.132840731, 0.128355071, 0.123361154, 0.117868118, 0.11237305
          },
          {
            0.131560058, 0.13535636, 0.134858093, 0.134359806, 0.134360978, 0.133862647, 0.133364291,
            0.133365407, 0.133366505, 0.132368582, 0.126370513, 0.120375538, 0.114378797, 0.109382282
          }
        };
      var par = new BgmCalibrationParameters
                {
                  CalibrationMethod = VolatilityBootstrapMethod.Cascading,
                  Tolerance = 0.0001,
                  PsiUpperBound = 1.0,
                  PsiLowerBound = 0.0,
                  PhiUpperBound = 1.002,
                  PhiLowerBound = 0.9
                };
      BgmCorrelation correlations = BgmCorrelation.CreateBgmCorrelation(BgmCorrelationType.PerfectCorrelation,
                                                                        expiries.Length, new double[0,0]);
      var retVal = BgmForwardVolatilitySurface.Create(
        asOf_, par, dc, expiries, tenors, CycleRule.None,
        BDConvention.None, Calendar.None, correlations,
        data, DistributionType.LogNormal);
      var pars = new[] {retVal as IModelParameter};
      var liborIndex = new InterestRateIndex("libor3M", Frequency.Quarterly, Currency.USD,
                                             DayCount.Actual360, Calendar.NYB, 2);
      var cmsIndex = new SwapRateIndex("cms5Y", Tenor.Parse("5Y"), Frequency.SemiAnnual, Currency.USD,
                                       DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2, liborIndex);
      return new Tuple<RateModelParameters, InterestRateIndex, SwapRateIndex>(new RateModelParameters(Model.BGM, new[] {Param.Sigma}, pars, cmsIndex),
                                                                              liborIndex, cmsIndex);
    }


    private RateModelParameters GetReplicationNormalModelParametersFromVolCube(DiscountCurve dc)
    {
      var index = new InterestRateIndex("libor3M", Frequency.Quarterly, Currency.USD,
                                        DayCount.Actual360, Calendar.NYB, 2);
      var capVols = ArrayUtil.Generate(10, i => 0.025);
      var capTenors = ArrayUtil.Generate(10, i => Dt.Add(Dt.Add(asOf_, new Tenor(i + 1, TimeUnit.Years)), 2));
      var strikes = ArrayUtil.Generate(10, i => RateVolatilityCalibrator.CalculateSwapRate(dc, asOf_, capTenors[i], index));
      var calibrator = new RateVolatilityATMCapCalibrator(asOf_, asOf_, dc, dt => index, dt => dc, strikes, capTenors, capVols, 100.0, VolatilityType.Normal);
      var volCube = new RateVolatilityCube(calibrator);
      volCube.Fit();
      var pars = new[] {volCube as IModelParameter};
      return new RateModelParameters(Model.NormalReplication, new[] {Param.Sigma}, pars, Tenor.Parse("3M"), Currency.USD);
    }

   
    [OneTimeSetUp]
    public void Initialize()
    {
      asOf_ = PricingDate != 0 ? new Dt(PricingDate) : Dt.Today();
      expiry_ = Dt.Add(asOf_, "24M");
      maturity_ = Dt.Add(asOf_, "27M");
      fwd_ = new[] { 0.025, 0.03, 0.035, 0.037, 0.033, 0.0356, 0.0367, 0.04, 0.041, 0.042 };
    
    }


    private ResultData GenerateResults(double[] otherResults, string[] otherLabels, DiscountCurve dc, RateModelParameters parameters, Process process)
    {
      double f = dc.F(expiry_, maturity_);
      double z0 = dc.F(expiry_, Dt.Add(expiry_, "3M"));
      double z1 = dc.F(maturity_, Dt.Add(maturity_, "3M"));
      var labels = new[]
                          {
                            "ImpliedVol", "ImpliedNormalVol", "SecondMoment", "Call", "Put", "CallOnRatio", "PutOnRatio", "CallOnGAvg", "PutOnGAvg", "CallOnAAvg", "PutOnAAvg", "CheckConsistencyNormalVsLognormalIVol"
                          };
      for (int i = 0; i < labels.Length; ++i)
        labels[i] = String.Concat(parameters.Tenor(process).ToString(), labels[i]);
      var retVal = new double[labels.Length];
      retVal[0] = parameters.ImpliedVolatility(process, asOf_, f,
                                               f, expiry_, expiry_);
      retVal[1] = parameters.ImpliedNormalVolatility(process, asOf_, f, f, expiry_, expiry_);
      retVal[2] = parameters.SecondMoment(process, asOf_, f, expiry_, expiry_);
      retVal[3] = parameters.Option(process, asOf_, OptionType.Call, f, 0.0, f, expiry_, expiry_);
      retVal[4] = parameters.Option(process, asOf_, OptionType.Put, f, 0.0, f, expiry_, expiry_);
      retVal[5] = parameters.OptionOnRatio(process, asOf_, OptionType.Call, z0 / z1, z0, z1, 0.0, z0 / z1, expiry_,
                                           expiry_, maturity_);
      retVal[6] = parameters.OptionOnRatio(process, asOf_, OptionType.Put, z0 / z1, z0, z1, 0.0, z0 / z1, expiry_,
                                           expiry_, maturity_);

      var rates = new List<double>(10);
      var expiries = new List<Dt>(10);
      var wts = new List<double>(10);
      Dt end = asOf_;
      for (int i = 0; i < 10; ++i)
      {
        Dt start = end;
        end = Dt.Add(start, "3M");
        expiries.Add(end);
        rates.Add(dc.F(start, end));
        wts.Add(1);
      }
      double ga = 1.0;
      for (int i = 0; i < 10; ++i)
        ga *= (1 + wts[i] * rates[i] * 0.25);
      ga = (ga - 1.0) / (10 * 0.25);
      retVal[7] = parameters.OptionOnAverage(process, asOf_, OptionType.Call, ga, wts, rates, 0.0, ga, expiry_,
                                             expiries, ForwardModel.AverageType.Geometric);
      retVal[8] = parameters.OptionOnAverage(process, asOf_, OptionType.Put, ga, wts, rates, 0.0, ga, expiry_,
                                             expiries, ForwardModel.AverageType.Geometric);
      double aa = 0.0;
      for (int i = 0; i < 10; ++i)
        aa += wts[i] * rates[i];
      ga /= 10;
      retVal[9] = parameters.OptionOnAverage(process, asOf_, OptionType.Call, ga, wts, rates, 0.0, ga, expiry_,
                                             expiries, ForwardModel.AverageType.Arithmetic);
      retVal[10] = parameters.OptionOnAverage(process, asOf_, OptionType.Put, ga, wts, rates, 0.0, ga, expiry_,
                                             expiries, ForwardModel.AverageType.Arithmetic);
      Dt maturity = Dt.Add(asOf_, "1Y");
      Dt tenor = Dt.Add(asOf_, "1Y");
      double strike = 0.8 * f;
      double impliedNormalVol = parameters.ImpliedNormalVolatility(process, asOf_, f, strike ,maturity , tenor);
      double impliedBlackVol = parameters.ImpliedVolatility(process, asOf_, f, strike, maturity, tenor);
      double p0 = Black.P(OptionType.Call, Dt.FractDiff(asOf_, maturity) / 365.0, f, strike, impliedBlackVol);
      double p1 = BlackNormal.P(OptionType.Call, Dt.FractDiff(asOf_, maturity) / 365.0, 0.0, f, strike, impliedNormalVol);
      retVal[11] = Math.Abs(p0 - p1) / p0;
      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      if (otherResults == null)
      {
        rd.Results[0].Actuals = retVal;
        rd.Results[0].Labels = labels;
      }
      else
      {
        var allRes = new List<double>(retVal);
        var allLab = new List<string>(labels);
        allRes.AddRange(otherResults);
        allLab.AddRange(otherLabels);
        rd.Results[0].Actuals = allRes.ToArray();
        rd.Results[0].Labels = allLab.ToArray();
      }
      return rd;
    }
    #endregion

    #region Tests

    [Test]
    public void BgmRateModelParameters()
    {
      var dc = GetDiscountCurve();
      ResultData rd = GenerateResults(null, null, dc, GetBgmModelParameters(), Process.Projection);
      MatchExpects(rd);
    }
    [Test]
    public void NormalBgmRateModelParameters()
    {
      var dc = GetDiscountCurve();
      ResultData rd = GenerateResults(null, null, dc, GetNormalBgmModelParameters(), Process.Projection);
      MatchExpects(rd);
    }


    [Test]
    public void SabrRateModelParameters()
    {
      var dc = GetDiscountCurve();
      ResultData rd = GenerateResults(null, null, dc, GetSabrModelParameters(), Process.Projection);
      MatchExpects(rd);
    }

    [Test]
    public void ShiftedBgmModelParameters()
    {
      var dc = GetDiscountCurve();
      ResultData rd = GenerateResults(null, null, dc, GetShiftedLognormalModelParameters(), Process.Projection);
      MatchExpects(rd);
    }

    [Test]
    public void ReplicationRateVolatilityCubeModelParameters()
    {
      var dc = GetDiscountCurve();
      var fwd = new List<double>();
      var lab = new List<string>();
      Dt dt = asOf_;
      var parameters = GetReplicationModelParametersFromVolCube(dc);
      for (int i = 0; i < 10; ++i)
      {
        dt = Dt.Add(dt, "1Y");
        fwd.Add(parameters.Interpolate(dt, 0.0, Param.Sigma, Process.Projection));
        lab.Add(String.Format("3MLiborVol_{0}", dt));
      }
      ResultData rd = GenerateResults(fwd.ToArray(), lab.ToArray(), dc, parameters, Process.Projection);
      MatchExpects(rd);
    }

    [Test]
    public void NormalReplicationRateVolatilityCubeModelParameters()
    {
      var dc = GetDiscountCurve();
      var fwd = new List<double>();
      var lab = new List<string>();
      Dt dt = asOf_;
      var parameters = GetReplicationNormalModelParametersFromVolCube(dc);
      for (int i = 0; i < 10; ++i)
      {
        dt = Dt.Add(dt, "1Y");
        fwd.Add(parameters.Interpolate(dt, dc.F(dt, Dt.Add(dt, "3M")), Param.Sigma, Process.Projection));
        lab.Add(String.Format("3MLiborVol_{0}", dt));
      }
      ResultData rd = GenerateResults(fwd.ToArray(), lab.ToArray(), dc, parameters, Process.Projection);
      MatchExpects(rd);
    }

    [Test]
    public void ReplicationBgmForwardVolatilitySurfaceModelParameters()
    {
      var dc = GetDiscountCurve();
      var fwd = new List<double>();
      var lab = new List<string>();
      Dt dt = asOf_;
      var data = GetReplicationModelParametersFromBgmFwdFwdVols(dc);
      var parameters = data.Item1;
      var liborIndex = data.Item2;
      var swapIndex = data.Item3;
      for (int i = 0; i < 20; ++i)
      {
        dt = Dt.Add(dt, "1Y");
        fwd.Add(parameters[Param.Sigma].Interpolate(dt, 0.0, liborIndex));
        fwd.Add(parameters[Param.Sigma].Interpolate(dt, 0.0, swapIndex));
        lab.Add(String.Format("3MLiborVol_{0}", dt));
        lab.Add(String.Format("5YCmsVol_{0}", dt));
      }
      ResultData resData = GenerateResults(fwd.ToArray(), lab.ToArray(), dc, parameters, Process.Projection);
      MatchExpects(resData);
    }

    [Test]
    public void ExceptionsOnMissingName()
    {
      var model = GetBgmModelParameters();
      var pars = new RateModelParameters1D(Model.BGM,
        new[] {Param.Sigma}, new[] {model[Param.Sigma]});
      var date = asOf_ + 100;

      Assert.That(() => pars.Interpolate(Param.Alpha, date, 1.0, pars.ReferenceIndex),
          Throws.TypeOf<ToolkitException>().With.Message.EqualTo(
        "Rate model parameter with the name Alpha is missing."));

      Assert.That(() => model.Interpolate(date, 1.0, Param.Alpha, Process.Funding),
        Throws.TypeOf<ToolkitException>().With.Message.EqualTo(
        "Rate model parameter with the name Alpha is missing."));
    }

    #endregion
  }
}
