using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base.ReferenceIndices;


namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Generic parameter depending on maturity and strike
  /// </summary>
  public interface IModelParameter
  {
    /// <summary>
    /// Interpolate method
    /// </summary>
    /// <param name="maturity">Maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="referenceIndex">Underlying reference index</param>
    /// <returns>Parameter for the given maturity and strike</returns>
    double Interpolate(Dt maturity, double strike, ReferenceIndex referenceIndex);
  }

  /// <summary>
  /// Constant parameter
  /// </summary>
  [Serializable]
  public class ConstParameter : IModelParameter
  {
    #region Data
    private double parameter_;
    #endregion

    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="parameter">parameter</param>
    public ConstParameter(double parameter)
    {
      parameter_ = parameter;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Implicit conversion
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public static implicit operator ConstParameter(double parameter)
    {
      return new ConstParameter(parameter);
    }

    #endregion

    #region IModelParameter Members
    /// <summary>
    /// Interpolate method
    /// </summary>
    /// <param name="maturity">Maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="referenceIndex">Underlying reference index</param>
    /// <returns>Parameter for the given maturity and strike</returns>
    public double Interpolate(Dt maturity, double strike, ReferenceIndex referenceIndex)
    {
      return parameter_;
    }
    #endregion
  }

  #region RateModelParameters1D

  /// <summary>
  /// One dimensional model parameters
  /// </summary>
  [Serializable]
  public class RateModelParameters1D
  {
    #region Data

    private ForwardModel model_;

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="modelName">Model enum </param>
    /// <param name="parameterNames">Param enum </param>
    /// <param name="parameters">parameter objects</param>
    /// <remarks> This specification of rate model parameters assumes that the parameters only depend on maturity.  </remarks>
    public RateModelParameters1D(RateModelParameters.Model modelName, RateModelParameters.Param[] parameterNames, IModelParameter[] parameters)
    {
      ModelName = modelName;
      Parameters = new Dictionary<RateModelParameters.Param, IModelParameter>(new ParamComparer());
      if (parameterNames == null || parameters == null)
        throw new ArgumentException("Parameter Names and parameters must be set");
      if (parameters.Length != parameterNames.Length)
        throw new ArgumentException("Parameter Names and parameters must be the same length");
      for (int i = 0; i < parameters.Length; i++)
        Parameters[parameterNames[i]] = parameters[i];
      Set();
    }

    public RateModelParameters1D ReplaceModel(ForwardModel model)
    {
      var p = (RateModelParameters1D)MemberwiseClone();
      p.model_ = model;
      return p;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the underlying forward model.
    /// </summary>
    /// <value>The model.</value>
    public ForwardModel Model => model_;

    /// <summary>
    /// Reference index describing underlying process
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; set; }

    /// <summary>
    /// Getter and setter for model name
    /// </summary>
    public RateModelParameters.Model ModelName { get; private set; }

    /// <summary>
    /// Accessor for parameters
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <returns>The parameter curve corresponding to name</returns>
    public IModelParameter this[RateModelParameters.Param name]
    {
      get { return Parameters[name]; }
    }

    private Dictionary<RateModelParameters.Param, IModelParameter> Parameters { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="param"></param>
    /// <param name="retVal"></param>
    /// <returns></returns>
    public bool TryGetValue(RateModelParameters.Param param, out IModelParameter retVal)
    {
      return Parameters.TryGetValue(param, out retVal);
    }


    private void Set()
    {
      switch (ModelName)
      {
        case RateModelParameters.Model.Replication:
        {
          model_ = new Replication();
          break;
        }
        case RateModelParameters.Model.BGM:
        {
          model_ = new LogNormalBlack();
          break;
        }
        case RateModelParameters.Model.ShiftedBGM:
        {
          model_ = new ShiftedLogNormal();
          break;
        }
        case RateModelParameters.Model.SABR:
        {
          model_ = new Sabr();
          break;
        }
        case RateModelParameters.Model.NormalBGM:
        {
          model_ = new NormalBlack();
          break;
        }
        case RateModelParameters.Model.NormalReplication:
        {
          model_ = new ReplicationNormal();
          break;
        }
        default:
        {
          model_ = new LogNormalBlack();
          break;
        }
      }
    }

    /// <summary>
    /// Computes the approximate Black implied vol for the given strike 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="F">Forward price/rate. Assumed to be a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry date</param>
    /// <param name="tenor">Price/rate tenor</param>
    /// <returns>Black implied vol for the given moneyness</returns>
    public double ImpliedVolatility(Dt asOf, double F, double strike, Dt expiry, Dt tenor)
    {
      double T = Dt.FractDiff(asOf, expiry) / 365.0;
      return model_.ImpliedVolatility(F, T, strike, this, tenor);
    }

    /// <summary>
    /// Computes the approximate Black implied vol for the given strike 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="F">Forward price/rate. Assumed to be a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry date</param>
    /// <param name="tenor">Price/rate tenor</param>
    /// <returns>Black implied vol for the given moneyness</returns>
    public double ImpliedNormalVolatility(Dt asOf, double F, double strike, Dt expiry, Dt tenor)
    {
      double T = Dt.FractDiff(asOf, expiry) / 365.0;
      return model_.ImpliedNormalVolatility(F, T, strike, this, tenor);
    }

    private static double IntrinsicValue(OptionType type, double F, double strike)
    {
      return type == OptionType.Call
               ? Math.Max(F - strike, 0.0)
               : type == OptionType.Put ? Math.Max(strike - F, 0.0) : 0.0;
    }

    /// <summary>
    /// Computes the price of the option for this model
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="type">Option type</param>
    /// <param name="F">Forward price/rate <m>F_0(U)</m></param>
    /// <param name="cvxyAdj"><m>E(F_T(U)) - F_0(U)</m>in case F_t(T) is not a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry</param>
    /// <param name="tenor">Tenor of the forward rate/price</param>
    /// <returns>The option price for this model</returns>
    /// <remarks>The option value is not normalized by the time zero numeraire Z(0,T) since this step is done while taking the pv</remarks>
    public double Option(Dt asOf, OptionType type, double F, double cvxyAdj, double strike, Dt expiry, Dt tenor)
    {
      if (asOf >= expiry)
        return IntrinsicValue(type, F, strike);
      double T = Dt.FractDiff(asOf, expiry) / 365.0;
      return model_.Option(type, F, cvxyAdj, T, strike, this, tenor);
    }

    /// <summary>
    /// Computes the price of an option on the ratio of 2 rates/prices with different tenor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="type">Option type</param>
    /// <param name="F">Forward price(rate) ratio <m>\frac{F_0(U)}{F_0(S)}</m></param>
    /// <param name="num">Forward price(rate) appearing in the numerator </param>
    /// <param name="den">Forward price(rate) appearing in the denominator</param>
    /// <param name="cvxyAdj"><m>E(\frac{F_T(U)}{F_T(S)} - \frac{F_0(U)}{F_0(S)}</m> in case the ratio is not a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry</param>
    /// <param name="tenorNum">Tenor of index at the numerator</param>
    /// <param name="tenorDen">Tenor of the index at the denominator</param>
    /// <returns>The option price for this model</returns>
    public double OptionOnRatio(Dt asOf, OptionType type, double F, double num, double den, double cvxyAdj,
                                  double strike, Dt expiry, Dt tenorNum, Dt tenorDen)
    {
      if (asOf >= expiry)
        return IntrinsicValue(type, F, strike);
      double T = Dt.FractDiff(asOf, expiry) / 365.0;
      return model_.OptionOnRatio(type, F, cvxyAdj, num, den, T, strike, this, tenorNum, tenorDen);
    }

    /// <summary>
    /// Calc option on averaged rates by second moment matching
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="type">Option type</param>
    /// <param name="F">Averaged rate</param>
    /// <param name="weights">Averaging weights</param>
    /// <param name="components">Average components</param>
    /// <param name="cvxyAdj">Convexity adjustment</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry</param>
    /// <param name="tenors">Reset dates of averaged rates</param>
    /// <param name="averageType">Average type</param>
    /// <returns>Option value</returns>
    public double OptionOnAverage(Dt asOf, OptionType type, double F, List<double> weights, List<double> components, double cvxyAdj, double strike, Dt expiry,
                                    List<Dt> tenors, ForwardModel.AverageType averageType)
    {
      if (asOf >= expiry)
        return IntrinsicValue(type, F, strike);
      double T = Dt.FractDiff(asOf, expiry) / 365.0;
      return model_.OptionOnAverage(asOf, type, F, weights, components, cvxyAdj, T, strike, this, tenors, averageType);
    }


    /// <summary>
    /// Second moment for this model
    /// </summary>
    /// <param name="asOf">Asof date</param>
    /// <param name="F">Forward</param>
    /// <param name="time">Running time</param>
    /// <param name="tenor">Maturity of the rate/price </param>
    /// <returns>Second moment for this model</returns>
    public double SecondMoment(Dt asOf, double F, Dt time, Dt tenor)
    {
      if (asOf >= time)
        return F * F;
      double T = Dt.FractDiff(asOf, time) / 365.0;
      return model_.SecondMoment(F, T, this, tenor);
    }

    #endregion

    #region ParamComparer

    /// <summary>
    /// Avoid boxing/unboxing when looking up parameters in dictionary_ 
    /// </summary>
    [Serializable]
    private class ParamComparer : IEqualityComparer<RateModelParameters.Param>
    {
      #region IEqualityComparer<Param> Members

      /// <summary>
      /// Overload of Equals method
      /// </summary>
      /// <param name="x">Param object</param>
      /// <param name="y">Param object</param>
      /// <returns></returns>
      public bool Equals(RateModelParameters.Param x, RateModelParameters.Param y)
      {
        return (int)x == (int)y;
      }

      /// <summary>
      /// Hash code
      /// </summary>
      /// <param name="x">Param</param>
      /// <returns></returns>
      public int GetHashCode(RateModelParameters.Param x)
      {
        return (int)x;
      }

      #endregion
    }

    #endregion
  }

  #endregion

  #region RateModelParameters

  /// <summary>
  /// Forward model parameters 
  /// </summary>
  [Serializable]
  public class RateModelParameters : BaseEntityObject,  IVolatilityObject 
  {
    #region Enum constants

    #region Model enum

    /// <summary>
    /// Model enum type
    /// </summary>
    public enum Model
    {
      /// <summary>
      /// No model specified
      /// </summary>
      None = 0,

      /// <summary>
      /// Sabr model 
      /// </summary>
      SABR = 1,

      /// <summary>
      /// Bgm model
      /// </summary>
      BGM = 2,

      /// <summary>
      /// Shifted Bgm model
      /// </summary>
      ShiftedBGM = 3,

      /// <summary>
      /// Model independent from Black vol surface
      /// </summary>
      Replication = 4,

      /// <summary>
      /// Hull model for ED futures
      /// </summary>
      Hull = 6,

      /// <summary>
      /// user override ca inputs 
      /// </summary>
      Custom = 7,

      /// <summary>
      /// Normal BGM
      /// </summary>
      NormalBGM = 8,

      /// <summary>
      /// Model independent from Black normal vol surface
      /// </summary>
      NormalReplication = 9

    }

    #endregion

    #region Param enum

    /// <summary>
    /// Parameter enum type
    /// </summary>
    public enum Param
    {
      /// <summary>
      /// Initial value of the stochastic volatility in SABR
      /// </summary>
      Alpha = 0,

      /// <summary>
      /// Exponent in the dynamics of the forward/swap rate in SABR
      /// </summary>
      Beta = 1,

      /// <summary>
      /// Correlation between stochastic volatility process and forward/swap rate in SABR
      /// </summary>
      Rho = 2,

      /// <summary>
      /// Diffusion coefficient of the stochastic volatility process in SABR 
      /// </summary>
      Nu = 3,

      /// <summary>
      /// Diffusion coefficient of forward/swap rate in LogNormal
      /// </summary>
      Sigma = 4,

      //VarPhi=5, SigmaI=6 are removed

      /// <summary>
      /// Shift in the shifted LogNormal model
      /// </summary>
      Kappa = 7,

      /// <summary>
      /// Curve of pairwise correlations between rates with different maturity and the same tenor. We assume that correlations depend only on the difference between two maturities T2 - T1,
      /// i.e. they are stationary. Use Interpolate(time) to get the correlation values. 
      /// </summary>
      Correlations = 8,

      /// <summary>
      /// user input of ca amts on curve
      /// </summary>
      Custom = 9,

      /// <summary>
      /// Smoothing parameter in cap/floor bootstrap.
      /// </summary>
      Lambda = 10,

      ///<summary>
      /// The Psi upper bound in BGM calibration
      ///</summary>
      UpperPsi = 11,

      ///<summary>
      /// The Psi lower bound in BGM calibration
      ///</summary>
      LowerPsi = 12,

      ///<summary>
      /// The Phi upper bound in BGM calibration
      ///</summary>
      UpperPhi = 13,

      ///<summary>
      /// The Phi lower bound in BGM calibration
      ///</summary>
      LowerPhi = 14,

      ///<summary>
      /// The mean reversion parameter in Hull-White process
      ///</summary>
      MeanReversion = 15,
    }

    #endregion

    #region Process Enum

    /// <summary>
    /// Process enum
    /// </summary>
    public enum Process
    {
      /// <summary>
      /// Projection process
      /// </summary>
      Projection = 0,

      /// <summary>
      /// Funding process
      /// </summary>
      Funding = 1,
    }

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="modelName">Model name</param>
    /// <param name="parameterNames">Parameter names</param>
    /// <param name="parameters">Model parameters</param>
    /// <param name="tenor">Tenor of the underlying index</param>
    /// <param name="ccy">Currency of the underlying index</param>
    /// <remarks>
    /// </remarks>
    public RateModelParameters(Model modelName, Param[] parameterNames, IModelParameter[] parameters, Tenor tenor, Currency ccy)
    {
      Parameters = new[]
                   {
                     new RateModelParameters1D(modelName, parameterNames, parameters)
                     {ReferenceIndex = GetReferenceIndex(ccy, tenor)}
                   };

    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="modelName">Model name</param>
    /// <param name="parameterNames">Parameter names</param>
    /// <param name="parameters">Parameters</param>
    /// <param name="index">Reference index</param>
    public RateModelParameters(Model modelName, Param[] parameterNames, IModelParameter[] parameters, ReferenceIndex index)
    {
      Parameters = new[] {new RateModelParameters1D(modelName, parameterNames, parameters) {ReferenceIndex = index}};
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="fundingModel">Funding model parameters</param>
    /// <param name="modelName">Projection model name</param>
    /// <param name="parameterNames">Projection parameters name</param>
    /// <param name="parameters">Projection parameters</param>
    /// <param name="correlation">Correlation between projection and funding index</param>
    /// <param name="tenor">Tenor of projection index</param>
    ///<remarks>Correlation between funding and projection process is assumed of the form <m>\rho(T_1, T_2) = \rho(T2 - T1)</m></remarks>
    public RateModelParameters(RateModelParameters fundingModel, Model modelName, Param[] parameterNames, IModelParameter[] parameters, Curve correlation,
                               Tenor tenor)
    {
      Parameters = new RateModelParameters1D[2];
      Parameters[1] = fundingModel.Parameters[0];
      Parameters[0] = new RateModelParameters1D(modelName, parameterNames, parameters)
                      {ReferenceIndex = GetReferenceIndex(fundingModel.Parameters[0].ReferenceIndex.Currency, tenor)};
      Correlation = correlation;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="fundingModel">Funding model parameters</param>
    /// <param name="modelName">Projection model name</param>
    /// <param name="parameterNames">Projection parameters name</param>
    /// <param name="parameters">Projection parameters</param>
    /// <param name="correlation">Correlation between projection and funding index</param>
    /// <param name="projectionIndex">Projection index</param>
    ///<remarks>Correlation between funding and projection process is assumed of the form <m>\rho(T_1, T_2) = \rho(T2 - T1)</m></remarks>
    public RateModelParameters(RateModelParameters fundingModel, Model modelName, Param[] parameterNames, IModelParameter[] parameters, Curve correlation,
                               ReferenceIndex projectionIndex)
    {
      Parameters = new RateModelParameters1D[2];
      Parameters[1] = fundingModel.Parameters[0];
      Parameters[0] = new RateModelParameters1D(modelName, parameterNames, parameters) {ReferenceIndex = projectionIndex};
      Correlation = correlation;
    }

    /// <summary>
    /// Constructor for hull white single volatility model
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="vol">Single volatility</param>
    /// <param name="tenor">Reference tenor</param>
    /// <param name="ccy">Currency</param>
    public RateModelParameters(Dt asOf, double vol, Tenor tenor, Currency ccy)
    {
      var caCurve = new VolatilityCurve(asOf, vol);
      Parameters = new[]
      {
        new RateModelParameters1D(RateModelParameters.Model.Hull,
          new[] { RateModelParameters.Param.Sigma }, new IModelParameter[] {caCurve})
          {ReferenceIndex = GetReferenceIndex(ccy, tenor)}
      };
    }

    /// <summary>
    /// Constructor for hull white single volatility model
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="vol">Single volatility</param>
    /// <param name="referenceIndex">Reference Index</param>
    public RateModelParameters(Dt asOf, double vol, ReferenceIndex referenceIndex)
    {
      var caCurve = new VolatilityCurve(asOf, vol);
      Parameters = new[]
      {
        new RateModelParameters1D(RateModelParameters.Model.Hull,
          new[] { RateModelParameters.Param.Sigma }, new IModelParameter[] {caCurve})
          {ReferenceIndex = referenceIndex}
      };
    }

    #endregion

    #region Properties

    public RateModelParameters1D[] Parameters { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Get reference index
    /// </summary>
    /// <param name="ccy"></param>
    /// <param name="tenor"></param>
    /// <returns></returns>
    private static ReferenceIndex GetReferenceIndex(Currency ccy, Tenor tenor)
    {
      if (!tenor.IsEmpty)
      {
        CurveTerms terms;
        RateCurveTermsUtil.TryGetDefaultCurveTerms(String.Format("{0}{1}_{2}", ccy, "LIBOR", tenor), out terms);
        if (terms != null)
          return terms.ReferenceIndex;
      }
      return new InterestRateIndex("", tenor, ccy, DayCount.Actual360, Calendar.None,
                                   BDConvention.None, 0);
    }

    private int ProcessIndex(Process process)
    {
      if (Parameters.Length == 1)
        return 0;
      return (int)process;
    }

    /// <summary>
    /// Interpolate
    /// </summary>
    /// <param name="tenor">Maturity tenor</param>
    /// <param name="strike">Strike</param>
    /// <param name="process">Process</param>
    /// <param name="parameter">Parameter</param>
    /// <returns>Parameter value</returns>
    public double Interpolate(Dt tenor, double strike, Param parameter, Process process)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].Interpolate(parameter, tenor, strike, Parameters[idx].ReferenceIndex);
    }

    /// <summary>
    /// Tenor
    /// </summary>
    /// <param name="process">Process enum</param>
    /// <returns>Tenor of the rate/price process</returns>
    /// <remarks>Tenor is empty for forward price processes</remarks>
    public Tenor Tenor(Process process)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].ReferenceIndex.IndexTenor;
    }

    /// <summary>
    /// Model id
    /// </summary>
    /// <param name="process">Process</param>
    /// <returns>Model id</returns>
    public Model ModelName(Process process)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].ModelName;
    }

    /// <summary>
    /// Correlation between funding and projection process. We assume a form <m>\rho(T_1, T_2) = \rho(T2 - T1)</m>
    /// </summary>
    public Curve Correlation { get; private set; }

    /// <summary>
    /// Accessor for projection process parameters
    /// </summary>
    /// <param name="param">Parameter name</param>
    /// <returns>IModelParameter</returns>
    public IModelParameter this[Param param]
    {
      get { return Parameters[0][param]; }
    }

    /// <summary>
    /// Accessor for projection/funding parameters
    /// </summary>
    /// <param name="process">Process</param>
    /// <param name="param">Parameter name</param>
    /// <returns></returns>
    public IModelParameter this[Process process, Param param]
    {
      get
      {
        int idx = ProcessIndex(process);
        return Parameters[idx][param];
      }
    }

    /// <summary>
    /// Number of underlying processes (1 or 2)
    /// </summary>
    public int Count
    {
      get { return Parameters.Length; }
    }


    /// <summary>
    ///Get parameter 
    /// </summary>
    /// <param name="process">Process</param>
    /// <param name="param">Parameter</param>
    /// <param name="modelParameter">Overwritten by parameter param </param>
    /// <returns>IModelParameter</returns>
    public bool TryGetValue(Process process, Param param, out IModelParameter modelParameter)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].TryGetValue(param, out modelParameter);
    }


    /// <summary>
    /// Computes the approximate Black implied vol for the given strike 
    /// </summary>
    /// <param name="process">Process</param>
    /// <param name="asOf">As of date</param>
    /// <param name="F">Forward price/rate. Assumed to be a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry date</param>
    /// <param name="tenor">Price/rate tenor</param>
    /// <returns>Black implied vol for the given moneyness</returns>
    public double ImpliedVolatility(Process process, Dt asOf, double F, double strike, Dt expiry, Dt tenor)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].ImpliedVolatility(asOf, F, strike, expiry, tenor);
    }

    /// <summary>
    /// Computes the approximate normal Black implied vol for the given strike 
    /// </summary>
    /// <param name="process">Process</param>
    /// <param name="asOf">As of date</param>
    /// <param name="F">Forward price/rate. Assumed to be a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry date</param>
    /// <param name="tenor">Price/rate tenor</param>
    /// <returns>Normal Black implied vol for the given moneyness</returns>
    public double ImpliedNormalVolatility(Process process, Dt asOf, double F, double strike, Dt expiry, Dt tenor)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].ImpliedNormalVolatility(asOf, F, strike, expiry, tenor);
    }

    /// <summary>
    /// Computes the price of the option for this model
    /// </summary>
    ///<param name="process">Process</param>
    /// <param name="asOf">As of date</param>
    /// <param name="type">Option type</param>
    /// <param name="F">Forward price/rate <m>F_0(U)</m></param>
    /// <param name="cvxyAdj"><m>E(F_T(U)) - F_0(U)</m>in case F_t(T) is not a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry</param>
    /// <param name="tenor">Tenor of the forward rate/price</param>
    /// <returns>The option price for this model</returns>
    /// <remarks>The option value is not normalized by the time zero numeraire Z(0,T) since this step is done while taking the pv</remarks>
    public double Option(Process process, Dt asOf, OptionType type, double F, double cvxyAdj, double strike, Dt expiry, Dt tenor)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].Option(asOf, type, F, cvxyAdj, strike, expiry, tenor);
    }

    /// <summary>
    /// Computes the price of an option on the ratio of 2 rates/prices with different tenor
    /// </summary>
    ///<param name="process">Process</param>
    /// <param name="asOf">As of date</param>
    /// <param name="type">Option type</param>
    /// <param name="F">Forward price(rate) ratio <m>\frac{F_0(U)}{F_0(S)}</m></param>
    /// <param name="num">Forward price(rate) appearing in the numerator </param>
    /// <param name="den">Forward price(rate) appearing in the denominator</param>
    /// <param name="cvxyAdj"><m>E(\frac{F_T(U)}{F_T(S)} - \frac{F_0(U)}{F_0(S)}</m> in case the ratio is not a martingale under the pricing measure</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry</param>
    /// <param name="tenorNum">Tenor of index at the numerator</param>
    /// <param name="tenorDen">Tenor of the index at the denominator</param>
    /// <returns>The option price for this model</returns>
    public double OptionOnRatio(Process process, Dt asOf, OptionType type, double F, double num, double den, double cvxyAdj,
                                  double strike, Dt expiry, Dt tenorNum, Dt tenorDen)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].OptionOnRatio(asOf, type, F, num, den, cvxyAdj, strike, expiry, tenorNum,
                                           tenorDen);
    }

    /// <summary>
    /// Calc option on averaged rates by second moment matching
    /// </summary>
    /// <param name="process">Process</param>
    /// <param name="asOf">As of date</param>
    /// <param name="type">Option type</param>
    /// <param name="F">Averaged rate</param>
    /// <param name="weights">Averaging weights</param>
    /// <param name="components">Average components</param>
    /// <param name="cvxyAdj">Convexity adjustment</param>
    /// <param name="strike">Strike</param>
    /// <param name="expiry">Expiry</param>
    /// <param name="tenors">Reset dates of averaged rates</param>
    /// <param name="averageType">Average type</param>
    /// <returns>Option value</returns>
    public double OptionOnAverage(Process process, Dt asOf, OptionType type, double F, List<double> weights, List<double> components, double cvxyAdj,
                                    double strike, Dt expiry, List<Dt> tenors, ForwardModel.AverageType averageType)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].OptionOnAverage(asOf, type, F, weights, components, cvxyAdj, strike, expiry, tenors,
                                             averageType);
    }

    /// <summary>
    /// Second moment for this model
    /// </summary>
    ///<param name="process">Process</param>
    /// <param name="asOf">Asof date</param>
    /// <param name="F">Forward</param>
    /// <param name="time">Running time</param>
    /// <param name="tenor">Maturity of the rate/price </param>
    /// <returns>Second moment for this model</returns>
    public double SecondMoment(Process process, Dt asOf, double F, Dt time, Dt tenor)
    {
      int idx = ProcessIndex(process);
      return Parameters[idx].SecondMoment(asOf, F, time, tenor);
    }

    #endregion

    #region IVolatilityObject Members

    /// <summary>
    /// Distribution type
    /// </summary>
    public DistributionType DistributionType
    {
      get
      {
        return (ModelName(Process.Projection) == Model.NormalReplication || ModelName(Process.Projection) == Model.NormalBGM)
                 ? DistributionType.Normal
                 : (ModelName(Process.Projection) == Model.ShiftedBGM) ? DistributionType.ShiftedLogNormal : DistributionType.LogNormal;
      }
    }

    #endregion
  }
  #endregion
}
