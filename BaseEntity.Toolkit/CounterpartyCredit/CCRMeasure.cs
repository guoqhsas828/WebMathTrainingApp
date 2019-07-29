/*
 * CCRMeasure.cs
 *
 *   2010. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Counterparty credit risk measures
  /// </summary>
  public enum CCRMeasure
  {
    /// <summary>
    /// <para>
    /// Counterparty Value Adjustment (CVA) is the expected discounted loss due to counter party default
    /// over the life of a transaction or longest transaction in the portfolio. When applicable, it is 
    /// calculated after taking into account wrong way risk, netting agreements and collateral.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process at time <m>t</m>. Denote the counter
    /// party default time and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// </para>
    /// <para>
    /// For unilateral case, in the event that the counter party defaults at time <m>\tau_c = t</m>, 
    /// the booking entity recovers a fraction <m>R_c</m> of the exposure <m>X(t) = \max \{PV(t), 0 \}</m>. Thus the unilateral 
    /// CVA is obtained by the following formula:
    /// <math env="align*">
    ///  \text{Unilateral} \ \ CVA = - (1 - R_c) \int_0^T DiscountedEE(t) \ \mathbb{P}(\tau_c = t), 
    /// </math>
    /// where the discounted expected exposure <m>DiscountedEE(t) = \mathbb{E}\left[D(t)X(t)|\tau_c = t \right]</m>.
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that the booking entity still survivals when counter party defaults. 
    /// Thus the bilateral CVA is obtained by the following formula:
    /// <math env="align*">
    /// \text{Bilateral} \ \ CVA = - (1 - R_c) \int_0^T DiscountedEE(t)\ \mathbb{P}(\tau_c = t, \tau_o > t),
    /// </math>
    /// where the discounted expected exposure <m>DiscountedEE(t) = \mathbb{E} \left[D(t)X(t)|\tau_c = t, \tau_o >t \right]</m>.
    /// </para>
    /// </summary>
    CVA,

    /// <summary>
    /// <para>
    /// Counterparty Value Adjustment without WWR (CVA0) is the expected discounted loss due to counter party default
    /// over the life of a transaction or longest transaction in the portfolio. Without wrong way risk, 
    /// the exposure and negative exposure are assumed uncorrelated with the counter party and booking entity credit quality.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process at time <m>t</m>. Denote the counter
    /// party default time and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// </para>
    /// <para>
    /// For unilateral case, in the event that the counter party defaults at time <m>\tau_c = t</m>, 
    /// the booking entity recovers a fraction <m>R_c</m> of the exposure <m>X(t) = \max \{PV(t), 0 \}</m>. Thus the unilateral 
    /// CVA is obtained by the following formula:
    /// <math env="align*">
    ///  \text{Unilateral} \ \ CVA0 = - (1 - R_c) \int_0^T DiscountedEE0(t) \ \mathbb{P}(\tau_c = t), 
    /// </math>
    /// where the discounted expected exposure without WWR <m>DiscountedEE0(t) = \mathbb{E}\left[D(t)X(t) \right]</m>.
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that the booking entity still survivals when counter party defaults. 
    /// Thus the bilateral CVA is obtained by the following formula:
    /// <math env="align*">
    /// \text{Bilateral} \ \ CVA0 = - (1 - R_c) \int_0^T DiscountedEE0(t)\ \mathbb{P}(\tau_c = t, \tau_o > t),
    /// </math>
    /// where the discounted expected exposure without WWR <m>DiscountedEE0(t) = \mathbb{E} \left[D(t)X(t) \right]</m>.
    /// </para>
    /// </summary>
    CVA0,

    /// <summary>
    /// <para>
    /// Debt Value Adjustment (DVA) is the expected discounted gain due to the booking entity default
    /// over the life of a transaction or longest transaction in the portfolio. When applicable, it is 
    /// calculated after taking into account wrong way risk, netting agreements and collateral.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process at time <m>t</m>. Denote the counter
    /// party default time and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// </para>
    /// <para>
    /// For unilateral case, in the event that the booking entity defaults at time <m>\tau_o = t</m>, 
    /// the counter party recovers a fraction <m>R_o</m> of the negative exposure <m>NX(t) = \min \{PV(t), 0\}</m>. 
    /// Thus the unilateral DVA is obtained by the following formula:
    /// </para>
    /// <math env="align*">
    /// \text{Unilateral} \ \ DVA = (1 - R_o) \int_0^T DiscountedNEE(t) \ \mathbb{P}(\tau_o = t),
    /// </math>
    /// where the discounted negative expected exposure <m>DiscountedNEE(t) = - \mathbb{E}\left[D(t)NX(t)|\tau_o = t \right]</m>.
    /// <para>
    /// For bilateral case, we take into account that the counter party still survivals when booking entity defaults. 
    /// Thus the bilateral DVA is obtained by the following formula:
    /// </para>
    /// <math env="align*">
    /// \text{Bilateral} \ \ DVA = (1 - R_o) \int_0^T DiscountedNEE(t)\ \mathbb{P}(\tau_o = t, \tau_c &gt; t),
    /// </math>
    /// where the discounted negative expected exposure <m>DiscountedNEE(t) = - \mathbb{E} \left[D(t)NX(t)|\tau_o = t, \tau_c &gt;t \right]</m>.
    /// </summary>
    DVA,

    /// <summary>
    /// <para>
    /// Debt Value Adjustment without Wrong Way Risk (DVA0) is the expected discounted gain due to the booking entity default
    /// over the life of a transaction or longest transaction in the portfolio. Without wrong way risk, 
    /// the exposure and negative exposure are assumed uncorrelated with the counter party and booking entity credit quality.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process at time <m>t</m>. Denote the counter
    /// party default time and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// </para>
    /// <para>
    /// For unilateral case, in the event that the booking entity defaults at time <m>\tau_o = t</m>, 
    /// the counter party recovers a fraction <m>R_o</m> of the negative exposure <m>NX(t) = \min \{PV(t), 0\}</m>. 
    /// Thus the unilateral DVA is obtained by the following formula:
    /// <math env="align*">
    ///  \text{Unilateral} \ \ DVA0 = - (1 - R_o) \int_0^T DiscountedNEE0(t) \ \mathbb{P}(\tau_o = t), 
    /// </math>
    /// where the discounted negative expected exposure without WWR <m>DiscountedNEE0(t) = - \mathbb{E}\left[D(t)NX(t) \right]</m>.
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that the counter party still survivals when booking entity defaults. 
    /// Thus the bilateral DVA is obtained by the following formula:
    /// <math env="align*">
    /// \text{Bilateral} \ \ DVA0 = - (1 - R_o) \int_0^T DiscountedNEE0(t)\ \mathbb{P}(\tau_o = t, \tau_c > t),
    /// </math>
    /// where the discounted negative expected exposure without WWR <m>DiscountedNEE0(t) = - \mathbb{E} \left[D(t)NX(t) \right]</m>.
    /// </para>
    /// </summary>
    DVA0,

    /// <summary>
    /// <para>
    /// Funding Value Adjustment (FVA) is defined as the present value of funding costs of hedging uncollateralized 
    /// derivative portfolio i.e. the cost to fund the collateral on a collateralized hedge to an uncollateralized
    /// trade over the life of the trade.
    /// </para>
    /// <para>
    /// FVA can be seen as composed of two elements: Funding Cost Adjustment (FCA) and Funding Benefit Adjustment (FBA).
    /// FCA is the cost to collateral posted on hedged uncollaeralized receivables (in-the-money derivatives). FBA is the 
    /// benefit due to holding collateral on hedged uncollateralized liabilities (out-of-money derivatives).
    /// </para>
    /// <math env="align*">
    /// FVA = FCA + FBA
    /// </math>
    /// </summary>
    FVA,

    /// <summary>
    /// <para>
    /// Funding Value Adjustment without Wrong Way Risk (FVA0) is defined as the present value of funding costs of hedging uncollateralized 
    /// derivative portfolio i.e. the cost to fund the collateral on a collateralized hedge to an uncollateralized
    /// trade over the life of the trade. Without wrong way risk, the exposure and negative exposure are assumed 
    /// uncorrelated with the counter party and booking entity credit quality.
    /// </para>
    /// <para>
    /// FVA0 can be seen as composed of two elements: Funding Cost Adjustment w/o WWR(FCA0) and Funding Benefit Adjustment w/o WWR(FBA0).
    /// FCA0 is the cost to collateral posted on hedged uncollaeralized receivables (in-the-money derivatives). FBA0 is the 
    /// benefit due to holding collateral on hedged uncollateralized liabilities (out-of-money derivatives).
    /// </para>
    /// <math env="align*">
    /// FVA0 = FCA0 + FBA0
    /// </math>
    /// </summary>
    FVA0,

    /// <summary>
    /// <para>
    /// Assume there is no default on both counter party and booking entity over the life of the portfolio.
    /// FVANoDefault is defined as the present value of funding costs 
    /// of hedging uncollateralized derivative portfolio i.e. the cost to fund the collateral on a collateralized hedge to an 
    /// uncollateralized trade over the life of the trade. 
    /// </para>
    /// <para>
    /// Funding Value Adjustment without default on both counter party and booking entity (FVANoDefault)
    /// can be seen as composed of two elements: Funding Cost Adjustment w/o both parties default(FCANoDefault) 
    /// and Funding Benefit Adjustment w/o both parties default(FBANoDefault).
    /// FCANoDefault is the cost to collateral posted on hedged uncollaeralized receivables (in-the-money derivatives). 
    /// FBANoDefault is the benefit due to holding collateral on hedged uncollateralized liabilities (out-of-money derivatives).
    /// </para>
    /// <math env="align*">
    /// FVANoDefault = FCANoDefault + FBANoDefault
    /// </math>
    /// </summary>
    FVANoDefault,

    /// <summary>
    /// <para>
    /// Funding Cost Adjustment (FCA) is the cost of funding the discounted expected exposure of the uncollateralized
    /// derivative portfolio, over the life of the portfolio, calculated at bank's borrowing spread <m>S_B</m>.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at 
    /// time <m>t</m>. Denote the counter party default time and booking entity default time by <m>\tau_c</m> and 
    /// <m>\tau_o</m> respectively. 
    /// </para>
    /// <para> 
    /// For unilateral case, this cost is calculated till booking entity defaults. 
    /// <math env="align*">
    ///  \text{Unilateral} \ \ FCA = - \int_0^T \mathbb{E} \left[ S_B(t)D(t)X(t) | \tau_o >t \right] \mathbb{P}(\tau_o > t)dt
    /// </math>
    /// </para> 
    /// <para>
    /// For bilateral case, this cost is calculated till counter party or booking entity default, whichever 
    /// comes first. 
    /// <math env="align*">
    ///  \text{Bilateral} \ \ FCA = - \int_0^T \mathbb{E} \left[ S_B(t)D(t)X(t) | \tau_o >t, \tau_c > t \right] \mathbb{P}(\tau_o > t, \tau_c > t)dt
    /// </math>
    /// </para>
    /// </summary>
    FCA,

    /// <summary>
    /// <para>
    /// Funding Cost Adjustment without Wrong Way Risk (FCA0) is the cost of funding the discounted expected exposure of the uncollateralized
    /// derivative portfolio, over the life of the portfolio, calculated at bank's borrowing spread <m>S_B</m>. Without
    /// WWR, the exposure and negative exposure are assumed uncorrelated with the counter party and booking entity credit quality.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at 
    /// time <m>t</m>. Denote the counter party default time and booking entity default time by <m>\tau_c</m> and 
    /// <m>\tau_o</m> respectively. 
    /// </para>
    /// <para> 
    /// For unilateral case, this cost is calculated till booking entity defaults. 
    /// <math env="align*">
    ///  \text{Unilateral} \ \ FCA0 = - \int_0^T \mathbb{E} \left[ S_B(t)D(t)X(t) \right] \mathbb{P}(\tau_o > t)dt
    /// </math>
    /// </para> 
    /// <para>
    /// For bilateral case, this cost is calculated till counter party or booking entity default, whichever 
    /// comes first. 
    /// <math env="align*">
    ///  \text{Bilateral} \ \ FCA0 = - \int_0^T \mathbb{E} \left[ S_B(t)D(t)X(t) \right] \mathbb{P}(\tau_o > t, \tau_c > t)dt
    /// </math>
    /// </para>
    /// </summary>
    FCA0,

    /// <summary>
    /// <para>
    /// Assume there is no default on both counter party and booking entity over the life of the portfolio.
    /// Funding Cost Adjustment without default on both counter party and booking entity (FCANoDefault)
    /// is the cost of funding the discounted expected exposure of the uncollateralized
    /// derivative portfolio, over the life of the portfolio, calculated at bank's borrowing spread <m>S_B</m>. 
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process and <m>E(t)</m> be the counterparty-level exposure at 
    /// time <m>t</m>. Denote the counter party default time and booking entity default time by <m>\tau_c</m> and 
    /// <m>\tau_o</m> respectively. Without default on both parties, <m>\mathbb{P}(\tau_o > t) \equiv 1</m> and 
    /// <m>\mathbb{P}(\tau_o > t, \tau_c > t) \equiv 1</m>, for any <m>0 \lt t \lt T</m>. 
    /// </para>
    /// <para> 
    /// This cost is calculated by the following formula:
    /// <math env="align*">
    ///  FCANoDefault = - \int_0^T \mathbb{E} \left[ S_B(t)D(t)E(t) \right] dt
    /// </math>
    /// </para> 
    /// </summary>
    FCANoDefault,

    /// <summary>
    /// <para>
    /// Funding Benefit Adjustment (FBA) is the funding benefit on the negative discounted expected exposure of the uncollateralized
    /// derivative portfolio, over the life of the portfolio, calculated at bank's lending spread <m>S_L</m>.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process and <m>NX(t) = \min \{PV(t), 0\}</m> be the negative exposure at 
    /// time <m>t</m>. Denote the counter party default time and booking entity default time by <m>\tau_c</m> and 
    /// <m>\tau_o</m> respectively. 
    /// </para>
    /// <para> 
    /// For unilateral case, this benefit is calculated till booking entity defaults. 
    /// <math env="align*">
    ///  \text{Unilateral} \ \ FBA = - \int_0^T \mathbb{E} \left[ S_L(t)D(t)NX(t) | \tau_o >t \right] \mathbb{P}(\tau_o > t)dt
    /// </math>
    /// </para> 
    /// <para>
    /// For bilateral case, this benefit is calculated till counter party or booking entity default, whichever 
    /// comes first. 
    /// <math env="align*">
    /// \text{Bilateral} \ \ FBA = - \int_0^T \mathbb{E} \left[ S_L(t)D(t)NX(t) | \tau_o >t, \tau_c > t \right] \mathbb{P}(\tau_o > t, \tau_c > t)dt
    /// </math>
    /// </para>
    /// </summary>
    FBA,

    /// <summary>
    /// <para>
    /// Funding Benefit Adjustment without Wrong Way Risk (FBA0) is the funding benefit on the negative discounted expected exposure of the uncollateralized
    /// derivative portfolio, over the life of the portfolio, calculated at bank's lending spread <m>S_L</m>. Without
    /// WWR, the exposure and negative exposure are assumed uncorrelated with the counter party and booking entity credit quality.
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process and <m>NX(t) = \min \{PV(t), 0\}</m> be the negative exposure at 
    /// time <m>t</m>. Denote the counter party default time and booking entity default time by <m>\tau_c</m> and 
    /// <m>\tau_o</m> respectively. 
    /// </para>
    /// <para> 
    /// For unilateral case, this benefit is calculated till booking entity defaults. 
    /// <math env="align*">
    ///  \text{Unilateral} \ \ FBA0 = - \int_0^T \mathbb{E} \left[ S_L(t)D(t)NX(t) \right] \mathbb{P}(\tau_o > t)dt
    /// </math>
    /// </para> 
    /// <para>
    /// For bilateral case, this benefit is calculated till counter party or booking entity default, whichever 
    /// comes first. 
    /// <math env="align*">
    ///  \text{Bilateral} \ \ FBA0 = - \int_0^T \mathbb{E} \left[ S_L(t)D(t)NX(t) \right] \mathbb{P}(\tau_o > t, \tau_c > t)dt
    /// </math>
    /// </para>
    /// </summary>
    FBA0,

    /// <summary>
    /// <para>
    /// Assume there is no default on both counter party and booking entity over the life of the portfolio.
    /// Funding Benefit Adjustment without default on both counter party and booking entity (FBANoDefault)
    /// is the funding benefit on the negative discounted expected exposure of the uncollateralized
    /// derivative portfolio, over the life of the portfolio, calculated at bank's lending spread <m>S_L</m>. 
    /// </para>
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor process and <m>NX(t) = \min \{PV(t), 0\}</m> be the exposure at 
    /// time <m>t</m>. Denote the counter party default time and booking entity default time by <m>\tau_c</m> and 
    /// <m>\tau_o</m> respectively. Without default on both parties, <m>\mathbb{P}(\tau_o > t) \equiv 1</m> and 
    /// <m>\mathbb{P}(\tau_o > t, \tau_c > t) \equiv 1</m>, for any <m>0 \lt t \lt T</m>. 
    /// </para>
    /// <para> 
    /// This benefit is calculated by the following formula:
    /// <math env="align*">
    ///  FBANoDefault = - \int_0^T \mathbb{E} \left[ S_L(t)D(t)NX(t) \right] dt
    /// </math>
    /// </para> 
    /// </summary>
    FBANoDefault,

    /// <summary>
    /// <para>
    /// The CVA Theta is defined as the difference between the CVA at current time <m> 0</m>, and the CVA at the specified future pricing date <m>\Delta t</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let <m>R_c</m> be the couter party recovery rate and 
    /// let <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at time <m>t</m>. 
    /// </para>
    /// <para>
    /// For unilateral case, the CVA Theta is obtained by the following formula:
    /// <math env="align*">
    ///  \text{Unilateral CVA Theta} = \text{CVA}_{\Delta t} - \text{CVA}_0 = (1 - R_c) \int_0^{\Delta t} DiscountedEE(t) \ \mathbb{P}(\tau_c = t), 
    /// </math>
    /// where the discounted expected exposure <m>DiscountedEE(t) = \mathbb{E}\left[D(t)X(t)|\tau_c = t \right]</m>.
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that the booking entity still survivals when counter party defaults. 
    /// Thus the bilateral CVA Theta is obtained by the following formula:
    /// <math env="align*">
    /// \text{Bilateral CVA Theta} = \text{CVA}_{\Delta t} - \text{CVA}_0 = (1 - R_c) \int_0^{\Delta t} DiscountedEE(t)\ \mathbb{P}(\tau_c = t, \tau_o > t),
    /// </math>
    /// where the discounted expected exposure <m>DiscountedEE(t) = \mathbb{E} \left[D(t)X(t)|\tau_c = t, \tau_o >t \right]</m>.
    /// </para>
    /// </summary>
    CVATheta,

    /// <summary>
    /// <para>
    /// The DVA Theta is defined as the difference between the DVA at current time <m>0</m>, and the DVA at the specified future pricing date <m>\Delta t</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let <m>R_o</m> be the booking entity recovery rate and 
    /// let <m>NX(t) = \min \{PV(t), 0 \}</m> be the negative exposure at time <m>t</m>. 
    /// </para>
    /// <para>
    /// For unilateral case, the DVA Theta is obtained by the following formula:
    /// <math env="align*">
    ///  \text{Unilateral DVA Theta} = \text{DVA}_{\Delta t} - \text{DVA}_0 = - (1 - R_o) \int_0^{\Delta t} DiscountedNEE(t) \ \mathbb{P}(\tau_o = t), 
    /// </math>
    /// where the negative discounted expected exposure <m>DiscountedNEE(t) = - \mathbb{E}\left[D(t)Nx(t)|\tau_o = t \right]</m>.
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that the counter party still survivals when booking entity defaults. 
    /// Thus the bilateral DVA Theta is obtained by the following formula:
    /// <math env="align*">
    /// \text{Bilateral DVA Theta} = \text{DVA}_{\Delta t} - \text{DVA}_0 = - (1 - R_o) \int_0^{\Delta t} DiscountedNEE(t)\ \mathbb{P}(\tau_o = t, \tau_c > t),
    /// </math>
    /// where the negative discounted expected exposure <m>DiscountedNEE(t) = - \mathbb{E} \left[D(t)NX(t)|\tau_o = t, \tau_c >t \right]</m>.
    /// </para>
    /// </summary>
    DVATheta,

    /// <summary>
    /// <para>
    /// The FCA Theta is defined as the difference between the FCA at current time <m>0</m>, and the FCA at the specified future pricing date <m>\Delta t</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let <m>S_B</m> be the bank's borrowing spread,
    /// <m>D(t)</m> be the stochastic discount factor and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at time <m>t</m>. 
    /// </para>
    /// <para>
    /// For unilateral case, the FCA Theta is obtained by the following formula:
    /// <math env="align*">
    ///  \text{Unilateral FCA Theta} = \text{FCA}_{\Delta t} - \text{FCA}_0 = \int_0^{\Delta t} \mathbb{E} \left[ S_B(t) D(t)X(t) | \tau_o > t \right] \ \mathbb{P}(\tau_o > t) dt, 
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that both the counter party and the booking entity survivals. 
    /// Thus the bilateral FCA Theta is obtained by the following formula:
    /// <math env="align*">
    /// \text{Bilateral FCA Theta} = \text{FCA}_{\Delta t} - \text{FCA}_0 = \int_0^{\Delta t} \mathbb{E} \left[ S_B(t) D(t)X(t) | \tau_o > t, \tau_c > t \right] \ \mathbb{P}(\tau_o > t, \tau_c > t) dt, 
    /// </math>
    /// </para>
    /// </summary>
    FCATheta,

    /// <summary>
    /// <para>
    /// The FBA Theta is defined as the difference between the FBA at current time <m>0</m>, and the FBA at the specified future pricing date <m>\Delta t</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let <m>S_L</m> be the bank's lending spread,
    /// <m>D(t)</m> be the stochastic discount factor and <m>NX(t) = \min \{PV(t), 0 \}</m> be the negative exposure at time <m>t</m>. 
    /// </para>
    /// <para>
    /// For unilateral case, the FBA Theta is obtained by the following formula:
    /// <math env="align*">
    ///  \text{Unilateral FBA Theta} = \text{FBA}_{\Delta t} - \text{FBA}_0 = \int_0^{\Delta t} \mathbb{E} \left[ S_L(t) D(t)NX(t) | \tau_o > t \right] \ \mathbb{P}(\tau_o > t) dt, 
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that both the counter party and the booking entity survivals. 
    /// Thus the bilateral FBA Theta is obtained by the following formula:
    /// <math env="align*">
    /// \text{Bilateral FBA Theta} = \text{FBA}_{\Delta t} - \text{FBA}_0 = \int_0^{\Delta t} \mathbb{E} \left[ S_L(t) D(t)NX(t) | \tau_o > t, \tau_c > t \right] \ \mathbb{P}(\tau_o > t, \tau_c > t) dt, 
    /// </math>
    /// </para>
    /// </summary>
    FBATheta,

    /// <summary>
    /// <para>
    /// The FVA Theta is defined as the difference between the FVA at current time <m>0</m>, and the FVA at the specified future pricing date <m>\Delta t</m>.
    /// </para>
    /// <para>
    /// Since FVA is defined as the sum of FCA and FBA, the FVA Theta is obtained by
    /// <math env="align*">
    ///  \text{Unilateral FVA Theta} = \text{Unilateral FCA Theta} + \text{Unilateral FBA Theta}
    /// </math>
    /// <math env="align*">
    ///  \text{Bilateral FVA Theta} = \text{Bilateral FCA Theta} + \text{Bilateral FBA Theta}
    /// </math>
    /// </para>
    /// </summary>
    FVATheta,

    /// <summary>
    /// The Portfolio Current Exposure (CE) is the same as the Undiscounted Portfolio Forward Value (EPV).
    /// </summary>
    CE,

    /// <summary>
    /// The Discounted CE is the same as the Discounted Portfolio Forward Value (DiscountedEPV).
    /// </summary>
    DiscountedCE,

    /// <summary>
    /// The Undiscounted Expected Exposure (EE) is defined as Discounted Expected Exposure <m>DiscountedEE</m> divided by spot discount factor.
    /// In other words, it is the expectation of exposure under time-<m>t</m> conditional forward measure. 
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at time <m>t</m>. Then the Undiscounted
    /// Expected Exposure is obtained by the following formula:
    /// </para>
    /// <para>
    /// For unilateral case, we only take into account the default of the counter party.
    /// <math env="align*">
    /// \text{Unilateral} \ \ EE(t) = \frac{\mathbb{E} \left[ D(t)X(t) | \tau_c = t \right]}{\mathbb{E}\left[ D(t) | \tau_c = t \right]},
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that the booking entity still survivals when counter party defaults. 
    /// <math env="align*">
    /// \text{Bilateral} \ \ EE(t) = \frac{\mathbb{E} \left[ D(t)X(t) | \tau_c = t, \tau_o > t \right]}{\mathbb{E}\left[ D(t) | \tau_c = t, \tau_o > t \right]},
    /// </math>
    /// </para>
    /// </summary>
    EE,

    /// <summary>
    /// <para>
    /// The Discounted Expected Exposure (DiscountedEE) is the (risk-neutral) expectation of exposure <m>X(t)</m> multiplied along the path 
    /// by stochastic discount factor <m>D(t)</m>. 
    /// </para>
    /// <para>
    /// In unilateral case, this expectation is conditional on event of counter party default.
    /// <math env="align*">
    /// \text{Unilateral} \ \ DiscountedEE(t) = \mathbb{E} \left[ D(t)X(t) | \tau_c = t \right]
    /// </math>
    /// </para>
    /// <para>
    /// In bilateral case, this expectation is conditional on event of counter party default and booking entity survival.
    /// <math env="align*">
    /// \text{Bilateral} \ \ DiscountedEE(t) = \mathbb{E} \left[ D(t)X(t) | \tau_c = t, \tau_o > t \right]
    /// </math>
    /// </para>
    /// </summary>
    DiscountedEE, 

    /// <summary>
    /// The Undiscounted Expected Exposure without WWR (EE0) is defined as Discounted Expected Exposure without WWR 
    /// <m>DiscountedEE0</m> divided by spot discount factor. In other words, it is the expectation of exposure under 
    /// time-<m>t</m> forward measure. 
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>X(t)</m> be the exposure at time <m>t</m>. Then the Undiscounted
    /// Expected Exposure without WWR is obtained by the following formula:
    /// <math env="align*">
    /// EE0(t) = \frac{\mathbb{E} \left[ D(t)X(t) \right]}{\mathbb{E}\left[ D(t) \right]},
    /// </math>
    /// </para>
    /// </summary>
    EE0,

    /// <summary>
    /// <para>
    /// The Discounted Expected Exposure without Wrong Way Risk (DiscountedEE0) is the 
    /// (risk-neutral) expectation of exposure <m>X(t)</m> multiplied along the path 
    /// by stochastic discount factor <m>D(t)</m>. Without WWR, the exposure and negative exposure is assumed uncorrelated 
    /// with the counter party and booking entity credit quality.
    /// </para>
    /// <para>
    /// The Dndiscounted Expected Exposure without WWR is obtained by the following formula:
    /// <math env="align*">
    /// DiscountedEE0(t) = \mathbb{E} \left[ D(t)X(t) \right]
    /// </math>
    /// </para>
    /// </summary>
    DiscountedEE0,

    /// <summary>
    /// The Undiscounted Negative Expected Exposure (NEE) is defined as Discounted Negative Expected Exposure <m>DiscountedNEE</m> divided by spot discount factor.
    /// In other words, it is the expectation of negative exposure under time-<m>t</m> conditional forward measure. 
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>NX(t) = \min \{PV(t), 0 \}</m> be the negative exposure 
    /// at time <m>t</m>. Then NEE is obtained by the following formula:
    /// </para>
    /// <para>
    /// For unilateral case, we only take into account the default of the booking entity.
    /// <math env="align*">
    /// \text{Unilateral} \ \ NEE(t) = - \frac{\mathbb{E} \left[ D(t)NX(t) | \tau_o = t \right]}{\mathbb{E}\left[ D(t) | \tau_o = t \right]},
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case, we take into account that the counter party still survivals when booking entity defaults. 
    /// <math env="align*">
    /// \text{Bilateral} \ \ NEE(t) = - \frac{\mathbb{E} \left[ D(t)NX(t) | \tau_o = t, \tau_c > t \right]}{\mathbb{E}\left[ D(t) | \tau_o = t, \tau_c > t \right]},
    /// </math>
    /// </para>
    /// </summary>
    NEE,

    /// <summary>
    /// <para>
    /// The Discounted Negative Expected Exposure (DiscountedNEE) is the (risk-neutral) expectation 
    /// of negative exposure <m>NX(t) = \min \{PV(t), 0 \}</m>
    /// multiplied along the path by stochastic discount factor <m>D(t)</m>. 
    /// </para>
    /// <para>
    /// In unilateral case, this expectation is conditional on event of booking entity default.
    /// <math env="align*">
    /// \text{Unilateral} \ \ DiscountedNEE(t) = - \mathbb{E} \left[ D(t)NX(t) | \tau_o = t \right]
    /// </math>
    /// </para>
    /// <para>
    /// In bilateral case, this expectation is conditional on event of booking entity default and counter party survival.
    /// <math env="align*">
    /// \text{Bilateral} \ \ DiscountedNEE(t) = - \mathbb{E} \left[ D(t)NX(t) | \tau_o = t, \tau_c > t \right]
    /// </math>
    /// </para>
    /// </summary>
    DiscountedNEE,

    /// <summary>
    /// The Undiscounted Negative Expected Exposure without WWR (NEE0) is defined as Discounted Negative Expected Exposure 
    /// without WWR <m>DiscountedNEE0</m> divided by spot discount factor. In other words, it is the expectation of exposure 
    /// under time-<m>t</m> forward measure. 
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>NX(t) = \min \{PV(t), 0 \}</m> be the negative exposure at time <m>t</m>. 
    /// Then the Undiscounted Negative Expected Exposure without WWR is obtained by the following formula:
    /// <math env="align*">
    /// NEE0(t) = - \frac{\mathbb{E} \left[ D(t)NX(t) \right]}{\mathbb{E}\left[ D(t) \right]}.
    /// </math>
    /// </para>
    /// </summary>
    NEE0,

    /// <summary>
    /// <para>
    /// The Discounted Negative Expected Exposure without Wrong Way Risk (DiscountedNEE0)
    /// is the (risk-neutral) expectation of exposure <m>NX(t) = \min \{PV(t), 0 \}</m>
    /// multiplied along the path by stochastic discount factor <m>D(t)</m>. Without WWR, the exposure and negative exposure are
    /// assumed uncorrelated with the counter party and booking party credit quality.
    /// </para>
    /// <para>
    /// The Undiscounted Negative Expected Exposure without WWR is obtained by the following formula:
    /// <math env="align*">
    /// DiscountedNEE0(t) = - \mathbb{E} \left[ D(t)NX(t) \right]
    /// </math>
    /// </para>
    /// </summary>
    DiscountedNEE0,

    /// <summary>
    /// <para>
    /// The Expected Positive Exposure (EPE) at date <m>t</m> is the time weighted average of Undiscounted Expected Exposure 
    /// (<m>EE</m>) that occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
    /// all the exposure dates prior to <m>t</m>, and <m>t_n = t</m>. Then EPE is obtained by the following formula:
    /// <math env="align*">
    /// EPE(t) = \frac{\sum_{i=1}^n EE(t_i) \Delta t_i}{T},
    /// </math>
    /// where <m>\Delta t_i = t_i - t_{i-1}</m> and <m>T = t_n - t_0</m>.
    /// </para>
    /// </summary>
    EPE,

        /// <summary>
        /// <para>
        /// The Expected Positive Exposure without WWR (EPE0) at date <m>t</m> is the time weighted average of Undiscounted Expected Exposure 
        /// without WWR (<m>EE0^U</m>) that occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
        /// </para>
        /// <para>
        /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
        /// all the exposure dates prior to <m>t</m>, and <m>t_n = t</m>. Then EPE0 is obtained by the following formula:
        /// <math env="align*">
        /// EPE(t) = \frac{\sum_{i=1}^n EE0^U(t_i) \Delta t_i}{T},
        /// </math>
        /// where <m>\Delta t_i = t_i - t_{i-1}</m> and <m>T = t_n - t_0</m>.
        /// </para>
        /// </summary>
        EPE0,

        /// <summary>
        /// <para>
        /// The Effective Expected Exposure (EEE) at date <m>t</m> is the maximum Undiscounted Expected Exposure (<m>EE</m>) that 
        /// occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
        /// </para>
        /// <para>
        /// Suppose <m>t_1, \cdots, t_{n-1}</m> are all the exposure dates prior to <m>t</m>, and denote <m>t_n = t</m>. 
        /// Then EEE is obtained by the following formula:
        /// <math env="align*">
        /// EEE(t) = \max_{1 \leq i \leq n} EE(t_i)
        /// </math>
        /// </para>
        /// Alternatively, it may be defined by induction as the greater of the <m>EE</m> at that date, or the <m>EEE</m> at the previous date.
        /// <math env="align*">
        /// EEE(t_k) = \max \{EE(t_k), EEE(t_{k-1})\} \ \ \ \ \text{and}\ \ \ \ \ EEE(t_0) = 0.
        /// </math>
        /// </summary>
        EEE,

        /// <summary>
        /// <para>
        /// The Effective Expected Exposure without WWR (EEE0) at date <m>t</m> is the maximum Undiscounted Expected Exposure without WWR (<m>EE0</m>) that 
        /// occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
        /// </para>
        /// <para>
        /// Suppose <m>t_1, \cdots, t_{n-1}</m> are all the exposure dates prior to <m>t</m>, and denote <m>t_n = t</m>. 
        /// Then EEE0 is obtained by the following formula:
        /// <math env="align*">
        /// EEE0(t) = \max_{1 \leq i \leq n} EE0(t_i)
        /// </math>
        /// </para>
        /// Alternatively, it may be defined by induction as the greater of the <m>EE0</m> at that date, or the <m>EEE0</m> at the previous date.
        /// <math env="align*">
        /// EEE0(t_k) = \max \{EE0(t_k), EEE0(t_{k-1})\} \ \ \ \ \text{and}\ \ \ \ \ EEE0(t_0) = 0.
        /// </math>
        /// </summary>
        EEE0,

        /// <summary>
        /// <para>
        /// The Effective Expected Positive Exposure (EEPE) is the time weighted average of Effective Expected Exposure (<m>EEE</m>)
        /// that occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
        /// </para>
        /// <para>
        /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
        /// all the exposure dates prior to <m>t</m>, and <m>t_n = t</m>. Then EEPE is obtained by the following formula:
        /// <math env="align*">
        /// EEPE(t) = \frac{\sum_{i=1}^n EEE(t_i) \Delta t_i}{T},
        /// </math>
        /// where <m>\Delta t_i = t_i - t_{i-1}</m> and <m>T = t_n - t_0</m>.
        /// </para>
        /// </summary>
        EEPE,

        /// <summary>
        /// <para>
        /// The Effective Expected Positive Exposure without WWR (EEPE0) is the time weighted average of Effective Expected Exposure
        /// without WWR (<m>EEE0</m>) that occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
        /// </para>
        /// <para>
        /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
        /// all the exposure dates prior to <m>t</m>, and <m>t_n = t</m>. Then EEPE0 is obtained by the following formula:
        /// <math env="align*">
        /// EEPE0(t) = \frac{\sum_{i=1}^n EEE0(t_i) \Delta t_i}{T},
        /// </math>
        /// where <m>\Delta t_i = t_i - t_{i-1}</m> and <m>T = t_n - t_0</m>.
        /// </para>
        /// </summary>
        EEPE0,

        /// <summary>
        /// <para>
        /// The Expected Negative Exposure (ENE) at date <m>t</m> is the time weighted average of Undiscounted Negative Expected Exposure 
        /// (<m>NEE</m>) that occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
        /// </para>
        /// <para>
        /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
        /// all the exposure dates prior to <m>t</m>, and <m>t_n = t</m>. Then ENE is obtained by the following formula:
        /// <math env="align*">
        /// ENE(t) = \frac{\sum_{i=1}^n NEE(t_i) \Delta t_i}{T},
        /// </math>
        /// where <m>\Delta t_i = t_i - t_{i-1}</m> and <m>T = t_n - t_0</m>.
        /// </para>
        /// </summary>
        ENE,

        /// <summary>
        /// <para>
        /// The Expected Negative Exposure without WWR (ENE0) at date <m>t</m> is the time weighted average of Undiscounted Negative Expected Exposure 
        /// without WWR (<m>NEE0</m>) that occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
        /// </para>
        /// <para>
        /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
        /// all the exposure dates prior to <m>t</m>, and <m>t_n = t</m>. Then ENE0 is obtained by the following formula:
        /// <math env="align*">
        /// ENE0(t) = \frac{\sum_{i=1}^n NEE0(t_i) \Delta t_i}{T},
        /// </math>
        /// where <m>\Delta t_i = t_i - t_{i-1}</m> and <m>T = t_n - t_0</m>.
        /// </para>
        /// </summary>
        ENE0,

        /// <summary>
        /// The Undiscounted Potential Future Exposure (PFE) is a quantile of exposure under conditional forward measure.
        /// <example>
        /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
        /// <m>D(t)</m> be the stochastic discount factor and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at time <m>t</m>. 
        /// Then a 95% PFE is the level of potential exposure (under conditional forward measure) that is exceeded with only 5% probability.
        /// <para>
        /// For unilateral case,
        /// <math env="align*">
        /// \ \ \mathbb{E} \left[\mathbb{1}_{\{X(t) \lt PFE(t)\}}\cdot D(t) /Z(t) | \tau_c = t \right] = 0.95,
        /// </math>
        /// where <m>Z(t) = \mathbb{E} \left[D(t) | \tau_c = t \right].</m>
        /// </para>
        /// <para>
        /// For bilateral case,
        /// <math env="align*">
        /// \ \ \mathbb{E} \left[\mathbb{1}_{\{X(t) \lt PFE(t)\}}\cdot D(t) /Z(t) | \tau_c = t, \tau_o >t \right] = 0.95,
        /// </math>
        /// where <m>Z(t) = \mathbb{E} \left[D(t) | \tau_c = t, \tau_o > t \right].</m>
        /// </para>
        /// </example>
        /// </summary>
        PFE,

    /// <summary>
    /// The Undiscounted Potential Future Exposure without WWR (PFE0) is a quantile of exposure under conditional forward measure. Without
    /// WWR, the exposure and negative exposure are assumed uncorrelated with the counter party and booking entity credit quality.
    /// <example>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at time <m>t</m>. 
    /// Then a 95% PFE is the level of potential exposure (under conditional forward measure) that is exceeded with only 5% probability.
    /// <para>
    /// <math env="align*">
    /// \ \ \mathbb{E} \left[\mathbb{1}_{\{X(t) \lt PFE0(t)\}}\cdot D(t) /Z(t) \right] = 0.95,
    /// </math>
    /// where <m>Z(t) = \mathbb{E} \left[D(t) \right].</m>
    /// </para>
    /// </example>
    /// </summary>
    PFE0,

    /// <summary>
    /// The Discounted Potential Future Exposure (DiscountedPFE) is a quantile of discounted exposure under conditional risk-neutral measure. 
    /// <example>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at time <m>t</m>. 
    /// Then a 95% DiscountedPFE is the level of potential discounted exposure (under risk-neutral measure) that is 
    /// exceeded with only 5% probability.
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \ \ \mathbb{P} \left\{X(t)D(t) \lt DiscountedPFE(t) | \tau_c = t \right\} = 0.95.
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \ \ \mathbb{P} \left\{X(t)D(t) \lt DiscountedPFE(t) | \tau_c = t, \tau_o >t \right\} = 0.95.
    /// </math>
    /// </para>
    /// </example>
    /// </summary>
    DiscountedPFE,

    /// <summary>
    /// The Discounted Potential Future Exposure without WWR (DiscountedPFE0) is a quantile of discounted exposure under conditional risk-neutral measure. 
    /// Without WWR, the exposure and negative exposure are assumed uncorrelated with the counter party and booking entity credit quality. 
    /// <example>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>X(t) = \max \{PV(t), 0 \}</m> be the exposure at time <m>t</m>. 
    /// Then a 95% DiscountedPFE0 is the level of potential discounted exposure (under risk-neutral measure) that is exceeded with only 5% probability.
    /// <para>
    /// <math env="align*">
    /// \ \ \mathbb{P} \left\{X(t)D(t) \lt DiscountedPFE0(t) \right\} = 0.95,
    /// </math>
    /// </para>
    /// </example>
    /// </summary>
    DiscountedPFE0,

    /// <summary>
    /// <para>
    /// The Maximum Potential Future Exposure (MPFE) is the maximum PFE at certain confidence level over all time buckets.
    /// </para>
    /// Denote the index set of the time buckets by <m>I</m>, i.e. each time in the buckets can be written with <m>t_i</m>, where <m>i \in I</m>. 
    /// Then the MPFE is obtained by the following formula:
    /// <math env="align*">
    /// MPFE = \max_{i \in I} PFE(t_i)
    /// </math>
    /// </summary>
    MPFE,

    /// <summary>
    /// The Undiscounted Potential Future Negative Exposure (PFNE) is a quantile of negative exposure 
    /// (in terms of absolute value) under conditional forward measure.
    /// <example>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>NX(t) = \min \{PV(t), 0 \}</m> be the negative exposure at time <m>t</m>. 
    /// Then a 95% PFNE is the level of potential negative exposure (under conditional forward measure) that is exceeded (in terms of
    /// absolute value) with only 5% probability.
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \ \ \mathbb{E} \left[\mathbb{1}_{\{-NX(t) \lt PFNE(t)\}}\cdot D(t) /Z(t) | \tau_o = t \right] = 0.95,
    /// </math>
    /// where <m>Z(t) = \mathbb{E} \left[D(t) | \tau_o = t \right].</m>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \ \ \mathbb{E} \left[\mathbb{1}_{\{-NX(t) \lt PFNE(t)\}}\cdot D(t) /Z(t) | \tau_o = t, \tau_c >t \right] = 0.95,
    /// </math>
    /// where <m>Z(t) = \mathbb{E} \left[D(t) | \tau_o = t, \tau_c > t \right].</m>
    /// </para>
    /// </example>
    /// </summary>
    PFNE,

    /// <summary>
    /// The Discounted Potential Future Negative Exposure (DiscountedPFNE) is a quantile of discounted negative exposure
    /// (in terms of absolute value) under conditional risk-neutral measure. 
    /// <example>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let 
    /// <m>D(t)</m> be the stochastic discount factor and <m>NX(t) = \min \{PV(t), 0 \}</m> be the negative exposure at time <m>t</m>. 
    /// Then a 95% DiscountedPFNE is the level of potential discounted negative exposure (under risk-neutral measure) that is 
    /// exceeded with only 5% probability.
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \ \ \mathbb{P} \left\{-NX(t)D(t) \lt DiscountedPFNE(t) | \tau_o = t \right\} = 0.95.
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \ \ \mathbb{P} \left\{-NX(t)D(t) \lt DiscountedPFNE(t) | \tau_o = t, \tau_c >t \right\} = 0.95.
    /// </math>
    /// </para>
    /// </example>
    /// </summary>
    DiscountedPFNE,

    /// <summary>
    /// <para>
    /// The Maximum Potential Future Negative Exposure (MPFNE) is the maximum PFNE at certain confidence level over all time buckets.
    /// </para>
    /// Denote the index set of the time buckets by <m>I</m>, i.e. each time in the buckets can be written with <m>t_i</m>, where <m>i \in I</m>. 
    /// Then the MPFNE is obtained by the following formula:
    /// <math env="align*">
    /// MPFE = \max_{i \in I} PFNE(t_i)
    /// </math>
    /// </summary>
    MPFNE,

    /// <summary>
    /// The Potential Future Credit Support Amount (PFCSA) is the maximum amount of collateral expected to be posted 
    /// by the counter party on a future date with a high degree of statistical confidence. Given certain condifence 
    /// level, the PFCSA is the difference between uncollateralized PFE and collateralized PFE.
    /// </summary>
    PFCSA,

    /// <summary>
    /// The Potential Future Negative Credit Support Amount (PFCSA) is the maximum amount of collateral expected to be posted 
    /// to the counter party on a future date with a high degree of statistical confidence. Given certain condifence 
    /// level, the PFNCSA is the difference between uncollateralized PFNE and collateralized PFNE. 
    /// </summary>
    PFNCSA,
    
    /// <summary>
    /// The Economic Capital (EC) is the unexpected discounted loss at a specified confidence level, e.g., 99%, over the life of 
    /// the transaction or longest transaction in the portfolio.
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \text{Unilateral} \ \ EC = - (1 - R_c) \int_0^T (DiscountedPFE(t) - DiscountedEE(t)) \mathbb{P}(\tau_c = t)
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \text{Bilateral}\ \ EC = - (1 - R_c) \int_0^T (DiscountedPFE(t) - DiscountedEE(t)) \mathbb{P}(\tau_c = t, \tau_o \gt t)
    /// </math>
    /// </para>
    /// </summary>
    EC,

    /// <summary>
    /// The Economic Capital without WWR (EC0) is the unexpected discounted loss at a specified confidence level, e.g., 99%, over the life of 
    /// the transaction or longest transaction in the portfolio. Without WWR, the exposure and negative exposure is assumed uncorrelated with 
    /// the counter party and booking entity credit quality.
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \text{Unilateral} \ \ EC0 = - (1 - R_c) \int_0^T (DiscountedPFE0(t) - DiscountedEE0(t)) \mathbb{P}(\tau_c = t)
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \text{Bilateral}\ \ EC0 = - (1 - R_c) \int_0^T (DiscountedPFE0(t) - DiscountedEE0(t)) \mathbb{P}(\tau_c = t, \tau_o \gt t)
    /// </math>
    /// </para>
    /// </summary>
    EC0,


    /// <summary>
    /// Sigma is the same as SigmaDiscountedEE.
    /// </summary>
    Sigma,

    /// <summary>
    /// The Standard Deviation of the DiscountedEE on a given date <m>t</m>. 
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \text{Unilateral} \ \ SigmaDiscountedEE(t) = \mathbb{E} \left[\Big(D(t)X(t)\Big)^2 \Big| \tau_c = t\right] - \Big(\mathbb{E} \left[D(t)X(t) \Big| \tau_c = t\right]\Big)^2
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \text{Bilateral}\ \ SigmaDiscountedEE(t) = \mathbb{E} \left[\Big(D(t)X(t)\Big)^2 \Big| \tau_c = t, \tau_o > t\right] - \Big(\mathbb{E} \left[D(t)X(t) \Big| \tau_c = t, \tau_o > t\right]\Big)^2
    /// </math>
    /// </para>
    /// </summary>
    SigmaDiscountedEE,

    /// <summary>
    /// The Standard Deviation of the DiscountedNEE on a given date <m>t</m>. 
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \text{Unilateral} \ \ SigmaDiscountedNEE(t) = \mathbb{E} \left[\Big(D(t)NX(t)\Big)^2 \Big| \tau_o = t\right] - \Big(\mathbb{E} \left[D(t)NX(t) \Big| \tau_o = t\right]\Big)^2
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \text{Bilateral}\ \ SigmaDiscountedNEE(t) = \mathbb{E} \left[\Big(D(t)NX(t)\Big)^2 \Big| \tau_o = t, \tau_c > t\right] - \Big(\mathbb{E} \left[D(t)NX(t) \Big| \tau_o = t, \tau_c > t\right]\Big)^2
    /// </math>
    /// </para>
    /// </summary>
    SigmaDiscountedNEE,

    /// <summary>
    /// The Standard Deviation of the NEE on a given date <m>t</m>. 
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \text{Unilateral} \ \ SigmaNEE(t) = \frac{\mathbb{E} \Big [D(t) NX(t)^2 \Big | \tau_o = t \Big] }{ \mathbb{E} \Big [ D(t) \Big | \tau_o = t \Big ]} 
    /// - \left( \frac{\mathbb{E} \Big[D(t) NX(t) \Big | \tau_o = t \Big] }{ \mathbb{E} \Big[D(t) \Big | \tau_o = t \Big]} \right)^2
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \text{Bilateral}\ \ SigmaNEE(t) = \frac{\mathbb{E} \Big [D(t) NX(t)^2 \Big | \tau_o = t, 
    /// \tau_c > t \Big] }{ \mathbb{E} \Big [ D(t) \Big | \tau_o = t, \tau_c > t \Big ]} 
    /// - \left( \frac{\mathbb{E} \Big[D(t) NX(t) \Big | \tau_o = t, \tau_c > t \Big] }{ \mathbb{E} \Big[D(t) \Big | \tau_o = t, \tau_c > t \Big]} \right)^2
    /// </math>
    /// </para>
    /// </summary>
    SigmaNEE,

    /// <summary>
    /// The Standard Deviation of the EE on a given date <m>t</m>. 
    /// <para>
    /// For unilateral case,
    /// <math env="align*">
    /// \text{Unilateral} \ \ SigmaEE(t) = \frac{\mathbb{E} \Big [D(t) X(t)^2 \Big | \tau_c = t \Big] }{ \mathbb{E} \Big [ D(t) \Big | \tau_c = t \Big ]} 
    /// - \left( \frac{\mathbb{E} \Big[D(t) X(t) \Big | \tau_c = t \Big] }{ \mathbb{E} \Big[D(t) \Big | \tau_c = t \Big]} \right)^2
    /// </math>
    /// </para>
    /// <para>
    /// For bilateral case,
    /// <math env="align*">
    /// \text{Bilateral}\ \ SigmaEE(t) = \frac{\mathbb{E} \Big [D(t) X(t)^2 \Big | \tau_c = t, 
    /// \tau_o > t \Big] }{ \mathbb{E} \Big [ D(t) \Big | \tau_c = t, \tau_o > t \Big ]} 
    /// - \left( \frac{\mathbb{E} \Big[D(t) X(t) \Big | \tau_c = t, \tau_o > t \Big] }{ \mathbb{E} \Big[D(t) \Big | \tau_c = t, \tau_o > t \Big]} \right)^2
    /// </math>
    /// </para>
    /// </summary>
    SigmaEE,


    /// <summary>
    /// The Standard Error of EE on a given date <m>t</m>, expressed as a percentage of EE(t).
    /// <para>
    /// Denote the number of paths by <m>N</m>, then
    /// <math env="align*">
    /// \text{Standard Error of EE(t)} = \frac{SigmaEE(t)}{EE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </para>
    /// </summary>
    StdErrEE,

    /// <summary>
    /// The Standard Error of DiscountedEE on a given date <m>t</m>, expressed as a percentage of DiscountedEE(t). 
    /// <para>
    /// Denote the number of paths by <m>N</m>, then
    /// <math env="align*">
    /// \text{Standard Error of DiscountedEE(t)} = \frac{SigmaDiscountedEE(t)}{DiscountedEE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </para>
    /// </summary>
    StdErrDiscountedEE,

    /// <summary>
    /// The Standard Error of NEE on a given date <m>t</m>, expressed as a percentage of NEE(t). 
    /// <para>
    /// Denote the number of paths by <m>N</m>, then
    /// <math env="align*">
    /// \text{Standard Error of NEE(t)} = \frac{SigmaNEE(t)}{NEE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </para>
    /// </summary>
    StdErrNEE,

    /// <summary>
    /// The Standard Error of DiscountedNEE on a given date <m>t</m>, expressed as a percentage of DiscountedNEE(t).
    /// <para>
    /// Denote the number of paths by <m>N</m>, then
    /// <math env="align*">
    /// \text{Standard Error of DiscountedNEE(t)} = \frac{SigmaDiscountedNEE(t)}{DiscountedNEE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </para>
    /// </summary>
    StdErrDiscountedNEE,

    /// <summary>
    /// <para>The counterparty default Radon-Nikodym process.</para>
    /// <para>
    /// CptyRn is the Radon Nikodym change of measure from the probability conditional on counterparty default at time <m>t</m> to the unconditional probability. 
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_n</m> are 
    /// all the exposure dates. Denote the counerparty default Radon-Nikodym process at time <m>t_i</m> by <m>CptyRn(i)</m>.
    /// </para>
    /// For unilateral case,
    /// <math env="align*">
    /// CptyRn(i) &amp; := \frac{\mathbb{Q}(\tau_C \leq t_i \big | \mathcal{F}_i) - \mathbb{Q}(\tau_C \leq t_{i-1} \big | \mathcal{F}_i)}{S_C(t_i) - S_C(t_{i-1})} \\\\
    /// </math>
    /// For bilateral case:
    /// <math env="align*">
    /// CptyRn(i) &amp; := \frac{\frac{1}{2} \cdot  \mathbb{Q} \left(t_i \geq \tau_C \gt t_{i-1}, \tau_O \gt t_{i-1} \big | \mathcal{F}_i \right)  + 
    /// \frac{1}{2} \cdot \mathbb{Q} \left(t_i \geq \tau_C \gt t_{i-1}, \tau_O \gt t_i \big | \mathcal{F}_i \right)}
    /// {\frac{1}{2} \cdot  \mathbb{Q} \left(t_i \geq \tau_C \gt t_{i-1}, \tau_O \gt t_{i-1} \right)  + 
    /// \frac{1}{2} \cdot \mathbb{Q} \left(t_i \geq \tau_C \gt t_{i-1}, \tau_O \gt t_i \right)} \\\\
    /// </math>
    /// </summary>
    CptyRn,

    /// <summary>
    /// <para>The booking entity default Radon-Nikodym process.</para>
    /// <para>
    /// OwnRn is the Radon Nikodym change of measure from the probability conditional on booking entity default at time <m>t</m> to the unconditional probability.  
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_n</m> are 
    /// all the exposure dates. Denote the counerparty default Radon-Nikodym process at time <m>t_i</m> by <m>CptyRn(i)</m>.
    /// </para>
    /// For unilateral case,
    /// <math env="align*">
    /// OwnRn(i) &amp; := \frac{\mathbb{Q}(\tau_O \leq t_i \big | \mathcal{F}_i) - \mathbb{Q}(\tau_O \leq t_{i-1} \big | \mathcal{F}_i)}{S_O(t_i) - S_O(t_{i-1})} 
    /// </math>
    /// For bilateral case:
    /// <math env="align*">
    /// OwnRn(i) &amp; := \frac{\frac{1}{2} \cdot  \mathbb{Q} \left(\tau_C \gt t_{i-1}, t_i \geq \tau_O \gt t_{i-1} \big | \mathcal{F}_i \right )  + 
    /// \frac{1}{2} \cdot  \mathbb{Q} \left(\tau_C \gt t_i, t_i \geq \tau_O \gt t_{i-1} \big | \mathcal{F}_i \right) }
    /// {\frac{1}{2} \cdot  \mathbb{Q} \left(\tau_C \gt t_{i-1}, t_i \geq \tau_O \gt t_{i-1} \right )  + 
    /// \frac{1}{2} \cdot  \mathbb{Q} \left(\tau_C \gt t_i, t_i \geq \tau_O \gt t_{i-1} \right)} \\\\
    /// </math>
    /// </summary>
    OwnRn,

    /// <summary>
    /// <para>The booking entity survival Radon-Nikodym process.</para>
    /// <para>
    /// FundingRn is the Radon Nikodym change of measure from the probability conditional on booking entity survival at time <m>t</m> to the unconditional probability.  
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_n</m> are 
    /// all the exposure dates. Denote the counerparty default Radon-Nikodym process at time <m>t_i</m> by <m>CptyRn(i)</m>.
    /// </para>
    /// For unilateral case,
    /// <math env="align*">
    /// FundingRn(i) &amp; :=  \frac{\mathbb{Q}(\tau_O \gt t_i \big | \mathcal{F}_i) + \mathbb{Q}(\tau_O \gt t_{i-1} \big | \mathcal{F}_i)}{\big(1- S_O(t_i)\big) + \big(1 - S_O(t_{i-1})\big)}
    /// </math>
    /// For bilateral case:
    /// <math env="align*">
    /// FundingRn(i) &amp; := \frac{\frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_{i-1}, \tau_O \gt t_{i-1} \big | \mathcal{F}_i \right) + \frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_i, \tau_O \gt t_{i-1} \big | \mathcal{F}_i \right) 
    /// + \frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_{i-1}, \tau_O \gt t_i \big | \mathcal{F}_i \right) + \frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_i, \tau_O \gt t_i \big | \mathcal{F}_i \right) }
    /// {\frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_{i-1}, \tau_O \gt t_{i-1} \right) + \frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_i, \tau_O \gt t_{i-1} \right) 
    /// + \frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_{i-1}, \tau_O \gt t_i \right) + \frac{1}{4} \cdot \mathbb{Q} \left(\tau_C \gt t_i, \tau_O \gt t_i \right)}
    /// </math>
    /// </summary>
    FundingRn,

    /// <summary>
    /// Zero Radon Nikodym Derivative, which is always 1. It indicates that case that there is no Wrong-Way risk.
    /// </summary>
    ZeroRn,

    /// <summary>
    /// Conditional PV (Mu)
    /// </summary>
    Mu,

    /// <summary>
    /// <para>
    /// The Effective Maturity under IMM Approach is defined as the time-weighted Discounted Expected Exposure 
    /// (calculations start at one year point and go out until Maturity) divided by the time-weighted Effective 
    /// DiscountedEE (calculations start at the reporting date and go to the shorter of residual residual maturity
    /// and one year.)
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
    /// all the exposure dates prior to <m>t</m>, and <m>t_n</m> is the maturity date. Then Effective Maturity is 
    /// obtained by the following formula:
    /// <math env="align*">
    /// Effective Maturity = 1 + \frac{\sum_{t_k \geq 1 \text{year}}^{maturity} DiscountedEE(t_k) \Delta t_k}{\sum_{k=1}^{t_k \leq 1 \text{year}} Effective DiscountedEE(t_k) \Delta t_k},
    /// </math>
    /// where <m>\Delta t_k = t_k - t_{k-1}</m>, and the <m>Effective DiscountedEE(t)</m> is the maximum Discounted Expected Exposure (<m>DiscountedEE</m>) that 
    /// occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
    /// </para>
    /// </summary>
    EffectiveMaturity,

    /// <summary>
    /// <para>
    /// The Effective Maturity under IMM Approach without WWR (EffectiveMaturity0) is defined as the time-weighted DiscountedEE0 
    /// (calculations start at one year point and go out until Maturity) divided by the time-weighted Effective 
    /// DiscountedEE0 (calculations start at the reporting date and go to the shorter of residual residual maturity
    /// and one year.)
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_{n-1}</m> are 
    /// all the exposure dates prior to <m>t</m>, and <m>t_n</m> is the maturity date. Then EffectiveMaturity0 is 
    /// obtained by the following formula:
    /// <math env="align*">
    /// EffectiveMaturity0 = 1 + \frac{\sum_{t_k \geq 1 \text{year}}^{maturity} DiscountedEE0(t_k) \Delta t_k}{\sum_{k=1}^{t_k \leq 1 \text{year}} EffectiveDiscountedEE0(t_k) \Delta t_k},
    /// </math>
    /// where <m>\Delta t_k = t_k - t_{k-1}</m>, and the <m>EffectiveDiscountedEE0(t)</m> is the maximum <m>DiscountedEE0</m> that 
    /// occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
    /// </para>
    /// </summary>
    EffectiveMaturity0,

    /// <summary>
    /// <para>
    /// Under the Advanced Internal Ratings Based Approach, the Risk Weighted Assets (RWA) is calculated for each
    /// transaction or netting set using the following formula:
    /// <math env="align*">
    /// RWA = \text{OutstandingEAD} \cdot 12.5 \cdot K,
    /// </math>
    /// where <m>OutstandingEAD = EAD - CVA</m>, therefore computation of the CVA should be precede the computation
    /// of the default risk charge. 
    /// </para>
    /// <para>
    /// According to Internal Model Method (IMM), the Exposure at Default (EAD) is EEPE multiplied by factor <m>\alpha</m>
    /// to compensate for inaccuracies in the model and adjust for a "bad state" of the economy. This factor <m>\alpha</m>
    /// is equal to <m>1.4</m> now and is subject to change from regulator, it also floors at <m>1.2</m>.
    /// <math env="align*">
    /// EAD_{IMM} = \max (EEPE, EEPE_{stressed}) \cdot \alpha
    /// </math>
    /// </para>
    /// <para>
    /// The capital requirement K is obtained by the following formula:
    /// <math env="align*">
    /// K = (1-R_c) \cdot \left ( N\left [\frac{N^{-1}(PD)}{\sqrt{1-R}} + \sqrt{\frac{R}{1-R}} \cdot N^{-1}(0.999) \right ]- PD \right ) \cdot \frac{1 + (M - 2.5) \cdot b}{1 - 1.5 \cdot b},
    /// </math>
    /// where <m>R</m> is the correlation factor based on default probability <m>PD</m>:
    /// <math env="align*">
    /// R = 0.12 \times (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) + 0.24 \times \left [1 - (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) \right ]
    /// </math>
    /// and <m>b</m> is a maturity adjustment factor based on PD:
    /// <math env="align*">
    /// b = (0.11852 - 0.05478 \times \ln (PD))^2
    /// </math>
    /// and <m>M</m> is effective maturity.
    /// </para>
    /// </summary>
    RWA,

    /// <summary>
    /// Risk Weighted Assets with no Wrong Way Risk (RWA0)
    /// </summary>
    RWA0,

        /// <summary>
        /// <para>
        /// The Exposure at Default (EAD) is defined as EEPE (over the first year) multiplied by factor <m>\alpha</m> to compensate for inaccuracies in the model and adjust for a "bad state" of the economy. 
        /// This factor <m>\alpha</m> is equal to <m>1.4</m> now and is subject to change from regulator, it also floors at <m>1.2</m>.
        /// <math env = "align*">
        /// EAD = EEPE \cdot \alpha
        /// </math>
        /// </para>
        /// </summary>
        EAD,

        /// <summary>
        /// <para>
        /// The Exposure at Default without WWR (EAD0) is defined as EEPE0 (over the first year) multiplied by factor <m>\alpha</m> to compensate for inaccuracies in the model and adjust for a "bad state" of the economy. 
        /// This factor <m>\alpha</m> is equal to <m>1.4</m> now and is subject to change from regulator, it also floors at <m>1.2</m>.
        /// <math env = "align*">
        /// EAD0 = EEPE0 \cdot \alpha
        /// </math>
        /// </para>
        /// </summary>
        EAD0,

        /// <summary>
        /// <para>
        /// The Capital Requirement K is obtained by the following formula:
        /// <math env="align*">
        /// K = (1-R_c) \cdot \left ( N\left [\frac{N^{-1}(PD)}{\sqrt{1-R}} + \sqrt{\frac{R}{1-R}} \cdot N^{-1}(0.999) \right ]- PD \right ) \cdot \frac{1 + (M - 2.5) \cdot b}{1 - 1.5 \cdot b},
        /// </math>
        /// where <m>R</m> is the correlation factor based on default probability <m>PD</m>:
        /// <math env="align*">
        /// R = 0.12 \times (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) + 0.24 \times \left [1 - (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) \right ]
        /// </math>
        /// and <m>b</m> is a maturity adjustment factor based on PD:
        /// <math env="align*">
        /// b = (0.11852 - 0.05478 \times \ln (PD))^2
        /// </math>
        /// and <m>M</m> is effective maturity.
        /// </para>
        /// </summary>
        CapitalRequirement,

        /// <summary>
        /// <para>
        /// The Capital Requirement K0 without WWR is obtained by the following formula:
        /// <math env="align*">
        /// K0 = (1-R_c) \cdot \left ( N\left [\frac{N^{-1}(PD)}{\sqrt{1-R}} + \sqrt{\frac{R}{1-R}} \cdot N^{-1}(0.999) \right ]- PD \right ) \cdot \frac{1 + (M0 - 2.5) \cdot b}{1 - 1.5 \cdot b},
        /// </math>
        /// where <m>R</m> is the correlation factor based on default probability <m>PD</m>:
        /// <math env="align*">
        /// R = 0.12 \times (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) + 0.24 \times \left [1 - (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) \right ]
        /// </math>
        /// and <m>b</m> is a maturity adjustment factor based on PD:
        /// <math env="align*">
        /// b = (0.11852 - 0.05478 \times \ln (PD))^2
        /// </math>
        /// and <m>M0</m> is effective maturity without WWR.
        /// </para>
        /// </summary>
        CapitalRequirement0,

    /// <summary>
    /// <para>
    /// BucketedCVA CVA on the event that counter party default occurs in bucket containing date 
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_n</m> are 
    /// all the exposure dates. For any given date <m>t</m>, The BucketedCVA at date <m>t</m> is defined as the CVA on 
    /// the event that counter party default occurs in time bucket <m>[t_i, t_{i+1})</m> such that <m>t_i \leq t \lt t_{i+1}</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let <m>D(t)</m> 
    /// be the stochastic discount factor process at time <m>t</m>, <m>R_c</m> be the counter party recovery rate,
    /// and <m>X(t) = \max \{PV(t), 0\}</m> be the exposure. Then the BucketedCVA is obtained by the following formula:
    /// </para>
    /// <math env="align*">
    ///  \text{Unilateral BucketedCVA(t)} = - (1 - R_c) \int_{t_i}^{t_{i+1}} DiscountedEE(t) \ \mathbb{P}(\tau_c = t), 
    /// </math>
    /// where the discounted expected exposure <m>DiscountedEE(t) = \mathbb{E}\left[D(t)X(t)|\tau_c = t \right]</m>.
    /// <math env="align*">
    /// \text{Bilateral BucketedCVA(t)} = - (1 - R_c) \int_{t_i}^{t_{i+1}} DiscountedEE(t)\ \mathbb{P}(\tau_c = t, \tau_o > t),
    /// </math>
    /// where the discounted expected exposure <m>DiscountedEE(t) = \mathbb{E} \left[D(t)X(t)|\tau_c = t, \tau_o >t \right]</m>.
    /// </summary>
    BucketedCVA,

    /// <summary>
    /// <para>
    /// BucketedCVA0 is the CVA0 on the event that counter party default occurs in bucket containing date.
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_n</m> are 
    /// all the exposure dates. For any given date <m>t</m>, The BucketedCVA0 at date <m>t</m> is defined as the CVA0 on 
    /// the event that counter party default occurs in time bucket <m>[t_i, t_{i+1})</m> such that <m>t_i \leq t \lt t_{i+1}</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. Let <m>D(t)</m> 
    /// be the stochastic discount factor process at time <m>t</m>, <m>R_c</m> be the counter party recovery rate,
    /// and <m>X(t) = \max \{PV(t), 0\}</m> be the exposure. Then the BucketedCVA is obtained by the following formula:
    /// </para>
    /// <math env="align*">
    ///  \text{Unilateral BucketedCVA0(t)} = - (1 - R_c) \int_{t_i}^{t_{i+1}} DiscountedEE0(t) \ \mathbb{P}(\tau_c = t), 
    /// </math>
    /// where the discounted expected exposure without WWR <m>DiscountedEE0(t) = \mathbb{E}\left[D(t)X(t) \right]</m>.
    /// <math env="align*">
    /// \text{Bilateral BucketedCVA0(t)} = - (1 - R_c) \int_{t_i}^{t_{i+1}} DiscountedEE0(t)\ \mathbb{P}(\tau_c = t, \tau_o > t),
    /// </math>
    /// where the discounted expected exposure without WWR <m>DiscountedEE0(t) = \mathbb{E} \left[D(t)X(t) \right]</m>.
    /// </summary>
    BucketedCVA0,

    /// <summary>
    /// <para>
    /// BucketedDVA is the DVA on the event that booking entity default occurs in bucket containing date 
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_n</m> are 
    /// all the exposure dates. For any given date <m>t</m>, The BucketedDVA at date <m>t</m> is defined as the DVA on 
    /// the event that booking entity default occurs in time bucket <m>[t_i, t_{i+1})</m> such that <m>t_i \leq t \lt t_{i+1}</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// Let <m>D(t)</m> be the stochastic discount factor process at time <m>t</m>, <m>R_o</m> be the booking entity recovery rate,
    /// and <m>NX(t) = \min \{PV(t), 0\}</m> be the negative exposure.
    /// Then the BucketedDVA is obtained by the following formula:
    /// </para>   
    /// <math env="align*">
    /// \text{Unilateral BucketedDVA} = (1 - R_o) \int_{t_i}^{t_{i+1}} DiscountedNEE(t) \ \mathbb{P}(\tau_o = t),
    /// </math>
    /// where the discounted negative expected exposure <m>DiscountedNEE(t) = - \mathbb{E}\left[D(t)NX(t)|\tau_o = t \right]</m>.
    /// <math env="align*">
    /// \text{Bilateral BucketedDVA} = (1 - R_o) \int_{t_i}^{t_{i+1}} DiscountedNEE(t)\ \mathbb{P}(\tau_o = t, \tau_c > t),
    /// </math>
    /// where the discounted negative expected exposure <m>DiscountedNEE(t) = - \mathbb{E} \left[D(t)NX(t)|\tau_o = t, \tau_c > t \right]</m>.
    /// </summary>
    BucketedDVA,

    /// <summary>
    /// <para>
    /// BucketedDVA0 is the DVA0 on the event that booking entity default occurs in bucket containing date. 
    /// </para>
    /// <para>
    /// Suppose <m>t_0 \lt t_1 \lt \cdots \lt t_n</m>, where <m>t_0</m> is the as of date, <m>t_1, \cdots, t_n</m> are 
    /// all the exposure dates. For any given date <m>t</m>, The BucketedDVA0 at date <m>t</m> is defined as the DVA0 on 
    /// the event that booking entity default occurs in time bucket <m>[t_i, t_{i+1})</m> such that <m>t_i \leq t \lt t_{i+1}</m>.
    /// </para>
    /// <para>
    /// Denote the counter party and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// Let <m>D(t)</m> be the stochastic discount factor process at time <m>t</m>, <m>R_o</m> be the booking entity recovery rate,
    /// and <m>NX(t) = \min \{PV(t), 0\}</m> be the negative exposure.
    /// Then the BucketedDVA0 is obtained by the following formula:
    /// </para>   
    /// <math env="align*">
    /// \text{Unilateral BucketedDVA0} = (1 - R_o) \int_{t_i}^{t_{i+1}} DiscountedNEE0(t) \ \mathbb{P}(\tau_o = t),
    /// </math>
    /// where the discounted negative expected exposure without WWR <m>DiscountedNEE0(t) = - \mathbb{E}\left[D(t)NX(t) \right]</m>.
    /// <math env="align*">
    /// \text{Bilateral BucketedDVA0} = (1 - R_o) \int_{t_i}^{t_{i+1}} DiscountedNEE0(t)\ \mathbb{P}(\tau_o = t, \tau_c > t),
    /// </math>
    /// where the discounted negative expected exposure without WWR <m>DiscountedNEE0(t) = - \mathbb{E} \left[D(t)NX(t) \right]</m>.
    /// </summary>
    BucketedDVA0,

    /// <summary>
    /// The Undiscounted Portfolio Forward Value (EPV) is the expectation of portfolio present value under 
    /// time-<m>t</m> forward measure without WWR.
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor and <m>PV(t)</m> be the portfolio present value at time <m>t</m>. 
    /// Then EPV is obtained by the following formula:
    /// <math env="align*">
    /// EPV(t) = \frac{\mathbb{E} \left[ D(t)PV(t) \right]}{\mathbb{E}\left[ D(t) \right]},
    /// </math>
    /// </para>
    /// </summary>
    EPV,

    /// <summary>
    /// The Discounted Portfolio Forward Value (DiscountedEPV) is the (risk-neutral) expectation of discounted portfolio 
    /// present value without WWR.
    /// <para>
    /// Let <m>D(t)</m> be the stochastic discount factor and <m>PV(t)</m> be the portfolio present value at time <m>t</m>. 
    /// Then DiscountedEPV is obtained by the following formula:
    /// <math env="align*">
    /// DiscountedEPV(t) = \mathbb{E} \left[ D(t)PV(t) \right].
    /// </math>
    /// </para>
    /// </summary>
    DiscountedEPV,

    /// <summary>
    /// EAD from forward start date
    /// </summary>
    ForwardEAD,

    /// <summary>
    /// EAD from forward start date, without WWR
    /// </summary>
    ForwardEAD0
  }
}
