/*
 * ConvertibleBondIntersectingTreesModel.cs
 *
 */
using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  ///   Intersecting (conditional) binomial tree based convertible bond model
  ///   The stock process (whose driving Brownian motion is correlated to the Brownian 
  ///   motion of Black Karasinski model) is constructed as a square block of prices
  /// </summary>
  /// <remarks>
  /// <para>A correlated two-factor model for pricing convertible bonds.</para>
  /// <para>The interest rate <formula inline="true"> r(t) </formula> and equity process
  /// <formula inline="true"> S(t) </formula> are modeled by the following coupled stochastic differential
  /// equations (SDEs):</para>
  /// <formula>
  /// \left\{\begin{matrix} \frac{dS}{S}=(r-q)dt+\sigma_sdW_s  \\  d\text{ln}r=\kappa( \sigma(t) - \text{ln}r) dt+\sigma_rdW_r \end{matrix}\right.
  /// </formula>
  /// <para>Where the stock process follows geometric Brownian motion with continuous dividend yield
  /// <formula inline="true"> q </formula> and stock volatility <formula inline="true"> \sigma_S </formula>.
  /// The short rate follows the Black-Karasinski model with mean-reversion speed <formula inline="true"> \kappa </formula>
  /// rate volatility <formula inline="true"> \sigma_r </formula>, and long-term asymptotic
  /// <formula inline="true"> \theta(t) </formula>.</para>
  /// <para>The two primary risk factors for convertible bonds are interest rate and equity price,
  /// which are correlated. Because each process is driven by its own Brownian motion, the correlation
  /// is in fact the correlation between the two Brownian motions.  This parameter is an input to the
  /// model and is left to the discretion of the user.</para>
  /// </remarks>
  [Serializable]
  public class ConvertibleBondIntersectingTreesModel
  {
    // Logger
    private static readonly log4net.ILog logger = 
      log4net.LogManager.GetLogger(typeof(ConvertibleBondIntersectingTreesModel));

    #region Constructors

    /// <summary>
    /// Constructor for ConvertibleBondIntersectingTreesModel
    /// </summary>
    /// <param name="bond">Convertible bond</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="discountCurve">Discoutn curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="n">Number of time steps</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="redemptionPrice">Redemption price</param>
    /// <param name="withAccrualOnCall">With accrual on call or not</param>
    /// <param name="withAccrualOnConversion">With accrual on conversion or not</param>
    /// <param name="s0">Current stock price</param>
    /// <param name="sigmaS">Stock volatility</param>
    /// <param name="div">dividends schedule</param>
    /// <param name="rho">Correlation between stock and short rate</param>
    /// <param name="kappa">Mean reversion speed of Black-Karasinski short rate</param>
    /// <param name="sigmaR">Volatility of Black-Karasinski short rate</param>
    /// <param name="shortRateModel">The short rate model for convertible bond pricing</param>
    public ConvertibleBondIntersectingTreesModel(
      Bond bond,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      int n,
      double recoveryRate,
      double redemptionPrice,
      bool withAccrualOnCall,
      bool withAccrualOnConversion,
      double s0,
      double sigmaS,
      StockCorrelatedModel.StockDividends div,
      double rho,
      double kappa,
      double sigmaR,
      ShortRateModelType shortRateModel
    )
    {
      _shortRateModelType = shortRateModel;

      bond_ = NormalizeBond(bond);
      redeptionPrice_ = redemptionPrice;
      withAccrualOnCall_ = withAccrualOnCall;
      withAccrualOnConversion_ = withAccrualOnConversion;
      asOf_ = discountCurve.AsOf;
      settle_ = settle;
      maturity_ = bond.Maturity;
      n_ = n;
      dt_ = (maturity_.ToDouble() - asOf.ToDouble()) / n_;
      sqrtDt_ = Math.Sqrt(dt_);

      // Set dS_ for delta calculation. Need larger dS_ for larger volatility
      dS_ = 0.04 + (sigmaS <= 0.1 ? 0 : (0.1 * (sigmaS - 0.1)));

      discountCurve_ = CloneUtil.Clone(discountCurve);
      survCurve_ = CloneUtil.Clone(survivalCurve);

      recoveryRate_ = recoveryRate;

      S0_ = s0;
      sigmaS_ = sigmaS;
      dividends_ = div;
      rho_ = rho;
      kappa_ = kappa;
      sigmaR_ = sigmaR;
      // Build the softcall probabilities map
      BuildSoftCallProbabilities();

      if (bond.CouponSchedule == null || bond.CouponSchedule.Count == 0)
      {
        couponSched_ = new Schedule(settle, bond.Effective, bond.FirstCoupon, bond.Maturity,
                                    bond.Freq, bond.BDConvention, bond.Calendar);
      }

      // pop up the tree dates
      treeDates_ = new Dt[n_ + 1];
      double doubleDate = asOf_.ToDouble();
      for (int i = 0; i <= n_; i++)
      {
        treeDates_[i] = new Dt(doubleDate);
        doubleDate += dt_;
      }

      // Get the short rate tree
      rateTree_ = RateModel.GetRateTree();
      discFacTree_ = RateModel.GetDiscountFactorTree();

      // Build the correlated stock model
      StockModel.BuildStockTree(RateModel);

      // Build the correlated stock model for hedge, delta, and gamma
      StockModelUp.BuildStockTree(RateModel);

      StockModelDown.BuildStockTree(RateModel);

      // Adjust the call and put prices for each tree period
      // based on the call and put start and end dates
      AdjustCallPutPrices();

      // Compute the accrual interest for each tree date
      CalculateAccrualInterests();

      // Compute the coupon payment for each tree date
      CalculateCouponPayments();

      // Compute conditional default probabilities for each tree date
      CalcSurvivalprobs();
    }

    /// <summary>
    /// Created a normalized bond to have 1000 par amount 
    /// with the same conversion price.
    /// The original bond is not touched.
    /// </summary>
    /// <param name="bond">The bond.</param>
    /// <returns>Bond.</returns>
    private static Bond NormalizeBond(Bond bond)
    {
      if (bond.ParAmount.AlmostEquals(1000.0))
        return bond;
      var normalizedBond = (Bond)bond.ShallowCopy();
      var conversionPrice = bond.ParAmount/bond.ConvertRatio;
      normalizedBond.ParAmount = 1000;
      normalizedBond.ConvertRatio = 1000/conversionPrice;
      return normalizedBond;
    }

    #endregion Constructors

    #region helper methods

    /// <summary>
    ///  Build the trigger interval -> soft-call probability map
    ///  The soft-call probability table is taken from the following reference:
    ///  http://papers.ssrn.com/sol3/papers.cfm?abstract_id=956813
    /// </summary>
    void BuildSoftCallProbabilities()
    {
      if (bond_.SoftCallTrigger < 0)
      {
        softCallProbabilityMap_ = null;
        return;
      }
      // we assume a fixed 20 out of 30 consecutive days soft call rule
      // The softCallProbabilityMap_[, 0] will store the trigger intervals such as:
      // (0, Tr*d^10, Tr*d^9, ..., Tr*u^19, infinity)
      int n = 32;
      softCallProbabilityMap_ = new double[n, 2];
      double downMultiplier = Math.Exp(-sigmaS_*Math.Sqrt(1.0/260.0));
      softCallProbabilityMap_[0, 0] = 0.0;
      for(int i = 1; i < n-1; i++)
      {
        softCallProbabilityMap_[i, 0] = bond_.SoftCallTrigger*S0_*Math.Pow(downMultiplier, 11-i);
      }
      // Set the last number to be very large
      softCallProbabilityMap_[n - 1, 0] = 1e8;

      // Populate the probabilities; these numbers will not change with trigger and upMultiplier (or downMultiplier)
      softCallProbabilityMap_[0, 1] = 0.0000000000000; softCallProbabilityMap_[1, 1] = 0.0001720674336; softCallProbabilityMap_[2, 1] = 0.0005162023008;
      softCallProbabilityMap_[3, 1] = 0.0025497265160; softCallProbabilityMap_[4, 1] = 0.0062726400793; softCallProbabilityMap_[5, 1] = 0.0173788107932;
      softCallProbabilityMap_[6, 1] = 0.0358682386577; softCallProbabilityMap_[7, 1] = 0.0730853416026; softCallProbabilityMap_[8, 1] = 0.1290301196277;
      softCallProbabilityMap_[9, 1] = 0.2147810682654; softCallProbabilityMap_[10,1] = 0.3303381875157; softCallProbabilityMap_[11,1] = 0.4748026356101;
      softCallProbabilityMap_[12,1] = 0.6048134192824; softCallProbabilityMap_[13,1] = 0.7104381565005; softCallProbabilityMap_[14,1] = 0.7995606642216;
      softCallProbabilityMap_[15,1] = 0.8635899219662; softCallProbabilityMap_[16,1] = 0.9143516551703; softCallProbabilityMap_[17,1] = 0.9461413435638;
      softCallProbabilityMap_[18,1] = 0.9698750413954; softCallProbabilityMap_[19,1] = 0.9825796149671; softCallProbabilityMap_[20,1] = 0.9915324337780;
      softCallProbabilityMap_[21,1] = 0.9955180343240; softCallProbabilityMap_[22,1] = 0.9981751013547; softCallProbabilityMap_[23,1] = 0.9991200175136;
      softCallProbabilityMap_[24,1] = 0.9997172895819; softCallProbabilityMap_[25,1] = 0.9998764283955; softCallProbabilityMap_[26,1] = 0.9999720044434;
      softCallProbabilityMap_[27,1] = 0.9999889694154; softCallProbabilityMap_[28,1] = 0.9999986700714; softCallProbabilityMap_[29,1] = 0.9999995306134;
      softCallProbabilityMap_[30,1] = 1.0000000000000; softCallProbabilityMap_[31,1] = 1.0000000000000;
      return;
    }
    /// <summary>
    ///  Compute the soft call probability for stock node value stock
    /// </summary>
    /// <param name="stock">Stock node value stock</param>
    /// <returns>Soft call probability</returns>
    private double GetSoftCallProbability(double stock)
    {
      if (stock < 0)
        return 0;

      int low = 0, high = softCallProbabilityMap_.GetLength(0)-1, mid = (low + high)/2;
      if (stock >= softCallProbabilityMap_[high, 0])
        return softCallProbabilityMap_[high, 1];
      else if (stock <= softCallProbabilityMap_[low, 0])
        return softCallProbabilityMap_[low, 1];

      while (low < high && !(stock >= softCallProbabilityMap_[mid, 0] && stock < softCallProbabilityMap_[mid + 1, 0]))
      {
        if (stock < softCallProbabilityMap_[mid, 0])
        {
          high = mid - 1;
          mid = (low + high) / 2;
        }
        else if (stock > softCallProbabilityMap_[mid + 1, 0])
        {
          low = mid + 1;
          mid = (low + high) / 2;
        }
        else
        {
          mid = mid + 1;
          break;
        }
      }      
      return softCallProbabilityMap_[mid, 1];
   }

    /// <summary>
    ///  Compute the conditional survival probabilities for each tree date
    /// </summary>
    private void CalcSurvivalprobs()
    {
      defaultProbs_ = new double[n_];
      for (int i = 0; i < n_; i++)
        defaultProbs_[i] = 0.0;
      double a = 0, b = 0, frac = 0, p1 = 0, p2 = 0;
      Dt date1, date2;
      if (survCurve_ != null)
      {
        for (int i = 1; i < treeDates_.Length; i++)
        {
          date1 = new Dt(treeDates_[i].Day, treeDates_[i].Month, treeDates_[i].Year);
          date2 = Dt.Add(date1, 1);
          frac = (treeDates_[i].Hour + treeDates_[i].Minute/60.0)/24.0;
          p1 = survCurve_.SurvivalProb(date1); 
          p2 = survCurve_.SurvivalProb(date2);
          a = p1 + (p2 - p1)*frac;
          
          date1 = new Dt(treeDates_[i-1].Day, treeDates_[i-1].Month, treeDates_[i-1].Year);
          date2 = Dt.Add(date1, 1);
          frac = (treeDates_[i-1].Hour + treeDates_[i-1].Minute / 60.0) / 24.0;
          p1 = survCurve_.SurvivalProb(date1);
          p2 = survCurve_.SurvivalProb(date2);
          b = p1 + (p2 - p1) * frac;
          defaultProbs_[i-1] = 1 - a / b;
        }
      }
      return;
    }

    /// <summary>
    /// Calculate the coupon payments and the
    /// discount period for each tree date period  
    /// </summary>
    private void CalculateCouponPayments()
    {
      couponPayments_ = new double[n_][];
      couponDiscountFraction_ = new double[n_][];
      
      // The coupon payments for the last period should be zero
      // because the final nodes at maturity handle the coupon
              
      // get the coupon payments only for a schedule with multiple coupons
      int numCoupons = couponSched_.Count, couponUsed = 1;
      if(numCoupons > 1)
      {
        Dt currentCouponDate = couponSched_.GetPaymentDate(numCoupons - 2);
        for(int k = n_; k >= 1; k--)
        {
          var coup = new List<double>();
          var coupTimeFraction = new List<double>();
          while(treeDates_[k-1] < currentCouponDate)
          {
            coup.Add(bond_.Coupon / (double)bond_.Freq); //todo this needs to be further refined to use coupon schedule
            coupTimeFraction.Add(currentCouponDate.ToDouble() - treeDates_[k - 1].ToDouble());
            couponUsed++;
            if (couponUsed < numCoupons)
              currentCouponDate = couponSched_.GetPaymentDate(numCoupons-1-couponUsed);
            else
            {
              currentCouponDate = Dt.Empty;
            }
          }
          couponPayments_[k - 1] = coup.ToArray();
          couponDiscountFraction_[k - 1] = coupTimeFraction.ToArray();
        }
      }
    }
    
    /// <summary>
    /// Calculate the accrual interest for each tree date should call occurs 
    /// </summary>
    private void CalculateAccrualInterests()
    {
      accrualInterest_ = new double[n_ + 1];
      for(int k = 0; k <= n_; k++)
        accrualInterest_[k] = 0.0;
      if(couponSched_==null || couponSched_.Count==0)
        return;

      int i = 0, numCoupons = couponSched_.Count;
      Dt couponPeriodStart = couponSched_.GetPeriodStart(i);
      Dt couponPeriodEnd = couponSched_.GetPaymentDate(i);
      for (int k = 0; k <= n_; k++)
      {
        if (i < numCoupons)
        {
          if (treeDates_[k] > couponPeriodStart && treeDates_[k] < couponPeriodEnd)
          {
            // Sometime the accrual is 0 because treeDate differ from couponPeriodStart by less than 1 day
            // In this case, we set the accrual to be diff / 1 day * accrual(1 day)
            if ((treeDates_[k].Year == couponPeriodStart.Year) &&
              (treeDates_[k].Month == couponPeriodStart.Month) && (treeDates_[k].Day == couponPeriodStart.Day))
            {
              accrualInterest_[k] = couponSched_.Fraction(
                couponPeriodStart, Dt.Add(couponPeriodStart, 1), bond_.DayCount);
              accrualInterest_[k] = (treeDates_[k].Hour + treeDates_[k].Minute / 60.0) / 24.0 * accrualInterest_[k];
            }
            else
            {
              accrualInterest_[k] = couponSched_.Fraction(couponPeriodStart, treeDates_[k], bond_.DayCount);
            }
          }
          else
          {
            i++;
            if (i < numCoupons)
            {
              couponPeriodStart = couponSched_.GetPeriodStart(i);
              couponPeriodEnd = couponSched_.GetPaymentDate(i);
              if ((treeDates_[k].Year == couponPeriodStart.Year) &&
                (treeDates_[k].Month == couponPeriodStart.Month) && (treeDates_[k].Day == couponPeriodStart.Day))
              {
                accrualInterest_[k] = couponSched_.Fraction(
                  couponPeriodStart, Dt.Add(couponPeriodStart, 1), bond_.DayCount);
                accrualInterest_[k] = (treeDates_[k].Hour + treeDates_[k].Minute / 60.0) / 24.0 * accrualInterest_[k];
              }
              else
              {
                accrualInterest_[k] = couponSched_.Fraction(couponPeriodStart, treeDates_[k], bond_.DayCount);
              }
            }
          }
        }
      }
      return;
    }

    /// <summary>
    /// Adjust the call and put prices for each tree period 
    /// based on the call and put start and end dates 
    /// </summary>
    private void AdjustCallPutPrices()
    {
      Dt[] callStarts = null;
      Dt[] callEnds = null;
      double[] callPrices = null;
      Dt[] putStarts = null;
      Dt[] putEnds = null;
      double[] putPrices = null;
      Dt[] dates = treeDates_;
      callPrices_ = new double[n_ + 1];
      putPrices_ = new double[n_ + 1];
      int i = 0, j = 0;


      if(bond_.CallSchedule == null || bond_.CallSchedule.Count == 0)
      {
        for (int k = 0; k <= n_; k++)
          callPrices_[k] = 1e8;
      }

      else
      {
        callStarts = (Dt[])Array.ConvertAll<CallPeriod, Dt>(bond_.CallSchedule.ToArray(), x => x.StartDate).Clone();
        callEnds = (Dt[])Array.ConvertAll<CallPeriod, Dt>(bond_.CallSchedule.ToArray(), x => x.EndDate).Clone();
        callPrices = (double[])Array.ConvertAll<CallPeriod, double>(bond_.CallSchedule.ToArray(), x => x.CallPrice*100.0).Clone();

        // The end date should be a whole day during 
        // which a call/put can still be exercised
        for (int k = 0; k < callEnds.Length; k++)
        {
          callStarts[k] = new Dt(callStarts[k].Day, callStarts[k].Month, callStarts[k].Year, 0, 0, 0);
          callEnds[k] = new Dt(callEnds[k].Day, callEnds[k].Month, callEnds[k].Year, 23, 59, 59);
        }
        i = 0;
        double sizeStep;
        for (int k = 0; k <= n_; k++)
        {
          if (k<n_)
          {
              sizeStep = treeDates_[k+1].ToDouble() - treeDates_[k].ToDouble();
          }
          else
          {
              sizeStep = treeDates_[n_].ToDouble() - treeDates_[n_ - 1].ToDouble();
          }

          callPrices_[k] = 1e8;
          if (i < callEnds.Length && treeDates_[k].ToDouble() <= (callEnds[i].ToDouble()+0.5*sizeStep))
          {
              if (treeDates_[k].ToDouble() >= (callStarts[i].ToDouble() - 0.5 * sizeStep))
              callPrices_[k] = callPrices[i];
          }
          else
          {
            i++;
            if (i < callEnds.Length && treeDates_[k].ToDouble() >= (callStarts[i].ToDouble()-0.5*sizeStep)
                && treeDates_[k].ToDouble() <= (callEnds[i].ToDouble())+0.5*sizeStep)
              callPrices_[k] = callPrices[i];
          }
        }
      }

      if (bond_.PutSchedule == null || bond_.PutSchedule.Count == 0)
      {
        for (int k = 0; k <= n_; k++)
          putPrices_[k] = 0;
      }
      else
      {
        putStarts = (Dt[])(Array.ConvertAll<PutPeriod, Dt>(bond_.PutSchedule.ToArray(), x=>x.StartDate)).Clone();
        putEnds = (Dt[])(Array.ConvertAll<PutPeriod, Dt>(bond_.PutSchedule.ToArray(), x => x.EndDate)).Clone();
        putPrices = (double[])Array.ConvertAll<PutPeriod, double>(bond_.PutSchedule.ToArray(), x => x.PutPrice * 100.0).Clone();
          double sizeStep;
        for (int k = 0; k < putEnds.Length; k++)
        {
          putStarts[k] = new Dt(putStarts[k].Day, putStarts[k].Month, putStarts[k].Year, 0, 0, 0);
          putEnds[k] = new Dt(putEnds[k].Day, putEnds[k].Month, putEnds[k].Year, 23, 59, 59);
        }
        j = 0;
        for (int k = 0; k <= n_; k++)
        {
          if (k < n_)
          {
                sizeStep = treeDates_[k + 1].ToDouble() - treeDates_[k].ToDouble();
          }
          else
          {
                sizeStep = treeDates_[n_].ToDouble() - treeDates_[n_ - 1].ToDouble();
          }

          putPrices_[k] = 0.0;
          if (j < putEnds.Length && treeDates_[k].ToDouble() <= (putEnds[j].ToDouble()+sizeStep*0.5))
          {
              if (treeDates_[k].ToDouble() >= (putStarts[j].ToDouble() - sizeStep * 0.5))
              putPrices_[k] = putPrices[j];
          }
          else
          {
            j++;
            if (j < putEnds.Length && treeDates_[k].ToDouble() >= (putStarts[j].ToDouble()-0.5*sizeStep)
                && treeDates_[k].ToDouble() <= (putEnds[j].ToDouble()+0.5*sizeStep))
              putPrices_[k] = putPrices[j];
          }
        }
      }
      return;
    }

    /// <summary>
    /// Discount back. 
    /// Check converting, calling, putting and holding for each step
    /// </summary>
    private void StepBack()
    {
      Dt convertStart = bond_.ConvertStartDate;
      Dt convertEnd = bond_.ConvertEndDate;
      double ratio = bond_.ConvertRatio;
      double survProb = 0, a, b, c, d, couponAmount, convertValue;
      bool withinConvert = false;
      bool called = false, putted = false;

      // Loop back
      // Get the recovery rates at each time step
      var recoveries = new double[n_];
      for(int i = 0; i < n_; i++)
      {
        recoveries[i] = RecoveryRate >= 0.0
                          ? RecoveryRate
                          : ((survCurve_ == null || survCurve_.SurvivalCalibrator == null)
                               ? 0.4
                               : survCurve_.SurvivalCalibrator.RecoveryCurve.RecoveryRate(treeDates_[i]));
      }
      for (int k = n_ - 1; k >= 0; k--)
      {
        withinConvert = treeDates_[k] <= convertEnd && treeDates_[k] >= convertStart;

        // Get the square (k+1 x k+1) block of stock prices for time k
        currentStockPrices_ = stockModel_.GetStockPrices(k);

        for (int i = 0; i <= k; i++)
        {
          double dis = discFacTree_[k][i];
          for (int j = 0; j <= k; j++)
          {
            a = prices_[i, j]*dis;
            b = prices_[i, j + 1]*dis;
            c = prices_[i + 1, j]*dis;
            d = prices_[i + 1, j + 1]*dis;
            prices_[i, j] = 0.25 * (a + b + c + d);

            if (calcBondFloor_)
            {
              bondFloorPrices_[i, j] = 0.25*dis*
                                       (bondFloorPrices_[i, j] + bondFloorPrices_[i, j + 1] +
                                        bondFloorPrices_[i + 1, j] + bondFloorPrices_[i + 1, j + 1]);
            }

            // count coupon payment only for bond but not stock component
            couponAmount = 0;
            if (couponPayments_[k] != null)
            {
              for (int m = 0; m < couponPayments_[k].Length; m++)
                couponAmount += 1000*couponPayments_[k][m]*
                                Math.Exp(-(rateTree_[k][i])*couponDiscountFraction_[k][m]);
            }
            // Consider default probability
            survProb = 1 - defaultProbs_[k];
            if (calcBondFloor_)
            {
              bondFloorPrices_[i, j] = bondFloorPrices_[i, j]*survProb + defaultProbs_[k]*recoveries[k]*1000.0;
              // Get the full price at current time k 
              bondFloorPrices_[i, j] += couponAmount;
            }

            prices_[i, j] = prices_[i, j] * survProb + defaultProbs_[k] * recoveries[k] * 1000.0;            
            // Get the full price at current time k            
            prices_[i, j] += couponAmount;

            // [1] Check calling
            // consider soft-call probability
            double noCallPrice = prices_[i, j], softCallProb = 1.0;            
            called = CheckCall(k, ref prices_[i, j]);
            if (softCallProbabilityMap_ != null && treeDates_[k] <= bond_.SoftCallEndDate)
            {
              var softCallPrice = stockModel_.GetSoftCallAdjustedStockPrice(k, i, j);
              softCallProb = GetSoftCallProbability(softCallPrice);
              prices_[i, j] = softCallProb*prices_[i, j] + (1 - softCallProb)*noCallPrice;
            }
            if (calcBondFloor_)
            {
              CheckCall(k, ref bondFloorPrices_[i, j]);
            }
            // [2] Check converting
            if (withinConvert)
            {
              convertValue = currentStockPrices_[i, j] * ratio;
              if (convertValue > prices_[i, j])
              {
                prices_[i, j] = convertValue + (withAccrualOnConversion_ ? accrualInterest_[k]*bond_.Coupon*1000.0 : 0);
              }
            }
            // [3] Check putting
            putted = CheckPut(k, ref prices_[i, j]);
            if (calcBondFloor_)
            {
              CheckPut(k, ref bondFloorPrices_[i, j]);
            }
          }
        }
      }
      if (calcBondFloor_)
      {
        bondFloor_ = bondFloorPrices_[0, 0];
      }
      return;
    }

    /// <summary>
    /// This method check if the bond should be put
    /// </summary>
    /// <param name="k"> Time step index </param>
    /// <param name="price">current convertible bond price to be possibly modified</param>
    /// <returns>True if put</returns>
    private bool CheckPut(int k, ref double price)
    {
      double put = (putPrices_[k] / 100.0 + accrualInterest_[k] * bond_.Coupon) * 1000.0;

      if (put >= price && k > 5)
      {
        bool checkAccrualOnCall;
        if (withAccrualOnCall_)
          checkAccrualOnCall = (accrualInterest_[k + 1] < accrualInterest_[k]);
        else
          checkAccrualOnCall = (accrualInterest_[k - 1] > accrualInterest_[k]);
        if ((checkAccrualOnCall) || (putPrices_[ Math.Max(k+1,n_)]<=1e-8))
        {
          price = put;
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// This method check if the bond should be called 
    /// </summary>
    /// <param name="k"> Time step index </param>
    /// <param name="price">current convertible bond price to be possibly modified</param>
    /// <returns>True if called</returns>
    private bool CheckCall(int k, ref double price)
    {
      // price is the k-discounted full price (with coupon)
      // we compare the quote call + accrual interest with price
      double call = (callPrices_[k] / 100.0 + accrualInterest_[k] * bond_.Coupon) * 1000.0;

      // TO BE REVISITED !!!
      // Here the dirty condition: (k > 5) &&(accrualInterest_[k-1] > accrualInterest_[k])
      // is to make sure the call only occurs at around coupon payment date
      // For call to occur at around coupon date, check accrualInterest_[k-1] > accrualInterest_[k]
      // since accrualInterest_[] is increasing function with jumps down at coupon date
      // Another problem arise if call start date is earlier than 1st coupon to be paid, so comes k>5
      // to ignore any call before time, say time 5.
            
      if(call <= price && k > 5)
      {
        bool checkAccrualOnCall;
        if (withAccrualOnCall_)
          checkAccrualOnCall = (accrualInterest_[k + 1] < accrualInterest_[k]);
        else
          checkAccrualOnCall = (accrualInterest_[k - 1] > accrualInterest_[k]);
        if ((checkAccrualOnCall) || (callPrices_[ Math.Max(k+1,n_)]>=1e8))
        {
          price = call;
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Calculate the convertible bond prices at maturity 
    /// </summary>
    private void SetFinalNodes()
    {
      Dt convertStart = bond_.ConvertStartDate;
      Dt convertEnd = bond_.ConvertEndDate;
      double ratio = bond_.ConvertRatio;
      bool withinConvert = maturity_ <= convertEnd && maturity_ >= convertStart;

      prices_ = new double[n_ + 1, n_ + 1];
      bondFloorPrices_ = new double[n_ + 1, n_ + 1];

      // Get the stock prices at maturity: n+1 x n+1 matrix
      currentStockPrices_ = stockModel_.GetStockPrices(n_);

      double remainingPrincipal = AmortizationUtil.PrincipalAt(bond_.AmortizationSchedule, 1.0, maturity_);

      int last = couponSched_.Count - 1;
      double frac = couponSched_.Fraction(
        couponSched_.GetPeriodStart(last), couponSched_.GetPeriodEnd(last), bond_.DayCount);
      
      double finalStaightBondValue = (RedemptionPrice/100.0 + bond_.Coupon * frac) * remainingPrincipal * 1000.0;
      for (int i = 0; i <= n_; i++)
      {
        for (int j = 0; j <= n_; j++)
        {
          if(calcBondFloor_)
            bondFloorPrices_[i, j] = finalStaightBondValue;
          // Check whether to convert to stock
          var totalStock = currentStockPrices_[i, j] * ratio;
          if (withinConvert)
          {
            if(totalStock > finalStaightBondValue)
            {
              prices_[i, j] = totalStock +
                              (withAccrualOnConversion_ ? frac*bond_.Coupon*remainingPrincipal*1000.0 : 0.0);
            }
            else
            {
              prices_[i, j] = finalStaightBondValue;
            }
          }
          else
          {
            // if not to convert, set the price to staight bond value
            prices_[i, j] = finalStaightBondValue;
          }
        }
      }
      return;
    }

    #endregion helper methods

    #region properties
    /// <summary>
    ///  Get the recovery rate (recovery rate from bond pricer)
    /// </summary>
    public double RecoveryRate
    {
      get { return recoveryRate_; }
    }

    /// <summary>
    ///  Get the as-of date
    /// </summary>
    public Dt AsOf
    {
      get { return asOf_; }
    }

    /// <summary>
    ///  Get maturity of bond
    /// </summary>
    public Dt Maturity
    {
      get { return maturity_; }
    }

    /// <summary>
    ///  Get the discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_;}
      set 
      { 
        discountCurve_ = value;
        rateModel_ = null;
      }
    }

    /// <summary>
    ///  Get survival curve
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get { return survCurve_; }
      set
      {
        survCurve_ = value; 
        CalcSurvivalprobs();
      }
    }

    /// <summary>
    ///  Get the redemption price
    /// </summary>
    public double RedemptionPrice
    {
      get { return redeptionPrice_; }
    }
    /// <summary>
    /// Time interval in years
    /// </summary>
    public double DeltaT
    {
      get { return dt_; }
      set { dt_ = value; }
    }

    /// <summary>
    ///  Get the Black-Karasinski short rate model
    /// </summary>
    public IBinomialShortRateTreeModel RateModel
    {
      get
      {
        if (rateModel_ == null)
        {
          switch (_shortRateModelType)
          {
          case ShortRateModelType.HullWhite:
            rateModel_ = new HullWhiteBinomialTreeModel(
              kappa_, sigmaR_, settle_, maturity_, n_, discountCurve_);
            break;
          case ShortRateModelType.None:
          case ShortRateModelType.BlackKarasinski:
            rateModel_ = new BlackKarasinskiBinomialTreeModel(
              kappa_, sigmaR_, settle_, maturity_, n_, discountCurve_);
            break;
          default:
            throw new ToolkitException(
              $"Unknown short rate model type: {_shortRateModelType}");
          }
        }
        return rateModel_;
      }
    }

    /// <summary>
    ///  Get the Black-Karasinski short rate model with up 5 bp bump
    /// </summary>
    public IBinomialShortRateTreeModel RateModelUp
    {
      get
      {
        var clonedDiscountCurve = (DiscountCurve)discountCurve_.CloneWithCalibrator();
        clonedDiscountCurve.Spread += 0.0005;
        if (rateModelUp_ == null)
          rateModelUp_ = new BlackKarasinskiBinomialTreeModel(
            kappa_, sigmaR_, settle_, maturity_, n_, clonedDiscountCurve);
        return rateModelUp_;
      }
    }

    /// <summary>
    ///  Get the Black-Karasinski short rate model with down 5 bp bump
    /// </summary>
    public IBinomialShortRateTreeModel RateModelDown
    {
      get
      {
        var clonedDiscountCurve = (DiscountCurve)discountCurve_.CloneWithCalibrator();
        clonedDiscountCurve.Spread -= 0.0005;
        if (rateModelDown_ == null)
          rateModelDown_ = new BlackKarasinskiBinomialTreeModel(
            kappa_, sigmaR_, settle_, maturity_, n_, clonedDiscountCurve);
        return rateModelDown_;
      }
    }

    /// <summary>
    ///  Get the correlated stock model
    /// </summary>
    public StockCorrelatedModel StockModel
    {
      get
      {
        if (stockModel_ == null)
        {
          stockModel_ = new StockCorrelatedModel(S0_, sigmaS_, dividends_, asOf_, maturity_, n_, rho_);

        }
        return stockModel_;
      }
    }


    /// <summary>
    ///  Get the correlated stock model with up bumped initial stock price
    /// </summary>
    public StockCorrelatedModel StockModelUp
    {
      get
      {
        if (stockModelUp_ == null)
        {
          stockModelUp_ = new StockCorrelatedModel(
            S0_ * (1 + dS_), sigmaS_, dividends_, asOf_, maturity_, n_, rho_);
        }
        return stockModelUp_;
      }
    }

    /// <summary>
    ///  Get the correlated stock model with down bumped initial stock price
    /// </summary>
    public StockCorrelatedModel StockModelDown
    {
      get
      {
        if (stockModelDown_ == null)
        {
          stockModelDown_ = new StockCorrelatedModel(
              S0_ * (1 - dS_), sigmaS_, dividends_, asOf_, maturity_, n_, rho_);
        }
        return stockModelDown_;
      }
    }

    /// <summary>
    ///  Get the stock model with volatility bumped up 0.5% absolutely
    /// </summary>
    public StockCorrelatedModel StockModelVolUp
    {
      get
      {
        if (stockModelVolUp_ == null)
          stockModelVolUp_ = new StockCorrelatedModel(
              S0_, sigmaS_ + stockVolBumpUp_, dividends_, asOf_, maturity_, n_, rho_);
        return stockModelVolUp_;
      }
    }

    /// <summary>
    ///  Get the stock model with volatility bumped down 0.5% absolutely
    /// </summary>
    public StockCorrelatedModel StockModelVolDown
    {
      get
      {
        if (stockModelVolDown_ == null)
        {
          if(sigmaS_ < stockVolBumpUp_)
            stockVolBumpDown_ = sigmaS_;
          stockModelVolDown_ = new StockCorrelatedModel(
            S0_, sigmaS_ - stockVolBumpDown_, dividends_, asOf_, maturity_, n_, rho_);
        }
        return stockModelVolDown_;
      }
    }


    /// <summary>
    ///  Get the parity of convertible bond.
    /// The parity price is the bond price at which neither a profit nor a loss is realized, ignoring transaction costs.  
    /// Parity assumes that the bond is purchased, immediately converted, and then the stock is sold at its market price.
    /// This is calculated as follows: Parity = { (Conv. Ratio) * [ S /(Par amount / 1000) ]} / 10
    /// No need to change hard coded 1000 and 10. Let p be bond price expressed in number of percentage, we need to get it
    /// from p / 100 * ParAmount = S * ConversionRatio. Easy to get p = { (Conv. Ratio) * [ S /(Par amount / 1000) ]} / 10
    /// 
    /// </summary>
    public double Parity
    {
      get
      {
        // Usually the bond price is expressed against $1000 face value
        // and the parity should be also against $1000 par no matter 
        // what the real par amount is.
        return S0_*bond_.ConvertRatio/10.0;
      }
    }

    /// <summary>
    ///  Get the conversion price.
    ///  Conversion price is stock price at which conversion can be exercised.
    /// </summary>
    public double ConversionPrice
    {
      get { return bond_.ParAmount / bond_.ConvertRatio; }
    }

    #endregion properties

    #region convertible bond measure methods

    /// <summary>
    ///  Static method calculates a binomial tree for the conditional probabilities:
    ///  <m>P_H</m> and <m>P_L</m>.
    ///  where<math>\begin{align}
    ///   P_H &amp;= \Pr\!\left( W_{k-1} = (m+1) \sqrt{dt} \mid W_k = m \sqrt{dt} \right)
    ///   \\ P_L &amp;= \Pr\!\left( W_{k-1} = (m-1) \sqrt{dt} \mid W_k = m \sqrt{dt} \right)
    ///  \end{align}</math> 
    ///  here <m>d t</m> is time interval, <m>\sqrt{d t}</m> is half interval
    ///  between adjacent nodes in a 
    ///  binomial tree of Brownian motion, <m>k = 0, 1, 2, \ldots</m>, is the time index,
    ///  and <m>m</m> is level of node. 
    /// </summary>
    /// <param name="n">number of steps</param>
    /// <returns>A conditional probability tree</returns>
    public static List<double[]> CalcConditionalProbabilities(int n)
    {
      var condProbs = new List<double[]>(n);
      condProbs.Add(new double[] { 0.0 });
      for (int i = 1; i < n; i++)
      {
        var condProb = new double[i + 1];
        for (int j = 0; j < i + 1; j++ )
        {
          condProb[j] = (double)(i - j) / (double)i; ;
        }
        condProbs.Add(condProb);
      }
      return condProbs;
    }

    /// <summary>
    ///  Calculate the model price of convertible bond
    /// </summary>
    /// <returns>Convertible bond model full price</returns>
    public double Pv()
    {
      if (prices_ != null && !needDelta_ && !needVega_ && !needOAS_)
      {        
        return prices_[0, 0];
      }
      else
      {
        // Get the short rate tree
        rateTree_ = RateModel.GetRateTree();
        discFacTree_ = RateModel.GetDiscountFactorTree();

        // Calculate the prices for the final square block of nodes
        SetFinalNodes();

        // discount back to time 0, this should consider soft-call probability
        StepBack();

        // The [0, 0] element stores the result price
        return prices_[0, 0];
      }
    }

    /// <summary>
    ///  Calculate the bond floor: bond price with conversion turning off 
    /// </summary>
    /// <returns>Bond floor</returns>
    public double BondFloor()
    {
      if (Double.IsNaN(bondFloor_))
        Pv();
      return bondFloor_ / 10.0;
    }

    /// <summary>
    ///  Calculate the convertible relevant bond floor, the lower bound of convertible bond price
    /// </summary>
    /// <returns>Relevant bond floor</returns>
    public double RelevantBondFloor()
    {
      if (Double.IsNaN(bondFloor_))
        Pv();
      return Math.Max(bondFloor_ / 10.0, S0_ / 10.0); 
    }

    /// <summary>
    ///  Calculate the sensitivity of convertible bond pricer vs. stock price
    ///  It is the change of convertible bond price per $1 of conversion value
    ///  (or per $1/ConversionRatio change of stock price)
    /// </summary>
    /// <returns></returns>
    public double Delta()
    {
      if (!Double.IsNaN(delta_))
        return delta_;
      // use hedge ratio to compute delta
      if (Double.IsNaN(hedgeRatio_))
        HedgeRatio();
      delta_ = hedgeRatio_ / bond_.ConvertRatio;
      return delta_;
    }

    /// <summary>
    ///  Calculate the gamma of convertible bond.
    ///  Gamma per share = par*{ [ V(1.005*S) - 2*V(S) + V(0.995*S) ]/(0.005*S)^2 } /(conversion ratio) /100 
    /// </summary>
    /// <returns></returns>
    public double Gamma()
    {
      if (!Double.IsNaN(gamma_))
        return Math.Abs(gamma_) < 1e-10 ? 0 : gamma_;
      if (Double.IsNaN(hedgeRatio_))
        HedgeRatio();
      gamma_ = (pvUp_ - 2 * pv0_ + pvDown_) / (dS_*S0_*dS_*S0_) / 
               bond_.ConvertRatio;
      return Math.Abs(gamma_) < 1e-10 ? 0 : gamma_;
    }

    /// <summary>
    ///  Calculate the convertible bond hedge ratio
    ///  Hedge Ratio = [ V(1.005*S) - V(0.995*S) ]/(0.01*S)
    /// </summary>
    /// <returns></returns>
    public double HedgeRatio()
    {
      if (!Double.IsNaN(pvUp_) && !Double.IsNaN(pvDown_))
      {
        hedgeRatio_ = (pvUp_ - pvDown_) / (S0_ * 2 * dS_);
        return hedgeRatio_;
      }
      // Get the stock model ready for up and down bump
      if (stockModelUp_ == null)
        StockModelUp.BuildStockTree(RateModel);
      if (stockModelDown_ == null)
        StockModelDown.BuildStockTree(RateModel);

      // Save the original stock model, price, and needDelta flag       
      StockCorrelatedModel savedModel = stockModel_;
      bool savedNeedDelta = needDelta_;
      double savedPrice = Double.NaN;
      if (prices_ == null || prices_.Length == 0)
      {
        pv0_ = Pv();
      }
      savedPrice = prices_[0, 0];
      pv0_ = prices_[0, 0];

      pvUp_ = Double.NaN;
      pvDown_ = Double.NaN;
      try
      {
        needDelta_ = true;
        stockModel_ = stockModelUp_;
        pvUp_ = Pv();
      }
      catch (Exception)
      {
      }
      finally
      {
        needDelta_ = savedNeedDelta;
        stockModel_ = savedModel;
        prices_[0, 0] = savedPrice;
      }
      try
      {
        needDelta_ = true;
        stockModel_ = stockModelDown_;
        pvDown_ = Pv();
      }
      catch (Exception)
      {
      }
      finally
      {
        needDelta_ = savedNeedDelta;
        stockModel_ = savedModel;
        prices_[0, 0] = savedPrice;
      }

      if (!Double.IsNaN(pvUp_) && !Double.IsNaN(pvDown_))
      {
        hedgeRatio_ = (pvUp_ - pvDown_) / (savedModel.S0 * 2 * dS_);
        return hedgeRatio_;
      }
      else
      {
        throw new ToolkitException("Cannot find the hedge ratio. Either up or down bump of initial stock failed");
      }
    }

    /// <summary>
    ///  Calculate the effective duration of convertible bond
    ///  Effective Duration = [Pv(down) - Pv(up)] / [2 * Pv() * dY] where dY = 25bps
    ///  The OAS (zspread) is added onto the spread
    /// </summary>
    /// <returns>Effective duration of convertible bond</returns>
    public double EffectiveDuration(double marketPrice, bool fixStock)
    {
      effectiveDuration_ = Double.NaN;
      if (Double.IsNaN(oas_))
      {
        try
        {
          oas_ = ImpliedDiscountSpread(marketPrice, false, fixStock);
        }
        catch (Exception)
        {
        }
        finally
        {
          if (Double.IsNaN(oas_))
            effectiveDuration_ = Double.NaN;
        }
        if (Double.IsNaN(oas_))
        {
          effectiveConvexity_ = Double.NaN;
          throw new ArgumentException("Effective duration failed due to unable to bracket an OAS; Try fixStock=true");
        }
      }
      // Compute the up and down prices
      double p0 = Double.NaN;
      if (prices_ == null)
        p0 = Pv();

      SurvivalCurve origSurvivalCurve = SurvivalCurve;
      DiscountCurve origDiscountCurve = CloneUtil.Clone(DiscountCurve); 
      double origSpread = DiscountCurve.Spread;

      double dY = 0.0025;      
      DiscountCurve.Spread += (oas_ + dY);
      rateModel_ = null;
      StockModel.BuildStockTree(RateModel);
      prices_ = null;
      double up = Pv();

      DiscountCurve.Spread = origSpread;
      DiscountCurve.Spread += (oas_ - dY);
      rateModel_ = null;
      StockModel.BuildStockTree(RateModel);
      prices_ = null;
      double down = Pv();

      DiscountCurve = origDiscountCurve;
      StockModel.BuildStockTree(RateModel);
      prices_ = null;

      effectiveDuration_ = 0.5 * (down - up) / (p0 * dY);
      effectiveConvexity_ = 0.5 * (down + up - 2 * p0) / (2 * p0 * 0.0025 * 0.0025);
      oas_ = Double.NaN;
      return effectiveDuration_;
    }

    /// <summary>
    ///  Calculate the effective convexity of convertible bond
    ///  Effective Duration = [Pv(down) + Pv(up) - 2*Pv()] / [2 * Pv() * dY * dY] where dY = 25bps
    /// </summary>
    /// <returns>Effective duration of convertible bond</returns>
    public double EffectiveConvexity(double marketPrice, bool fixStock)
    {
      effectiveConvexity_ = Double.NaN;
      if (Double.IsNaN(oas_))
      {
        try
        {
          oas_ = ImpliedDiscountSpread(marketPrice, false, fixStock);
        }
        catch (Exception)
        {
        }
        finally
        {
          if (Double.IsNaN(oas_))
            effectiveConvexity_ = Double.NaN;
        }
        if (Double.IsNaN(oas_))
        {
          effectiveDuration_ = Double.NaN;
          throw new ArgumentException("Effective convexity failed due to unable to bracket an OAS; Try fixStock=true");
        }
      }

      // Compute the up and down prices
      double p0 = Double.NaN;
      if (prices_ == null)
        p0 = Pv();

      SurvivalCurve origSurvivalCurve = SurvivalCurve;
      DiscountCurve origDiscountCurve = CloneUtil.Clone(DiscountCurve);
      double origSpread = DiscountCurve.Spread;

      double dY = 0.0025;
      DiscountCurve.Spread += (oas_ + dY);
      rateModel_ = null;
      StockModel.BuildStockTree(RateModel);
      prices_ = null;
      double up = Pv();

      DiscountCurve.Spread = origSpread;
      DiscountCurve.Spread += (oas_ - dY);
      rateModel_ = null;
      StockModel.BuildStockTree(RateModel);
      prices_ = null;
      double down = Pv();

      DiscountCurve = origDiscountCurve;
      StockModel.BuildStockTree(RateModel);
      prices_ = null;

      effectiveDuration_ = 0.5 * (down - up) / (p0 * dY);
      effectiveConvexity_ = 0.5 * (down + up - 2 * p0) / (2 * p0 * 0.0025 * 0.0025);
      oas_ = Double.NaN;
      return effectiveConvexity_;
    }

    /// <summary>
    ///  Calculate the vega of convertible bond.
    ///  Vega = [V(vol curve+0.5%) - V(volcurve-0.5%)] / 1%
    /// </summary>
    /// <returns>Stock volatility sensitivity</returns>
    public double VegaEquity()
    {
      if (!Double.IsNaN(pvStockVolUp_) && !Double.IsNaN(pvStockVolDown_))
      {
        vega_ = (pvStockVolUp_ - pvStockVolDown_) / (10.0 * (stockVolBumpUp_ + stockVolBumpDown_));
        return vega_;
      }
      // Get the stock model ready for up nd down stock volatility bump
      if (stockModelVolUp_ == null)
        StockModelVolUp.BuildStockTree(RateModel);
      if (stockModelVolDown_ == null)
        StockModelVolDown.BuildStockTree(RateModel);

      StockCorrelatedModel savedModel = stockModel_;
      bool savedNeedVega = needVega_;
      double savedPrice = Double.NaN;
      if (prices_ == null || prices_.Length == 0)
      {
        pv0_ = Pv();
      }
      savedPrice = prices_[0, 0];
      pv0_ = prices_[0, 0];

      pvStockVolUp_ = Double.NaN;
      pvStockVolDown_ = Double.NaN;
      // Bump stock vol up
      try
      {
        needVega_ = true;
        stockModel_ = stockModelVolUp_;
        pvStockVolUp_ = Pv();
      }
      catch (Exception)
      {
      }
      finally
      {
        needVega_ = savedNeedVega;
        stockModel_ = savedModel;
        prices_[0, 0] = savedPrice;
      }
      // Bump stock vol down
      try
      {
        needVega_ = true;
        stockModel_ = stockModelVolDown_;
        pvStockVolDown_ = Pv();
      }
      catch (Exception)
      {
      }
      finally
      {
        needVega_ = savedNeedVega;
        stockModel_ = savedModel;
        prices_[0, 0] = savedPrice;
      }
      if (!Double.IsNaN(pvStockVolUp_) && !Double.IsNaN(pvStockVolDown_))
      {
        vega_ = (pvStockVolUp_ - pvStockVolDown_)/(10.0 * (stockVolBumpUp_ + stockVolBumpDown_));
        return vega_;
      }
      else
      {
        throw new ToolkitException("Cannot find the vega. Either up or down bump of stock volatility failed");
      }
    }

    /// <summary>
    ///  Calculate the rate vega of convertible bond
    ///  Vega = change in convertible bond price for a 1% change in rate volatility
    ///       = [ V(RateVol+0.5%) - V(RateVol) ] /0.5%
    /// </summary>
    /// <returns>Convertible bond vega on rate</returns>
    public double VegaRate()
    {
      // Compute the price before bumping rate volatility
      if (prices_ != null && !Double.IsNaN(prices_[0, 0]))
        pv0_ = prices_[0, 0];
      else
      {
        pv0_ = Pv();
      }

      // Save some states before computing new price
      // StockModel.Clone() will clone btree & ctree inside
      StockCorrelatedModel savedModel = StockModel.Clone();      
      var rateTreeClone = RateModel.CloneRateTree();
      double rateVega = 0.0;
      try
      {


        // Build a new rate tree with bumped volatility
        var rateModelVega = RateModel.BumpSigma(rateVolBump_);

        // Set new rate tree to rateModel and updated discount factors
        RateModel.SetRateTree(rateModelVega.GetRateTree());

        // Build the stock tree based on volatility-bumped rate tree      
        StockModel.BuildStockTree(rateModelVega);

        // Reprice
        prices_ = null;
        double pvVega = Pv();
        rateVega = (pvVega - pv0_) * 0.1 / rateVolBump_; // *0.1 to bring price to bond quote convention
      }
      finally
      {
        // Restore original states
        prices_ = null;
        RateModel.SetRateTree(rateTreeClone);
        StockModel.Reset(savedModel);
      }
      return rateVega;
    }

    /// <summary>
    ///  Calculate the stock optiona value, the difference between convertible 
    ///  bond price with conversion turn on and bond floor price 
    /// </summary>
    /// <returns>Stock option value</returns>
    public double StockOptionValue()
    {
      if (!Double.IsNaN(pv0_))
        return (pv0_ - bondFloor_) / 10.0;
      Pv();
      pv0_ = prices_[0, 0];
      return (pv0_ - bondFloor_) / 10.0;
    }

    /// <summary>
    ///  Calculate the BreakEven of convertible bond, number of years to amortize the premium
    ///  BreakEven = (clean price - parity) / (annual coupon - dividend rate * clean price)
    /// </summary>
    /// <returns>Convertible bond BreakEven</returns>
    public double BreakEven(double cleanPrice)
    {
      double annualCoupon = bond_.Coupon;
      double yield = GetDividendYield();
      // Whenever the dinominator does not make sense, return N/A not to show negative value or blowup
      if (annualCoupon - yield * cleanPrice <= 0)
        return Double.NaN;
      return (cleanPrice - Parity / 100.0) / (annualCoupon - yield * cleanPrice);
    }

    /// <summary>
    ///  Calculate the cashflow payback of convertible bond, number of years to amortize the premium
    ///  cashflow payback = (clean price - parity) / (annual coupon - dividend rate * parity)
    /// </summary>
    /// <returns>Convertible bond cashflow payback</returns>
    public double CashflowPayback(double cleanPrice)
    {
      double annualCoupon = bond_.Coupon;
      double yield = GetDividendYield();      
      // Whenever the dinominator does not make sense, return N/A not to show negative value or blowup
      if (annualCoupon - yield * Parity / 100.0 <= 0)
        return Double.NaN;
      return (cleanPrice - Parity / 100.0) / (annualCoupon - yield * Parity / 100.0);
    }

    /// <summary>
    ///  Compute the dividend yield from discrete dividend schedules
    /// </summary>
    /// <returns>Annualized dividend yield</returns>
    private double GetDividendYield()
    {
      double yield = 0;
      if (dividends_.IsDividendYield)
        yield = dividends_.DividendYield;
      else
      {
        //For a common stock, the current dividend yield is the ratio between a full-year 
        //dividend and current stock price. See: http://en.wikipedia.org/wiki/Dividend_yield

        // If the dividend schedule has only one input, we assume quarterly dividend 
        // payment frequency such that the full-year dividend is 4 * dividend amount
        if (dividends_.DividendDates.Length == 1)
          yield = 4.0*dividends_.DividendAmounts[0]/S0_;

        // If multiple dividends are given, check if time interval between pricing date
        // and last dividend date is longer than one year or not. If so, we use full-year
        // dividend amount to calculate dividend yield
        if(dividends_.DividendDates.Length > 1 && 
          Dt.Diff(AsOf, dividends_.DividendDates[dividends_.DividendDates.Length-1]) >= 365)
        {
          double div = 0.0;
          for(int i = 0; i < dividends_.DividendDates.Length; i++)
          {
            if (Dt.Diff(AsOf, dividends_.DividendDates[i]) < 365)
              div += dividends_.DividendAmounts[i];
            else
              break;
          }
          yield = div/S0_;
        }

        // If multiple dividends are given, but no full-year dividend can be computed
        // we estimate the full-year dividend by: 
        //    full-year dividend = sum of given dividends * 365 / time interval(AsOf, last dividendt date)
        if (dividends_.DividendDates.Length > 1 && 
          Dt.Diff(AsOf, dividends_.DividendDates[dividends_.DividendDates.Length - 1]) < 365)
        {
          double div = 0;
          // Get the time interval dt between last two dividends and expect next 
          // dividend date to occur at a date dt days after the last dividend date
          // and the expected amount of dividend is the last dividend amount
          int last = dividends_.DividendDates.Length - 1,
              dt = Dt.Diff(dividends_.DividendDates[last - 1], dividends_.DividendDates[last]);
          int i = 0;
          for (; i < dividends_.DividendDates.Length; i++)
            div += dividends_.DividendAmounts[i];
          
          // Add expected dividends if possible
          Dt future = Dt.Add(dividends_.DividendDates[last], dt);
          while (Dt.Diff(AsOf, future) < 365)
          {
            div += dividends_.DividendAmounts[last];
            future = Dt.Add(future, dt);
          }          

          yield = div / S0_;
        }
      }              
      return yield;
  }

    /// <summary>
    ///   Calculate discount rate spread (zspread/OAS) implied by full price
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates the constant spread (continuously compounded) over
    ///   discount curve for cashflow to match a specified full price
    ///   <paramref name="fullPrice"/>.</para>
    ///
    ///   <para>This is also commonly called the Z-Spread for non-callable bonds
    ///   and the OAS (option adjusted spread) for callable bonds.</para>
    /// 
    ///   <para>In other words the OAS is the Z-spread of the stripped(non-callable) bond 
    ///   when properly adjusting for the value of the embedded call option. Works for both callable and 
    ///   non-callable bonds. Callable bonds will require a HWTree pricer instead of a bond pricer.</para>
    /// 
    ///   <para>For non-defaultable callable bonds the zspread is the OAS i.e the shift that
    ///   needs to applied to the short rate in order to make model price and market price match.</para>
    /// 
    ///   <para>For defaultable callable bonds we approximate the zspread as the hazard rate of
    ///   a flat CDS Spread Curve with zero recovery which makes the model price match the bond
    ///   market price.</para>
    /// </remarks>
    ///
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <param name="incSurvival">If true include probability of survival</param>
    /// <param name="fixStock">Fix stock or not</param>
    ///
    /// <returns>spread over discount curve implied by price</returns>
    ///
    /// <summary>
    ///   Calculate ZSpread/OAS(Option Adjusted Spread) of a Callable Bond given full price.
    /// </summary>
    ///
    /// <remarks> The OAS is the Z-spread of the stripped(non-callable) bond when properly adjusting
    /// for the value of the embedded call option. 
    /// </remarks>
    ///
    public double ImpliedDiscountSpread(double fullPrice, bool incSurvival, bool fixStock)
    {
      var calc = (ConvertibleBondIntersectingTreesModel)MemberwiseClone();
      calc.DiscountCurve = CloneUtil.Clone(DiscountCurve);
      return calc.DoImplyDiscountSpread(fullPrice, incSurvival, fixStock);
    }

    private double DoImplyDiscountSpread(double fullPrice, bool incSurvival, bool fixStock)
    {
      logger.Debug(String.Format("Trying to solve oas for full price {0}", fullPrice));

      // If survival curve is not included, set it to null
      if (!incSurvival)
        SurvivalCurve = null;

      double result = Double.NaN;
      Brent rf = new Brent();
      rf.setToleranceX(1e-5);

      // Get the maximum spread of discount curve to be reduced
      double max = 1000;
      if (DiscountCurve.Tenors != null)
      {
        for (int i = 0; i < DiscountCurve.Tenors.Count; i++)
        {
          if (DiscountCurve.Tenors[i].Product is SwapLeg)
          {
            double coupon = ((SwapLeg) DiscountCurve.Tenors[i].Product).Coupon;
            if (coupon < max)
              max = coupon;
          }
          if(DiscountCurve.Tenors[i].Product is Note)
          {
            double coupon = ((Note)DiscountCurve.Tenors[i].Product).Coupon;
            if (coupon < max)
              max = coupon;
          }
        }
      }

      double lower = max == 1000 ? -0.001 : (-max+0.0001);
      double upper = 0.5;

      Double_Double_Fn fn = (double x, out string exceptionString) =>
      {
        double price = 0.0;
        exceptionString = null;
        try
        {
          price = EvaluatePriceShiftDC(x, fixStock);
        }
        catch (Exception e)
        {
          exceptionString = e.Message;
        }
        return price;
      };
      DelegateSolverFn solverFn = new DelegateSolverFn(fn, null);

      try
      {
        if( !incSurvival )
          SurvivalCurve = null;
        result = rf.solve(solverFn, fullPrice, lower, upper);        
      }
      catch (Exception e)
      {
        throw new ToolkitException(
          String.Format("Unable to find oas matching price {0}. Try fixStock=true. Last tried spread {1}",
                        fullPrice, rf.getCurrentSolution()), e);
      }

      logger.Debug(String.Format("Found zspread/ oas {0}", result));
      
      return result;
    }

     
    /// <summary>
    ///  Calculate the interest rate sensitivity
    ///  Interest sensitivity = [Pv(5 bps ir curve up) - Pv(5 bps ir curve down)] / 10bps
    /// </summary>
    /// <returns>Interest sensitivity</returns>
    public double InterestSensitivity()
    {
      if (rateTreeUp_ == null)
        rateTreeUp_ = RateModelUp.GetRateTree();
      if (rateTreeDown_ == null)
        rateTreeDown_ = RateModelDown.GetRateTree();

      StockCorrelatedModel savedModel = StockModel.Clone();
      var rateTreeClone = RateModel.CloneRateTree();

      double rateSensitivity = 0;
      RateModel.SetRateTree(RateModelUp.GetRateTree());
      prices_ = null;
      calcBondFloor_ = false;
      double up = Pv();
      prices_ = null;
      RateModel.SetRateTree(rateModelDown_.RateTree);
      calcBondFloor_ = false;
      double down = Pv();
      prices_ = null;
      RateModel.SetRateTree(rateTreeClone);

      // Find the rate sensitivity by dividing 10 bps
      rateSensitivity = (up - down) *0.1 / 10.0;

      // Restore states
      StockModel.Reset(savedModel);
      prices_ = null;
      calcBondFloor_ = true;
      return rateSensitivity;
    }

    /// <summary>
    ///  Calculate the credit sensitivity
    ///  Credit Sensitivity = [Pv(5 bps credit curve up) - Pv(5 bps credit curve down)] / 10bps 
    /// </summary>
    /// <returns>Credit Sensitivity</returns>
    public double CreditSensitivity()
    {
      if (survCurve_ == null) // for the risk-free valuation scenario
        return 0.0;

      double Pv0 = (prices_ != null ? prices_[0, 0] : this.Pv()) / 10.0;
      // save the original survival curve
      SurvivalCurve savedSurvCurve = CloneUtil.Clone(survCurve_);
      SurvivalCurve[] bumpedCuvres = new SurvivalCurve[] { survCurve_ };
      string[] bumpTenors = null;
      double[] avgUpBumps = bumpedCuvres.BumpQuotes(bumpTenors, QuotingConvention.CreditSpread, 0.05,
                BumpFlags.BumpRelative | BumpFlags.RefitCurve);
      CalcSurvivalprobs();
      prices_ = null;
      calcBondFloor_ = false;
      double up = Pv() / 10.0;
      prices_ = null;
      calcBondFloor_ = true;
      survCurve_ = savedSurvCurve;
      CalcSurvivalprobs();

      return (up - Pv0) / 5.0;
    }

    /// <summary>
    ///   Calculate the CDS spread/basis implied by full price.
    /// </summary>
    ///
    /// <remarks>
    ///   Calculates constant spread over survival curve spreads for
    ///   cashflow to match a specified full price.
    /// </remarks>
    ///
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    ///
    /// <returns>Spreads shift (also known as basis) to the Survival Curve implied by price</returns>
    ///
    public double ImpliedCDSSpread(double fullPrice)
    {
      double result = 0;
      if (fullPrice < 0)
        throw new ArgumentOutOfRangeException("fullPrice", "Full price must be +Ve");

      if (SurvivalCurve == null)
      {
        result = Double.NaN;
      }

      // Get the implied flat CDS curve and implied CDS level
      SurvivalCurve flatImpliedSurvivalCurve = null;
      try
      {
        flatImpliedSurvivalCurve = ImpliedFlatSpreadCurve(fullPrice, RecoveryRate > 0 ? RecoveryRate : 0.4);
      }
      catch (Exception e)
      {
        e.ToString();
        throw new ToolkitException(
          "Market level, credit spreads, and recovery assumption appear to be inconsistent." +
          " Check your inputs.  Cannot fit implied CDS curve."
          );
      }

      double impliedCDSLevel = CurveUtil.ImpliedSpread(
        flatImpliedSurvivalCurve, Maturity, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.None);

      double curveLevel = CurveUtil.ImpliedSpread(
        SurvivalCurve, Maturity, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.None);

      result = impliedCDSLevel - curveLevel;
      logger.Debug(String.Format("Found credit level {0}", result));

      return result;
    }

    /// <summary>
    ///  Compute an implied flat credit curve with which to match market price
    /// </summary>
    /// <param name="fullPrice">Full market price</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <returns>Implied flat credit curve</returns>
    public SurvivalCurve ImpliedFlatSpreadCurve(double fullPrice, double recoveryRate)
    {
      return ((ConvertibleBondIntersectingTreesModel)MemberwiseClone())
        .DoImplyFlatSpreadCurve(fullPrice, recoveryRate);
    }

    private SurvivalCurve DoImplyFlatSpreadCurve(double fullPrice, double recoveryRate)
    {
      const Currency ccy = Currency.USD;
      const string category = "None";
      const DayCount cdsDayCount = DayCount.Actual360;
      const Frequency cdsFreq = Frequency.Quarterly;
      const BDConvention cdsRoll = BDConvention.Following;
      Calendar cdsCalendar = Calendar.NYB;
      const InterpMethod interpMethod = InterpMethod.PCHIP;
      const ExtrapMethod extrapMethod = ExtrapMethod.Smooth;
      const NegSPTreatment nspTreatment = NegSPTreatment.Allow;
      double recov = recoveryRate;
      if (recoveryRate != RecoveryRate)
        recov = RecoveryRate > 0 ? RecoveryRate : 0.4;

      SurvivalCurve flatSpreadCurve =
        SurvivalCurve.FitCDSQuotes(AsOf, ccy, category, cdsDayCount, cdsFreq, cdsRoll, cdsCalendar,
                                   interpMethod, extrapMethod, nspTreatment, DiscountCurve, new string[] { "Maturity" },
                                   new Dt[] { Maturity }, null, new double[] { 1000 }, new double[] { recov }, 0, true, null);
      // Save original SC
      CalibratedCurve origSC = null;
      if (SurvivalCurve != null)
      {
        origSC = (CalibratedCurve)SurvivalCurve.Clone();
        origSC.Calibrator = (Calibrator)SurvivalCurve.SurvivalCalibrator.Clone();
      }

      // Set the flat curve
      SurvivalCurve = flatSpreadCurve;

      // Set up root finder
      Brent rf = new Brent();
      rf.setToleranceX(10e-6);
      rf.setToleranceF(10e-6);
      const double lowerBound = -0.1;
      const double upperBound = 1.0;

      Double_Double_Fn fn = (double x, out string exceptionString) =>
      {
        double price = 0.0;
        exceptionString = null;
        try
        {
          price = EvaluatePriceShiftSC(x);
        }
        catch (Exception e)
        {
          exceptionString = e.Message;
        }
        return price;
      };
      DelegateSolverFn solverFn = new DelegateSolverFn(fn, null);

      try
      {
        rf.solve(solverFn, fullPrice, lowerBound + 10e-8, upperBound);
      }
      catch (Exception e)
      {
        // Restore survival curve
        SurvivalCurve = (SurvivalCurve)origSC;
        prices_ = null;
        throw new ToolkitException(
          String.Format("Unable to find flat credit level matching price {0}. Last tried spread {1}",
                        fullPrice, rf.getCurrentSolution()), e);
      }
      double result = rf.getCurrentSolution();
      CurveUtil.CurveBump(flatSpreadCurve, null, result * 10000.0, true, false, true);

      // Restore survival curve
      SurvivalCurve = (SurvivalCurve)origSC;
      prices_ = null;

      logger.Debug(String.Format("Found credit level {0}", result));

      return flatSpreadCurve;
    }

    /// <summary>
    ///   Calculate the CDS spread/basis implied by full price.
    /// </summary>
    /// <remarks>
    ///   Calculates constant spread over survival curve spreads for
    ///   cashflow to match a specified full price.
    /// </remarks>
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <returns>Spreads shift (also known as basis) to the Survival Curve implied by price</returns>
    public double CDSSpreadShift(double fullPrice)
    {
      if (fullPrice < 0)
        throw new ArgumentOutOfRangeException("fullPrice", "Full price must be +Ve");

      if (SurvivalCurve == null)
        throw new ArgumentException("No Survival Curve passed to the pricer");

      return ((ConvertibleBondIntersectingTreesModel) MemberwiseClone())
        .DoImplyCdsSpreadShift(fullPrice);
    }

    private double DoImplyCdsSpreadShift(double fullPrice)
    {
      // save original SC
      CalibratedCurve origSC = (CalibratedCurve)SurvivalCurve.Clone();
      origSC.Calibrator = (Calibrator)SurvivalCurve.SurvivalCalibrator.Clone();

      // Find smallest quote
      CurveTenorCollection tenors = SurvivalCurve.Tenors;
      int count = SurvivalCurve.Tenors.Count;
      double minQuote = CurveUtil.MarketQuote(tenors[0]);
      for (int i = 1; i < count; ++i)
      {
        double quote = CurveUtil.MarketQuote(tenors[i]);
        if (quote < minQuote)
          minQuote = quote;
      }

      // Set up root finder
      Brent rf = new Brent();
      rf.setToleranceX(10e-6);
      rf.setToleranceF(10e-6);
      double lower = minQuote;
      double upper = 0.1;
      rf.setLowerBounds(-lower + 10e-8);
      rf.setUpperBounds(upper);
      Double_Double_Fn fn = new Double_Double_Fn(
        delegate(double x, out string exceptionString)
        {
          double price = 0.0;
          exceptionString = null;
          try
          {
            price = EvaluatePriceShiftSC(x);
          }
          catch (Exception e)
          {
            exceptionString = e.Message;
          }
          return price;
        }
        );
      DelegateSolverFn solverFn = new DelegateSolverFn(fn, null);

      try
      {
        rf.solve(solverFn, fullPrice, -lower + 10e-8, upper);
      }
      catch (Exception e)
      {
        // Restore survival curve
        SurvivalCurve = SurvivalCurve;
        prices_ = null;
        throw new ToolkitException(
          String.Format("Unable to find credit spread matching price {0}. Last tried spread {1}",
                        fullPrice, rf.getCurrentSolution()), e);
      }

      double result = rf.getCurrentSolution();

      // Restore survival curve
      SurvivalCurve = (SurvivalCurve)origSC;
      prices_ = null;

      logger.Debug(String.Format("Found credit spread {0}", result));

      return result;
    }

    // <summary>
    //  Evaluate the convertible bond price with shift credit curve
    // </summary>
    // <param name="x">Spread added to credit curve</param>
    // <returns>Bumped convertible bond price</returns>
    private double EvaluatePriceShiftSC(double x)
    {
      // save original SC
      CalibratedCurve origSC = (CalibratedCurve)SurvivalCurve.Clone();
      origSC.Calibrator = (Calibrator)SurvivalCurve.Calibrator.Clone();

      // Clone and shift original survival Curve
      SurvivalCurve shiftedSurvivalCurve = (SurvivalCurve)SurvivalCurve.Clone();
      shiftedSurvivalCurve.Calibrator = (Calibrator)SurvivalCurve.Calibrator.Clone();
      CurveUtil.CurveBump(shiftedSurvivalCurve, null, x * 10000.0, true, false, true);

      // Update Pricer and Tree Survival Curve
      SurvivalCurve = shiftedSurvivalCurve;

      prices_ = null;
      double price = Pv() / 1000.0;

      logger.DebugFormat("Trying rate spread {0} --> price {1}", x, price);

      // Restore survival curve in HW Tree and Pricer
      SurvivalCurve = (SurvivalCurve)origSC;
      return price;
    }

    // <summary>
    //  Evaluate the convertible bond price with shift discount curve
    // </summary>
    // <param name="x">Spread added to discount curve</param>
    // <param name="fixStock">Fix stock or not</param>
    // <returns>Bumped convertible bond price</returns>
    private double EvaluatePriceShiftDC(double x, bool fixStock)
    {
      double origSpread = DiscountCurve.Spread;

      // Update spread
      rateModel_ = null;
      DiscountCurve.Spread = origSpread + x;
      // Update stocks based on fixStock
      if(!fixStock)
        StockModel.BuildStockTree(RateModel);
      // Null prices_
      prices_ = null;

      double price = Pv()/1000.0;

      logger.DebugFormat("Trying rate spread {0} --> price {1}", x, price);

      // Restore spread
      DiscountCurve.Spread = origSpread;

      return price;
    }

    internal void Reset()
    {
      prices_ = null;
    }

    #endregion convertible bond measure methodsmethods

    #region data

    private int n_;
    private double dt_ = 10.0;
    private double sqrtDt_ = 0.0;
    private DiscountCurve discountCurve_;
    private SurvivalCurve survCurve_;
    private double S0_;
    private double sigmaS_;
    private StockCorrelatedModel.StockDividends dividends_;
    private double rho_;
    private double kappa_;
    private double sigmaR_;
    private double recoveryRate_ = -1;

    private Dt asOf_;
    private Dt settle_;
    private Dt maturity_;
    private double[,] softCallProbabilityMap_;
    private IReadOnlyList<double[]> rateTree_;
    private IReadOnlyList<double[]> rateTreeUp_;
    private IReadOnlyList<double[]> rateTreeDown_;
    private IReadOnlyList<double[]> discFacTree_;

    private IBinomialShortRateTreeModel rateModel_;
    private readonly ShortRateModelType _shortRateModelType;
    private StockCorrelatedModel stockModel_;
    private StockCorrelatedModel stockModelUp_;
    private StockCorrelatedModel stockModelDown_;
    private StockCorrelatedModel stockModelVolUp_;
    private StockCorrelatedModel stockModelVolDown_;
    private BlackKarasinskiBinomialTreeModel rateModelUp_;
    private BlackKarasinskiBinomialTreeModel rateModelDown_;
    private Bond bond_;
    private double redeptionPrice_;
    private bool withAccrualOnCall_;
    private bool withAccrualOnConversion_;
    private bool calcBondFloor_ = true;
    private double bondFloor_ = Double.NaN;

    #region data for some sensitivities
    private double delta_ = Double.NaN;
    private double gamma_ = Double.NaN;
    private double hedgeRatio_ = Double.NaN;
    private double vega_ = Double.NaN;
    private double pv0_ = Double.NaN;
    private double pvUp_ = Double.NaN;
    private double pvDown_ = Double.NaN;
    private bool needDelta_ = false;
    private double pvStockVolUp_ = Double.NaN;
    private double pvStockVolDown_ = Double.NaN;
    private bool needVega_ = false;
    private bool needOAS_ = false;
    private double oas_ = Double.NaN;
    private double effectiveDuration_ = Double.NaN;
    private double effectiveConvexity_ = Double.NaN;
    #endregion data for some sensitivities

    private Schedule couponSched_;
    private Dt[] treeDates_;
    private double[,] currentStockPrices_;
    private double[,] prices_;

    private double[,] bondFloorPrices_;

    private double[] defaultProbs_;
    private double[] callPrices_;
    private double[] putPrices_;
    private double[] accrualInterest_;
    private double[][] couponPayments_;
    private double[][] couponDiscountFraction_;

    private double dS_ = 0.04;
    private double stockVolBumpUp_ = 0.005;
    private double stockVolBumpDown_ = 0.005;
    private double rateVolBump_ = 0.01;
    #endregion data

    /// <summary>
    ///  Inner class to built the rate-correlated stock prices tree
    /// </summary>
    [Serializable]
    public class StockCorrelatedModel
    {
      #region Stock dividend class

      /// <summary>
      /// Stock dividend schedule
      /// </summary>
      [Serializable]
      public class StockDividends
      {

        /// <summary>
        ///  Constructor
        /// </summary>
        /// <param name="dates">Dividend dates</param>
        /// <param name="amounts">Dividend dollar amounts</param>
        public StockDividends(Dt[] dates, double[] amounts)
        {
          DividendDates = dates;
          DividendAmounts = amounts;
        }

        /// <summary>
        ///  Constructor
        /// </summary>
        /// <param name="dates">Dividend dates</param>
        /// <param name="amounts">Dividend dollar amounts</param>
        /// <param name="freq">Dividend payment frequency</param>
        public StockDividends(Dt[] dates, double[] amounts, Frequency freq)
        {
          DividendDates = dates;
          DividendAmounts = amounts;
        }

        /// <summary>
        ///  Dividend yield 
        /// </summary>
        private double _yield;

        /// <summary>
        ///  Get dividend yield if dividend is a yield
        /// </summary>
        /// <remarks>
        ///   <para>When both dividend dates and dividends are null (or of length 0)
        ///  the continous of 0 dividend yield is implied.</para>
        /// </remarks>
        public double DividendYield
        {
          get
          {
            if(IsDividendYield)
              return _yield;
            else
              return Double.NaN;
          }
        }

        /// <summary>
        ///  Get dividend pay dates
        /// </summary>
        public Dt[] DividendDates { get; private set; }

        /// <summary>
        ///  Get dividend dollar amounts
        /// </summary>
        public double[] DividendAmounts { get; private set; }

        /// <summary>
        ///  True is the dividend is a yield
        /// </summary>
        public bool IsDividendYield
        {
          get
          {
            if ((DividendDates == null || DividendDates.Length == 0) &&
              (DividendAmounts != null && DividendAmounts.Length == 1))
            {
              _yield = DividendAmounts[0];
              return true;
            }
            if((DividendDates == null || DividendDates.Length == 0) &&
              (DividendAmounts == null || DividendAmounts.Length == 0))
            {
              _yield = 0.0;
              return true;
            }
            return false;
          }
        }
      }

      #endregion Stock dividend class

      #region constructors      
      /// <summary>
      ///  Constructor of StockCorrelatedModel
      /// </summary>
      /// <param name="S0">Initial stock price</param>
      /// <param name="sigma">Stock volatility</param>
      /// <param name="divYield">Dividends schedule</param>
      /// <param name="T">Time to maturity</param>
      /// <param name="n">Number of time intervals</param>
      /// <param name="rho">Correlation between rate and stock</param>
      public StockCorrelatedModel(double S0, double sigma, StockDividends divYield, double T, int n, double rho)
      {
        S0_ = S0;
        sigma_ = sigma;
        dividends_ = divYield;
        T_ = T;
        n_ = n;
        dt_ = T / n;
        sqrtDt_ = Math.Sqrt(dt_);
        rho_ = rho;
        // Set the discrete dividend handling flag
        forwardLookDiscreteDividend_ = true;
      }

      /// <summary>
      ///  Constructor
      /// </summary>
      /// <param name="S0">Initial stock price</param>
      /// <param name="sigma">Volatility of stock price</param>
      /// <param name="divYield">Dividends schedule</param>
      /// <param name="asOf">AsOf date</param>
      /// <param name="maturity">Convertible bond maturity</param>
      /// <param name="n">Number of tree layers</param>
      /// <param name="rho">Correlation between rate and stock</param>      
      public StockCorrelatedModel(double S0, double sigma, StockDividends divYield, Dt asOf, Dt maturity, int n, double rho)
      {
        S0_ = S0;
        sigma_ = sigma;
        dividends_ = divYield;
        asOf_ = asOf;
        maturity_ = maturity;
        T_ = maturity_.ToDouble() - asOf_.ToDouble();
        n_ = n;
        dt_ = T_ / n;
        sqrtDt_ = Math.Sqrt(dt_);
        rho_ = rho;
        // Set the discrete dividend handling flag
        forwardLookDiscreteDividend_ = true;
      }

      #endregion constructors

      #region methods for StockCorrelatedModel
      /// <summary>
      ///  Build the correlated binomial trees used for getting stock prices
      /// </summary>
      /// <param name="rateTree">Short interest rate binomial tree</param>
      public void BuildStockTree(IReadOnlyList<double[]> rateTree)
      {
        bTree_ = new List<double[]>();
        // If dividend is a continuous yield, use S0_ as starting node
        bool isDividendYield = dividends_.IsDividendYield;
        double yield = isDividendYield ? dividends_.DividendYield : 0.0;
        if(isDividendYield || forwardLookDiscreteDividend_)
          bTree_.Add(new double[] {Math.Log(S0_)});
        else
        {
          // Otherwise use S0_ - Pv(all future discrete dividends) as starting node
          bTree_.Add(new double[] {Math.Log(S0_ - dividendsPv_[0])});
        }

        cTree_ = new List<double[]>();
        cTree_.Add(new double[] {0.0});

        List<double[]> condProbs = CalcConditionalProbabilities(n_ + 1);
        double sigmaRhoSqrtDt = sigma_*rho_*sqrtDt_;
        double sigmrSquareBy2 = sigma_*sigma_/2.0;
        double sigmaSqrtRhoDt = sigma_*Math.Sqrt((1 - rho_*rho_)*dt_);
        for (int k = 1; k <= n_; k++)
        {
          bTree_.Add(new double[k + 1]);
          cTree_.Add(new double[k + 1]);
          for (int l = 0; l <= k; l++)
          {
            double bUp = (l >= k ? 0 : bTree_[k - 1][l]);
            double bDn = (l < 1 ? 0 : bTree_[k - 1][l - 1]);
            double rUp = (l == 0 ? rateTree[k - 1][0] : (l == k ? rateTree[k - 1][l - 1] : rateTree[k - 1][l]));
            double rDn = (l == 0 ? rateTree[k - 1][0] : (l == k ? rateTree[k - 1][l - 1] : rateTree[k - 1][l - 1]));
            double val = condProbs[k][l]*(bUp + (rUp - yield - sigmrSquareBy2)*dt_ - sigmaRhoSqrtDt) +
                         (1 - condProbs[k][l]) * (bDn + (rDn - yield - sigmrSquareBy2) * dt_ + sigmaRhoSqrtDt);
            bTree_[k][l] = val;

            double cUp = (l >= k ? 0 : cTree_[k - 1][l]);
            double cDn = (l < 1 ? 0 : cTree_[k - 1][l - 1]);
            val = condProbs[k][l]*(cUp - sigmaSqrtRhoDt) +
                  (1 - condProbs[k][l])*(cDn + sigmaSqrtRhoDt);
            cTree_[k][l] = val;
          }
        }

        // Setup divs
        PrecomputeDividendAdjustments();
      }

      /// <summary>
      ///  Build the correlated binomial trees used for getting stock prices
      /// </summary>
      /// <param name="model">BlackKarasinskiBinomialTreeModel</param>
      public void BuildStockTree(IBinomialShortRateTreeModel model)
      {        
        if (model != null)
        {
          // Build the dividend present value vector
          if (!dividendCalcReady_)
          {
            if (dividends_ != null && !dividends_.IsDividendYield)
            {
              if (dividendsPv_ == null || dividendsPv_.Length == 0)
                dividendsPv_ = new double[n_ + 1];

              if (accumuDividends_ == null || accumuDividends_.Length == 0)
                accumuDividends_ = new double[n_ + 1];


              var nodeTimes = new double[n_ + 1];
              for (int i = 0, j = 0; i <= n_; i++)
              {
                nodeTimes[i] = asOf_.ToDouble() + i*dt_;
                if (j < dividends_.DividendDates.Length && nodeTimes[i] >= dividends_.DividendDates[j].ToDouble())
                {
                  accumuDividends_[i] += accumuDividends_[i - 1] + dividends_.DividendAmounts[j];
                  j++;
                }
                else
                {
                  accumuDividends_[i] = i > 0 ? accumuDividends_[i - 1] : 0.0;
                }
              }
              var irCurve = (DiscountCurve) model.DiscountCurve.Clone();
              for (int i = 0; i <= n_; i++)
              {
                dividendsPv_[i] = 0.0;
                for (int j = 0; j < dividends_.DividendDates.Length; j++)
                {
                  if (dividends_.DividendDates[j].ToDouble() >= nodeTimes[i])
                  {
                    Dt nodeDate = new Dt(nodeTimes[i]);
                    dividendsPv_[i] += dividends_.DividendAmounts[j]*
                                       irCurve.DiscountFactor(nodeDate, dividends_.DividendDates[j]);
                  }
                }
              }
            }
            dividendCalcReady_ = true;
          }
        }
        BuildStockTree(model == null ? null : model.GetRateTree());
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="savedModel"></param>
      internal void Reset(StockCorrelatedModel savedModel)
      {
        bTree_ = savedModel.bTree_;
        cTree_ = savedModel.cTree_;
      }

      /// <summary>
      ///  Get the stock prices square for current time index k
      /// </summary>
      /// <param name="k">Current time index</param>
      /// <returns>A (k+1, k+1) matrix of stock prices</returns>
      public double[,] GetStockPrices(int k)
      {
        var stocks = new double[k + 1,k + 1];

        for (int l = 0; l <= k; l++)
        {
          for (int m = 0; m <= k; m++)
          {
            stocks[l, m] = dividends_.IsDividendYield
                             ?
                               Math.Exp(bTree_[k][l] + cTree_[k][m])
                             :
                               Math.Exp(bTree_[k][l] + cTree_[k][m]) + 
                               (forwardLookDiscreteDividend_?(-accumuDividends_[k]):dividendsPv_[k]);
          }
        }
        return stocks;
      }

      /// <summary>
      /// Gets the soft call adjusted stock price.
      /// </summary>
      /// <param name="k">The k.</param>
      /// <param name="l">The l.</param>
      /// <param name="m">The m.</param>
      /// <returns>
      ///   <see cref="Double"/>
      /// </returns>
      public double GetSoftCallAdjustedStockPrice(int k, int l, int m)
      {
        double stockPrice = Math.Exp(bTree_[k][l] + cTree_[k][m]);
        if(dividends_.IsDividendYield)
          return stockPrice;
        return stockPrice - divAdjustments_[k];
      }

      /// <summary>
      /// Gets the dividend time adjustment.
      /// </summary>
      private void PrecomputeDividendAdjustments()
      {
        // Handle no divs
        if (dividends_ == null || dividends_.DividendDates == null || dividends_.DividendDates.Length == 0)
        {
          divAdjustments_ = ArrayUtil.NewArray(n_, 0.0);
          return;
        }

        const double frac = 30 / 365.0;
        divAdjustments_ = new double[n_];
        int ti = 0;
        Dt curDate = asOf_;
        double curDateVal = curDate.ToDouble();
        for (int i = 0; i < dividends_.DividendDates.Length; i++)
        {
          var maxDate = (i == dividends_.DividendDates.Length - 1 ? maturity_ : dividends_.DividendDates[i + 1]);
          var divDateVal = dividends_.DividendDates[i].ToDouble();
          while (curDate < maxDate && ti < n_)
          {
            if (curDateVal - frac <= divDateVal && divDateVal <= curDateVal)
            {
              // Dividend is in last 30 days, linear adjust
              divAdjustments_[ti] = dividends_.DividendAmounts[i] * (1.0 - ((curDateVal - divDateVal) / frac));
            }
            else
            {
              // Div is outside the soft-call window
              divAdjustments_[ti] = 0;
            }

            // Next time step
            ti++;
            curDateVal = asOf_.ToDouble() + ti*dt_;
            curDate = new Dt(curDateVal);
          }
        }
      }

      /// <summary>
      ///  Clone a stock Correlated model by cloning the bTree_ and cTree_ inside
      /// </summary>
      /// <returns>Cloned StockCorrelatedModel</returns>
      internal StockCorrelatedModel Clone()
      {
        StockCorrelatedModel model = new StockCorrelatedModel(
          this.S0, this.Volatility, this.dividends_, this.T, this.N, this.Correlation);

        model.bTree_ = new List<double[]>(bTree_.Count);
        model.cTree_ = new List<double[]>(cTree_.Count);

        for (int i = 0; i < bTree_.Count; i++ )
        {
          model.bTree_.Add((double[])bTree_[i].Clone());
        }
        for (int i = 0; i < cTree_.Count; i++ )
        {
          model.cTree_.Add((double[])cTree_[i].Clone());
        }
        return model;
      }

      /// <summary>
      ///  Get the upper and lower bounds of the stock tree
      /// </summary>
      /// <param name="k"></param>
      /// <returns></returns>
      public double[,] GetStockBounds(int k)
      {
        double[,] bounds = new double[2,k + 1];
        double[,] stocks = null;
        for (int m = 0; m <= k; m++)
        {
          stocks = GetStockPrices(k);
          bounds[0, m] =stocks[0, m];
          bounds[1, m] = stocks[k, m];
        }
        return bounds;
      }

      #endregion methods for StockCorrelatedModel

      #region properties

      /// <summary>
      ///  The initial stock price
      /// </summary>
      public double S0
      {
        get { return S0_; }
      }

      /// <summary>
      ///  Volatility of stock return
      /// </summary>
      public double Volatility
      {
        get { return sigma_; }
      }

      /// <summary>
      ///  Get number of tree layers
      /// </summary>
      public int N
      {
        get { return n_;}
      }

      /// <summary>
      ///  Get the maturity time in years
      /// </summary>
      public double T
      {
        get { return T_; }
      }

      /// <summary>
      ///  Correlation between two driving Brownian motions (WR and WS)
      /// </summary>
      public double Correlation
      {
        get { return rho_; }
      }
      #endregion properties

      #region data
      // initial stock price
      private double S0_;
      // stock volatility
      private double sigma_;
      // dividends schedule
      private StockDividends dividends_;
      // present values of dividends for each time step
      private double[] dividendsPv_;
      // accumulated dividends looking forward along the time line
      private double[] accumuDividends_;
      // flag to use either forward looking discrete dividend approach or
      // use backward looking present value of discrete dividends approach
      private bool forwardLookDiscreteDividend_;
      // number of timm intervals
      private int n_;
      // time to maruity
      private double T_;
      // asof date
      private Dt asOf_;
      // maturity of the tree
      private Dt maturity_;
      // time interval
      private double dt_;
      // square root of time interval
      private double sqrtDt_;
      // correlation between two Brownian motions that drive rate and stock
      private double rho_;
      // intersecting binomial trees used to fetch square block of stock prices 
      private List<double[]> bTree_ = null;
      private List<double[]> cTree_ = null;
      private bool dividendCalcReady_;
      private double[] divAdjustments_;
      #endregion data

    }
  }
}
