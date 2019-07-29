//
// BaseCorrelationStrikeMethod.cs
//   2005-2014. All rights reserved.
//

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	/// Specifies the method for calculating base correlations strikes.
	/// </summary>
	/// <remarks>
  /// <para>In  Toolkit, the correlation smile is conceptually expressed as a function, <formula inline="true">c = f(x)</formula>, 
  /// where <formula inline="true">c</formula> is the base correlation of a first loss tranche with detachment <formula inline="true">d</formula>
  /// (or the tranche correlation of a senior <formula inline="true">[d-100]</formula> tranche for the senior spread method only), 
  /// <formula inline="true">x</formula> is some transformation of the detachment(attachment for senior spread method only) <formula inline="true">d</formula>, 
  /// that is, <formula inline="true">x = x(d)</formula>. 
  /// Here the variable <formula inline="true">x</formula> is called the 'strike' and the function <formula inline="true">x(d)</formula> is
  /// called the 'strike method' or 'mapping method'.  Currently seven strike or mapping methods are available.</para>
	/// </remarks>
  /// <seealso cref="BaseCorrelation">Base Correlation Overview</seealso>
	public enum BaseCorrelationStrikeMethod
	{
		/// <summary>
		/// Unscaled detachment point strikes
		/// <para>Sometimes it is convenient to work with base correlations
		/// directly associated with their detachment points. This method
		/// provides that flexibility.</para>
    /// <formula>x = d</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    /// </list>
    /// </summary>
		Unscaled,

		/// <summary>
		/// Expected Loss method or Moneyness
		/// <para>The expected loss method was originally published by JP Morgan
		/// and was the initial market standard.  Also known as "Moneyness".</para>
    /// <formula>x = d / s</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    ///   <item><term><formula inline="true">s</formula> is the ratio
    ///   of expected basket losses over the portfolio notional</term></item>
    /// </list>
    /// </summary>
		ExpectedLoss,

    /// <summary>
    /// Expected Loss PV method or Moneyness Pv
		/// <para>This is a variation of the Expected Loss method where the present
		/// value of the exected loss is used.</para>
    /// <formula>x = d / s</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    ///   <item><term><formula inline="true">s</formula> is the ratio of protection PV
    ///     on the whole portfolio over the portfolio notional</term></item>
    /// </list>
		/// </summary>
		ExpectedLossPV,

    /// <summary>
    /// Expected Loss Ratio method
		/// <para>The strike points are calculated as the ratios of the equity tranche losses to 
		/// the whole basket losses with the corresponding base correlations on the original portfolio.
		/// To interpolate correlations on a new portfolio, both the strike and the corresponding correlation
		/// are calculated on the new portfolio.</para>
    /// <formula>x = r</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    ///   <item><term><formula inline="true">r</formula> is the ratio
    ///     of the expected loss of the equity tranche at detachment <formula inline="true">d</formula> over
    ///     the expected loss of the whole portfolio</term></item>
    /// </list>
    /// </summary>
		ExpectedLossRatio,

    /// <summary>
    /// Expected Loss Ratio Pv method
		/// <para>The strike points are calculated as the ratios of the equity tranche protection pvs to 
		/// the whole basket protection pv with the corresponding base correlations on the original portfolio.
		/// To interpolate correlations on a new portfolio, both the strike and the corresponding correlation
		/// are calculated on the new portfolio.</para>
    /// <formula>x = r</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    ///   <item><term><formula inline="true">r</formula> is the ratio
    ///     of the protection pv of the equity tranche at detachment <formula inline="true">d</formula> over
    ///     the protection pv of the whole portfolio</term></item>
    /// </list>
    /// </summary>
    ExpectedLossPvRatio,

    /// <summary>
    /// Probability method
		/// <para>The strike points are calculated as the probability that the portfolio loss
		/// does not exceed the detachment point.  The strikes are calculated based on 
		/// the original portfolio.
		/// To interpolate correlations on a new portfolio, both the strike and the corresponding correlation
		/// are calculated on the new portfolio.</para>
    /// <formula>x = p</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    ///   <item><term><formula inline="true">p</formula> is the
    ///     cumulative probability that the percentage of portfolio loss
    ///     <formula inline="true">L</formula> does not exceed the detachment
    ///     <formula inline="true">d</formula>, <formula inline="true">p = \mathrm{Prob}[L \leq d]</formula></term></item>
    /// </list>
    /// </summary>
		Probability,

    /// <summary>
    /// Equity spread method
		/// <para>The base correlation curve is built from strikes calculated as spreads of equity tranches, 
    /// and corresponding base correlations on the original (index) portfolio. To interpolate correlations  
    /// on a new (bespoke) portfolio, both the strike and the corresponding correlation
    /// are calculated on the new portfolio.</para>
    /// <formula>x = s</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    ///   <item><term><formula inline="true">s</formula> is the spread of the corresponding
    ///   first loss tranche at detachment <formula inline="true">d</formula></term></item>
    /// </list>
    /// </summary>
		EquitySpread,

    /// <summary>
    /// Senior Spread method
    /// <para>The base correlation curve is build from strikes calculated as spreads of senior [d-100] tranches, 
    /// and corresponding senior tranche correlations on the original (index) portfolio. To interpolate correlations  
    /// on a new (bespoke) portfolio, both the strike and the corresponding senior tranche correlation
    /// are calculated on the new portfolio.</para>
    /// <formula>x = s</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">d</formula> is the detachment</term></item>
    ///   <item><term><formula inline="true">s</formula> is the spread of the corresponding senior
    ///     <formula inline="true">[d-100]</formula> tranche</term></item>
    /// </list>
    /// </summary>
		SeniorSpread,

    /// <summary>
    /// Equity Protection method
    /// <para>The strikes are the expected cumulative losses on the equity tranches as the proportions of 
    /// the tranche notionals.</para>
    /// <formula>x = l</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">l</formula> is the the expected loss on the
    ///     equity tranche as the proportion of the tranche notional</term></item>
    /// </list>
    /// </summary>
    EquityProtection,

    /// <summary>
    /// Equity Protection Pv method
    /// <para>The strikes are the protection pvs on the equity tranches as the proportions of 
    /// the tranche notionals.</para>
    /// <formula>x = v</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">v</formula> is the the protection pv on the
    ///     equity tranche as the proportion of the tranche notional</term></item>
    /// </list>
    /// </summary>
    EquityProtectionPv,

    /// <summary>
    /// Protection method
    /// <para>The strikes are the expected cumulative losses on the base tranches as the proportions of 
    /// the whole portfolio notionals.</para>
    /// <formula>x = L</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">L</formula> is the the expected loss on the
    ///     equity tranche as the proportion of the basket notional</term></item>
    /// </list>
    /// </summary>
    Protection,

    /// <summary>
    /// Protection Pv method
    /// <para>The strikes are the protection pvs on the base tranches as the proportions of 
    /// the whole portfolio notionals.</para>
    /// <formula>x = V</formula>
    /// <para>where:</para>
    /// <list type="bullet">
    ///   <item><term><formula inline="true">x</formula> is called the 'strike'</term></item>
    ///   <item><term><formula inline="true">V</formula> is the the protection pv on the first
    ///     loss tranche as the proportion of the basket notional</term></item>
    /// </list>
    /// </summary>
    ProtectionPv,

    /// <summary>
    /// User defined mapping method
    /// <para>User implements the interface IStrikeEvaluator and passes an instance of it
    /// to the base correlation constructor.</para> 
    /// </summary>
    UserDefined,

    /// <summary>
    /// Expected loss forward
    /// <para>Same as ExpectedLoss if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
    ExpectedLossForward,
    
    /// <summary>
    /// Expected loss ratio forward
    /// <para>Same as ExpectedLossRatio if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment</para>
    /// </summary>
    ExpectedLossRatioForward,
    
    /// <summary>
    /// Protection forward
    /// <para>Same as Protection if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
    ProtectionForward,
    
    /// <summary>
    /// Equity protection forward
    /// <para>Same as EquityProtection if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
    EquityProtectionForward,

    /// <summary>
    /// Expected loss pv forward
    /// <para>Same as Expected Loss PV if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
		ExpectedLossPVForward,

    /// <summary>
    /// Protection pv forward
    /// <para>Same as ProtectionPv if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
    ProtectionPvForward,

    /// <summary>
    /// Equity protection PV forward
    /// <para>Same as EquityProtection if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
    EquityProtectionPvForward,

    /// <summary>
    /// Expected loss pv ratio foward
    /// <para>Same as ExpectedLossRatio if there are no defaults.
    /// If the portfolio contains defaulted names, strike is computed as if there were
    /// no defaults on the reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
    ExpectedLossPvRatioForward,

    /// <summary>
    /// Unscaled Forward
    /// <para>Same as Unscaled if there are no defaults. If the portfolio contains
    /// defaulted name, strike is computed as if there were no defaults on the 
    /// reduced basket for the adjusted tranche detachment.</para>
    /// </summary>
    UnscaledForward
  }
}
