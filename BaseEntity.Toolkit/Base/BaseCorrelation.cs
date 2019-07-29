/*
 * BaseCorrelation.cs
 *
 * A class for base correlation data
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;

using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   A class for base correlation data.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This class provides basic data structures and defines basic interface
  ///   for base correlation objects.</para>
  ///
  ///  <para>Base Correlations are defined as the single correlation inputs required
  ///   for a series of first loss(equity) tranches that give tranche values consistent
  ///   with quoted spreads.  The original JP Morgan definition uses the standardized
  ///   Large Pool model.  Our approach is to base all the calculations on the
  ///   heterogeneous basket model, taking into account the heterogeneity in recoveries,
  ///   survival probabilities, notional, etc.</para>
  ///
  ///  <para>There are two ways to calculate base correlations from market spread quotes
  ///   of a sequence of contiguous tranches, such as the tranches 0~3%, 3~6%, 6~9%, ...,
  ///   for DJ Tranched TRAC-X Europe.  The methods of calculation are identified
  ///   by enum <c>BaseCorrelationMethod</c> Although both methods yield almost same results in 
  ///   most cases the Arbitrage Free method is currently the industry standard.
  ///   </para>
  ///
  ///   <list type="bullet">
  ///     <item><term>ProtectionMatching</term>
  ///       <description>Suppose the detachment points are
  ///       <formula inline="true">d_1, d_2, \ldots</formula> and let <formula inline="true">d_0 = 0</formula>.
  ///       This method first finds
  ///       the implied tranche correlation to match the market spread for each tranche
  ///       <formula inline="true">[d_{i-1}, d_i]</formula> and uses the correlation
  ///       to calculate the protection PV, <formula inline="true">\mathrm{Prot}[d_{i-1},d_i]</formula>, of the tranche.
  ///
  ///       <para>Then it finds the protection PV of each first loss tranche <formula inline="true">[0, d_i]</formula>
  ///       by recursion:
  ///       <formula>
  ///         \mathrm{Prot}[0,d_i] = \mathrm{Prot}[0, d_{i-1}] + \mathrm{Prot}[d_{i-1},d_i]
  ///         \qquad i = 2, 3, \ldots
  ///       </formula>
  ///       </para>
  ///
  ///      <para>Once the protection PVs on the first loss tranches are known, the base correlation are calculated
  ///      as the implied correlations matching the protection values.</para>
  ///     </description></item>
  ///     <item><term>ArbitageFree</term>
  ///       <description>This method finds a sequence of correlations such that if you long on the first
  ///       loss tranche <formula inline="true">[0,d_i]</formula> and short on the tranche
  ///       <formula inline="true">[0,d_{i-1}]</formula>, you should have no arbitrage advantage nor disadvantage
  ///       over the tranche <formula inline="true">[d_{i-1},d_i]</formula>. 
  ///
  ///      <para>The calculation is performed by recursion, starting with the equity tranche <formula inline="true">[0,d_1]</formula>,
  ///      the correlation of which is simply the implied tranche correlation.  Once we know the base correlation of
  ///      of the first loss tranche <formula inline="true">[0,d_i]</formula>, we calculate the pricer of the tranche
  ///      using spread from market spread of the next tranche
  ///      <formula inline="true">[d_i,d_{i+1}]</formula>.  Then we find the implied correlation which matches the price
  ///      on the new first loss tranche <formula inline="true">[0,d_{i+1}]</formula>.</para>
  ///     </description></item>
  ///   </list>
  ///   
  ///   <para>Hence the 'Arbitrage Free' method goes through the following steps:</para>
  ///       <list type="number">
  ///						<item><term>Calculate the implied correlation for the equity tranche (i.e 0-3%)</term>
  ///						</item>
  ///					<item><term>Using this correlation, calculate the value of the equity tranche if its spread was the same as 
  ///						    the one of the next tranche in the capital stucture (i.e 3-7%). The value will be negative since the
  ///							  3-7% tranche has a lower spread than the equity tranche.</term>  </item>
  ///					<item><term>The value of the 3-7% tranche using its own (quoted) spread is zero (by defintition) so the value of 
  ///						    the 0-7% tranche using the 3-7% spread must equal the value of the 0-3%(equity) tranche using 
  ///							  the 3-7% spread</term> </item>
  ///				  <item><term>Find the level of correlation that sets the value of the 0-7% tranche (earning the 3-7% spread)  
  ///						    equal to the value calculated in step 2. ( the value of the equity tranche using its own correlation 
  ///							  but the 3-7% spread).  THIS correlation will be the (arbitrage-free) base correlation for the 0-7% tranche</term></item>
  ///					<item><term>Use this correlation to value the 0-7% tranche using the 7-10% spread and then repeat step 1-4.</term></item>
  ///				</list> 
  ///		<para></para>		  
  ///   <para>To be able to interpolate base correlation values for tranches with different detachment points and with 
  ///         different underlying portfolios we express the detachment points in terms of strikes. Using various strike
  ///         methods, detachment points are mapped to strikes adjusting them for changes in underlying portfolio losses. 
  ///         If, for example, the chosen strike method is based on the portfolio expected-loss then the tranche detachment 
  ///         points will be expressed in terms of multiples of the expected loss of the entire undelying CDO (portfolio). 
  ///   </para>
  ///   
  ///   <para>---------------------------------------------------------------</para>          
  ///   <example> EXAMPLE: </example>
  ///			
  ///		<para>STEP 1: Calibrate correlations to the CDX: (Expected CDX loss = 6%)</para>
  ///   
  ///   <table>
  ///     <colgroup align="center"/><colgroup><col align="right"/><col align="center"/></colgroup>
  ///     <thead><tr><th>Attachment/detachment point</th><th>Strike</th><th>Base Correlation</th></tr></thead>
  ///     <tbody>
  ///     <tr><td>3%</td><td>50.0%</td><td>0.20</td></tr>
  ///     <tr><td>7%</td><td>116.7%</td><td>0.25</td></tr>
  ///     <tr><td>10%</td><td>166.7%</td><td>0.30</td></tr>
  ///     <tr><td>15%</td><td>250.0%</td><td>0.35</td></tr>
  ///     <tr><td>30%</td><td>500.0%</td><td>0.60</td></tr>
  ///     </tbody>
  ///   </table>
  ///   
  ///   <para>The expected loss of the index is 6% so the 3% loss point is 50% of the index loss</para>
  ///   
  ///   
  ///		<para>STEP 2: Designate a new tranche/portfolio and determine new strikes</para>
  ///		
  ///		   
  ///		<para>Consider a 4%-6% tranche of a CDO whose total expected losses are 10% of the entire portfolio.  This means: </para>   
  ///	
  ///   <table>
  ///     <colgroup span="1" align="center"/><colgroup span="2" align="center"/>
  ///     <thead><tr><th>Attachment/detachment point</th><th>Strike</th><th>Base Correlation</th></tr></thead>
  ///     <tbody>
  ///     <tr><td>4%</td><td>40%</td><td>???</td></tr>
  ///     <tr><td>6%</td><td>60%</td><td>???</td></tr>
  ///     </tbody>
  ///   </table>   
  ///		<para>The 4% loss point represents 40% of the total expected losses (of this new CDO) and 6% represents 60%.</para>
  ///
  ///	<para>STEP 3: interpolate new base correlations	</para>
  ///	
  ///	<para>The efforts in step one produce a curve with strike values on the X axis and correlations on the Y axis, 
  ///	 and we want to interpolate off this curve to get values associated with the new strikes we determined in Step 2.  
  ///	 Naturally this depends on the interpolation/extrapolation methods one chooses to use. </para>
  ///		   
  ///  <para>For linear interpolation and "smooth" extrapolation (linear matching the slope at the endpoint) you'd get:</para>
  ///	 
  ///   <table>
  ///     <colgroup span="1" align="center"/><colgroup span="2" align="center"/>
  ///     <thead><tr><th>Attachment/detachment point</th><th>Strike</th><th>Base Correlation</th></tr></thead>
  ///     <tbody>
  ///     <tr><td>4%</td><td>40%</td><td>.1925</td></tr>
  ///     <tr><td>6%</td><td>60%</td><td>.2075</td></tr>
  ///     </tbody>
  ///   </table>
  /// 
  ///<para>---------------------------------------------------------------</para>      
  ///  
  ///  <para>The relationship between base correlations and detachment points is called the smile function.  
  ///   Given a series of base correlations and detachments, the relationship can be established by some
  ///   interpolation methods.
  ///   Once such a relationship is established, we can use it to imply the correlation
  ///   of a first loss tranche with a different detachment point and usually on a different portfolio.</para>
  ///
  ///  <para>In  Toolkit, the (base correlation) smile is conceptually expressed as a function, <formula inline="true">f(x)</formula>, 
  ///   of strikes. Strikes are nothing else but a transformation of the detachments <formula inline="true">d</formula>, 
  ///   that is, <formula inline="true">x = x(d)</formula>,
  ///   where <formula inline="true">x</formula> is the 'strike' and the function <formula inline="true">x(d)</formula> 
  ///   the 'strike method'.  The available strike methods are listed by enum <c>BaseCorrelationStrikeMethod</c>.</para>
  ///
  ///   <list type="bullet">
  ///     <item><term>Unscaled</term>
  ///       <description><formula inline="true">x = d</formula>.
  ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
  ///       base correlation is computed as <formula inline="true">c^* = f(d^*)</formula>.
  ///     </description></item>
  ///     <item><term>ExpectedLoss</term>
  ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">s</formula> is the ratio
  ///       of expected basket losses over the portfolio notional.
  ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
  ///       base correlation is computed as <formula inline="true">c^* = f(d^*/s^*)</formula>, where
  ///       <formula inline="true">s^*</formula> is the expected loss ratio of the underlying portfolio.
  ///       The later may differ than the portfolio on which the smile function is calibrated.
  ///     </description></item>
  ///     <item><term>ExpectedLossPv</term>
  ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">s</formula> is the ratio
  ///       of protection PV on the whole portfolio over the portfolio notional.
  ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
  ///       base correlation is computed as <formula inline="true">c^* = f(d^*/s^*)</formula>, where
  ///       <formula inline="true">s^*</formula> is the protection PV ratio of the underlying portfolio.
  ///       The later may differ than the portfolio on which the smile function is calibrated.
  ///     </description></item>
  ///     <item><term>ExpectedLossRatio</term>
  ///       <description><formula inline="true">x = r</formula>, where <formula inline="true">r</formula> is the ratio
  ///       of the expected loss of the first loss tranche at detachment, <formula inline="true">d</formula>, over
  ///       the expected loss of the whole portfolio.
  ///
  ///       <para>Suppose we are going to calculate the correlation of a first loss tranche with detachment
  ///       <formula inline="true">d^*</formula> on a different portfolio.  For this tranche, the expected loss ratio
  ///       depends on the correlation. The relationship can be denoted <formula inline="true">r = R(c)</formula>.
  ///       This method finds both the base correlation <formula inline="true">c^*</formula> and the corresponding
  ///       loss ratio <formula inline="true">r^*</formula> by solving the following two equations simultaneously
  ///       <formula>
  ///         r^* = R(c^*) \qquad\mathrm{and}\qquad c^* = f(r^*)
  ///       </formula>
  ///       </para>
  ///     </description></item>
  ///     <item><term>Probability</term>
  ///       <description><formula inline="true">x = p</formula>, where <formula inline="true">p</formula> is the
  ///       cumulative probability that the percentage of portfolio loss <formula inline="true">L</formula> does not exceed
  ///       the detachment <formula inline="true">d</formula>, <formula inline="true">p = \mathrm{Prob}[L \leq d]</formula>.
  ///
  ///       <para>Suppose we are going to calculate the correlation of a first loss tranche with detachment
  ///       <formula inline="true">d^*</formula> on a different portfolio.  For this tranche, the cumulative loss probability
  ///       depends on the correlation. The relationship can be denoted <formula inline="true">p = P(c)</formula>.
  ///       This method finds both the base correlation <formula inline="true">c^*</formula> and the corresponding
  ///       probability <formula inline="true">p^*</formula> by solving the following two equations simultaneously
  ///       <formula>
  ///         p^* = P(c^*) \qquad\mathrm{and}\qquad c^* = f(p^*)
  ///       </formula>
  ///       </para>
  ///     </description></item>
  ///   </list>
  ///
  /// </remarks>
  [Serializable]
  public partial class BaseCorrelation : BaseCorrelationObject, ICorrelationBump
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseCorrelation));

    #region Config
    // hard coded, retire later
    private static bool use8dot1Solver_ = false;
    private static bool use8dot5Solver_ = false;
    #endregion // Config

    #region Types

    /// <summary>
    ///   Interface for user defined mapping method
    /// </summary>
    /// 
    /// <remarks>
    ///   This class provides an interface for user defined strike mapping methods.
    /// </remarks>
    /// 
    /// <example>
    /// The following is an example which implements equity spread mapping.
    /// <code language="C#">
    ///  <summary>
    ///    Define a class implementing the interface IStrikeEvaluator 
    ///  </summary>
    ///    class EquitySpreadMapping : BaseEntity.Toolkit.Base.BaseCorrelation.IStrikeEvaluator
    ///    {
    ///      private SyntheticCDOPricer pricer_;
    ///
    ///      // Implementation of Clone method
    ///      public object Clone()
    ///      {
    ///        EquitySpreadMapping obj = new EquitySpreadMapping();
    ///        obj.pricer_ = (SyntheticCDOPricer)pricer_.Clone();
    ///        return obj;
    ///      }
    ///
    ///
    ///      // Implementation of SetPricer method
    ///      public void SetPricer(SyntheticCDOPricer pricer)
    ///      {
    ///        pricer_ = pricer;
    ///      }
    ///
    ///      // Implementation of Strike method
    ///      public double Strike(double factor)
    ///      {
    ///        pricer_.Basket.SetFactor(factor);
    ///        pricer_.Basket.Reset();
    ///        return pricer_.BreakEvenPremium();
    ///      }
    /// 
    ///      // Implementation of Strike method
    ///      public double Strike()
    ///      {
    ///        return pricer_.BreakEvenPremium();
    ///      }
    ///    } // class EquitySpreadMapping
    ///
    ///   ....
    /// 
    ///   // Calibrating a base correlation object using this mapping method
    ///   BaseCorrelationObject bco = new BaseCorrelationTermStruct(
    ///     BaseCorrelationMethod.ArbitrageFree,
    ///     BaseCorrelationStrikeMethod.UserDefined, new EquitySpreadMapping(),
    ///     BaseCorrelationCalibrationMethod.TermStructure,
    ///     cdoArrays, asOf, settle,
    ///     survivalCurves, recoveryCurves, discountCurve, principalArrays,
    ///     stepSize, stepUnit, copula, gridSize, quadraturePoints, 0, 0.0, 0.0,
    ///     strikeInterp, strikeExtrap, tenorInterp, tenorExtrap,
    ///     0.0, 1.0);
    /// </code>
    /// </example>
    public interface IStrikeEvaluator
    {
      /// <summary>
      ///   Clone itself
      /// </summary>
      /// <returns>Cloned object</returns>
      object Clone();

      /// <summary>
      ///   Set the pricer used to calculate the strike
      /// </summary>
      /// <param name="pricer">CDO pricer</param>
      void SetPricer(SyntheticCDOPricer pricer);

      /// <summary>
      ///   Calculate the strike corresponding to a given correlation
      /// </summary>
      /// <param name="correlation">correlation</param>
      /// <returns>strike</returns>
      double Strike(double correlation);

      /// <summary>
      ///   Calculate the strike based on whatever correlation embedded in the pricer
      /// </summary>
      /// <returns>strike</returns>
      double Strike();
    }
    #endregion // Types

    #region New_Constructors
    /// <summary>
    ///   Construct a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method constructs a base correlation object from a sequence of synthetic CDO tranches.
    ///   It requires the tranches be continuous without any gap and calculates both
    ///   the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <param name="entityNames">Names of the underlying credit entities</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    /// <exclude />
    internal BaseCorrelation(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      SyntheticCDO[] cdos,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX,
      string[] entityNames
      )
    {
      // Validate
      if (cdos == null || cdos.Length < 1)
        throw new ArgumentException("Must specify cdos");
      if (cdos[0].Attachment != 0.0)
        throw new ArgumentException(String.Format("The start attachment point {0} is not zero", cdos[0].Attachment));
      for (int i = 1; i < cdos.Length; ++i)
      {
        if (cdos[i].Attachment != cdos[i - 1].Detachment)
          throw new ArgumentException(String.Format("Discontinued tranches {0}~{1} and {2}~{3}",
            cdos[i - 1].Attachment, cdos[i - 1].Detachment,
            cdos[i].Attachment, cdos[i].Detachment));
        if (cdos[i].Maturity != cdos[0].Maturity)
          throw new ArgumentException(String.Format("Maturity of tranche #{0} differs", i));
      }

      // Check strike evaluator
      if (strikeMethod == BaseCorrelationStrikeMethod.UserDefined && strikeEvaluator == null)
        throw new System.NullReferenceException("strikeEvaluator cannot be null with UserDefined strke method");
      this.strikeEvaluator_ = strikeEvaluator;

      // Create a basket pricer
      BasketPricer basket = CreateBasketPricer(
        cdos, asOf, settle, survivalCurves, recoveryCurves, principals,
        stepSize, stepUnit, copula, gridSize, integrationPointsFirst, integrationPointsSecond);

      this.Initialize(method, strikeMethod, cdos, basket, discountCurve, toleranceF, toleranceX, entityNames);
    }


    /// <summary>
    ///   Construct a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method constructs a base correlation object from a sequence of synthetic CDO tranches
    ///   and a given basket pricer.  It requires the tranches be continuous without any gap and calculates
    ///   both the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// 
    ///  <para>The basket pricer can be more general than a simple heterogeneous basket pricer with
    ///   Gauss copula.
    ///   For example, it may use a different copula or use correlation term structure.</para>
    /// 
    ///  <para>This function is called by BaseCorrelationTermStruct class to do bootstraping.</para>
    /// </remarks>
    ///
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="basePricers">Base tranche pricers</param>
    /// <param name="tranchePricers">Tranche pricers</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <param name="entityNames">Names of the underlying credit entities</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    /// <exclude />
    internal BaseCorrelation(
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      SyntheticCDOPricer[] basePricers,
      SyntheticCDOPricer[] tranchePricers,
      double toleranceF,
      double toleranceX,
      string[] entityNames
      )
    {
      // Validate
      if (basePricers == null || basePricers.Length < 1)
        throw new ArgumentException("Must specify base pricers");

      // Check strike evaluator
      if (strikeMethod == BaseCorrelationStrikeMethod.UserDefined && strikeEvaluator == null)
        throw new NullReferenceException("strikeEvaluator cannot be null with UserDefined strke method");
      this.strikeEvaluator_ = strikeEvaluator;

      //TODO: Check base tranche is valid (with 0 attachment)?
      //TODO: Check tranche consistency?
      //These checks are done in calling function, but should do it here if we expose this method.
      this.Initialize(strikeMethod, basePricers, tranchePricers, toleranceF, toleranceX, entityNames);
    }

    #endregion // New_Constructors

    #region Constructors

    /// <summary>
    ///   Construct a base correlation smile directly from strikes and base correlations.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Construct a base correlation set directly from strikes and base correlations.
    ///   Strikes may be calculated independently using the Strike function.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="strikes">Array of normalized detachment points</param>
    /// <param name="correlations">Array of (base) correlations matching strikes</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      double[] strikes,
      double[] correlations
      )
    {
      if (strikes == null)
        throw new ArgumentException("Null strike array");
      if (correlations == null)
        throw new ArgumentException("Null correlation array");
      if (correlations.Length != strikes.Length)
        throw new ArgumentException("The number of correlations must match the number of strikes");

      // Check strike evaluator
      if (strikeMethod == BaseCorrelationStrikeMethod.UserDefined && strikeEvaluator == null)
        throw new NullReferenceException("strikeEvaluator cannot be null with UserDefined strke method");
      this.strikeEvaluator_ = strikeEvaluator;

      method_ = method;
      strikeMethod_ = strikeMethod;
      interpMethod_ = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const, 0.0, 1.0);

      // Copy strikes and correlations
      strikes_ = new double[strikes.Length];
      correls_ = new double[correlations.Length];
      tcorrels_ = new double[correlations.Length];
      for (int i = 0; i < strikes.Length; ++i)
      {
        strikes_[i] = strikes[i];
        correls_[i] = correlations[i];
        if (i == 0)
          tcorrels_[i] = correlations[i];
        else
          tcorrels_[i] = Double.NaN;
      }
      scalingFactor_ = 1.0;

      return;
    }

    /// <summary>
    ///   Construct a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method constructs a base correlation object from a sequence of synthetic CDO tranches.
    ///   It requires the tranches be continuous without any gap and calculates both
    ///   the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      SyntheticCDO[] cdos,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX
      )
    {
      // Validate
      if (cdos == null || cdos.Length < 1)
        throw new ArgumentException("Must specify cdos");
      if (cdos[0].Attachment != 0.0)
        throw new ArgumentException(String.Format("The start attachment point {0} is not zero", cdos[0].Attachment));
      for (int i = 1; i < cdos.Length; ++i)
      {
        if (cdos[i].Attachment != cdos[i - 1].Detachment)
          throw new ArgumentException(String.Format("Discontinued tranches {0}~{1} and {2}~{3}",
            cdos[i - 1].Attachment, cdos[i - 1].Detachment, cdos[i].Attachment, cdos[i].Detachment));
        if (cdos[i].Maturity != cdos[0].Maturity)
          throw new ArgumentException(String.Format("Maturity of tranche #{0} differs", i));
      }

      // Check strike evaluator
      if (strikeMethod == BaseCorrelationStrikeMethod.UserDefined && strikeEvaluator == null)
        throw new System.NullReferenceException("strikeEvaluator cannot be null with UserDefined strke method");
      this.strikeEvaluator_ = strikeEvaluator;

      // Create a basket pricer
      BasketPricer basket = CreateBasketPricer(
        cdos, asOf, settle, survivalCurves, recoveryCurves, principals,
        stepSize, stepUnit, copula, gridSize, integrationPointsFirst, integrationPointsSecond);

      this.Initialize(method, strikeMethod, cdos, basket, discountCurve, toleranceF, toleranceX, null);
    }

    /// <summary>
    ///   Construct a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method constructs a base correlation object from a sequence of synthetic CDO tranches
    ///   and a given basket pricer.  It requires the tranches be continuous without any gap and calculates
    ///   both the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// 
    ///  <para>The basket pricer can be more general than a simple heterogeneous basket pricer with
    ///   Gauss copula.
    ///   For example, it may use a different copula or use correlation term structure.</para>
    /// 
    ///  <para>This function is called by BaseCorrelationTermStruct class to do bootstraping.</para>
    /// </remarks>
    ///
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="basePricers">Base tranche pricers</param>
    /// <param name="tranchePricers">Tranche pricers</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    /// <exclude />
    public BaseCorrelation(
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      SyntheticCDOPricer[] basePricers,
      SyntheticCDOPricer[] tranchePricers,
      double toleranceF,
      double toleranceX
      )
    {
      // Validate
      if (basePricers == null || basePricers.Length < 1)
        throw new ArgumentException("Must specify base pricers");

      // Check strike evaluator
      if (strikeMethod == BaseCorrelationStrikeMethod.UserDefined && strikeEvaluator == null)
        throw new NullReferenceException("strikeEvaluator cannot be null with UserDefined strke method");
      this.strikeEvaluator_ = strikeEvaluator;

      //TODO: Check base tranche is valid (with 0 attachment)?
      //TODO: Check tranche consistency?
      //These checks are done in calling function, but should do it here if we expose this method.
      this.Initialize(strikeMethod, basePricers, tranchePricers, toleranceF, toleranceX, null);
    }

    /// <summary>
    ///   Construct a base correlation smile by combining several base correlation objects
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This constructor requires all the input base correlation objects have the same
    ///   strike methods.  In the resulted base correlation object, the strikes are the
    ///   union of all the strike points of the input objects and the base correlations
    ///   are calculated as the weighted average of the smile functions represented by the input
    ///   base correlations.</para>
    ///
    ///   <para>Here is a description of the algorithm. Let <formula inline="true">x \rightarrow f_i(x)</formula>
    ///   be the smile function of the <i>i</i>th object, where <formula inline="true">x</formula>
    ///   is strike. Let <formula inline="true">\alpha_i</formula> be the
    ///   corresponding weight with <formula inline="true">\sum_i \alpha_i = 1</formula>.  Then the
    ///   resulting object represents a smile function given by
    ///   <formula>
    ///      f(x) = \sum_i \alpha_i f_i( x )
    ///   </formula>
    ///  </para>
    ///
    ///  <para>If the input <c>weights</c> does not sum up to 1,
    ///   the weights will be automatically scaled to ensure such property.</para>
    /// </remarks>
    ///
    /// <param name="baseCorrelations">Base correlations to combine</param>
    /// <param name="weights">Weights to combine base correlations</param>
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="interpMethod">Interpolation method between strikes</param>
    /// <param name="extrapMethod">Extrapolation method for strikes</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelation[] baseCorrelations,
      double[] weights,
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod
      )
    {
      // Validation
      if (baseCorrelations == null || baseCorrelations.Length < 1)
        throw new ArgumentException("Must specify Base correlations");
      if (baseCorrelations.Length != weights.Length)
        throw new ArgumentException("Base correlations and strike should be of the same size");

      // Check strike evaluator
      if (strikeMethod == BaseCorrelationStrikeMethod.UserDefined && strikeEvaluator == null)
        throw new NullReferenceException("strikeEvaluator cannot be null with UserDefined strke method");
      this.strikeEvaluator_ = strikeEvaluator;

      // check sum of weights
      double sumWeights = 0;
      for (int i = 0; i < weights.Length; ++i)
        sumWeights += weights[i];

      // make the weights add up to 1
      for (int i = 0; i < weights.Length; ++i)
        weights[i] /= sumWeights;

      // make a combined strikes array
      ArrayList list = new ArrayList();
      for (int i = 0; i < baseCorrelations.Length; ++i)
      {
        if (baseCorrelations[i].StrikeMethod != strikeMethod)
          throw new ArgumentException("Only base correlations with same strike methods can be combined.");

        double[] si = baseCorrelations[i].Strikes;
        for (int j = 0; j < si.Length; ++j)
        {
          double strike = si[j];
          int pos = list.BinarySearch(strike);
          if (pos < 0)
            list.Insert(~pos, strike);
        }
      }
      double[] strikes = new double[list.Count];
      for (int i = 0; i < list.Count; ++i)
        strikes[i] = (double)list[i];

      // calculate correlations
      double[] corrs = new double[strikes.Length];
      for (int j = 0; j < strikes.Length; ++j)
      {
        double strike = strikes[j];
        double corr = 0;
        for (int i = 0; i < weights.Length; ++i)
        {
          corr += weights[i] * baseCorrelations[i].GetCorrelation(strike);
        }
        corrs[j] = corr;
      }

      // initialize members
      this.strikes_ = strikes;
      this.correls_ = corrs;
      this.method_ = method;
      this.strikeMethod_ = strikeMethod;
      this.interpMethod_ = InterpFactory.FromMethod(interpMethod, extrapMethod, 0.0, 1.0);
      this.tcorrels_ = null;
      this.scalingFactor_ = 1.0;

      return;
    }

    /// <summary>
    ///   Construct a base correlation smile by combining several base correlation objects
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This constructor requires all the input base correlation objects have the same
    ///   strike methods.  In the resulted base correlation object, the strikes are the
    ///   union of all the strike points of the input objects and the base correlations
    ///   are calculated as the weighted average of the smile functions represented by the input
    ///   base correlations.</para>
    ///
    ///   <para>Here is a description of the algorithm. Let <formula inline="true">x \rightarrow f_i(x)</formula>
    ///   be the smile function of the <i>i</i>th object, where <formula inline="true">x</formula>
    ///   is strike. Let <formula inline="true">\alpha_i</formula> be the
    ///   corresponding weight with <formula inline="true">\sum_i \alpha_i = 1</formula>.  Then the
    ///   resulting object represents a smile function given by
    ///   <formula>
    ///      f(x) = \sum_i \alpha_i f_i( x )
    ///   </formula>
    ///  </para>
    ///
    ///  <para>If the input <c>weights</c> does not sum up to 1,
    ///   the weights will be automatically scaled to ensure such property.</para>
    /// </remarks>
    ///
    /// <param name="baseCorrelations">Base correlations to combine</param>
    /// <param name="weights">Weights to combine base correlations</param>
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="interpMethod">Interpolation method between strikes</param>
    /// <param name="extrapMethod">Extrapolation method for strikes</param>
    /// <param name="min">Minimum return value (for Smooth extrap only)</param>
    /// <param name="max">Maximum return value (for Smooth extrap only)</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelation[] baseCorrelations,
      double[] weights,
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      double min,
      double max
      )
    {
      // Validation
      if (baseCorrelations == null || baseCorrelations.Length < 1)
        throw new ArgumentException("Must specify Base correlations");
      if (baseCorrelations.Length != weights.Length)
        throw new ArgumentException("Base correlations and strike should be of the same size");

      // Check strike evaluator
      if (strikeMethod == BaseCorrelationStrikeMethod.UserDefined && strikeEvaluator == null)
        throw new System.NullReferenceException("strikeEvaluator cannot be null with UserDefined strke method");
      this.strikeEvaluator_ = strikeEvaluator;

      // check sum of weights
      double sumWeights = 0;
      for (int i = 0; i < weights.Length; ++i)
        sumWeights += weights[i];

      // make the weights add up to 1
      for (int i = 0; i < weights.Length; ++i)
        weights[i] /= sumWeights;

      // make a combined strikes array
      ArrayList list = new ArrayList();
      for (int i = 0; i < baseCorrelations.Length; ++i)
      {
        if (baseCorrelations[i].StrikeMethod != strikeMethod)
          throw new ArgumentException("Only base correlations with same strike methods can be combined.");

        double[] si = baseCorrelations[i].Strikes;
        for (int j = 0; j < si.Length; ++j)
        {
          double strike = si[j];
          if(double.IsNaN(strike))
            continue;
          int pos = list.BinarySearch(strike);
          if (pos < 0)
            list.Insert(~pos, strike);
        }
      }
      double[] strikes = new double[list.Count];
      for (int i = 0; i < list.Count; ++i)
        strikes[i] = (double)list[i];

      // calculate correlations
      double[] corrs = new double[strikes.Length];
      for (int j = 0; j < strikes.Length; ++j)
      {
        double strike = strikes[j];
        double corr = 0;
        for (int i = 0; i < weights.Length; ++i)
        {
          corr += weights[i] * baseCorrelations[i].GetCorrelation(strike);
          interpOnFactors_ = interpOnFactors_ || baseCorrelations[i].InterpOnFactors;
        }
        corrs[j] = corr;
      }

      // initialize members
      this.strikes_ = strikes;
      this.correls_ = corrs;
      this.method_ = method;
      this.strikeMethod_ = strikeMethod;
      this.Extended = (max > 1);
      this.interpMethod_ = InterpFactory.FromMethod(interpMethod, extrapMethod, min, max);
      this.tcorrels_ = null;
      this.scalingFactor_ = 1.0;

      return;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      BaseCorrelation obj = (BaseCorrelation)base.Clone();

      obj.interpMethod_ = (Interp)interpMethod_.clone();
      obj.strikes_ = CloneUtil.Clone(strikes_);
      obj.correls_ = CloneUtil.Clone(correls_);
      obj.tcorrels_ = CloneUtil.Clone(tcorrels_);
      obj.dps_ = CloneUtil.Clone(dps_);

      obj.strikeEvaluator_ = strikeEvaluator_ == null ? null : (IStrikeEvaluator)strikeEvaluator_.Clone();

      return obj;
    }

    #region Old_Constructors
    /// <summary>
    ///   Construct a base correlation smile directly from strikes and base correlations.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Construct a base correlation set directly from strikes and base correlations.
    ///   Strikes may be calculated independently using the Strike function.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikes">Array of normalized detachment points</param>
    /// <param name="correlations">Array of (base) correlations matching strikes</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      double[] strikes,
      double[] correlations
      )
    {
      if (strikes == null)
        throw new ArgumentException("Null strike array");
      if (correlations == null)
        throw new ArgumentException("Null correlation array");
      if (correlations.Length != strikes.Length)
        throw new ArgumentException("The number of correlations must match the number of strikes");

      method_ = method;
      strikeMethod_ = strikeMethod;
      interpMethod_ = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const, 0.0, 1.0);

      // Copy strikes and correlations
      strikes_ = new double[strikes.Length];
      correls_ = new double[correlations.Length];
      for (int i = 0; i < strikes.Length; ++i)
      {
        strikes_[i] = strikes[i];
        correls_[i] = correlations[i];
      }
      tcorrels_ = null;
      scalingFactor_ = 1.0;

      return;
    }

    /// <summary>
    ///   Construct a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method constructs a base correlation object from a sequence of synthetic CDO tranches.
    ///   It requires the tranches be continuous without any gap and calculates both
    ///   the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      SyntheticCDO[] cdos,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX
      )
    {
      // Validate
      if (cdos == null || cdos.Length < 1)
        throw new ArgumentException("Must specify cdos");
      if (cdos[0].Attachment != 0.0)
        throw new ArgumentException(String.Format("The start attachment point {0} is not zero", cdos[0].Attachment));
      for (int i = 1; i < cdos.Length; ++i)
      {
        if (cdos[i].Attachment != cdos[i - 1].Detachment)
          throw new ArgumentException(String.Format("Discontinued tranches {0}~{1} and {2}~{3}",
                                                                                                                               cdos[i - 1].Attachment, cdos[i - 1].Detachment,
                                                                                                                               cdos[i].Attachment, cdos[i].Detachment));
        if (cdos[i].Maturity != cdos[0].Maturity)
        if (cdos[i].Maturity != cdos[0].Maturity)
          throw new ArgumentException(String.Format("Maturity of tranche #{0} differs", i));
      }

      // Create a basket pricer
      BasketPricer basket = CreateBasketPricer(
        cdos, asOf, settle, survivalCurves, recoveryCurves, principals,
        stepSize, stepUnit, copula, gridSize, integrationPointsFirst, integrationPointsSecond);

      this.Initialize(method, strikeMethod, cdos, basket, discountCurve, toleranceF, toleranceX, null);
    }


    /// <summary>
    ///   Construct a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method constructs a base correlation object from a sequence of synthetic CDO tranches
    ///   and a given basket pricer.  It requires the tranches be continuous without any gap and calculates
    ///   both the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// 
    ///  <para>The basket pricer can be more general than a simple heterogeneous basket pricer with
    ///   Gauss copula.
    ///   For example, it may use a different copula or use correlation term structure.</para>
    /// 
    ///  <para>This function is called by BaseCorrelationTermStruct class to do bootstraping.</para>
    /// </remarks>
    ///
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="basePricers">Base tranche pricers</param>
    /// <param name="tranchePricers">Tranche pricers</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    /// <exclude />
    public BaseCorrelation(
      BaseCorrelationStrikeMethod strikeMethod,
      SyntheticCDOPricer[] basePricers,
      SyntheticCDOPricer[] tranchePricers,
      double toleranceF,
      double toleranceX
      )
    {
      // Validate
      if (basePricers == null || basePricers.Length < 1)
        throw new ArgumentException("Must specify base pricers");

      //TODO: Check base tranche is valid (with 0 attachment)?
      //TODO: Check tranche consistency?
      //These checks are done in calling function, but should do it here if we expose this method.
      this.Initialize(strikeMethod, basePricers, tranchePricers, toleranceF, toleranceX, null);
    }

    /// <summary>
    ///   Construct a base correlation smile by combining several base correlation objects
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This constructor requires all the input base correlation objects have the same
    ///   strike methods.  In the resulted base correlation object, the strikes are the
    ///   union of all the strike points of the input objects and the base correlations
    ///   are calculated as the weighted average of the smile functions represented by the input
    ///   base correlations.</para>
    ///
    ///   <para>Here is a description of the algorithm. Let <formula inline="true">x \rightarrow f_i(x)</formula>
    ///   be the smile function of the <i>i</i>th object, where <formula inline="true">x</formula>
    ///   is strike. Let <formula inline="true">\alpha_i</formula> be the
    ///   corresponding weight with <formula inline="true">\sum_i \alpha_i = 1</formula>.  Then the
    ///   resulting object represents a smile function given by
    ///   <formula>
    ///      f(x) = \sum_i \alpha_i f_i( x )
    ///   </formula>
    ///  </para>
    ///
    ///  <para>If the input <c>weights</c> does not sum up to 1,
    ///   the weights will be automatically scaled to ensure such property.</para>
    /// </remarks>
    ///
    /// <param name="baseCorrelations">Base correlations to combine</param>
    /// <param name="weights">Weights to combine base correlations</param>
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="interpMethod">Interpolation method between strikes</param>
    /// <param name="extrapMethod">Extrapolation method for strikes</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelation[] baseCorrelations,
      double[] weights,
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod
      )
    {
      // Validation
      if (baseCorrelations == null || baseCorrelations.Length < 1)
        throw new ArgumentException("Must specify Base correlations");
      if (baseCorrelations.Length != weights.Length)
        throw new ArgumentException("Base correlations and strike should be of the same size");

      // check sum of weights
      double sumWeights = 0;
      for (int i = 0; i < weights.Length; ++i)
        sumWeights += weights[i];

      // make the weights add up to 1
      for (int i = 0; i < weights.Length; ++i)
        weights[i] /= sumWeights;

      // make a combined strikes array
      ArrayList list = new ArrayList();
      for (int i = 0; i < baseCorrelations.Length; ++i)
      {
        if (baseCorrelations[i].StrikeMethod != strikeMethod)
          throw new ArgumentException("Only base correlations with same strike methods can be combined.");

        double[] si = baseCorrelations[i].Strikes;
        for (int j = 0; j < si.Length; ++j)
        {
          double strike = si[j];
          int pos = list.BinarySearch(strike);
          if (pos < 0)
            list.Insert(~pos, strike);
        }
      }
      double[] strikes = new double[list.Count];
      for (int i = 0; i < list.Count; ++i)
        strikes[i] = (double)list[i];

      // calculate correlations
      double[] corrs = new double[strikes.Length];
      for (int j = 0; j < strikes.Length; ++j)
      {
        double strike = strikes[j];
        double corr = 0;
        for (int i = 0; i < weights.Length; ++i)
        {
          corr += weights[i] * baseCorrelations[i].GetCorrelation(strike);
        }
        corrs[j] = corr;
      }

      // initialize members
      this.strikes_ = strikes;
      this.correls_ = corrs;
      this.method_ = method;
      this.strikeMethod_ = strikeMethod;
      this.interpMethod_ = InterpFactory.FromMethod(interpMethod, extrapMethod, 0.0, 1.0);
      this.tcorrels_ = null;
      this.scalingFactor_ = 1.0;

      return;
    }

    /// <summary>
    ///   Construct a base correlation smile by combining several base correlation objects
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This constructor requires all the input base correlation objects have the same
    ///   strike methods.  In the resulted base correlation object, the strikes are the
    ///   union of all the strike points of the input objects and the base correlations
    ///   are calculated as the weighted average of the smile functions represented by the input
    ///   base correlations.</para>
    ///
    ///   <para>Here is a description of the algorithm. Let <formula inline="true">x \rightarrow f_i(x)</formula>
    ///   be the smile function of the <i>i</i>th object, where <formula inline="true">x</formula>
    ///   is strike. Let <formula inline="true">\alpha_i</formula> be the
    ///   corresponding weight with <formula inline="true">\sum_i \alpha_i = 1</formula>.  Then the
    ///   resulting object represents a smile function given by
    ///   <formula>
    ///      f(x) = \sum_i \alpha_i f_i( x )
    ///   </formula>
    ///  </para>
    ///
    ///  <para>If the input <c>weights</c> does not sum up to 1,
    ///   the weights will be automatically scaled to ensure such property.</para>
    /// </remarks>
    ///
    /// <param name="baseCorrelations">Base correlations to combine</param>
    /// <param name="weights">Weights to combine base correlations</param>
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="interpMethod">Interpolation method between strikes</param>
    /// <param name="extrapMethod">Extrapolation method for strikes</param>
    /// <param name="min">Minimum return value (for Smooth extrap only)</param>
    /// <param name="max">Maximum return value (for Smooth extrap only)</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelation(
      BaseCorrelation[] baseCorrelations,
      double[] weights,
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      double min,
      double max
      )
    {
      // Validation
      if (baseCorrelations == null || baseCorrelations.Length < 1)
        throw new ArgumentException("Must specify Base correlations");
      if (baseCorrelations.Length != weights.Length)
        throw new ArgumentException("Base correlations and strike should be of the same size");

      // check sum of weights
      double sumWeights = 0;
      for (int i = 0; i < weights.Length; ++i)
        sumWeights += weights[i];

      // make the weights add up to 1
      for (int i = 0; i < weights.Length; ++i)
        weights[i] /= sumWeights;

      // make a combined strikes array
      ArrayList list = new ArrayList();
      for (int i = 0; i < baseCorrelations.Length; ++i)
      {
        if (baseCorrelations[i].StrikeMethod != strikeMethod)
          throw new ArgumentException("Only base correlations with same strike methods can be combined.");

        double[] si = baseCorrelations[i].Strikes;
        for (int j = 0; j < si.Length; ++j)
        {
          double strike = si[j];
          int pos = list.BinarySearch(strike);
          if (pos < 0)
            list.Insert(~pos, strike);
        }
      }
      double[] strikes = new double[list.Count];
      for (int i = 0; i < list.Count; ++i)
        strikes[i] = (double)list[i];

      // calculate correlations
      double[] corrs = new double[strikes.Length];
      for (int j = 0; j < strikes.Length; ++j)
      {
        double strike = strikes[j];
        double corr = 0;
        for (int i = 0; i < weights.Length; ++i)
        {
          corr += weights[i] * baseCorrelations[i].GetCorrelation(strike);
        }
        corrs[j] = corr;
      }

      // initialize members
      this.strikes_ = strikes;
      this.correls_ = corrs;
      this.method_ = method;
      this.strikeMethod_ = strikeMethod;
      this.Extended = (max > 1);
      this.interpMethod_ = InterpFactory.FromMethod(interpMethod, extrapMethod, min, max);
      this.tcorrels_ = null;
      this.scalingFactor_ = 1.0;

      return;
    }
    #endregion // Old_Constructors

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Initialize a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This function is called by two constructors.  It initializes a base correlation object
    ///   from a sequence of synthetic CDO tranches
    ///   and a given basket pricer.  This method requires the tranches be continoues 
    ///   without any gap and calculates
    ///   both the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// 
    ///  <para>The basket pricer can be more general than a simple heterogeneous basket pricer with
    ///   Gauss copula.
    ///   For example, it may use a different copula or use correlation term structure.</para>
    /// 
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="basket">Basket pricer</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <param name="entityNames">Names of the underlying credit entities</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    /// <exclude />
    private void Initialize(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      SyntheticCDO[] cdos,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX,
      string[] entityNames
      )
    {
      Timer timer = new Timer(); timer.start();

      // Check tolerance value
      CheckTolerance(ref toleranceF, ref toleranceX, basket);

      // compute the base correlations
      method_ = method;
      correls_ = new double[cdos.Length];
      tcorrels_ = new double[cdos.Length];
      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[cdos.Length];
      for (int i = 0; i < cdos.Length; ++i)
        pricers[i] = new SyntheticCDOPricer(cdos[i], basket, discountCurve, 1.0, null);
      double[,] tmp;
      calibrationFailed_ = true;
      try
      {
        tmp = CorrelationCalc.ImpliedCorrelation(pricers, method, toleranceF, toleranceX);
        calibrationFailed_ = false;
      }
      catch (SolverException e)
      {
        tmp = (double[,])e.Data[ExceptionDataKey];
        errorMsg_ = "There're NaN in correlations. " + e.Message;
      }

      for (int i = 0; i < cdos.Length; ++i)
      {
        correls_[i] = tmp[i, 1];
        tcorrels_[i] = tmp[i, 0];
      }

      // compute the strikes
      interpMethod_ = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const, 0.0, 1.0);
      strikeMethod_ = strikeMethod;

      dps_ = new double[cdos.Length];
      for (int i = 0; i < cdos.Length; ++i)
        dps_[i] = cdos[i].Detachment;
      strikes_ = Strike(basket, strikeMethod, strikeEvaluator_, dps_, correls_, discountCurve);

      this.scalingFactor_ = DetachmentScalingFactor(strikeMethod, basket, discountCurve); // never used
      this.EntityNames = entityNames ?? Utils.GetCreditNames(basket.SurvivalCurves);
      return;
    }

    /// <summary>
    ///   Initialize a base correlation smile from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This function is called by two constructors.  It initializes a base correlation object
    ///   from a sequence of synthetic CDO tranches
    ///   and a given basket pricer.  This method requires the tranches be continous 
    ///   without any gap and calculates
    ///   both the base correlations and strikes base on the heterogeneous basket model.
    ///  </para>
    /// 
    ///  <para>The basket pricer can be more general than a simple heterogeneous basket pricer with
    ///   Gauss copula.
    ///   For example, it may use a different copula or use correlation term structure.</para>
    /// 
    /// </remarks>
    ///
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="basePricers">Base tranche pricers</param>
    /// <param name="tranchePricers">Tranche pricers</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <param name="entityNames">Names of the underlying credit entities</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    /// <exclude />
    public void Initialize(
      BaseCorrelationStrikeMethod strikeMethod,
      SyntheticCDOPricer[] basePricers,
      SyntheticCDOPricer[] tranchePricers,
      double toleranceF,
      double toleranceX,
      string[] entityNames
      )
    {
      Timer timer = new Timer(); timer.start();

      // Check tolerance value
      BasketPricer basket = basePricers[0].Basket;
      CheckTolerance(ref toleranceF, ref toleranceX, basket);

      // compute the base correlations
      method_ = tranchePricers != null ?
          BaseCorrelationMethod.ProtectionMatching
          : BaseCorrelationMethod.ArbitrageFree;
      correls_ = new double[basePricers.Length];
      tcorrels_ = new double[basePricers.Length];
      double[,] tmp;
      calibrationFailed_ = true;
      try
      {
        tmp = CorrelationCalc.ImpliedCorrelation(
          basePricers, tranchePricers, toleranceF, toleranceX);
        calibrationFailed_ = false;
      }
      catch (SolverException e)
      {
        tmp = (double[,])(e.Data[ExceptionDataKey]);
        errorMsg_ = "There're NaN in correlations. " + e.Message;
      }

      for (int i = 0; i < basePricers.Length; ++i)
      {
        correls_[i] = tmp[i, 1];
        tcorrels_[i] = tmp[i, 0];
      }

      // compute the strikes
      interpMethod_ = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const, 0.0, 1.0);
      strikeMethod_ = strikeMethod;

      dps_ = new double[basePricers.Length];
      for (int i = 0; i < basePricers.Length; ++i)
        dps_[i] = basePricers[i].CDO.Detachment;
      DiscountCurve discountCurve = basePricers[0].DiscountCurve;
      strikes_ = Strike(basePricers, strikeMethod, strikeEvaluator_, correls_);

      this.scalingFactor_ = DetachmentScalingFactor(strikeMethod, basket, discountCurve); // never used
      this.EntityNames = entityNames ?? Utils.GetCreditNames(basePricers[0].SurvivalCurves);

      CalibrationTime = timer.getElapsed();
      return;
    }

    /// <summary>
    ///   Interpolate base correlation at a strike point
    /// </summary>
    ///
    /// <remarks>
    ///   This method conceptually defines a smile function <formula inline="true">c = f(x)</formula>,
    ///   where <formula inline="true">c</formula> is base correlation and <formula inline="true">x</formula>
    ///   is strike.
    /// </remarks>
    ///
    /// <param name="strike">strike point to interpolate</param>
    ///
    public double
    GetCorrelation(double strike)
    {
      return GetCorrelation(Strikes, strike);
    }


    /// <summary>
    ///   Interpolate base correlation at detachment point
    /// </summary>
    ///
    /// <remarks>
    ///  <para>This method takes a portfolio and a detachment point and calculates
    ///   the implied correlation for the corresponding first loss tranche, based on the smile function
    ///   represented by this object.</para>
    ///
    ///  <para>The smile is conceptually taken as a function <formula inline="true">f(x)</formula>, 
    ///   where the strike <formula inline="true">x</formula> is a certain tranformation of detachment
    ///   <formula inline="true">d</formula>.  The available strike methods are:</para>
    ///
    ///   <list type="bullet">
    ///     <item><term>Unscaled</term>
    ///       <description><formula inline="true">x = d</formula>.
    ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
    ///       base correlation is computed as <formula inline="true">c^* = f(d^*)</formula>.
    ///     </description></item>
    ///     <item><term>ExpectedLoss</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">s</formula> is the ratio
    ///       of expected basket losses over the portfolio notional.
    ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
    ///       base correlation is computed as <formula inline="true">c^* = f(d^*/s^*)</formula>, where
    ///       <formula inline="true">s^*</formula> is the expected loss ratio of the underlying portfolio.
    ///       The later may differ than the portfolio on which the smile function is calibrated.
    ///     </description></item>
    ///     <item><term>ExpectedLossPv</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">s</formula> is the ratio
    ///       of protection PV on the whole portfolio over the portfolio notional.
    ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
    ///       base correlation is computed as <formula inline="true">c^* = f(d^*/s^*)</formula>, where
    ///       <formula inline="true">s^*</formula> is the protection PV ratio of the underlying portfolio.
    ///       The later may differ than the portfolio on which the smile function is calibrated.
    ///     </description></item>
    ///     <item><term>ExpectedLossRatio</term>
    ///       <description><formula inline="true">x = r</formula>, where <formula inline="true">r</formula> is the ratio
    ///       of the expected loss of the first loss tranche at detachment, <formula inline="true">d</formula>, over
    ///       the expected loss of the whole portfolio.
    ///
    ///       <para>Suppose we are going to calculate the correlation of a first loss tranche with detachment
    ///       <formula inline="true">d^*</formula> on a different portfolio.  For this tranche, the expected loss ratio
    ///       depends on the correlation. The relationship can be denoted <formula inline="true">r = R(c)</formula>.
    ///       This method finds both the base correlation <formula inline="true">c^*</formula> and the corresponding
    ///       loss ratio <formula inline="true">r^*</formula> by solving the following two equations simultaneously
    ///       <formula>
    ///         r^* = R(c^*) \qquad\mathrm{and}\qquad c^* = f(r^*)
    ///       </formula>
    ///       </para>
    ///     </description></item>
    ///     <item><term>Probability</term>
    ///       <description><formula inline="true">x = p</formula>, where <formula inline="true">p</formula> is the
    ///       cumulative probability that the percentage of portfolio loss <formula inline="true">L</formula> does not exceed
    ///       the detachment <formula inline="true">d</formula>, <formula inline="true">p = \mathrm{Prob}[L \leq d]</formula>.
    ///
    ///       <para>Suppose we are going to calculate the correlation of a first loss tranche with detachment
    ///       <formula inline="true">d^*</formula> on a different portfolio.  For this tranche, the cumulative loss probability
    ///       depends on the correlation. The relationship can be denoted <formula inline="true">p = P(c)</formula>.
    ///       This method finds both the base correlation <formula inline="true">c^*</formula> and the corresponding
    ///       probability <formula inline="true">p^*</formula> by solving the following two equations simultaneously
    ///       <formula>
    ///         p^* = P(c^*) \qquad\mathrm{and}\qquad c^* = f(p^*)
    ///       </formula>
    ///       </para>
    ///     </description></item>
    ///   </list>
    ///
    /// </remarks>
    ///
    /// <param name="dp">detachment point</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    public override double
    GetCorrelation(
      double dp,
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX
      )
    {
      // Create a basket pricer
      SyntheticCDO cdo = new SyntheticCDO(
        settle, maturity, Currency.None, 0.0, // premium, not used
        DayCount.None, Frequency.Quarterly, BDConvention.Following, Calendar.None);
      cdo.Attachment = 0.0;
      cdo.Detachment = dp;
      BasketPricer basketPricer = CreateBasketPricer(
        new SyntheticCDO[] { cdo }, asOf, settle, 
        survivalCurves, recoveryCurves, principals,
        stepSize, stepUnit, copula, gridSize,
        integrationPointsFirst, integrationPointsSecond);

      double corr = CalcCorrelation(cdo, basketPricer, discountCurve,
        toleranceF, toleranceX, MinCorrelation, MaxCorrelation);

      return corr;
    }


    /// <summary>
    ///   Interpolate base correlation at detachment point
    /// </summary>
    /// 
    /// <remarks>
    ///  <para>This method takes a portfolio and a detachment point and calculates
    ///   the implied correlation for the corresponding first loss tranche, based on the smile function
    ///   represented by this object.</para>
    ///
    ///  <para>The smile is conceptually taken as a function <formula inline="true">f(x)</formula>, 
    ///   where the strike <formula inline="true">x</formula> is a certain tranformation of detachment
    ///   <formula inline="true">d</formula>.  The available strike methods are:</para>
    ///
    ///   <list type="bullet">
    ///     <item><term>Unscaled</term>
    ///       <description><formula inline="true">x = d</formula>.
    ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
    ///       base correlation is computed as <formula inline="true">c^* = f(d^*)</formula>.
    ///     </description></item>
    ///     <item><term>ExpectedLoss</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">s</formula> is the ratio
    ///       of expected basket losses over the portfolio notional.
    ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
    ///       base correlation is computed as <formula inline="true">c^* = f(d^*/s^*)</formula>, where
    ///       <formula inline="true">s^*</formula> is the expected loss ratio of the underlying portfolio.
    ///       The later may differ than the portfolio on which the smile function is calibrated.
    ///     </description></item>
    ///     <item><term>ExpectedLossPv</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">s</formula> is the ratio
    ///       of protection PV on the whole portfolio over the portfolio notional.
    ///       For any first loss tranche with detachment <formula inline="true">d^*</formula>, the corresponding
    ///       base correlation is computed as <formula inline="true">c^* = f(d^*/s^*)</formula>, where
    ///       <formula inline="true">s^*</formula> is the protection PV ratio of the underlying portfolio.
    ///       The later may differ than the portfolio on which the smile function is calibrated.
    ///     </description></item>
    ///     <item><term>ExpectedLossRatio</term>
    ///       <description><formula inline="true">x = r</formula>, where <formula inline="true">r</formula> is the ratio
    ///       of the expected loss of the first loss tranche at detachment, <formula inline="true">d</formula>, over
    ///       the expected loss of the whole portfolio.
    ///
    ///       <para>Suppose we are going to calculate the correlation of a first loss tranche with detachment
    ///       <formula inline="true">d^*</formula> on a different portfolio.  For this tranche, the expected loss ratio
    ///       depends on the correlation. The relationship can be denoted <formula inline="true">r = R(c)</formula>.
    ///       This method finds both the base correlation <formula inline="true">c^*</formula> and the corresponding
    ///       loss ratio <formula inline="true">r^*</formula> by solving the following two equations simultaneously
    ///       <formula>
    ///         r^* = R(c^*) \qquad\mathrm{and}\qquad c^* = f(r^*)
    ///       </formula>
    ///       </para>
    ///     </description></item>
    ///     <item><term>Probability</term>
    ///       <description><formula inline="true">x = p</formula>, where <formula inline="true">p</formula> is the
    ///       cumulative probability that the percentage of portfolio loss <formula inline="true">L</formula> does not exceed
    ///       the detachment <formula inline="true">d</formula>, <formula inline="true">p = \mathrm{Prob}[L \leq d]</formula>.
    ///
    ///       <para>Suppose we are going to calculate the correlation of a first loss tranche with detachment
    ///       <formula inline="true">d^*</formula> on a different portfolio.  For this tranche, the cumulative loss probability
    ///       depends on the correlation. The relationship can be denoted <formula inline="true">p = P(c)</formula>.
    ///       This method finds both the base correlation <formula inline="true">c^*</formula> and the corresponding
    ///       probability <formula inline="true">p^*</formula> by solving the following two equations simultaneously
    ///       <formula>
    ///         p^* = P(c^*) \qquad\mathrm{and}\qquad c^* = f(p^*)
    ///       </formula>
    ///       </para>
    ///     </description></item>
    ///   </list>
    ///
    /// </remarks>
    ///
    /// <param name="cdo">Base tranche</param>
    /// <param name="basketPricer">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <returns>Detachment Correlation</returns>
    /// 
    /// <exception cref="ArgumentException">
    ///   Basket maturity and cdo maturity not match.
    /// </exception>
    public override double
    GetCorrelation(
      SyntheticCDO cdo,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX)
    {
      if (basketPricer.Maturity != cdo.Maturity)
        throw new System.ArgumentException(String.Format(
          "Basket maturity {0} and CDO maturity {1} not match",
          basketPricer.Maturity, cdo.Maturity));
      
      double result = this.CalcCorrelation(
        cdo, basketPricer, discountCurve,
        toleranceF, toleranceX, MinCorrelation, MaxCorrelation);

      return result;
    }

    /// <summary>
    ///   Interpolate base correlation at a detachment point for an array of dates.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>When the parameter <paramref name="names"/> is null, the name list is
    ///    taken from the names of the survival curves inside the <paramref name="basket"/>.
    ///   </para>
    /// 
    ///   <para>If the parameter <paramref name="dates"/> is not empty, this function
    ///    constructs a correlation term structure with the correlations interpolated on
    ///    the input dates.  Otherwise, a single factor correlation interpolated on the
    ///    <paramref name="cdo"/> maturity is returned.</para>
    /// 
    ///   <para>This function modifies directly the states of <paramref name="cdo"/> and
    ///    <paramref name="basket"/>, including maturity, correlation object and loss levels.
    ///    If it is desired to preserve the states of cdo and basket, the caller can pass
    ///    cloned copies of them and leave the original ones intact.</para>
    /// </remarks>
    /// 
    /// <param name="cdo">
    ///   Base tranche, modified on output.
    /// </param>
    /// <param name="names">
    ///   Array of underlying names, or null, which means to use the
    ///   credit names in the <paramref name="basket"/>.
    /// </param>
    /// <param name="dates">
    ///   Array of dates to interpolate, or null, which means to use
    ///   the <paramref name="cdo"/> maturity date.
    /// </param>
    /// <param name="basket">
    ///   Basket to interpolate correlation, modified on output.
    /// </param>
    /// <param name="discountCurve">
    ///   Discount curve
    /// </param>
    /// <param name="toleranceF">
    ///   Relative error allowed in PV when calculating implied correlations.
    ///   A value of 0 means to use the default accuracy level.
    /// </param>
    /// <param name="toleranceX">
    ///   Accuracy level of implied correlations.
    ///   A value of 0 means to use the default accuracy level.
    /// </param>
    /// 
    /// <returns>
    ///   A <see cref="CorrelationTermStruct"/> object containing
    ///   the interpolated correlations if <paramref name="dates"/> is not empty;
    ///   otherwise, a <see cref="SingleFactorCorrelation"/> object.
    /// </returns>
    /// 
    /// <exception cref="NullReferenceException">
    ///   Either <paramref name="cdo"/>, <paramref name="basket"/> or
    ///   <paramref name="discountCurve"/> are null.
    /// </exception>
    public override CorrelationObject GetCorrelations(
      SyntheticCDO cdo,
      string[] names,
      Dt[] dates,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX
      )
    {
      if (names == null)
        names = basket.EntityNames;
      if (dates == null || dates.Length == 0)
      {
        basket.Maturity = cdo.Maturity;
        basket.Reset();
        return new SingleFactorCorrelation(names,
          Math.Sqrt(GetCorrelation(cdo, basket,
          discountCurve, toleranceF, toleranceX)));
      }

      CorrelationTermStruct cot = new CorrelationTermStruct(
        names, new double[dates.Length], dates,
        this.MinCorrelation, this.MaxCorrelation);
      basket.Correlation = cot;
      basket.RawLossLevels =
        new UniqueSequence<double>(0.0, cdo.Detachment);
      for (int i = 0; i < dates.Length; ++i)
      {
        cdo.Maturity = basket.Maturity = dates[i];
        basket.Reset();
        double corr = GetCorrelation(cdo, basket,
          discountCurve, toleranceF, toleranceX);
        cot.SetFactorAtDate(i, Math.Sqrt(corr));
      }
      return cot;
    }

    /// <summary>
    ///   Imply tranche correlation from base correlation smile
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method first calculates two base correlations at the attachment and the detachment points,
    ///   based on the methods described by <c>BaseCorrelationStrikeMethod</c>.
    ///   Then it computes the implied tranche correlation by inversing the algorithm denoted by <c>BaseCorrelationMethod</c>.
    ///   </para>
    ///
    ///   <list type="bullet">
    ///     <item><term>ProtectionMatching</term>
    ///       <description>Suppose the detachment points are
    ///       <formula inline="true">d_1</formula> and <formula inline="true">d_2</formula>.  It claculates the protection PVs, 
    ///       <formula inline="true">\mathrm{Prot}[0,d_1]</formula> and <formula inline="true">\mathrm{Prot}[0,d_2]</formula>,
    ///       based on corresponding base correlations.
    ///
    ///       <para>Then the protection PV of the tranche <formula inline="true">[d_1, d_2]</formula> is
    ///       <formula>
    ///         \mathrm{Prot}[d_1,d_2] = \mathrm{Prot}[0, d_2] - \mathrm{Prot}[0,d_1]
    ///       </formula>
    ///       The tranche correlation is calculated
    ///      as the implied correlation matching the protection value.</para>
    ///     </description></item>
    ///     <item><term>ArbitageFree</term>
    ///       <description>This method first calculates the protection PV, <formula inline="true">\mathrm{Prot}[d_1,d_2]</formula>,
    ///       as in the case of <c>ProtectionMatching</c>.  It then calculates Dv01
    ///       <formula>
    ///         \mathrm{Dv01}[d_1,d_2] = \mathrm{Dv01}[0, d_2] - \mathrm{Dv01}[0,d_1]
    ///       </formula>
    ///       and the fair spread
    ///       <formula>
    ///         \mathrm{Fair Spread} = \frac{\mathrm{Prot}[d_1,d_2]}{\mathrm{Dv01}[d_1,d_2]}
    ///       </formula>
    ///       The tranche correlation is the one which matches the spread.
    ///     </description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="cdo">CDO tranche</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="apBump">Bump for base correlation at attachment point</param>
    /// <param name="dpBump">Bump for base correlation at detachment point</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    public override double
    TrancheCorrelation(
            SyntheticCDO cdo,
            Dt asOf,
            Dt settle,
            SurvivalCurve[] survivalCurves,
            RecoveryCurve[] recoveryCurves,
            DiscountCurve discountCurve,
            double[] principals,
            int stepSize,
            TimeUnit stepUnit,
            double apBump,
            double dpBump,
            Copula copula,
            double gridSize,
            int integrationPointsFirst,
            int integrationPointsSecond,
            double toleranceF,
            double toleranceX
            )
    {
      // Create a basket pricer
      BasketPricer basketPricer = CreateBasketPricer(new SyntheticCDO[] { cdo }, asOf, settle,
                                                      survivalCurves, recoveryCurves, principals,
                                                      stepSize, stepUnit, copula, gridSize,
                                                      integrationPointsFirst, integrationPointsSecond);

      CheckTolerance(ref toleranceF, ref toleranceX, basketPricer);

      double corr1 = CalcCorrelation(cdo, basketPricer, discountCurve, toleranceF, toleranceX, MinCorrelation, MaxCorrelation);

      if (cdo.Attachment <= 1.0E-7)
        return corr1;

      double ap = cdo.Attachment;
      double dp = cdo.Detachment;
      cdo.Detachment = ap;
      cdo.Attachment = 0;
      double corr0 = corr1;
      try
      {
        corr0 = CalcCorrelation(cdo, basketPricer, discountCurve, toleranceF, toleranceX, MinCorrelation, MaxCorrelation);
      }
      finally
      {
        cdo.Attachment = ap;
        cdo.Detachment = dp;
      }
      corr0 += apBump;
      corr1 += dpBump;
      if (Math.Abs(corr1 - corr0) < 1.0E-7)
        return (corr0 + corr1) / 2;

      SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basketPricer, discountCurve, 1.0, null);
      double corr = CorrelationCalc.TrancheCorrelation(pricer, method_, corr0, corr1,
                                                        toleranceF, toleranceX);

      return corr;
    }


    /// <summary>
    ///   Calculate the strike level for an array of detachment points of a basket.
    /// </summary>
    ///
    /// <remarks>
    ///  <para>The strike <formula inline="true">x</formula> are calculated according to the following
    ///  <c>BaseCorrelationStrikeMethod</c>.</para>
    ///
    ///   <list type="bullet">
    ///     <item><term>Unscaled</term>
    ///       <description><formula inline="true">x = d</formula>, where <formula inline="true">d</formula>
    ///       is detachment point.
    ///     </description></item>
    ///     <item><term>ExpectedLoss</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">d</formula>
    ///       is detachment and <formula inline="true">s</formula> is the ratio
    ///       of expected basket losses over the portfolio notional.
    ///     </description></item>
    ///     <item><term>ExpectedLossPv</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">d</formula>
    ///       is detachment and <formula inline="true">s</formula> is the ratio
    ///       of protection PV on the whole portfolio over the portfolio notional.
    ///     </description></item>
    ///     <item><term>ExpectedLossRatio</term>
    ///       <description><formula inline="true">x = r</formula>, where <formula inline="true">r</formula> is the ratio
    ///       of the expected loss of the first loss tranche over
    ///       the expected loss of the whole portfolio.
    ///     </description></item>
    ///     <item><term>Probability</term>
    ///       <description><formula inline="true">x = p</formula>, where <formula inline="true">p</formula> is the
    ///       cumulative probability that the percentage of portfolio loss <formula inline="true">L</formula> does not exceed
    ///       the detachment <formula inline="true">d</formula>, <formula inline="true">p = \mathrm{Prob}[L \leq d]</formula>.
    ///     </description></item>
    ///   </list>
    ///
    /// </remarks>
    ///
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date of basket</param>
    /// <param name="dp">Detachment points to compute strikes for</param>
    /// <param name="corr">Base correlations at the detachment points</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    ///
    /// <returns>A vector of strike levels matching the CDO tranches</returns>
    ///
    public static double[] Strike(
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      Dt asOf,
      Dt settle,
      Dt maturity,
      double[] dp,
      double[] corr,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond
      )
    {
      // Validation
      if (dp == null || dp.Length < 1)
        throw new ArgumentException("Must specify detachment points");

      // Set up basket pricer for calculations
      double[,] lossLevels = new double[dp.Length, 2];
      for (int i = 0; i < dp.Length; i++)
      {
        lossLevels[i, 0] = (i > 0) ? dp[i - 1] : 0.0;
        lossLevels[i, 1] = dp[i];
      }
      BasketPricer basketPricer = CreateBasketPricer(
                asOf, settle, maturity, lossLevels,
                survivalCurves, recoveryCurves, principals,
                stepSize, stepUnit, copula, gridSize,
                integrationPointsFirst, integrationPointsSecond);

      // compute the strikes
      double[] strikes = Strike(basketPricer, strikeMethod, strikeEvaluator, dp, corr, discountCurve);

      return strikes;
    }

    /// <summary>
    ///   Calculate strikes at specific detachment points
    /// </summary>
    /// <param name="basket">Basket pricer</param>
    /// <param name="strikeMethod">Strike method</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="dps">Array of detachment points to calculate strikes at</param>
    /// <param name="correlations">Array of base correlations</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <returns>Array of strikes</returns>
    /// <exclude />
    public static double[]
    Strike(
      BasketPricer basket,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      double[] dps,
      double[] correlations,
      DiscountCurve discountCurve
      )
    {
      if (dps == null || dps.Length == 0)
        return null;

      if (null == correlations || 0 == correlations.Length)
        correlations = null;

      if (dps != null && correlations != null && dps.Length != correlations.Length)
        throw new ArgumentException(String.Format("Detachments (Length={0} and base correlations (Length={1} not match",
          dps.Length, correlations.Length));

      double[] strikes;
      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
        {
            // use expected loss
            double scalingFactor = DetachmentScalingFactor(strikeMethod, basket, discountCurve);
            strikes = new double[dps.Length];
            for (int i = 0; i < dps.Length; ++i)
              strikes[i] = dps[i] * scalingFactor;
          }
          break;
        case BaseCorrelationStrikeMethod.UnscaledForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPVForward:
        case BaseCorrelationStrikeMethod.ExpectedLossForward:
          double sf = DetachmentScalingFactor(strikeMethod, basket, discountCurve);
          strikes = new double[dps.Length];
          double el, a, d;
          double mult = GetMultiplier(strikeMethod, basket);
          for (int i = 0; i < dps.Length; ++i)
          {
            el = 0;
            a = 0;
            d = dps[i];
            basket.AdjustTrancheLevels(false, ref a, ref d, ref el);
            strikes[i] = d * mult * sf;
          }
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward: 
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
          {
            strikes = new double[dps.Length];
            for (int i = 0; i < dps.Length; ++i)
            {
              ProtectionFn fn = new ProtectionFn(null, false, null, null, strikeMethod, dps[i], basket);
              strikes[i] = correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i]));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
          {
            if (null == discountCurve)
              throw new ArgumentException("Must specify discount curve if using the ExpectedLossPvRatio strike method");
            SyntheticCDO cdo = new SyntheticCDO(
              basket.Settle, basket.Maturity, Currency.None, 0.0, DayCount.Actual360,
              Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
            cdo.Attachment = 0.0;
            strikes = new double[dps.Length];
            for (int i = 0; i < dps.Length; ++i)
            {
              cdo.Detachment = dps[i];
              ProtectionPvFn fn = new ProtectionPvFn(null, false, null, null, strikeMethod, cdo, basket, discountCurve);
              strikes[i] = correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i]));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.Probability:
          {
            strikes = new double[dps.Length];
            for (int i = 0; i < dps.Length; ++i)
            {
              ProbabilityFn fn = new ProbabilityFn(null, false, null, null, dps[i], basket);
              strikes[i] = correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i]));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
          {
            if (null == discountCurve)
              throw new ArgumentException("Must specify discount curve if using the ExpectedLossPvRatio strike method");
            SyntheticCDO cdo = new SyntheticCDO(
              basket.Settle, basket.Maturity, Currency.None, 0.0, DayCount.Actual360,
              Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
            cdo.Attachment = 0.0;
            strikes = new double[dps.Length];
            for (int i = 0; i < dps.Length; ++i)
            {
              cdo.Detachment = dps[i];
              SpreadFn fn = new SpreadFn(null, false, null, null, cdo, basket, discountCurve,
                        BaseCorrelationStrikeMethod.SeniorSpread == strikeMethod);
              strikes[i] = 1 - (correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i])));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.UserDefined:
          {
            if (strikeEvaluator == null)
              throw new System.NullReferenceException("strikeEvaluator cannot be null with UserDefined mapping method");
            double totalPrincipal = basket.TotalPrincipal; 
            SyntheticCDO cdo = new SyntheticCDO(
              basket.Settle, basket.Maturity, Currency.None, 0.0, DayCount.Actual360,
              Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
            cdo.Attachment = 0.0;
            strikes = new double[dps.Length];
            for (int i = 0; i < dps.Length; ++i)
            {
              cdo.Detachment = dps[i];
              SyntheticCDOPricer pricer =
                new SyntheticCDOPricer(cdo, basket, discountCurve, totalPrincipal * dps[i], null);
              strikeEvaluator.SetPricer(pricer);
              UserFn fn = new UserFn(null, false, null, null, strikeEvaluator);
              strikes[i] = (correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i])));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.Unscaled:
        default:
          strikes = dps;
          break;
      }

      return strikes;
    }

    // Private method to create a pricer for calibration of the base correlations.
    //
    private static BasketPricer
    CreateBasketPricer(
            SyntheticCDO[] cdos,
            Dt asOf,
            Dt settle,
            SurvivalCurve[] survivalCurves,
            RecoveryCurve[] recoveryCurves,
            double[] principals,
            int stepSize,
            TimeUnit stepUnit,
            Copula copula,
            double gridSize,
            int integrationPointsFirst,
            int integrationPointsSecond
            )
    {
      // Set up basket pricer for calculations
      double[,] lossLevels = new double[cdos.Length, 2];
      for (int i = 0; i < cdos.Length; i++)
      {
        lossLevels[i, 0] = cdos[i].Attachment;
        lossLevels[i, 1] = cdos[i].Detachment;
      }
      return CreateBasketPricer(
                asOf, settle, cdos[0].Maturity, lossLevels, survivalCurves,
                recoveryCurves, principals, stepSize, stepUnit, copula, gridSize,
                integrationPointsFirst, integrationPointsSecond);
    }

    // Private method to create a pricer for calibration of the base correlations.
    // All basket pricers should be created through this method.
    //
    private static BasketPricer
    CreateBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      double[,] lossLevels,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond
      )
    {
      // Set up basket pricer for calculations
      string[] names = Utils.GetCreditNames(survivalCurves);

      FactorCorrelation correlation = new SingleFactorCorrelation(names, 0.3);
      correlation = CorrelationFactory.CreateFactorCorrelation(correlation);

      HeterogeneousBasketPricer basketPricer =
        new HeterogeneousBasketPricer(asOf, settle, maturity, survivalCurves, recoveryCurves,
                    principals, copula, correlation, stepSize, stepUnit, lossLevels);
      if (gridSize > 0)
        basketPricer.GridSize = gridSize;
      if (integrationPointsFirst > 0)
        basketPricer.IntegrationPointsFirst = integrationPointsFirst;
      else
        basketPricer.IntegrationPointsFirst =
          BasketPricerFactory.DefaultQuadraturePoints(copula, survivalCurves.Length);
      if (integrationPointsSecond > 0)
        basketPricer.IntegrationPointsSecond = integrationPointsSecond;

      return basketPricer;
    }

    /// <summary>
    ///   Compute the scaling factor for detachment points.
    /// </summary>
    ///
    /// <remarks>
    ///   For base correlations, detachment points are normalized based on
    ///   one of several methods. This function calculates the factor
    ///   for normalizing strikes based on the characteristics of the
    ///   underlying asset pool.
    /// </remarks>
    ///
    /// <param name="strikeMethod">Scaling method to use</param>
    /// <param name="basket">Underlying basket</param>
    /// <param name="dc">Discount curve</param>
    ///
    /// <returns>Detachment point scaling factor</returns>
    /// <exclude />
    public static double DetachmentScalingFactor(
      BaseCorrelationStrikeMethod strikeMethod,
      BasketPricer basket,
      DiscountCurve dc
      )
    {
      double factor;
     switch (strikeMethod)
      {
       case BaseCorrelationStrikeMethod.ExpectedLoss:
       case BaseCorrelationStrikeMethod.ExpectedLossForward:
          // use expected loss
          factor = 1.0 / basket.BasketLoss(basket.Settle, basket.Maturity);
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPVForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
          if (null == dc)
            throw new ArgumentException("Must specify discount curve if using the ExpectedLossPv strike method");
            factor = 1.0 / basket.BasketLossPv(basket.Settle, basket.Maturity, dc);
            break;
        case BaseCorrelationStrikeMethod.Unscaled:
        case BaseCorrelationStrikeMethod.UnscaledForward:
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
        case BaseCorrelationStrikeMethod.Probability:
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
        case BaseCorrelationStrikeMethod.UserDefined:
        factor = 1.0;
        break;
        default:
          throw new ArgumentException("Unsupported strike calculation method");
      }

      return factor;
    }

    /// <summary>
    ///   Calculate strikes for specific detachments
    /// </summary>
    /// <param name="pricers">CDO pricers</param>
    /// <param name="strikeMethod">Strike method</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="correlations">Array of base correlations</param>
    /// <returns>Array of strikes</returns>
    /// <exclude />
    public static double[] Strike(
      SyntheticCDOPricer[] pricers,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      double[] correlations
      )
    {
      if (pricers == null || pricers.Length == 0)
        return null;

      if (null == correlations || 0 == correlations.Length)
        correlations = null;

      if (correlations != null && correlations != null && correlations.Length != pricers.Length)
        throw new ArgumentException(String.Format("Pricers (Length={0} and base correlations (Length={1} not match",
          pricers.Length, correlations.Length));

      double[] strikes = null;
      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
          {
            // use expected loss
            double scalingFactor = DetachmentScalingFactor(strikeMethod, pricers[0].Basket, pricers[0].DiscountCurve);
            strikes = new double[pricers.Length];
            for (int i = 0; i < pricers.Length; ++i)
              strikes[i] = pricers[i].CDO.Detachment * scalingFactor;
          }
          break;
        case BaseCorrelationStrikeMethod.UnscaledForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPVForward:
        case BaseCorrelationStrikeMethod.ExpectedLossForward:
          {
            double scalingFactor = DetachmentScalingFactor(strikeMethod, pricers[0].Basket, pricers[0].DiscountCurve);
            double mult = GetMultiplier(strikeMethod, pricers[0].Basket);
            strikes = new double[pricers.Length];
            double el, a, d;
            for (int i = 0; i < pricers.Length; ++i)
            {
              el = 0;
              a = 0;
              d = pricers[i].CDO.Detachment;
              pricers[i].Basket.AdjustTrancheLevels(false, ref a, ref d, ref el);
              strikes[i] = d*mult*scalingFactor;
            }
          }
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
          {
            strikes = new double[pricers.Length];
            for (int i = 0; i < pricers.Length; ++i)
            {
              ProtectionFn fn = new ProtectionFn(null, false, null, null, strikeMethod, pricers[i].CDO.Detachment, pricers[i].Basket);
              if (pricers[i].CDO.Detachment <= 1e-8 && strikeMethod != BaseCorrelationStrikeMethod.EquityProtection)
                strikes[i] = 0;
              else
                strikes[i] = correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i]));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
          {
            strikes = new double[pricers.Length];
            for (int i = 0; i < pricers.Length; ++i)
            {
              ProtectionPvFn fn = new ProtectionPvFn(null, false, null, null, strikeMethod, pricers[i].CDO, pricers[i].Basket, pricers[i].DiscountCurve);
              if (pricers[i].CDO.Detachment <= 1e-8 && strikeMethod != BaseCorrelationStrikeMethod.EquityProtectionPv)
                strikes[i] = 0;
              else
                strikes[i] = correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i]));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.Probability:
          {
            strikes = new double[pricers.Length];
            for (int i = 0; i < pricers.Length; ++i)
            {
              ProbabilityFn fn = new ProbabilityFn(null, false, null, null, pricers[i].CDO.Detachment, pricers[i].Basket);
              if (pricers[i].CDO.Detachment <= 1e-8)
                strikes[i] = 0;
              else
                strikes[i] = correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i]));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
          {
            strikes = new double[pricers.Length];
            for (int i = 0; i < pricers.Length; ++i)
            {
              SpreadFn fn = new SpreadFn(null, false, null, null, pricers[i].CDO, pricers[i].Basket, pricers[i].DiscountCurve,
                        BaseCorrelationStrikeMethod.SeniorSpread == strikeMethod);
              strikes[i] = 1 - (correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i])));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.UserDefined:
          {
            if (strikeEvaluator == null)
              throw new System.NullReferenceException("strikeEvaluator cannot be null with UserDefined mapping method");
            double totalPrincipal = pricers[0].TotalPrincipal;
            strikes = new double[pricers.Length];
            for (int i = 0; i < pricers.Length; ++i)
            {
              SyntheticCDO cdo = (SyntheticCDO)pricers[i].CDO.Clone();
              cdo.Attachment = 0.0;
              SyntheticCDOPricer pricer = new SyntheticCDOPricer(
                cdo, pricers[i].Basket, pricers[i].DiscountCurve, totalPrincipal * cdo.Detachment, pricers[i].RateResets);
              strikeEvaluator.SetPricer(pricer);
              UserFn fn = new UserFn(null, false, null, null, strikeEvaluator);
              strikes[i] = (correlations == null ? fn.strike() : fn.strike(Math.Sqrt(correlations[i])));
            }
          }
          break;
        case BaseCorrelationStrikeMethod.Unscaled:
        default:
          {
            strikes = new double[pricers.Length];
            for (int i = 0; i < strikes.Length; ++i)
              strikes[i] = pricers[i].CDO.Detachment;
          }
          break;
      }

      return strikes;
    }

    /// <summary>
    ///   Calculate strikes at a specific correlation
    /// </summary>
    /// <param name="pricer">CDO pricer</param>
    /// <param name="strikeMethod">Strike method</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="correlation">Base correlations</param>
    /// <returns>The strike correponding to a correlation</returns>
    /// <exclude />
    public static double Strike(
      SyntheticCDOPricer pricer,
      BaseCorrelationStrikeMethod strikeMethod,
      IStrikeEvaluator strikeEvaluator,
      double correlation
      )
    {
      if (pricer == null)
        return 0;

      if (Double.IsNaN(correlation))
        return Double.NaN;

      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
          {
            // use expected loss
            double scalingFactor = DetachmentScalingFactor(strikeMethod, pricer.Basket, pricer.DiscountCurve);
            return pricer.CDO.Detachment * scalingFactor;
          }
        case BaseCorrelationStrikeMethod.UnscaledForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPVForward:
        case BaseCorrelationStrikeMethod.ExpectedLossForward:
          {
            double el = 0;
            double a = 0;
            double d = pricer.CDO.Detachment;
            pricer.Basket.AdjustTrancheLevels(false, ref a, ref d, ref el);
            double scalingFactor = DetachmentScalingFactor(strikeMethod, pricer.Basket, pricer.DiscountCurve);
            double mult = GetMultiplier(strikeMethod, pricer.Basket);
            return d*scalingFactor*mult;
          }
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
          {
            if (pricer.CDO.Detachment <= 1e-8 && (strikeMethod != BaseCorrelationStrikeMethod.EquityProtection && strikeMethod != BaseCorrelationStrikeMethod.EquityProtectionForward))
              return 0;
            ProtectionFn fn = new ProtectionFn(null, false, null, null, strikeMethod, pricer.CDO.Detachment, pricer.Basket);
            return fn.strike(Math.Sqrt(correlation));
          }
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
          {
            if (pricer.CDO.Detachment <= 1e-8 && strikeMethod != BaseCorrelationStrikeMethod.EquityProtectionPv)
              return 0;
            ProtectionPvFn fn = new ProtectionPvFn(null, false, null, null, strikeMethod, pricer.CDO, pricer.Basket, pricer.DiscountCurve);
            return fn.strike(Math.Sqrt(correlation));
          }
        case BaseCorrelationStrikeMethod.Probability:
          {
            if (pricer.CDO.Detachment <= 1e-8)
              return 0;
            ProbabilityFn fn = new ProbabilityFn(null, false, null, null, pricer.CDO.Detachment, pricer.Basket);
            return fn.strike(Math.Sqrt(correlation));
          }
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
          {
            bool senior = BaseCorrelationStrikeMethod.SeniorSpread == strikeMethod;
            SpreadFn fn = new SpreadFn(null, false, null, null, pricer.CDO, pricer.Basket, pricer.DiscountCurve,
              senior);
            return 1 - fn.strike(Math.Sqrt(correlation));
          }
        case BaseCorrelationStrikeMethod.UserDefined:
          {
            if (strikeEvaluator == null)
              throw new System.NullReferenceException("strikeEvaluator cannot be null with UserDefined mapping method");
            if (pricer.CDO.Attachment != 0)
              throw new System.ArgumentException("CDO attachment must be zero to calculate strike");
            strikeEvaluator.SetPricer(pricer);
            UserFn fn = new UserFn(null, false, null, null, strikeEvaluator);
            return fn.strike(Math.Sqrt(correlation));
          }
        case BaseCorrelationStrikeMethod.Unscaled:
        default:
          {
            return pricer.CDO.Detachment;
          }
      }
    }
    
    
    /// <summary>
    ///   Interpolate base correlation at detachment point
    /// </summary>
    ///
    /// <param name="cdo">Tranche</param>
    /// <param name="basketPricer">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <param name="min">Minimum</param>
    /// <param name="max">Maximum</param>
    /// <returns>Detachment correlation</returns>
    internal double
    CalcCorrelation(
      SyntheticCDO cdo,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX,
      double min, double max)
    {
      CheckTolerance(ref toleranceF, ref toleranceX, basketPricer);

      // Compute the strikes
      if (strikes_.Length == 1)
      {
        return correls_[0];
      }

      switch (strikeMethod_)
      {
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
          {
            StrikeFn fn = new SpreadFn(
              interpMethod_, interpOnFactors_, strikes_, correls_, cdo, basketPricer, discountCurve,
              strikeMethod_ == BaseCorrelationStrikeMethod.SeniorSpread
              );
            if (cdo.Detachment > 0.9999999999)
              return this.GetCorrelation(fn.strike(0.0));
            double corr = fn.solve(toleranceF, toleranceX, max);
            return corr;
          }
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
          {
            StrikeFn fn = new ProtectionFn(
              interpMethod_, interpOnFactors_, strikes_, correls_, strikeMethod_, cdo.Detachment, basketPricer);
            if (cdo.Detachment > 0.9999999999)
              return this.GetCorrelation(fn.strike(0.0));
            double corr = fn.solve(toleranceF, toleranceX, max);
            return corr;
          }
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
          {
            StrikeFn fn = new ProtectionPvFn(
              interpMethod_, interpOnFactors_, strikes_, correls_, strikeMethod_, cdo, basketPricer, discountCurve);
            if (cdo.Detachment > 0.9999999999)
              return this.GetCorrelation(fn.strike(0.0));
            double corr = fn.solve(toleranceF, toleranceX, max);
            return corr;
          }
        case BaseCorrelationStrikeMethod.Probability:
          {
            StrikeFn fn = new ProbabilityFn(
              interpMethod_, interpOnFactors_, strikes_, correls_, cdo.Detachment, basketPricer);
            if (cdo.Detachment > 0.9999999999)
              return this.GetCorrelation(fn.strike(0.0));
            double corr = fn.solve(toleranceF, toleranceX, max);
            return corr;
          }
        case BaseCorrelationStrikeMethod.Unscaled:
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
          {
            double strikeFactor = DetachmentScalingFactor(strikeMethod_, basketPricer, discountCurve);
            double s = cdo.Detachment * strikeFactor;
            CorrelationEvaluator ce = new CorrelationEvaluator(interpMethod_, interpOnFactors_, strikes_, correls_);
            double corr = ce.evaluate(s);
            return corr;
          }
        case BaseCorrelationStrikeMethod.UnscaledForward:
        case BaseCorrelationStrikeMethod.ExpectedLossForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPVForward:
          {
            double strikeFactor = DetachmentScalingFactor(strikeMethod_, basketPricer, discountCurve);
            double el = 0;
            double a = 0;
            double d = cdo.Detachment;
            basketPricer.AdjustTrancheLevels(false, ref a, ref d, ref el);
            double mult = GetMultiplier(strikeMethod_, basketPricer);
            double s = mult * d * strikeFactor;
            CorrelationEvaluator ce = new CorrelationEvaluator(interpMethod_, interpOnFactors_, strikes_, correls_);
            double corr = ce.evaluate(s);
            return corr;
          }
        case BaseCorrelationStrikeMethod.UserDefined:
          {
            if (strikeEvaluator_ == null)
              throw new System.NullReferenceException("strikeEvaluator cannot be null with UserDefined mapping method");
            double totalPrincipal = basketPricer.TotalPrincipal;
            cdo = (SyntheticCDO)cdo.Clone();
            cdo.Attachment = 0.0;
            SyntheticCDOPricer pricer = new SyntheticCDOPricer(
              cdo, basketPricer, discountCurve, totalPrincipal * cdo.Detachment, null);
            strikeEvaluator_.SetPricer(pricer);
            UserFn fn = new UserFn(null, false, null, null, strikeEvaluator_);
            if (cdo.Detachment > 0.9999999999)
              return this.GetCorrelation(fn.strike(0.0));
            double corr = fn.solve(toleranceF, toleranceX, max);
            return corr;
          }
        default:
          throw new System.NotSupportedException("Unknown base correlation strike method" + strikeMethod_);
      }
    }

    private static double GetMultiplier(BaseCorrelationStrikeMethod strikeMethod, 
      BasketPricer basketPricer)
    {
      return strikeMethod == BaseCorrelationStrikeMethod.UnscaledForward
        ? 1.0
        : (basketPricer.TotalPrincipal - basketPricer.DefaultedPrincipal)
        /basketPricer.TotalPrincipal;
    }


    // helper function
    //
    private
    double GetCorrelation(double[] strikes, double strike)
    {
      if (Strikes.Length == 1)
        return correls_[0];

      // We are no longer sure that the strikes are in ascending order
      // So we need a helper class to do interpolation.
      CorrelationEvaluator ce = new CorrelationEvaluator(interpMethod_, interpOnFactors_, strikes, correls_);
      double corr = ce.evaluate(strike);

      //if ((Extended && ((corr < -2.0000000001 || corr > 2.0000000001)))
      //  || (!Extended && (corr < -1.0000000001 || corr > 1.0000000001)))
      if (corr < -2.0000000001 || corr > 2.0000000001)
      {
        throw new SystemException(String.Format(
          "Invalid base correlation value {0} at strike {1}", corr, strike));
      }

      return corr;
    }

    // helper function
    //
    internal static void CheckTolerance(
      ref double toleranceF,
      ref double toleranceX,
      BasketPricer basketPricer)
    {
      // check tolerance
      if (toleranceF <= 0)
      {
        toleranceF = 1.0 / Math.Abs(basketPricer.TotalPrincipal);
        if (toleranceF > 0.000001 && !use8dot5Solver_)
          toleranceF = 0.000001;
      }
      if (toleranceX <= 0)
      {
        toleranceX = 100 * toleranceF;
        if (toleranceX > 0.0001)
          toleranceX = 0.0001;
      }
    }

    ///
    /// <summary>
    ///   Convert correlation to a data table
    /// </summary>
    ///
    /// <returns>Content orgainzed in a data table</returns>
    ///
    public override DataTable Content()
    {
      throw new System.NotImplementedException();
    }

    /// <summary>
    ///   Set the correlation data from another
    ///   correlation object of the same type.
    /// </summary>
    /// <param name="source">Source correlation object</param>
    internal override void SetCorrelations(CorrelationObject source)
    {
      if (source == null)
        throw new ArgumentException("The source object can not be null.");

      BaseCorrelation other = source as BaseCorrelation;
      if (other == null)
        throw new ArgumentException("The source object is not a base correlation object.");

      if (this.correls_ == null)
        throw new NullReferenceException("The correlation data is null.");

      if (other.correls_ == null || other.correls_.Length != this.correls_.Length)
        throw new ArgumentException("The source correlation data does not match this data.");

      for (int i = 0; i < correls_.Length; ++i)
      {
        this.correls_[i] = other.correls_[i];
        this.strikes_[i] = other.strikes_[i];
      }

      if (other.tcorrels_ != null && other.tcorrels_.Length != 0)
      {
        if (this.tcorrels_ == null)
          tcorrels_ = new double[other.tcorrels_.Length];
        for (int i = 0; i < correls_.Length; ++i)
          this.tcorrels_[i] = other.tcorrels_[i];
      }
      return;
    }

    private static int CountNaNs(double[] corrs)
    {
      int count = 0;
      if (corrs != null)
      {
        for (int i = 0; i < corrs.Length; ++i)
          if (Double.IsNaN(corrs[i]))
            ++count;
      }
      return count;
    }

    #region Old_Methods
    /// <summary>
    ///   Calculate the strike level for an array of detachment points of a basket.
    /// </summary>
    ///
    /// <remarks>
    ///  <para>The strike <formula inline="true">x</formula> are calculated according to the following
    ///  <c>BaseCorrelationStrikeMethod</c>.</para>
    ///
    ///   <list type="bullet">
    ///     <item><term>Unscaled</term>
    ///       <description><formula inline="true">x = d</formula>, where <formula inline="true">d</formula>
    ///       is detachment point.
    ///     </description></item>
    ///     <item><term>ExpectedLoss</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">d</formula>
    ///       is detachment and <formula inline="true">s</formula> is the ratio
    ///       of expected basket losses over the portfolio notional.
    ///     </description></item>
    ///     <item><term>ExpectedLossPv</term>
    ///       <description><formula inline="true">x = d / s</formula>, where <formula inline="true">d</formula>
    ///       is detachment and <formula inline="true">s</formula> is the ratio
    ///       of protection PV on the whole portfolio over the portfolio notional.
    ///     </description></item>
    ///     <item><term>ExpectedLossRatio</term>
    ///       <description><formula inline="true">x = r</formula>, where <formula inline="true">r</formula> is the ratio
    ///       of the expected loss of the first loss tranche over
    ///       the expected loss of the whole portfolio.
    ///     </description></item>
    ///     <item><term>Probability</term>
    ///       <description><formula inline="true">x = p</formula>, where <formula inline="true">p</formula> is the
    ///       cumulative probability that the percentage of portfolio loss <formula inline="true">L</formula> does not exceed
    ///       the detachment <formula inline="true">d</formula>, <formula inline="true">p = \mathrm{Prob}[L \leq d]</formula>.
    ///     </description></item>
    ///   </list>
    ///
    /// </remarks>
    ///
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date of basket</param>
    /// <param name="dp">Detachment points to compute strikes for</param>
    /// <param name="corr">Base correlations at the detachment points</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    ///
    /// <returns>A vector of strike levels matching the CDO tranches</returns>
    ///
    public static double[] Strike(
      BaseCorrelationStrikeMethod strikeMethod,
      Dt asOf,
      Dt settle,
      Dt maturity,
      double[] dp,
      double[] corr,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond
      )
    {
      // compute the strikes
      return Strike(strikeMethod, null, asOf, settle, maturity, dp, corr,
        survivalCurves, recoveryCurves, discountCurve, principals, stepSize, stepUnit,
        copula, gridSize, integrationPointsFirst, integrationPointsSecond);
    }

    /// <summary>
    ///   Calculate strikes at specific detachment points
    /// </summary>
    /// <param name="basket">Basket pricer</param>
    /// <param name="strikeMethod">Strike method</param>
    /// <param name="dps">Array of detachment points to calculate strikes at</param>
    /// <param name="correlations">Array of base correlations</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <returns>Array of strikes</returns>
    /// <exclude />
    public static double[]
    Strike(
      BasketPricer basket,
      BaseCorrelationStrikeMethod strikeMethod,
      double[] dps,
      double[] correlations,
      DiscountCurve discountCurve
      )
    {
      return Strike(basket, strikeMethod, null, dps, correlations, discountCurve);
    }

    /// <summary>
    ///   Calculate strikes for specific detachments
    /// </summary>
    /// <param name="pricers">CDO pricers</param>
    /// <param name="strikeMethod">Strike method</param>
    /// <param name="correlations">Array of base correlations</param>
    /// <returns>Array of strikes</returns>
    /// <exclude />
    public static double[]
    Strike(
      SyntheticCDOPricer[] pricers,
      BaseCorrelationStrikeMethod strikeMethod,
      double[] correlations
      )
    {
      return Strike(pricers, strikeMethod, null, correlations);
    }

    #endregion // Old_Methods

    #endregion // Methods

    #region Walker
    internal override void Walk(VisitFn visit)
    {
      visit(this);
    }
    #endregion Walker

    #region Properties

    /// <summary>
    ///   Method of calculating base correlation
    /// </summary>
    public BaseCorrelationMethod Method
    {
      get { return method_; }
      set { method_ = value; }
    }


    /// <summary>
    ///   Method computing base correlation strikes
    /// </summary>
    public BaseCorrelationStrikeMethod StrikeMethod
    {
      get { return strikeMethod_; }
      set { strikeMethod_ = value; }
    }


    /// <summary>
    ///   Interpolator for strikes
    /// </summary>
    public Interp Interp
    {
      get { return interpMethod_; }
      set { interpMethod_ = value; }
    }


    /// <summary>
    ///   Interpolation method for strikes
    /// </summary>
    public InterpMethod InterpMethod
    {
      get { return InterpFactory.ToInterpMethod(interpMethod_); }
    }


    /// <summary>
    ///   Extrapolation method for strikes
    /// </summary>
    public ExtrapMethod ExtrapMethod
    {
      get { return InterpFactory.ToExtrapMethod(interpMethod_); }
    }

    /// <summary>
    ///   Whether to interpolate on factors instead of on correlations
    /// </summary>
    public bool InterpOnFactors
    {
      get { return interpOnFactors_; }
      set { interpOnFactors_ = value; }
    }

    /// <summary>
    ///   Number of strike points
    /// </summary>
    public int NumStrikes
    {
      get { return strikes_.Length; }
    }


    /// <summary>
    ///   Array of strike points (readonly)
    /// </summary>
    public double[] Strikes
    {
      get { return strikes_; }
    }


    /// <summary>
    ///   Array of (base) correlations (readonly)
    /// </summary>
    public double[] Correlations
    {
      get { return correls_; }
    }


    /// <summary>
    ///   Array of tranche correlations from original calibration (readonly)
    /// </summary>
    [Browsable(false)]
    public double[] TrancheCorrelations
    {
      get { return tcorrels_; }
    }


    /// <summary>
    ///   Scale factor used for scaling of original strikes
    /// </summary>
    public double ScalingFactor
    {
      get { return scalingFactor_; }
      set { scalingFactor_ = value; }
    }

    /// <summary>
    ///   User defined mapping method
    /// </summary>
    public IStrikeEvaluator StrikeEvaluator
    {
      get { return strikeEvaluator_; }
      set { strikeEvaluator_ = value; }
    }

    /// <summary>
    ///   Associated detachment points for strikes
    /// </summary>
    public double[] Detachments
    {
      get { return dps_; }
      set
      {
        if (value != null && value.Length != strikes_.Length)
          throw new System.ArgumentException(String.Format(
            "Number of detachments ({0}) and strikes ({1}) not match",
            value.Length, strikes_.Length));
        dps_ = value;
      }
    }

    /// <summary>
    ///   Indicate if the correlation has been successfully calibrated
    /// </summary>
    /// <remarks>
    ///   If this property is true, some or all of the base correlations
    ///   or strikes are NaN.  The user should make sure the correlation
    ///   work properly.
    /// </remarks>
    public bool CalibrationFailed
    {
      get { return calibrationFailed_; }
      set { calibrationFailed_ = value; }
    }

    /// <summary>
    ///   Message explaning the reason of calibration failure
    /// </summary>
    /// <remarks>
    ///   This property may present when some or all of the base correlations
    ///   or strikes are NaN.  The user should make sure the correlation
    ///   work properly.
    /// </remarks>
    public string ErrorMessage
    {
      get { return errorMsg_; }
      set { errorMsg_ = value; }
    }

    #endregion Properties

    #region Data

    private BaseCorrelationMethod method_;
    private BaseCorrelationStrikeMethod strikeMethod_;
    private Interp interpMethod_;
    private bool interpOnFactors_;

    private double scalingFactor_; // factor used for scaling of original strikes
    private double[] strikes_;    // normalized detachment points
    private double[] correls_;    // array of base correlation data (if calculated)
    private double[] tcorrels_;   // array of tranche correlation data
    private double[] dps_;
    private bool calibrationFailed_;
    private string errorMsg_;
    private IStrikeEvaluator strikeEvaluator_;
    #endregion // Data

    #region Strike_Evaluators
    /// <summary>
    /// Adjusts baskets= and CDO for forward strikes.
    /// </summary>
    /// <param name="basket">The basket.</param>
    /// <param name="cdo">The CDO.</param>
    /// <param name="strikeMethod">The strike method.</param>
    /// <remarks>This is function check that if forward strike method
    /// is requested and the basket contains defaulted names.  If so,
    /// it replace the basket with a new one containg only the remaining names
    /// and created a new cdo with attached/detachment adjusted accordingly.
    /// </remarks>
    /// <returns>True if the basket and cdo are adjusted; false otherwise</returns>
    private static bool AdjustForForwardStrikes(
      ref BasketPricer basket,
      ref SyntheticCDO cdo,
      ref BaseCorrelationStrikeMethod strikeMethod)
    {
      switch (strikeMethod)
      {
        default:
          return false; // nothing to do
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
          strikeMethod = BaseCorrelationStrikeMethod.ExpectedLossPvRatio;
          break;
          //TODO: Check other forward strike methods, they should do the same thing.
          //TODO: Schedlue this for 10.1.1.
      }
      if (basket.DefaultedPrincipal == 0.0) return false; // no predefaulted names.

      // Find the adjustments.
      double ap = cdo.Attachment, dp = cdo.Detachment, loss = 0;
      basket.AdjustTrancheLevels(false, ref ap, ref dp, ref loss);
      // Create a CDO with adjusted attachment/detachment.
      cdo = (SyntheticCDO)cdo.ShallowCopy();
      cdo.Attachment = ap;
      cdo.Detachment = dp;
      // Create a new basket with teh defaulted name removed.
      basket = basket.Duplicate();
      basket.Reset();
      basket.Reset(new CreditPool(basket.Principals, basket.SurvivalCurves,
        basket.OriginalBasket.AsPoolOfLCDS, null));
      basket.RawLossLevels = new UniqueSequence<double>(0, dp);
      return true;
    }

    internal class UserFn : StrikeFn
    {
      public UserFn(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs,
        IStrikeEvaluator fn
        )
        : base(interp, onfactor, strikes, baseCorrs, false)
      {
        fn_ = fn;
      }

      public override double strike(double factor)
      {
        if (Double.IsNaN(factor)) return Double.NaN;
        return fn_.Strike(factor*factor);
      }

      public override double strike()
      {
        return fn_.Strike();
      }

      private IStrikeEvaluator fn_;
    } // class UserFn

    internal class ProtectionFn : StrikeFn
    {
      public ProtectionFn(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs,
        BaseCorrelationStrikeMethod method,
        double dp,
        BasketPricer basketPricer
        )
        : base(interp, onfactor, strikes, baseCorrs, false)
      {
        dp_ = dp;
        bp_ = basketPricer;
        bool forwardLooking = (method == BaseCorrelationStrikeMethod.ExpectedLossRatioForward ||
                method == BaseCorrelationStrikeMethod.ProtectionForward ||
                method == BaseCorrelationStrikeMethod.EquityProtectionForward);
          includePastLoss_ = !forwardLooking;
          double notAdj = 1.0;
          if(forwardLooking)
          {
              var cdo = new SyntheticCDO(basketPricer.Settle, basketPricer.Maturity, Currency.None, DayCount.None, Frequency.None,
                                             BDConvention.None, Calendar.None, 0.0, 0.0, 0.0, 1.0);
              var pricer = new SyntheticCDOPricer(cdo, basketPricer, null);
              notAdj = pricer.CurrentNotional/pricer.Notional;
              dp = basketPricer.AdjustTrancheLevel(false, dp);
          }
        // calculate scaling factor
          switch(method)
        {
          case BaseCorrelationStrikeMethod.ExpectedLossRatio:// Since this BasketLoss only counts nondefaulted curve, locally add back the previous loss 
                scaling_ = bp_.BasketLoss(bp_.Settle, bp_.Maturity) + bp_.PreviousLoss;
                break;
            case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
                scaling_ = bp_.BasketLoss(bp_.Settle, bp_.Maturity);
            break;
          case BaseCorrelationStrikeMethod.EquityProtection:
          case BaseCorrelationStrikeMethod.EquityProtectionForward:
                scaling_ = dp*notAdj;
            break;
          default:
            scaling_ = notAdj;
            break;
        }
      }

      public override double strike(double factor)
      {
        if (Double.IsNaN(factor)) return Double.NaN;
        bp_.SetFactor(factor);
          double pastLossAdj = (includePastLoss_) ? 0.0 : bp_.PreviousLoss;
        double ratio = (bp_.AccumulatedLoss(bp_.Maturity, 0.0, dp_) - pastLossAdj) / scaling_;
        return ratio;
      }

      public override double strike()
      {
          double pastLossAdj = (includePastLoss_) ? 0.0 : bp_.PreviousLoss;
        double ratio = (bp_.AccumulatedLoss(bp_.Maturity, 0.0, dp_) - pastLossAdj) / scaling_;
        return ratio;
      }

      double scaling_;
      double dp_;
      BasketPricer bp_;
      bool includePastLoss_;
    } // class ProtectionFn

    internal class ProtectionPvFn : StrikeFn
    {
      public ProtectionPvFn(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs,
        BaseCorrelationStrikeMethod method,
        SyntheticCDO cdo,
        BasketPricer basketPricer,
        DiscountCurve discountCurve
        )
        : base(interp, onfactor, strikes, baseCorrs, false)
      {
        if(!AdjustForForwardStrikes(ref basketPricer, ref cdo, ref method))
          cdo = (SyntheticCDO)cdo.Clone();
        cdo.Attachment = 0.0;
        double notional = basketPricer.TotalPrincipal;
        double dp = cdo.Detachment;
        switch (method)
        {
          case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
            scaling_ = BasketLossPv(cdo, basketPricer, discountCurve, notional);
            break;
          case BaseCorrelationStrikeMethod.EquityProtectionPv:
            scaling_ = notional * dp;
            break;
          case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
            double el = 0;
            double a = 0;
            double d = dp;
            basketPricer.AdjustTrancheLevels(false, ref a, ref d, ref el);
            scaling_ = (notional - basketPricer.DefaultedPrincipal)*d;
            break;
          case BaseCorrelationStrikeMethod.ProtectionPvForward:
            scaling_ = notional - basketPricer.DefaultedPrincipal;
            break;
          default:
            scaling_ = notional;
            break;
        }
        pricer_ = new SyntheticCDOPricer(cdo,
          basketPricer, discountCurve, notional * dp, false, null);
      }

      internal static double BasketLossPv(
        SyntheticCDO cdo, BasketPricer basketPricer,
        DiscountCurve discountCurve, double notional)
      {
        BasketPricer basket = Duplicate(basketPricer);
        double dp = cdo.Detachment;
        cdo.Detachment = 1.0;
        SyntheticCDOPricer pricer = new SyntheticCDOPricer(
          cdo, basket, discountCurve, notional, false, null);
        double scaling = -pricer.ProtectionPv();
        cdo.Detachment = dp;
        return scaling;
      }

      private static BasketPricer Duplicate(BasketPricer basket)
      {
        // Find the original input basket and duplicate it.
        var adapter = basket as BasketBootstrapCorrelationPricer;
        if (adapter != null) basket = adapter.InnerBasket;
        basket = basket.Duplicate();

        // Set the detachment to 100% for we care only the full basket loss.
        basket.RawLossLevels = new UniqueSequence<double>(0.0, 1.0);
        BaseCorrelationBasketPricer bcBasket = basket as BaseCorrelationBasketPricer;
        if (bcBasket != null) bcBasket.Detachment = 1.0;

        // Correlation doesn't matter since we only use it to calculate
        // the full basket loss.  Set to zero for speed and high accuracy.
        basket.CorrelationTermStruct = new CorrelationTermStruct(
          basket.EntityNames, new[] {0.0}, new[] {basket.Maturity});

        // Reset the basket and return.
        basket.Reset();
        return basket;
      }

      public override double strike(double factor)
      {
        if (Double.IsNaN(factor)) return Double.NaN;
        pricer_.Basket.SetFactor(factor);
        double ratio = - pricer_.ProtectionPv() / scaling_;
        return ratio;
      }

      public override double strike()
      {
        double ratio = - pricer_.ProtectionPv() / scaling_;
        return ratio;
      }

      double scaling_;
      SyntheticCDOPricer pricer_;
    } // class ProtectionPvFn


    internal class ProbabilityFn : StrikeFn
    {
      public ProbabilityFn(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs,
        double dp,
        BasketPricer basketPricer
        )
        : base(interp, onfactor, strikes, baseCorrs, false)
      {
        bp_ = basketPricer;
        lossLevels_ = new double[] { 0.0, dp };
      }

      public override double strike(double factor)
      {
        if (Double.IsNaN(factor)) return Double.NaN;
        bp_.SetFactor(factor);
        double[,] res = bp_.CalcLossDistribution(true, bp_.Maturity, lossLevels_);
        double probability = res[1, 1];
        return probability;
      }

      public override double strike()
      {
        double[,] res = bp_.CalcLossDistribution(true, bp_.Maturity, lossLevels_);
        double probability = res[1, 1];
        return probability;
      }

      double[] lossLevels_;
      BasketPricer bp_;
    } // class PribabilityFn

    internal class SpreadFn : StrikeFn
    {
      public SpreadFn(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs,
        SyntheticCDO cdo,
        BasketPricer basketPricer,
        DiscountCurve discountCurve,
        bool senior
        )
        : base(interp, onfactor, strikes, baseCorrs, true)
      {
        cdo = (SyntheticCDO)cdo.Clone();
        if (senior)
        {
          cdo.Attachment = cdo.Detachment;
          cdo.Detachment = 1.0;
          if (basketPricer.NoAmortization || !basketPricer.LossLevelAddComplement)
          {
            basketPricer = basketPricer.Duplicate();
            basketPricer.NoAmortization = false;
            basketPricer.LossLevelAddComplement = true;
          }
        }
        else
          cdo.Attachment = 0.0;
        cdo.Fee = 0;

        pricer_ = new SyntheticCDOPricer(cdo, basketPricer, discountCurve, 1.0, null);
      }

      public override double strike(double factor)
      {
        if (Double.IsNaN(factor)) return Double.NaN;
        pricer_.Basket.SetFactor(factor);
        double prem = pricer_.BreakEvenPremium();
        return 1 - prem;
      }

      public override double strike()
      {
        double prem = pricer_.BreakEvenPremium();
        return 1 - prem;
      }

      SyntheticCDOPricer pricer_;
    } // class SpreadFn

    internal abstract class StrikeFn : CorrelationEvaluator
    {
      #region Constructors
      public StrikeFn(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs,
        bool complement
        )
        : base(interp, onfactor, strikes, baseCorrs, complement)
      { }
      #endregion // Constructors

      #region Methods
      public double solve(double toleranceF, double toleranceX, double upperBound)
      {
        Solver rf = new Brent();
        rf.setToleranceX(toleranceX);
        rf.setToleranceF(toleranceF);
        const double min = 1.0E-10;
        double max = Math.Sqrt(upperBound);
        double low = min, high = max;

        if (use8dot1Solver_)
        {
          // Bound valid results to positive
          rf.setLowerBounds(0.0);
          rf.setUpperBounds(1.0);
          // starting ranges
          low = 0; high = 0.5;
        }
        else if (monotone(strikes_, getCorrArray()))
        {
          // These codes only work with monotonic increasing curves
          int idx = bracketIndex(Math.Min(toleranceX,toleranceF));
          if (idx < 0)
          {
            idx = -idx - 1;
            return getCorrelation(idx);
          }
          if (idx >= strikes_.Length)
          {
            low = getFactor(strikes_.Length - 1);
            high = max;
          }
          else if (idx == 0)
          {
            low = min;
            high = getFactor(0);
          }
          else
          {
            low = getFactor(idx - 1);
            high = getFactor(idx);
          }

          // Do we hit the flat region?
          if (low >= high - toleranceX)
          {
            double x = (low + high) / 2;
            if (Math.Abs(evaluate(x)) <= toleranceF)
              return x * x;
            // For safety, search the whole domain of correlations
            low = min; high = max;
          }
          // Bound valid results to positive
          rf.setLowerBounds(low);
          rf.setUpperBounds(high);
        }

        double res;
        if (low > min || high < max)
        {
          try
          {
            res = rf.solve(this, 0.0, low, high);
            res *= res;
            return res;
          }
          catch (SolverException) { }
        }

        // try a full search
        rf.setLowerBounds(min);
        rf.setUpperBounds(max);
        res = rf.solve(this, 0.0, 0.4);
        res *= res;
        return res;
      }

      public abstract double strike(double factor);
      public abstract double strike();

      private static bool monotone(double[] strikes, double[] correls)
      {
        int last = strikes.Length - 1;
        if (last == 0)
          return false;
        double dir = (strikes[1] - strikes[0]) * (correls[1] - correls[0]);
        if (dir >= 0)
        {
          for (int i = 0; i < last; ++i)
            if ((strikes[i + 1] - strikes[i]) * (correls[i + 1] - correls[i]) < 0)
              return false;
          return true;
        }
        return false;
      }

      private int bracketIndex(double toleranceF)
      {
        double factor, s;
        int high = strikes_.Length - 1;
        int idx = 0, low = 0;

        // Binary search for bracket
        do
        {
          idx = (low + high) / 2;
          factor = getFactor(idx);
          s = strike(factor);
          double tol = (1 + Math.Abs(strikes_[idx])) * toleranceF;
          if (s > strikes_[idx] + tol)
            low = idx;
          else if (s < strikes_[idx] - tol)
            high = idx;
          else
            // found solution
            return -(idx + 1);
        } while (high - low > 1);

        if (idx != low && low == 0)
        {
          // Check extreme case 1: lower end
          factor = getFactor(low);
          s = strike(factor);
          double tol = (1 + Math.Abs(strikes_[low])) * toleranceF;
          if (s > strikes_[low] + tol)
            return high; // bracket (strike[0],strike[1])
          else if (s < strikes_[low] - tol)
            return low; // bracket (0, strike[0])
          else
            // found solution
            return -(low + 1);
        }
        else if (idx != high && high == strikes_.Length - 1)
        {
          // Check extreme case 2: upper end
          factor = getFactor(high);
          s = strike(factor);
          double tol = (1 + Math.Abs(strikes_[high])) * toleranceF;
          if (s > strikes_[high] + tol)
            return high + 1; // bracket (strike[N-1], 1)
          else if (s < strikes_[high] - tol)
            return high; // bracket (strike[N-2], strike[N-1])
          else
            // found solution
            return -(high + 1);
        }
        else
          return high;
      }

      public override double evaluate(double factor)
      {
        if (Double.IsNaN(factor)) return Double.NaN;
        double s = strike(factor);
        double corr = base.evaluate(s);
        return corr - factor * factor;
      }
      #endregion // Methods
    }; // class StrikeFn

    /// <summary>
    ///   Interpolating base correlation based on given strike
    /// </summary>
    /// <remarks>
    ///   This class will automatically sort the input data in ascending order.
    /// </remarks>
    internal class CorrelationEvaluator : SolverFn
    {
      #region Constructors
      public CorrelationEvaluator(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs
        )
      {
        interpolateFactor_ = onfactor;
        SetCorrelations(strikes, baseCorrs, onfactor, false);
        if (interp != null)
          interp_ = new Interpolator(interp, strikes_, corrs_);
      }

      public CorrelationEvaluator(
        Interp interp,
        bool onfactor,
        double[] strikes,
        double[] baseCorrs,
        bool complement
        )
      {
        interpolateFactor_ = onfactor;
        SetCorrelations(strikes, baseCorrs, onfactor, complement);
        if (interp != null)
          interp_ = new Interpolator(interp, strikes_, corrs_);
      }

      private void SetCorrelations(
        double[] strikes, double[] corrs,
        bool onfactor, bool complement)
      {
        int nancnt = BaseCorrelation.CountNaNs(corrs);
        if (nancnt != 0 || onfactor)
        {
          int len = corrs.Length - nancnt;
          if (len == 0)
            throw new InvalidOperationException("All correlations are NaN");
          corrs_ = new double[len];
          for (int i = 0, idx = 0; i < corrs.Length; ++i)
            if (!Double.IsNaN(corrs[i]))
              corrs_[idx++] = onfactor ? Math.Sqrt(corrs[i]) : corrs[i];
        }
        else
          corrs_ = corrs;

        if (strikes == null)
        {
          strikes_= null;
          return;
        }

        if (nancnt != 0 || complement)
        {
          int len = corrs.Length - nancnt;
          strikes_ = new double[len];
          for (int i = 0, idx = 0; i < strikes.Length; ++i)
            if (!Double.IsNaN(corrs[i]))
              strikes_[idx++] = complement ? (1 - strikes[i]) : strikes[i];
        }
        else
          strikes_ = strikes;

        if (strikes_ != null && !increasing(strikes_))
          sortStrikes(); // no guarantee that strikes is increasing
      }
      #endregion // Constructors

      #region Methods
      public override double evaluate(double strike)
      {
        double x = 0;
        //if (!check_x(strikes_, corrs_, strike, ref x))
          x = interp_.evaluate(strike);
        if (interpolateFactor_)
          return x * x;
        else
          return x;
      }

      private static bool check_x(double[] xa, double[] ya, double x, ref double y)
      {
        for(int i = 0; i < xa.Length;++i)
          if(Math.Abs(x-xa[i])<1E-15)
          {
            y = ya[i];
            return true;
          }
        return false;
      }

      private static bool increasing(double[] strikes)
      {
        int last = strikes.Length - 1;
        if (last == 0)
          return false;
        for (int i = 0; i < last; ++i)
          if (strikes[i+1] - strikes[i] <= 0)
            return false;
        return true;
      }

      private void sortStrikes()
      {
        List<double> slist = new List<double>();
        List<double> clist = new List<double>();

        for (int i = 0; i < strikes_.Length; ++i)
        {
          double xi = strikes_[i];
          int pos = slist.BinarySearch(xi);
          if (pos < 0)
          {
            pos = ~pos;
            slist.Insert(pos, xi);
            clist.Insert(pos, corrs_[i]);
          }
        }
        strikes_ = slist.ToArray();
        corrs_ = clist.ToArray();
      }

      internal double getFactor(int i)
      {
        double f = corrs_[i];
        return interpolateFactor_ ? f : Math.Sqrt(f);
      }

      internal double getCorrelation(int i)
      {
        double c = corrs_[i];
        return interpolateFactor_ ? (c * c) : c;
      }

      internal double[] getCorrArray()
      {
        return corrs_;
      }
      #endregion // Methods

      #region Data
      protected Interpolator interp_;
      protected double[] strikes_;
      private double[] corrs_;
      private bool interpolateFactor_;
      
      #endregion // Data
    }

    #endregion //Strike_Evaluators

    #region BaseCorrelationBump

    /// <summary>
    ///   Bump base correlations selected by detachments.
    /// </summary>
    /// 
    /// <param name="selectDetachments">
    ///   Array of the selected base tranches to bump.  This should be an array
    ///   of detachments points associated with the strikes.  A null value means
    ///   to bump the correlations at all strikes.
    /// </param>
    /// <param name="bumpSizes">
    ///   Array of the bump sizes applied to the correlations on the selected detachment
    ///   points.  If the array is null or empty, no bump is performed.  Else if it
    ///   contains only a single number, the number is applied to all detachment points.
    ///   Otherwise, the array is required to have the same length as the array of
    ///   detachments.
    /// </param>
    /// <param name="relative">
    ///   Boolean value indicating if a relative bump is required.
    /// </param>
    /// <param name="lowerBound">
    ///   The lower bound of the bumped correlations.  If any bumped value is below
    ///   the bound, it is adjust up to the bound.  Normally this should be 0.
    /// </param>
    /// <param name="upperBound">
    ///   The upper bound of the bumped correlations.  If any bumped value is above
    ///   this, the value is adjust down to the bound.  Normally this should be 1.
    /// </param>
    /// 
    /// <returns>The average the absolute changes in correlations.</returns>
    public double BumpCorrelations(
      double[] selectDetachments,
      double[] bumpSizes,
      bool relative,
      double lowerBound,
      double upperBound)
    {
      if (bumpSizes == null || bumpSizes.Length == 0)
        return 0.0;

      double avg = 0.0;
      if (selectDetachments == null || selectDetachments.Length == 0)
      {
        if (bumpSizes.Length == 1)
          return BumpCorrelations(bumpSizes[0], relative, lowerBound, upperBound);
#if DEBUG
        if (bumpSizes.Length != correls_.Length)
          throw new System.ArgumentException(String.Format(
            "bumpSizes (len={0}) and Correlations (len={1}) not match",
            bumpSizes.Length, correls_.Length));
#endif
        for (int i = 0; i < bumpSizes.Length; ++i)
          avg += BumpCorrelation(i, bumpSizes[i], relative, lowerBound, upperBound);
        return avg / bumpSizes.Length;
      }

      if (bumpSizes.Length != 1 && bumpSizes.Length != selectDetachments.Length)
        throw new System.ArgumentException(String.Format(
          "bumpSizes (len={0}) and selectDetachments (len={1}) not match",
          bumpSizes.Length, selectDetachments.Length));
      for (int i = 0, count = 0; i < selectDetachments.Length; ++i)
      {
        int idx = FindDetachmentIndex(selectDetachments[i]);
        if (idx >= 0)
          avg += (BumpCorrelation(idx, bumpSizes.Length == 1 ? bumpSizes[0] : bumpSizes[i],
            relative, lowerBound, upperBound) - avg) / (++count);
      }
      return avg;
    }

    /// <summary>
    ///  Bump all the correlations simultaneously
    /// </summary>
    /// 
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="lowerBound">Lower bound of the bumped correlations.</param>
    /// <param name="upperBound">Upper bound of the bumped correlations.</param>
    ///
    /// <returns>The average change in correlations</returns>
    private double BumpCorrelations(
      double bump, bool relative,
      double lowerBound, double upperBound)
    {
      double avg = 0.0;
      for (int i = 0; i < correls_.Length; i++)
      {
        double x = (relative ?
          correls_[i] * ((bump > 0.0) ? bump : (1.0 / (1.0 - bump) - 1.0)) : bump)
         + correls_[i];

        if (x < lowerBound)
          x= lowerBound;
        else if (x > upperBound)
          x = upperBound;

        avg += x - correls_[i];
        correls_[i] = x;
      }

      return avg / correls_.Length;
    }

    /// <summary>
    ///   Bump an correlation by index of strike
    /// </summary>
    ///
    /// <param name="i">Index of strike i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="lowerBound">Lower bound of the bumped correlations.</param>
    /// <param name="upperBound">Upper bound of the bumped correlations.</param>
    ///
    /// <returns>The average change in correlation</returns>
    private double BumpCorrelation(
      int i, double bump, bool relative,
      double lowerBound, double upperBound)
    {
      if (i < 0 || i > correls_.Length)
        throw new ArgumentException(String.Format("Invalid strike index {0}", i));

      // x is the bumped value
      double x = (relative ?
        correls_[i] * ((bump > 0.0) ? bump : (1.0 / (1.0 - bump) - 1.0)) : bump)
        + correls_[i];

      // adjust to the bounds
      if (x < lowerBound)
        x = lowerBound;
      else if (x > upperBound)
        x = upperBound;

      // actual bumped value
      double delta = x - correls_[i];
      correls_[i] = x;

      return delta;
    }

    /// <summary>
    ///   Find correlation index correponding to a given detachment
    /// </summary>
    /// <param name="dp">Detachment</param>
    /// <returns>Valid index or -1</returns>
    private int FindDetachmentIndex(double dp)
    {
      if (dps_ == null)
        throw new System.NullReferenceException("Detachment points cannot be null");
      for (int i = 0; i < dps_.Length; ++i)
        if (Math.Abs(dp - dps_[i]) < 1E-6)
          return i;
      return -1;
    }

    /// <summary>
    ///   Bump base correlations selected by detachments, tenor dates and components.
    /// </summary>
    /// 
    /// <param name="selectComponents">
    ///   Array of names of the selected components to bump.  This parameter applies
    ///   to mixed base correlation objects and it is ignored for non-mixed single
    ///   object.  A null value means bump all components.
    /// </param>
    /// <param name="selectTenorDates">
    ///   Array of the selected tenor dates to bump.  This parameter applies to base
    ///   correlation term structures and it is ignored for simple base correlation
    ///   without term structure.  A null value means bump all tenors.
    /// </param>
    /// <param name="selectDetachments">
    ///   Array of the selected base tranches to bump.  This should be an array
    ///   of detachments points associated with the strikes.  A null value means
    ///   to bump the correlations at all strikes.
    /// </param>
    /// <param name="trancheBumps">
    ///   Array of the BbumpSize objects applied to the correlations on the selected detachment
    ///   points.  If the array is null or empty, no bump is performed.  Else if it
    ///   contains only a single element, the element is applied to all detachment points.
    ///   Otherwise, the array is required to have the same length as the array of
    ///   detachments.
    /// </param>
    /// <param name="indexBump">Array of bump sizes on index</param>
    /// <param name="relative">
    ///   Boolean value indicating if a relative bump is required.
    /// </param>
    /// <param name="onquotes">
    ///   True if bump market quotes instead of correlation themselves.
    /// </param>
    /// <param name="hedgeInfo">
    ///   Hedge delta info.  Null if no head info is required.
    /// </param>
    /// 
    /// <returns>
    ///   The average of the absolute changes in correlations, which may be different
    ///   than the bump size requested due to the restrictions on lower bound and upper
    ///   bound.
    /// </returns>
    /// 
    public override double BumpCorrelations(
      string[] selectComponents,
      Dt[] selectTenorDates,
      double[] selectDetachments,
      BumpSize[] trancheBumps,
      BumpSize indexBump,
      bool relative, bool onquotes,
      ArrayList hedgeInfo)
    {
      BaseCorrelationTermStruct bct = new BaseCorrelationTermStruct(
        new Dt[] { Dt.Empty }, new BaseCorrelation[] { this });
      bct.Name = this.Name;
      return bct.BumpCorrelations(selectComponents, selectTenorDates, selectDetachments,
        trancheBumps, indexBump, relative, onquotes, hedgeInfo);
    }

    #endregion // BaseCorrelationBump

    #region ICorrelationBump Members
    ///
    /// <summary>
    ///   Bump correlations by index of strike
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    /// </remarks>
    ///
    /// <param name="i">Index of strike i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    public override double BumpCorrelations(int i, double bump, bool relative, bool factor)
    {
      if (i < 0 || i > correls_.Length)
        throw new ArgumentException(String.Format("Invalid strike index {0}", i));

      double delta = (relative) ?
        correls_[i] * ((bump > 0.0) ? bump : (1.0 / (1.0 - bump) - 1.0))
        : bump;

      correls_[i] += delta;

      return delta;
    }

    /// <summary>
    ///  Bump all the correlations simultaneously
    /// </summary>
    /// 
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlations</returns>
    public override double BumpCorrelations(double bump, bool relative, bool factor)
    {
      double avg = 0.0;
      for (int i = 0; i < correls_.Length; i++)
      {
        double delta = (relative) ?
          correls_[i] * ((bump > 0.0) ? bump : (1.0 / (1.0 - bump) - 1.0))
          : delta = bump;
        correls_[i] += delta;
        avg += delta;
      }

      return avg / correls_.Length;
    }

    /// <summary>
    ///   Get name
    /// </summary>
    ///
    /// <param name="i">index</param>
    ///
    /// <returns>name</returns>
    ///
    public string GetName(int i)
    {
      if (i < 0 || i >= strikes_.Length)
        throw new System.ArgumentException(String.Format("index {0} is out of range", i));
      if (dps_ != null)
        return dps_[i].ToString("P");
      return "Strike " + i;
    }

    /// <summary>
    ///   Number of strikes
    /// </summary>
    [Browsable(false)]
    public int NameCount
    {
      get { return correls_.Length; }
    }

    /// <summary>
    ///   Error key for exception data
    /// </summary>
    internal static string ExceptionDataKey
    {
      get { return "BaseCorrelations"; }
    }
    #endregion

    #region CalcEquivalentIndexDP
    /// <summary>
    ///  Calculate the index attachment/detachment corresponding to those of bespoke pricers
    /// </summary>
    /// <param name="pricers">Bespoke CDO pricers</param>
    /// <param name="baseCorrelation">Base corelation term structure for an index</param>
    /// <param name="DpStrikes">Optional array of detachment strikes for CDO pricers</param>
    /// <param name="maturities">Zero or more correlation surface tenor dates</param>
    /// <param name="survivalCurves">Array of survival curves</param>
    /// <returns>2-D array of index attachment/detachment points</returns>
    public static double[,] CalcEquivalentIndexDP(
      SyntheticCDOPricer[] pricers, 
      BaseCorrelationTermStruct baseCorrelation,
      double[] DpStrikes,
      Dt[] maturities,
      SurvivalCurve[] survivalCurves)
    {
      if ((baseCorrelation.Calibrator == null || baseCorrelation.Calibrator.IndexTerm == null) && 
        (survivalCurves == null||survivalCurves.Length==0))
        throw new ArgumentException("Scaled index survival curves missing");
      // Check if the dates are on the specified correlation surface
      // If maturity is not one of bcs tenor dates, throw error message
      int numMaturities = 1;
      if (maturities != null && maturities.Length > 0)
      {
        Dt[] bcsDates = baseCorrelation.Dates;
        foreach (Dt t in maturities)
        {
          if(!t.IsEmpty())
            if (!(Array.Exists<Dt>(bcsDates, delegate(Dt T) { return Dt.Cmp(T, t) == 0; })))
              throw new ArgumentException("The tenor " + t.ToString() + " cannot be found on correlation surface "+baseCorrelation.Name);
        }
        numMaturities = maturities.Length;
      }
      
      // Get the attachment/detachment points for the bespoke CDO pricer
      // And compute the strikes for Bespoke CDO Pricers, these serve
      // as the target strikes to be matched by index      
      double[,] apdp = new double[pricers.Length, 2];
      double[,] indexApDp = new double[pricers.Length, 2*numMaturities];
      double[,] strikes = new double[pricers.Length, 2];

      for (int i = 0; i < pricers.Length; ++i)
      {
        apdp[i, 0] = pricers[i].CDO.Attachment;
        apdp[i, 1] = pricers[i].CDO.Detachment;
      }

      // Get the strikes of beskpoke cdo pricers as targets
      strikes = GetTargetStrikes(pricers, baseCorrelation, DpStrikes);

      SurvivalCurve[] curves = null;
      if (baseCorrelation.Calibrator != null && baseCorrelation.Calibrator.IndexTerm != null)
        curves = baseCorrelation.Calibrator.IndexTerm.GetScaleSurvivalCurves();
      else
        curves = survivalCurves;

      // For first pricer and attachment...
      for (int i = 0; i < 2 * numMaturities; i += 2)
      {
        indexApDp[0, i] = IndexStrikeEvaluator.Solve(
          strikes[0, 0], pricers[0], baseCorrelation,
          (maturities==null||maturities.Length == 0) ? Dt.Empty : maturities[i / 2], curves);
      }
      for (int i = 0; i < pricers.Length - 1; ++i)
      {
        // For first pricer and detachment...
        for (int j = 1; j < 2 * numMaturities; j += 2)
        {
          indexApDp[i, j] = IndexStrikeEvaluator.Solve(
            strikes[i, 1], pricers[i], baseCorrelation,
            (maturities == null || maturities.Length == 0) ? Dt.Empty : maturities[(j - 1) / 2], curves);
        }
        // If current tranche detachment is next tranche's attachment, no need to solve
        if (apdp[i, 1] == apdp[i + 1, 0])
        {
          for (int j = 0; j < 2 * numMaturities; j += 2)
          {
            indexApDp[i + 1, j] = indexApDp[i, j + 1];
          }
        }
        else
        {
          for (int j = 0; j < 2 * numMaturities; j += 2)
          {
            indexApDp[i + 1, j] = IndexStrikeEvaluator.Solve(
              strikes[i + 1, 0], pricers[i + 1], baseCorrelation,
              (maturities == null || maturities.Length == 0) ? Dt.Empty : maturities[j / 2], curves);
          }
        }
      }
      // For last pricer and detachment...
      for (int j = 1; j < 2 * numMaturities; j += 2)
      {
        indexApDp[pricers.Length - 1, j] = IndexStrikeEvaluator.Solve(
          strikes[pricers.Length - 1, 1], pricers[pricers.Length - 1], baseCorrelation,
          (maturities == null || maturities.Length == 0) ? Dt.Empty : maturities[(j - 1) / 2], curves);
      }
      return indexApDp;
    }

    // Compute the target strikes for cdo pricers
    private static double[,] GetTargetStrikes(SyntheticCDOPricer[] pricers, 
      BaseCorrelationTermStruct baseCorrelation, double[] DpStrikes)
    {      
      double[,] apdp = new double[pricers.Length, 2];      
      double[,] strikes = new double[pricers.Length, 2];

      for (int i = 0; i < pricers.Length; ++i)
      {
        apdp[i, 0] = pricers[i].CDO.Attachment;
        apdp[i, 1] = pricers[i].CDO.Detachment;
      }

      // Need to assign the non-null DpStrikes to strikes
      if (DpStrikes != null && DpStrikes.Length > 0)
      {
        for (int i = 0; i < DpStrikes.Length; ++i)
        {
          // Compute the attachment strike for first attachment point
          // Check if attachment is 0, simply set apstrike to be 0 for some mapping methods
          // Methods such as Equity/SeniorSpread, EquityProtection/Pv have decreasing strikes
          if (i == 0)
          {
            if (apdp[i, 0] < 1e-8)
            {
              BaseCorrelationStrikeMethod method = baseCorrelation.BaseCorrelations[0].StrikeMethod;
              if (! (method == BaseCorrelationStrikeMethod.EquitySpread || method == BaseCorrelationStrikeMethod.SeniorSpread ||
                   method == BaseCorrelationStrikeMethod.EquityProtection || method == BaseCorrelationStrikeMethod.EquityProtectionPv))
                strikes[i, 0] = 0;
              else
              {
                // This corresponds to one of four decreasing strike method
                // Rather than call APStrike to get NaN, simply set it NaN
                strikes[i, 0] = Double.NaN;
              }
            }
            else
              strikes[i, 0] = ((BaseCorrelationBasketPricer)pricers[i].Basket).APStrike;
            strikes[i, 1] = DpStrikes[i];
          }
          else
          {
            strikes[i, 0] = strikes[i - 1, 1];
            strikes[i, 1] = DpStrikes[i];
          }
        }
      }
      else
      {
        BaseCorrelationBasketPricer basketPricer = (BaseCorrelationBasketPricer)pricers[0].Basket;
        if (apdp[0, 0] < 1e-8)
        {
          BaseCorrelationStrikeMethod method = baseCorrelation.BaseCorrelations[0].StrikeMethod;
          if (!IndexStrikeEvaluator.DecreasingStrikeMethod(method))
            strikes[0, 0] = 0;
          else
            strikes[0, 0] = Double.NaN;
        }
        else
          strikes[0, 0] = basketPricer.APStrike;
        strikes[0, 1] = basketPricer.DPStrike;
        for (int i = 1; i < pricers.Length; ++i)
        {
          basketPricer = (BaseCorrelationBasketPricer)pricers[i].Basket;
          if (apdp[i, 0] == apdp[i - 1, 1])
            strikes[i, 0] = strikes[i - 1, 1];
          else
            strikes[i, 0] = basketPricer.APStrike;
          strikes[i, 1] = basketPricer.DPStrike;
        }
      }
      return strikes;
    }

    #endregion CalcEquivalentIndexDP

    #region IndexStrikeEvaluator
    private class IndexStrikeEvaluator : SolverFn
    {
      private IndexStrikeEvaluator(
        SyntheticCDOPricer pricer, BaseCorrelationTermStruct baseCorrelation, Dt maturity, SurvivalCurve[] survCurves)
      {
        pricer_ = pricer;
        baseCorrelation_ = baseCorrelation;
        theMaturity_ = maturity;
        survivalCurves_ = survCurves;
        Initialize();
      }

      /// <summary>
      ///  Generate the Synthetic CDO Pricer at bespoke maturity with 1 as detachment
      ///  The detachment will be reset to x in the evaluate
      /// </summary>
      /// <exclude/>
      private static void Initialize()
      {
        BaseCorrelationStrikeMethod strikeMethod = baseCorrelation_.BaseCorrelations[0].StrikeMethod;
        Dt asOf = pricer_.Basket.AsOf;
        Dt settle = pricer_.Basket.Settle;
        Dt maturity = theMaturity_.IsEmpty() ? pricer_.Basket.Maturity : theMaturity_;

        // Get the scaled index survival curves
        SurvivalCurve[] survCurves = null;
        if (baseCorrelation_.Calibrator != null && baseCorrelation_.Calibrator.IndexTerm != null)
          survCurves = baseCorrelation_.Calibrator.IndexTerm.GetScaleSurvivalCurves();
        else
          survCurves = survivalCurves_;
        DiscountCurve discountCurve = null;
        if (baseCorrelation_.Calibrator != null && baseCorrelation_.Calibrator.IndexTerm != null)
          discountCurve = baseCorrelation_.Calibrator.IndexTerm.DiscountCurve;
        else
          discountCurve = survCurves[0].SurvivalCalibrator.DiscountCurve;

        // The new CDO pricer uses bespoke cdo maturity
        SyntheticCDO cdo = new SyntheticCDO(asOf, maturity, Currency.None, 0.0, DayCount.Actual360,
          Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
        cdo.Attachment = 0.0;
        cdo.Detachment = 1;

        Copula copula = new Copula(CopulaType.Gauss);
        SyntheticCDOPricer[] pricers = BasketPricerFactory.CDOPricerSemiAnalytic(
          new SyntheticCDO[] { cdo }, Dt.Empty, asOf, settle, discountCurve, new Dt[] { maturity },
          survCurves, null, copula, baseCorrelation_, pricer_.Basket.StepSize,
          pricer_.Basket.StepUnit, 0, 0, null, false, false);

        bespokeMaturityPricer_ = pricers[0];

        // Need to populate the strike matrix along tenor and along detachment points
        tenorDates_ = baseCorrelation_.Dates;
        dps_ = baseCorrelation_.BaseCorrelations[0].Detachments;
        strikeMatrix = new double[dps_.Length, tenorDates_.Length];
        for (int i = 0; i < tenorDates_.Length; ++i)
          for (int j = 0; j < dps_.Length; ++j)
            strikeMatrix[j, i] = baseCorrelation_.BaseCorrelations[i].Strikes[j];                    
      }

      // Called to set the trial detachment point in Evaluate()
      private static void InitializePricer(double detachment)
      {
        BaseCorrelationStrikeMethod strikeMethod = baseCorrelation_.BaseCorrelations[0].StrikeMethod;
        Dt asOf = pricer_.Basket.AsOf;
        Dt settle = pricer_.Basket.Settle;
        Dt maturity = theMaturity_.IsEmpty() ? pricer_.Basket.Maturity : theMaturity_;

        // Get the scaled index survival curves
        SurvivalCurve[] survCurves = null;
        if (baseCorrelation_.Calibrator != null && baseCorrelation_.Calibrator.IndexTerm != null)
          survCurves = baseCorrelation_.Calibrator.IndexTerm.GetScaleSurvivalCurves();
        else
          survCurves = survivalCurves_;
        DiscountCurve discountCurve = null;
        if (baseCorrelation_.Calibrator != null && baseCorrelation_.Calibrator.IndexTerm != null)
          discountCurve = baseCorrelation_.Calibrator.IndexTerm.DiscountCurve;
        else
          discountCurve = survCurves[0].SurvivalCalibrator.DiscountCurve;

        // The new CDO pricer uses bespoke cdo maturity
        SyntheticCDO cdo = new SyntheticCDO(asOf, maturity, Currency.None, 0.0, DayCount.Actual360,
          Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
        cdo.Attachment = 0.0;
        cdo.Detachment = detachment;

        Copula copula = new Copula(CopulaType.Gauss);


        string[] names = new string[survCurves.Length];
        for (int i = 0; i < survCurves.Length; i++)
          if (null != survCurves[i])
            names[i] = survCurves[i].Name;
        CorrelationObject correlation = new SingleFactorCorrelation(names, Math.Sqrt(theInterpolatedCorrelation_));

        
        SyntheticCDOPricer[] pricers = BasketPricerFactory.CDOPricerSemiAnalytic(
          new SyntheticCDO[] { cdo }, Dt.Empty, asOf, settle, discountCurve, new Dt[]{maturity},
          survCurves, null, copula, correlation, pricer_.Basket.StepSize,
          pricer_.Basket.StepUnit, 0, 0, null, false, false);
        double accuracy = 0;
        int quadPoints = BasketPricerFactory.GetQuadraturePoints(ref accuracy);
        pricers[0].Basket.AccuracyLevel = accuracy;
        bespokeMaturityPricer_ = pricers[0];
      }

      internal static double GetInterpolatedCorrelation(
        BaseCorrelationTermStruct baseCorrelation, double bespokeStrike, Dt theMaturity)
      {
        BaseCorrelation bcAtTheMaturity = baseCorrelation.GetBaseCorrelation(theMaturity);
        double[] strikes = bcAtTheMaturity.Strikes;
        double[] corrNumbers = bcAtTheMaturity.Correlations;
        double[] cleanStrikes;
        double[] cleanCorrNumbers;
        int count = 0;
        for (int i = 0; i < strikes.Length; i++)
        {
          if (Double.IsNaN(strikes[i]))
            break;
          count++;
        }
        cleanCorrNumbers = new double[count];
        cleanStrikes = new double[count];
        for (int i = 0; i < count; i++)
        {
          cleanStrikes[i] = strikes[i];
          cleanCorrNumbers[i] = corrNumbers[i];
        }
        // Some strike methods such as EquitySpread have decreasing strikes
        // The strikes are sorted ascendingly to satify the interpolation
        BaseCorrelationStrikeMethod strikeMethod = baseCorrelation.BaseCorrelations[0].StrikeMethod;
        if (DecreasingStrikeMethod(strikeMethod))
          Array.Sort(cleanStrikes, cleanCorrNumbers);

        Interpolator interp = new Interpolator(
          bcAtTheMaturity.Interp, cleanStrikes, cleanCorrNumbers); 
        return interp.evaluate(bespokeStrike);
      }

      internal static double Solve(double bespokeStrike, SyntheticCDOPricer pricer, 
        BaseCorrelationTermStruct baseCorrelation, Dt theMaturity, SurvivalCurve[] survCurves)
      {
        if (pricer.CDO.Attachment == 0 && Double.IsNaN(bespokeStrike))
          return 0;
        // Set up root finder
        Brent2 rf = new Brent2();
        rf.setToleranceX(ToleranceX);
        rf.setToleranceF(ToleranceF);
        rf.setLowerBounds(0.0);
        rf.setUpperBounds(1.0);        
        rf.setMaxIterations(MaxIterations);

        // Compute the correlation from bc surface at bespokeStrike and theMaturity 
        // used to set up the single-factor CDO pricer.
        theInterpolatedCorrelation_ = GetInterpolatedCorrelation(baseCorrelation, bespokeStrike, theMaturity);

        IndexStrikeEvaluator fn = new IndexStrikeEvaluator(pricer, baseCorrelation, theMaturity, survCurves);

        double res = Double.NaN;
        try
        {
          if (bespokeStrike <= 1e-8)
          {
            if (!DecreasingStrikeMethod(baseCorrelation.BaseCorrelations[0].StrikeMethod))
              return 0;
          }
          if (Double.IsNaN(bespokeStrike))
            return 0;

          double lBound = 0;
          double uBound = 0.2;

          if (baseCorrelation.BaseCorrelations[0].StrikeMethod == BaseCorrelationStrikeMethod.Unscaled)
            res = bespokeStrike;
          else
          {
            // Get the lower and upper bound detachment points
            CalcBounds(fn, bespokeStrike, ref lBound, ref uBound);
            res = rf.solve(fn, bespokeStrike, lBound, uBound);
          }
        }
        catch (Exception e)
        {
          logger.DebugFormat("{0}", e.Message);
        }
        return res;
      }

      public static bool DecreasingStrikeMethod(BaseCorrelationStrikeMethod method)
      {
        if (!(method == BaseCorrelationStrikeMethod.EquitySpread ||
              method == BaseCorrelationStrikeMethod.SeniorSpread ||
              method == BaseCorrelationStrikeMethod.EquityProtection ||
              method == BaseCorrelationStrikeMethod.EquityProtectionForward ||
              method == BaseCorrelationStrikeMethod.EquityProtectionPv ||
              method == BaseCorrelationStrikeMethod.EquityProtectionPvForward))
          return false;
        else
          return true;
      }
      
      private static void CalcBounds(IndexStrikeEvaluator fn, double bespokeStrike, ref double lBound, ref double uBound)
      {
        Dt maturity = pricer_.Basket.Maturity;
        double[] maturityStrikes = new double[dps_.Length];
        double[] doubleDates = Dt.ToExcelDates(tenorDates_);
        double[] tenorStrikes = new double[tenorDates_.Length];
        
        // Extrapolate strikes at the bespoke maturity
        for (int i = 0; i < dps_.Length; ++i)
        {
          for (int j = 0; j < tenorDates_.Length; ++j)
            tenorStrikes[j] = strikeMatrix[i, j];
          Interpolator interp = new Interpolator(baseCorrelation_.Interp, doubleDates, tenorStrikes);
          maturityStrikes[i] = interp.evaluate(Dt.ToExcelDate(maturity));
          if(Double.IsNaN(maturityStrikes[i]))
            maturityStrikes[i] = interp.evaluate(Dt.ToExcelDate(maturity));
        }

        // Find the lower/upper bound among the maturityStrikes
        BaseCorrelationStrikeMethod strikeMethod = baseCorrelation_.BaseCorrelations[0].StrikeMethod;
        int k = 0;
        for (; k < dps_.Length; ++k)
        {
          if (Double.IsNaN(maturityStrikes[k]))
            break;

          // Strikes for some strike methods decrease with dps (such as EquitySpread)
          if (!DecreasingStrikeMethod(strikeMethod))
          {
            if (bespokeStrike < maturityStrikes[k])
              break;
          }
          else
            if (bespokeStrike > maturityStrikes[k])
              break;            
        }
        if (k == 0)
          uBound = dps_[k];
        else if (k == dps_.Length)
        {
          lBound = 0;
          uBound = 1.0;
        }
        else
        {
          lBound = dps_[k - 1];
          if (!Double.IsNaN(maturityStrikes[k]))
          {
            Interp interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const);
            double interpolated_Dps = 0;
            List<double> maturityStrikesClean = new List<double>();
            List<double> dpsClean = new List<double>();
            for (int i = 0; i < maturityStrikes.Length; i++)
              if (!Double.IsNaN(maturityStrikes[i]))
              {
                maturityStrikesClean.Add(maturityStrikes[i]);
                dpsClean.Add(dps_[i]);
              }
            double[] maturityStrikesClone = (double[])maturityStrikesClean.ToArray();
            double[] dpsClone = (double[])dpsClean.ToArray();
            if (DecreasingStrikeMethod(strikeMethod))
            {
              Array.Sort(maturityStrikesClone, dpsClone);
              if(maturityStrikesClone.Length < 3)
                interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);
              Interpolator interpolator = new Interpolator(interp, maturityStrikesClone, dpsClone);
              interpolated_Dps = interpolator.evaluate(bespokeStrike);
            }
            else
            {
              Interpolator interpolator = new Interpolator(interp, maturityStrikes, dps_);
              interpolated_Dps = interpolator.evaluate(bespokeStrike);
            }
            double dist_1 = Math.Abs(interpolated_Dps - lBound), dist_2 = Math.Abs(interpolated_Dps - dps_[k]);
            uBound = dist_1 < dist_2 ? interpolated_Dps + dist_1 : dps_[k];
            if (uBound < dps_[k])
              if ((fn.evaluate(uBound) - bespokeStrike) * (fn.evaluate(lBound) - bespokeStrike) > 0)
                uBound = dps_[k];
          }
          else
          {
            if (strikeMethod == BaseCorrelationStrikeMethod.ExpectedLoss ||
               strikeMethod == BaseCorrelationStrikeMethod.ExpectedLossPV ||
               strikeMethod == BaseCorrelationStrikeMethod.Unscaled)
              uBound = 0.9999;
            else
            {
              // Calculate the maximum dps that still result in good 
              // strike and then set it to be the upper bound
              bool suceed = false;
              double lower = dps_[k - 1];
              double upper = 1.0;
              double mid = (lower + upper) / 2.0;
              while (!suceed)
              {
                InitializePricer(mid);
                double resStrike = Double.NaN;
                try
                {
                  resStrike = BaseCorrelation.Strike(
                  new SyntheticCDOPricer[] { bespokeMaturityPricer_ },
                  baseCorrelation_.BaseCorrelations[0].StrikeMethod,
                  null)[0];
                  suceed = true;
                }
                catch (Exception e)
                {
                  suceed = false;
                  logger.DebugFormat(e.Message);
                }
                finally
                {
                  upper = mid;
                  mid = (lower + upper) / 2.0;
                }
              }
              uBound = mid;
            }
          }
        }

        if (uBound < lBound)
        {
          double temp = lBound;
          lBound = uBound;
          uBound = temp;
        }

        if (lBound < 1e-8)
          if (DecreasingStrikeMethod(strikeMethod))
            lBound = 3e-3;
        return;
      }

      public override double evaluate(double x)
      {
        InitializePricer(x);
        double strike = Double.NaN;
        bool ok = true;
        try
        {
          strike = BaseCorrelation.Strike(new SyntheticCDOPricer[] { bespokeMaturityPricer_ },
            baseCorrelation_.BaseCorrelations[0].StrikeMethod, null)[0];
        }
        catch (Exception e)
        {
          ok = false;
          logger.Debug(e.Message);
        }
        finally
        {
          if (!ok)
            strike = Double.NaN;
        }
        return strike;
      }

      private static double theInterpolatedCorrelation_;
      private static SyntheticCDOPricer pricer_;
      private static SyntheticCDOPricer bespokeMaturityPricer_;
      private static BaseCorrelationTermStruct baseCorrelation_;
      private static Dt[] tenorDates_ = null;
      private static Dt theMaturity_ = Dt.Empty;
      private static double[] dps_ = null;
      private static double[,] strikeMatrix = null;
      private static SurvivalCurve[] survivalCurves_ = null;
      const double ToleranceX = 1E-3;
      const double ToleranceF = 1E-3;
      const int MaxIterations = 40;
    }
    #endregion IndexStrikeEvaluator

    
  } // class BaseCorrelation

}
