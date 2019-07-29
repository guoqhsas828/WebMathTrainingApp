/*
 * CorrelationCalc.cs
 *
 *  -2008. All rights reserved.
 *
 * Calculate implied compound correlation and base correlations
 *
 */

using System;

using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   <para>Price a <see cref="SyntheticCDO">Synthetic CDO</see> using the
  ///   <see cref="BasketPricer">Basket Pricer Model</see>.</para>
  /// </summary>
  ///
  /// <seealso cref="SyntheticCDO">Synthetic CDO Tranche Product</seealso>
  /// <seealso cref="BasketPricer">Basket Pricer</seealso>
  ///
  public class CorrelationCalc
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CorrelationCalc));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    private
        CorrelationCalc()
    {
      pricer_ = null;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// 
    /// <exclude />
    public CorrelationCalc(SyntheticCDOPricer pricer)
    {
      pricer_ = pricer;
    }

    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Calculate the implied tranche break-even correlation for a CDO tranche
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates the uniform one-factor correlation which implies a
    ///   present value of zero for the tranche.</para>
    /// </remarks>
    ///
    /// <param name="target">The value of PV to match</param>
    /// <param name="pricer">The synthetic CDO pricer</param>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Break-even correlation for Synthetic CDO tranche</returns>
    ///
    public static double
    ImpliedCorrelationPv(
        double target,
        SyntheticCDOPricer pricer,
        double toleranceF,
        double toleranceX)
    {
      pricer = (SyntheticCDOPricer)pricer.Clone();
      CorrelationCalc calc = new CorrelationCalc(pricer);
      double corr = calc.ImpliedCorrelationPv(target, toleranceF, toleranceX);
      return corr;
    }


    /// <summary>
    ///   Calculate the implied correlation for a CDO tranche
    ///   to a given value of protection pv
    /// </summary>
    ///
    /// <param name="target">The protection pv to match</param>
    /// <param name="pricer">The synthetic CDO pricer</param>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>The implied correlation for Synthetic CDO tranche</returns>
    public static double
    ImpliedCorrelationProtectionPv(
        double target,
        SyntheticCDOPricer pricer,
        double toleranceF,
        double toleranceX)
    {
      pricer = (SyntheticCDOPricer)pricer.Clone();
      CorrelationCalc calc = new CorrelationCalc(pricer);
      double corr = calc.EstimateImpliedCorrelationProtectionPv(target, toleranceF, toleranceX);
      return corr;
    }


    /// <summary>
    ///   Calculate the implied base and tranche correlations for an array of CDO tranches
    /// </summary>
    ///
    /// <param name="pricers">Array of Synthetic CDO pricers</param>
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Implied break even correlations and base correlations
    ///   for Synthetic CDO tranches</returns>
    public static double[,]
    ImpliedCorrelation(
        SyntheticCDOPricer[] pricers,
        BaseCorrelationMethod method,
        double toleranceF,
        double toleranceX)
    {
      // Validate
      if (pricers == null || pricers.Length < 1)
        throw new ArgumentException("Must specify at least one CDO pricer");
      if (pricers[0].CDO.Attachment != 0.0)
        throw new ArgumentException(String.Format("The start attachment point {0} is not zero", pricers[0].CDO.Attachment));
      for (int i = 1; i < pricers.Length; ++i)
      {
        if (pricers[i].CDO.Attachment != pricers[i - 1].CDO.Detachment)
          throw new ArgumentException(String.Format("Discontinued tranches {0}~{1} and {2}~{3}",
                                                                                                                               pricers[i - 1].CDO.Attachment, pricers[i - 1].CDO.Detachment,
                                                                                                                               pricers[i].CDO.Attachment, pricers[i].CDO.Detachment));
        if (pricers[i].CDO.Maturity != pricers[0].CDO.Maturity)
          throw new ArgumentException(String.Format("Maturity of tranche #{0} differs", i));
        if (pricers[i].Basket != pricers[0].Basket)
          throw new ArgumentException(String.Format("All pricers must share the same underlying basket. Tranche #{0} differs", i));
        if (pricers[i].DiscountCurve != pricers[0].DiscountCurve)
          throw new ArgumentException(String.Format("All pricers must share the same Discount Curve. Tranche #{0} differs", i));
      }

      switch (method)
      {
        case BaseCorrelationMethod.ArbitrageFree:
          return ImpliedCorrelationAbitrageFree(pricers, toleranceF, toleranceX);
        case BaseCorrelationMethod.ProtectionMatching:
          return ImpliedCorrelationProtectionMatching(pricers, toleranceF, toleranceX);
      }

      return null;
    }


    /// <summary>
    ///   Imply tranche correlation from base correlation
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates the implied tranche correlation for a synthetic CDO tranche from a
    ///   base correlation pair.</para>
    /// </remarks>
    ///
    /// <param name="pricer">synthetic CDO pricer</param>
    /// <param name="method">base correlation method (arbitrage free or protection match)</param>
    /// <param name="apCorrelation">attachment correlation</param>
    /// <param name="dpCorrelation">detachment correlation</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Implied tranche correlation</returns>
    public static double
    TrancheCorrelation(
        SyntheticCDOPricer pricer,
        BaseCorrelationMethod method,
        double apCorrelation,
        double dpCorrelation,
        double toleranceF,
        double toleranceX
        )
    {
      pricer = (SyntheticCDOPricer)pricer.Clone();
      double totalPrincipal = pricer.TotalPrincipal;
      BasketPricer basket = pricer.Basket;
      SyntheticCDO cdo = pricer.CDO;

      double corr = Double.NaN;
      if (Double.IsNaN(dpCorrelation))
      {
        corr = ImpliedCorrelationPv(0.0, pricer, toleranceF, toleranceX);
        return corr;
      }

      switch (method)
      {
        case BaseCorrelationMethod.ArbitrageFree:
          {
            SetFactor(basket, Math.Sqrt(dpCorrelation));

            double oldPrem = cdo.Premium;
            double oldFee = cdo.Fee;
            double oldAP = cdo.Attachment;
            double oldDP = cdo.Detachment;

            double upfrontfee = 0;//pricer.UpFrontFeePv();
            cdo.Fee = 0.0;

            cdo.Attachment = 0;
            cdo.Premium = 1.0;
            pricer.Notional = totalPrincipal * cdo.TrancheWidth;
            double protection1 = -pricer.ProtectionPv();
            double fee1 = pricer.FeePv();

            SetFactor(basket, Math.Sqrt(apCorrelation));
            cdo.Detachment = oldAP;
            pricer.Notional = totalPrincipal * cdo.TrancheWidth;
            double protection0 = -pricer.ProtectionPv();
            double fee0 = pricer.FeePv();

            double premium = (protection1 - protection0 - upfrontfee) / (fee1 - fee0);
            if (premium >= 0)
            {
              cdo.Premium = premium;
            }
            else
            {
              // In this case, we set primium to be zero and
              // increase upfront fee to the break-even level
              cdo.Premium = 0;
              //cdo.Fee = cdo.Fee * (protection1 - protection0) / upfrontfee;
              cdo.Fee = protection1 - protection0;
            }
            cdo.Attachment = oldAP;
            cdo.Detachment = oldDP;
            pricer.Notional = totalPrincipal * cdo.TrancheWidth;
            corr = ImpliedCorrelationPv(0.0, pricer, toleranceF, toleranceX);
            cdo.Premium = oldPrem;
            cdo.Fee = oldFee;
          }
          break;
        case BaseCorrelationMethod.ProtectionMatching:
          {
            double target = ComputeProtectPv(dpCorrelation, cdo.Detachment, pricer)
              - ComputeProtectPv(apCorrelation, cdo.Attachment, pricer);
            corr = CorrelationCalc.ImpliedCorrelationProtectionPv(target,
              pricer, toleranceF, toleranceX);
          }
          break;
        default:
          throw new ArgumentOutOfRangeException("method", "Unsupported base correlation method");
      }

      return corr;
    }

    //
    // Private routines
    //

    // routine to compute protection pv
    // Called by TrancheCorrelation
    //
    private static double
    ComputeProtectPv(
        double correlation,
        double trancheEnd,
        SyntheticCDOPricer pricer
        )
    {
      double totalPrincipal = pricer.TotalPrincipal;
      BasketPricer basket = pricer.Basket;
      SetFactor(basket, Math.Sqrt(correlation));
      SyntheticCDO cdo = pricer.CDO;
      double oldAP = cdo.Attachment;
      double oldDP = cdo.Detachment;
      cdo.Attachment = 0;
      cdo.Detachment = trancheEnd;
      pricer = new SyntheticCDOPricer(cdo, basket, pricer.DiscountCurve,
        totalPrincipal * cdo.TrancheWidth, pricer.RateResets);
      double pv = pricer.ProtectionPv();
      cdo.Attachment = oldAP;
      cdo.Detachment = oldDP;

      return pv;
    }


    const int InitialSearchPoints = 7;

    // BEGIN OF SOLVER ROUTINES
    //
    // These are general solver routines with a bit tune for correlations
    //
    enum BracketResult
    {
      SolutionBracket,
      SolutionFound,
      FailBracket
    };

    //
    // Bracket the target: using the monotonic property
    //
    private BracketResult
    BracketBaseTranche(
        Double_Double_Fn fn,
        double target,
        double toleranceF,
        double[] result,
        out string exceptDesc)
    {
      exceptDesc = null;

      double xl, fl, xh, fh;
      xl = 0.40;
      fl = fn(xl, out exceptDesc);
      if (target <= fl)
      {
        xh = xl; fh = fl;
        xl = 0.0;
        fl = fn(xl, out exceptDesc);
        if (target <= fl)
        {
          if (fl - target < toleranceF)
          {
            result[0] = xl * xl;
            return BracketResult.SolutionFound;
          }
          throw new SolverException(String.Format("The break even correlation cannot be found, possibly because the premium {0} is too big.", pricer_.CDO.Premium));
        }
      }
      else // target > fl 
      {
        xh = 0.80;
        fh = fn(xh, out exceptDesc);
        if (target >= fh)
        {
          xl = xh; fl = fh;
          xh = 0.99;
          fh = fn(xh, out exceptDesc);
          if (target >= fh)
          {
            if (target - fh < toleranceF)
            {
              result[0] = xh * xh;
              return BracketResult.SolutionFound;
            }
            xl = xh; fl = fh;
            xh = 1.0;
            fh = fn(xh, out exceptDesc);
            if (target - fh < toleranceF)
            {
              // for extremly high correlation, using simple linear interpolation
              double x = xl + (xh - xl) / (fh - fl) * (target - fl);
              result[0] = x * x;
              return BracketResult.SolutionFound;
            }

            xl = xh; fl = fh;
            xh = 2.0;
            fh = fn(xh, out exceptDesc);
            if (target - fh > toleranceF)
              throw new SolverException(String.Format("The break even correlation cannot be found, possibly because the premium {0} is too small or correlation is larger than 0.99", pricer_.CDO.Premium));
          }
        }
      }

      result[0] = xl;
      result[1] = fl;
      result[2] = xh;
      result[3] = fh;
      return BracketResult.SolutionBracket;
    }

    private static double
    BrentSolver(
        Double_Double_Fn fn,
        double target,
        double a, double fa,
        double b, double fb,
        double toleranceF,
        double toleranceX,
        out string message)
    {
      const int maxIterations = 1000;
      message = null;

      double c, d, e;
      fa -= target; fb -= target;
      double fc = fb;
      c = b;
      e = d = b - a;
      for (int numIter = 1; numIter <= maxIterations; numIter++)
      {
        if ((fb * fc) >= 0.0)
        {
          c = a;
          fc = fa;
          e = d = b - a;
        }
        if (Math.Abs(fc) <= Math.Abs(fb))
        {
          a = b;
          b = c;
          c = a;
          fa = fb;
          fb = fc;
          fc = fa;
        }
        double tol1 = 2.0 * toleranceX * Math.Abs(b) + 0.5 * toleranceF;
        double xm = 0.5 * (c - b);
        if (Math.Abs(xm) <= tol1 || Math.Abs(fb) <= toleranceF)
          return b;
        if (Math.Abs(e) >= tol1 && Math.Abs(fa) > Math.Abs(fb))
        {
          double p, q;
          double s = fb / fa;
          if (a == c)
          {
            p = 2.0 * xm * s;
            q = 1.0 - s;
          }
          else
          {
            double r = fb / fc;
            q = fa / fc;
            p = s * (2.0 * xm * q * (q - r) - (b - a) * (r - 1.0));
            q = (q - 1.0) * (r - 1.0) * (s - 1.0);
          }
          if (p > 0.0)
            q = -q;
          p = Math.Abs(p);
          double min1 = 3.0 * xm * q - Math.Abs(tol1 * q);
          double min2 = Math.Abs(e * q);
          if (2.0 * p < (min1 < min2 ? min1 : min2))
          {
            e = d;
            d = p / q;
          }
          else
          {
            d = xm;
            e = d;
          }
        }
        else
        {
          d = xm;
          e = d;
        }
        a = b;
        fa = fb;
        if (Math.Abs(d) > tol1)
          b += d;
        else
          b += (xm > 0.0 ? Math.Abs(tol1) : -Math.Abs(tol1));
        fb = fn(b, out message) - target;
      }

      return b;
    }


    //
    // Bracket the target: no monotonicity assumption
    //
    private BracketResult
    BracketGeneralTranche(
        Double_Double_Fn fn,
        double target,
        double xMin, double xMax,
        double toleranceA,
        double toleranceF,
        double[] result,
        out string exceptDesc)
    {
      exceptDesc = null;

      double x0, f0, xl, fl, xh, fh;

      result[0] = x0 = xl = xMin;
      result[1] = f0 = fl = fn(xl, out exceptDesc);
      double dl = fl - target;
      if (dl >= -toleranceF && dl <= toleranceF)
        return BracketResult.SolutionFound;

      result[2] = xh = xMin + 0.7 * (xMax - xMin);
      result[3] = fh = fn(xh, out exceptDesc);
      double dh = fh - target;
      if (dh >= -toleranceF && dh <= toleranceF)
      {
        result[0] = xh * xh;
        return BracketResult.SolutionFound;
      }

      if (dl * dh < 0)
        return BracketResult.SolutionBracket;

      result[0] = xl = xh;
      result[1] = fl = fh;
      dl = dh;

      result[2] = xh = xMax;
      result[3] = fh = fn(xh, out exceptDesc);
      dh = fh - target;
      if (dh >= -toleranceF && dh <= toleranceF)
      {
        result[0] = xh * xh;
        return BracketResult.SolutionFound;
      }

      if (dl * dh < 0)
        return BracketResult.SolutionBracket;

      result[0] = x0;
      result[1] = f0;
      result[2] = xl;
      result[3] = fl;
      BracketResult rcode = DoBracketTranche(
          fn,
          target,
          toleranceA,
          toleranceF,
          result,
          out exceptDesc);
      if (BracketResult.FailBracket != rcode)
        return rcode;

      result[0] = xl;
      result[1] = fl;
      result[2] = xh;
      result[3] = fh;
      rcode = DoBracketTranche(
          fn,
          target,
          toleranceA,
          toleranceF,
          result,
          out exceptDesc);
      if (BracketResult.FailBracket == rcode)
        throw new SolverException("Fail to bracket a solution");

      return rcode;
    }

    private BracketResult
    DoBracketTranche(
        Double_Double_Fn fn,
        double target,
        double toleranceA,
        double toleranceF,
        double[] result,
        out string exceptDesc)
    {
      exceptDesc = null;

      double xl = result[0];
      double dl = result[1] - target;
      double xh = result[2];
      double dh = result[3] - target;

      if (xh - xl < toleranceA)
        return BracketResult.FailBracket;

      double x = (xl + xh) / 2;
      double f = fn(x, out exceptDesc);
      double d = f - target;
      if (d >= -toleranceF && d <= toleranceF)
      {
        result[0] = x * x;
        return BracketResult.SolutionFound;
      }

      result[2] = x;
      result[3] = f;
      if (dl * d < 0)
        return BracketResult.SolutionBracket;

      BracketResult rcode = DoBracketTranche(
          fn,
          target,
          toleranceA,
          toleranceF,
          result,
          out exceptDesc);
      if (BracketResult.FailBracket != rcode)
        return rcode;

      result[0] = x;
      result[1] = f;
      result[2] = xh;
      result[3] = dh + target;
      rcode = DoBracketTranche(
          fn,
          target,
          toleranceA,
          toleranceF,
          result,
          out exceptDesc);
      return rcode;
    }
    //
    //
    // END OF SOLVER ROUTINES


    /// <summary>
    ///   Calculate the implied tranche break-even correlation for a CDO tranche
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates the uniform one-factor correlation which implies a
    ///   present value of zero for the tranche.</para>
    /// </remarks>
    ///
    /// <param name="target">The value of PV to match</param>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Break-even correlation for Synthetic CDO tranche</returns>
    /// 
    /// <exclude />
    public double
    ImpliedCorrelationPv(
        double target,
        double toleranceF,
        double toleranceX)
    {
      // DOTO: how to make this work with base correlation object?
      if (pricer_.Correlation is BaseCorrelationObject)
        throw new System.NotSupportedException("Implied tranche correlation does work with Base Correlation Object yet");

      // Save original basket pricer and correlation
      double result;
      if (pricer_.CDO.Attachment < 1e-5)
        result = EstimateImpliedBaseCorrelationPv(
            target,
            toleranceF,
            toleranceX);
      else
        result = EstimateImpliedCorrelationPv(
            target,
            toleranceF,
            toleranceX);
      return result;
    }

    // Special case: base correlation
    private double
    EstimateImpliedBaseCorrelationPv(
        double target,
        double toleranceF,
            double toleranceX)
    {
      string exceptDesc = null;

      Double_Double_Fn fn = new Double_Double_Fn(this.EvaluatePv);
      double[] bracket = new double[4];
      if (BracketResult.SolutionFound == BracketBaseTranche(fn, target, toleranceF, bracket, out exceptDesc))
        return bracket[0];
      double x0 = BrentSolver(
          fn, target,
          bracket[0], bracket[1],
          bracket[2], bracket[3],
          toleranceF, toleranceX, //5e-9, 5e-7,
          out exceptDesc);

      return x0 * x0;
    }

    //
    // Private function for root find for base correlation.
    // Called by ImpliedBaseCorrelationPv() and ImpliedCorrelationPv()
    //
    private double
    EvaluatePv(double x, out string exceptDesc)
    {
      BasketPricer basket_ = pricer_.Basket;
      int savedPoints = 0;
      double pv = 0.0;
      exceptDesc = null;
      try
      {
        if (x > 0.945 && basket_.IntegrationPointsFirst < 40)
        {
          savedPoints = basket_.IntegrationPointsFirst; 
          basket_.IntegrationPointsFirst = 40;
          basket_.Reset();
        }
        basket_.SetFactor(x);
        pv = pricer_.FlatPrice();
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }
      finally
      {
        if (savedPoints > 0)
        {
          basket_.IntegrationPointsFirst = savedPoints;
          basket_.Reset();
        }
      }

      return pv;
    }

    // General case: find correlation such that the pv match the target
    private double
    EstimateImpliedCorrelationPv(
        double target,
        double toleranceF,
            double toleranceX)
    {
      string exceptDesc = null;
      Double_Double_Fn fn = new Double_Double_Fn(this.EvaluatePv);
      double[] bracket = new double[4];
      BracketResult rcode = BracketGeneralTranche(
        fn, target,
        0.0, 0.99,
        0.1, toleranceF,
        bracket, out exceptDesc);
      if (BracketResult.SolutionFound == rcode)
        return bracket[0];
      double x0 = BrentSolver(
          fn, target,
          bracket[0], bracket[1],
          bracket[2], bracket[3],
          toleranceF, toleranceX, //5e-9, 5e-7,
          out exceptDesc);
      return x0 * x0;
    }


    // find correlation such that the pv equals the target
    private double
    EstimateImpliedCorrelationPv(
        double target,
        double[] pvArray,
            int maxIdx,
            int minIdx,
            double toleranceF,
            double toleranceX)
    {
      double xl, xh, fl, fh;
      {
        // bracket a interval
        int N = pvArray.Length - 1;

        if (target >= pvArray[maxIdx])
        {
          if (target - pvArray[maxIdx] < toleranceF)
            return (double)maxIdx / N;
          throw new SolverException(String.Format("The break even correlation cannot be found, possibly because the premium {0} is too big.", pricer_.CDO.Premium));
        }
        if (target <= pvArray[minIdx])
        {
          if (pvArray[minIdx] - target < toleranceF)
            return (double)minIdx / N;
          throw new SolverException(String.Format("The break even correlation cannot be found, possibly because the premium {0} is too small.", pricer_.CDO.Premium));
        }

        for (int i = 0; i < N; ++i)
        {
          double pvThis = pvArray[i];
          double pvNext = pvArray[i + 1];
          if (pvThis <= target && target <= pvNext)
          {
            minIdx = i; maxIdx = i + 1;
            break;
          }
          if (pvThis >= target && target >= pvNext)
          {
            minIdx = i + 1; maxIdx = i;
            break;
          }
        }

        xh = Math.Sqrt(((double)maxIdx) / N);
        xl = Math.Sqrt(((double)minIdx) / N);
        fh = pvArray[maxIdx];
        fl = pvArray[minIdx];
      }

      string exceptDesc = null;
      Double_Double_Fn fn = new Double_Double_Fn(this.EvaluatePv);
      double x0 = BrentSolver(
          fn, target,
          xl, fl, xh, fh,
          toleranceF, toleranceX, //5e-9, 5e-7,
          out exceptDesc);

      return x0 * x0;
    }

    /// <summary>
    ///   Calculate the implied correlation for a CDO tranche
    ///   to a given value of protection pv
    /// </summary>
    ///
    /// <param name="target">The protection pv to match</param>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>The implied correlation for Synthetic CDO tranche</returns>
    private double
    EstimateImpliedCorrelationProtectionPv(
        double target,
        double toleranceF,
            double toleranceX)
    {
      string exceptDesc = null;
      Double_Double_Fn fn = new Double_Double_Fn(this.EvaluateProtectionPv);
      double[] bracket = new double[4];
      if (BracketResult.SolutionFound == BracketGeneralTranche(
              fn, target, 0.0, 0.99, 0.1, toleranceF, bracket, out exceptDesc))
        return bracket[0];
      double x0 = BrentSolver(
          fn, target,
          bracket[0], bracket[1],
          bracket[2], bracket[3],
          toleranceF, toleranceX, // 5e-9, 5e-7,
          out exceptDesc);
      return x0 * x0;
    }


    //
    // Private function for root find for correlations.
    // Called by ImpliedImpliedCorrelationProtectionPv()
    //
    private double
    EvaluateProtectionPv(double x, out string exceptDesc)
    {
      BasketPricer basket_ = pricer_.Basket;
      int savedPoints = 0;
      double pv = 0.0;
      exceptDesc = null;
      try
      {
        if (x > 0.945 && basket_.IntegrationPointsFirst < 40)
        {
          basket_.IntegrationPointsFirst = 40;
          basket_.Reset();
        }
        SetFactor(basket_, x);
        pv = pricer_.ProtectionPv();
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }
      finally
      {
        if (savedPoints > 0)
        {
          basket_.IntegrationPointsFirst = savedPoints;
          basket_.Reset();
        }
      }

      return pv;
    }

    //
    // Base and tranche Correlation calculations
    //

    // Local routine Called by ImpliedCorrelation
    private static double[,]
    ImpliedCorrelationAbitrageFree(
        SyntheticCDOPricer[] origPricers,
        double toleranceF, double toleranceX)
    {
      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[origPricers.Length];
      for (int i = 0; i < pricers.Length; ++i)
        pricers[i] = (SyntheticCDOPricer)origPricers[i].Clone();
      double[,] results = new double[pricers.Length, 2];

      if (pricers.Length == 0)
        return results;

      // we use the first pricer as the benchmark
      DiscountCurve discountCurve = pricers[0].DiscountCurve;
      BasketPricer basket = pricers[0].Basket;
      double totalPrincipal = pricers[0].TotalPrincipal;

      // calculate correlations
      double lastPv = 0;
      for (int i = 0; i < pricers.Length; ++i)
      {
        SyntheticCDOPricer pricer = pricers[i];
        SyntheticCDO cdo = pricer.CDO;

        //- include upfront fee if fee settle is on settle
        Dt oldFeeSettle = cdo.FeeSettle;
        if (oldFeeSettle == pricer.Settle)
          cdo.FeeSettle = Dt.Add(oldFeeSettle, 1);

        double oldAp = cdo.Attachment;
        cdo.Attachment = 0;
        try
        {
          // Note that all the tranche notional must be based on the same total principal
          pricer = new SyntheticCDOPricer(cdo, basket, discountCurve,
              totalPrincipal * cdo.Detachment, false, pricer.RateResets);
          results[i, 1] = CorrelationCalc.ImpliedCorrelationPv(
              lastPv, pricer, toleranceF, toleranceX);
        }
        catch (SolverException e)
        {
          cdo.FeeSettle = oldFeeSettle;
          for (; i < pricers.Length; ++i)
            results[i, 0] = results[i, 1] = Double.NaN;
          e.Data.Add(BaseCorrelation.ExceptionDataKey, results);
          throw e;
        }
        cdo.Attachment = oldAp;

        if (0 == i)
        {
          results[i, 0] = results[i, 1];
        }
        else
        {
          results[i, 0] = Double.NaN;
        }
        if (i < pricers.Length - 1)
        {
          BasketPricer basketPricer = pricer.Basket;
          SetFactor(basketPricer, Math.Sqrt(results[i, 1]));
          oldAp = cdo.Attachment;
          double oldFee = cdo.Fee;
          double oldPrem = cdo.Premium;
          cdo.Attachment = 0;
          cdo.Premium = pricers[i + 1].CDO.Premium;
          cdo.Fee = pricers[i + 1].CDO.Fee;
          lastPv = pricer.FlatPrice();
          cdo.Premium = oldPrem;
          cdo.Attachment = oldAp;
          cdo.Fee = oldFee;
        }
        cdo.FeeSettle = oldFeeSettle;
      }

      return results;
    }

    // Local routine Called by ImpliedCorrelation
    private static double[,]
    ImpliedCorrelationProtectionMatching(
        SyntheticCDOPricer[] origPricers,
        double toleranceF, double toleranceX)
    {
      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[origPricers.Length];
      for (int i = 0; i < pricers.Length; ++i)
      {
        pricers[i] = (SyntheticCDOPricer)origPricers[i].Clone();

        // include upfront fee if fee settle is on settle
        SyntheticCDO cdo = pricers[i].CDO;
        Dt oldFeeSettle = cdo.FeeSettle;
        if (oldFeeSettle == pricers[i].Settle)
          cdo.FeeSettle = Dt.Add(oldFeeSettle, 1);
      }
      double[,] results = new double[pricers.Length, 2];

      if (pricers.Length == 0)
        return results;

      // we use the first pricer as the benchmark
      DiscountCurve discountCurve = pricers[0].DiscountCurve;
      BasketPricer basket = pricers[0].Basket;
      double totalPrincipal = pricers[0].TotalPrincipal;

      // create an array of pricers with consistent total principal
      for (int j = 0; j < pricers.Length; ++j)
      {
        SyntheticCDO cdo = pricers[j].CDO;
        pricers[j] = new SyntheticCDOPricer(
            cdo,
            basket,
            discountCurve,
            totalPrincipal * cdo.TrancheWidth,
            false, pricers[j].RateResets);
      }

      // calculate correlations
      int N = InitialSearchPoints;
      double[,] pvMat = new double[N + 1, pricers.Length];
      int[] maxIndices = new int[pricers.Length];
      int[] minIndices = new int[pricers.Length];
      double[] pvArray = new double[N + 1];

      // first step, find tranche correlations and protections
      double[] protectionPvs = new double[pricers.Length];
      Exception exception = null;
      int numCalculated = pricers.Length;
      {
        SetFactor(basket, 0.0);
        for (int j = 0; j < pricers.Length; ++j)
        {
          SyntheticCDOPricer pricer = pricers[j];
          pvMat[0, j] = pricer.FlatPrice();
          maxIndices[j] = 0;
          minIndices[j] = 0;
        }
        for (int i = 1; i <= N; ++i)
        {
          double factor = Math.Sqrt(((double)i) / N);
          SetFactor(basket, factor);
          for (int j = 0; j < pricers.Length; ++j)
          {
            SyntheticCDOPricer pricer = pricers[j];
            double pv = pricer.FlatPrice();
            pvMat[i, j] = pv;
            if (pv > pvMat[maxIndices[j], j])
              maxIndices[j] = i;
            else if (pv < pvMat[minIndices[j], j])
              minIndices[j] = i;
          }
        }

        for (int j = 0; j < pricers.Length; ++j)
        {
          for (int i = 0; i <= N; ++i)
            pvArray[i] = pvMat[i, j];

          SyntheticCDOPricer pricer = pricers[j];
          CorrelationCalc calc = new CorrelationCalc(pricer);
          try
          {
            double correlation = calc.EstimateImpliedCorrelationPv(
                0.0, pvArray, maxIndices[j], minIndices[j],
                toleranceF, toleranceX);
            results[j, 0] = correlation;
            SetFactor(basket, Math.Sqrt(correlation));
            protectionPvs[j] = pricer.ProtectionPv();
          }
          catch (SolverException e)
          {
            numCalculated = j;
            for (int k = j; k < pricers.Length; ++k)
            {
              results[k, 0] = results[k, 1] = Double.NaN;
              protectionPvs[k] = Double.NaN;
            }
            e.Data.Add(BaseCorrelation.ExceptionDataKey, results);
            exception = e;
            break;
          }
        } // for loop
      }

      // second step, compute base correlations
      {
        SetFactor(basket, 0.0);
        double pv = 0.0;
        for (int j = 0; j < numCalculated; ++j)
        {
          SyntheticCDOPricer pricer = pricers[j];
          pvMat[0, j] = (pv += pricer.ProtectionPv());
          maxIndices[j] = 0;
          minIndices[j] = 0;
        }
        for (int i = 1; i <= N; ++i)
        {
          double factor = Math.Sqrt(((double)i) / N);
          SetFactor(basket, factor);
          pv = 0.0;
          for (int j = 0; j < numCalculated; ++j)
          {
            SyntheticCDOPricer pricer = pricers[j];
            pvMat[i, j] = (pv += pricer.ProtectionPv());
            if (pv > pvMat[maxIndices[j], j])
              maxIndices[j] = i;
            else if (pv < pvMat[minIndices[j], j])
              minIndices[j] = i;
          }
        }

        double target = 0;
        for (int j = 0; j < numCalculated; ++j)
        {
          for (int i = 0; i <= N; ++i)
            pvArray[i] = pvMat[i, j];
          target += protectionPvs[j];
          if (Double.IsNaN(target))
            results[j, 1] = Double.NaN;
          else
          {
            try
            {
              double correlation;
              if (j > 0)
              {
                correlation = CorrelationCalc.EstimateImpliedCorrelation(
                    target,
                    pvArray,
                    maxIndices[j],
                    minIndices[j],
                    pricers, j,
                    basket,
                    toleranceF,
                    toleranceX);
              }
              else // j == 0, we know the result
                correlation = results[0, 0];
              results[j, 1] = correlation;
            }
            catch (SolverException e)
            {
              for (int k = j; k < numCalculated; ++k)
                results[k, 1] = Double.NaN;
              if (exception == null)
              {
                e.Data.Add(BaseCorrelation.ExceptionDataKey, results);
                exception = e;
              }
              throw exception;
            }
          }
        } // for loop
      }

      return results;
    }

    // find a factor such that the protection pv matches target
    private static double
    EstimateImpliedCorrelation(
        double target,
        double[] pvArray,
            int maxIdx,
            int minIdx,
            SyntheticCDOPricer[] pricers,
            int lastIdx,
            BasketPricer basketPricer,
            double toleranceF,
            double toleranceX)
    {
      double xl, xh, fl, fh;
      {
        int N = pvArray.Length - 1;

        if (target >= pvArray[maxIdx])
        {
          if (target - pvArray[maxIdx] < toleranceF)
            return (double)maxIdx / N;
          throw new SolverException(String.Format("The break even correlation cannot be found, possibly because the premiums are too big."));
        }
        if (target <= pvArray[minIdx])
        {
          if (pvArray[minIdx] - target < toleranceF)
            return (double)minIdx / N;
          throw new SolverException(String.Format("The break even correlation cannot be found, possibly because the premiums are too small."));
        }

        for (int i = 0; i < N; ++i)
        {
          double pvThis = pvArray[i];
          double pvNext = pvArray[i + 1];
          if (pvThis <= target && target <= pvNext)
          {
            minIdx = i; maxIdx = i + 1;
            break;
          }
          if (pvThis >= target && target >= pvNext)
          {
            minIdx = i + 1; maxIdx = i;
            break;
          }
        }

        xh = Math.Sqrt(((double)maxIdx) / N);
        xl = Math.Sqrt(((double)minIdx) / N);
        fh = pvArray[maxIdx];
        fl = pvArray[minIdx];
      }

      while (xh - xl < -toleranceX || xh - xl > toleranceX)
      {
        if (fh - fl > -toleranceF && fh - fl < toleranceF)
          break;
        double x = (xh + xl) / 2;
        double f = ProtectionPv(x, basketPricer, pricers, lastIdx);
        if (f > target)
        {
          xh = x;
          fh = f;
        }
        else if (f < target)
        {
          xl = x;
          fl = f;
        }
        else
          break;
      }

      double x0 = (xh + xl) / 2;
      return x0 * x0;
    }


    // Calculate break even premium using the factors given in an array a
    // returns the break even premium in this setting
    private static double ProtectionPv(
        double factor, BasketPricer basketPricer,
            SyntheticCDOPricer[] pricers, int lastIdx)
    {
      // Update factor
      SetFactor(basketPricer, factor);

      // Price using this parameter
      double sum = 0;
      for (int i = 0; i <= lastIdx; ++i)
      {
        SyntheticCDOPricer pricer = new SyntheticCDOPricer(
            pricers[i].CDO,
            basketPricer,
            pricers[i].DiscountCurve,
            pricers[i].Notional,
            false, pricers[i].RateResets);
        sum += pricer.ProtectionPv();
      }

      //LogUtil.DebugFormat( logger, "Trying factor {0} --> be premium {1}", factor, sum );

      return sum;
    }
    //
    // End of base correlation calculations

    /// <summary>
    ///   Set basket factor
    /// </summary>
    /// 
    /// <remarks>
    ///  <para>This function sets factor and reset the basket.</para>
    /// </remarks>
    /// <param name="basket">Basket pricer</param>
    /// <param name="factor">factor</param>
    private static void SetFactor(BasketPricer basket, double factor)
    {
      //Correlation corr = basket.Correlation;
      //if (corr is CorrelationTermStruct)
      //    ((CorrelationTermStruct)corr).SetFactorFrom(basket.Maturity, factor);
      //else
      basket.SetFactor(factor);
      //basket.Reset();
    }

    #endregion // Methods

    #region Properties
    #endregion // Properties

    #region Data
    SyntheticCDOPricer pricer_;
    #endregion // Data

    #region NEW_METHODS
    /// <summary>
    ///   Calculate the implied base and tranche correlations for an array of CDO tranches
    /// </summary>
    ///
    /// <param name="basePricers">Array of base tranche pricers</param>
    /// <param name="tranchePricers">Array of normal tranche pricers</param>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    /// <returns>Implied break even correlations and base correlations
    ///   for Synthetic CDO tranches</returns>
    public static double[,]
    ImpliedCorrelation(
        SyntheticCDOPricer[] basePricers,
        SyntheticCDOPricer[] tranchePricers,
            double toleranceF,
            double toleranceX)
    {
      SyntheticCDOPricer[] pricers = basePricers;

      // Validate
      if (pricers == null || pricers.Length < 1)
        throw new ArgumentException("Must specify at least one CDO pricer");
      bool topDown = false;
      for (int i = 0; i < pricers.Length; ++i)
      {
        if (pricers[i].CDO.Attachment != 0.0)
          throw new ArgumentException(String.Format("The start attachment point {0} is not zero", pricers[i].CDO.Attachment));
        if (pricers[i].CDO.Detachment == 1.0)
          topDown = true;
      }

      if (tranchePricers == null)
        if (topDown)
          return ImpliedCorrelationArbitrageFreeTopDown(pricers, toleranceF, toleranceX);
        else
          return ImpliedCorrelationArbitrageFree(pricers, toleranceF, toleranceX);
      else
        return ImpliedCorrelationProtectionMatching(tranchePricers, toleranceF, toleranceX);
    }

    // Local routine Called by ImpliedCorrelation
    private static double[,]
    ImpliedCorrelationArbitrageFree(
        SyntheticCDOPricer[] pricers,
        double toleranceF, double toleranceX)
    {
      double[,] results = new double[pricers.Length, 2];

      if (pricers.Length == 0)
        return results;

      // we use the total principal of the first pricer as the benchmark
      double totalPrincipal = pricers[0].TotalPrincipal;

      // calculate correlations
      double lastPv = 0;
      for (int i = 0; i < pricers.Length; ++i)
      {
        SyntheticCDOPricer pricer = pricers[i];
        SyntheticCDO cdo = pricer.CDO;

        // include upfront fee if fee settle is on settle
        Dt oldFeeSettle = cdo.FeeSettle;
        if (oldFeeSettle == pricer.Settle)
          cdo.FeeSettle = Dt.Add(oldFeeSettle, 1);

        try
        {
          // Note that all the tranche notional must be based on the same total principal
          pricer = new SyntheticCDOPricer(
              cdo, pricer.Basket, pricer.DiscountCurve,
              totalPrincipal * cdo.Detachment, false, pricer.RateResets);
          CorrelationCalc calc = new CorrelationCalc(pricer);
          results[i, 1] = calc.ImpliedCorrelationPv(
              lastPv, toleranceF, toleranceX);
        }
        catch (SolverException e)
        {
          cdo.FeeSettle = oldFeeSettle;
          for (; i < pricers.Length; ++i)
            results[i, 0] = results[i, 1] = Double.NaN;
          e.Data.Add(BaseCorrelation.ExceptionDataKey, results);
          throw e;
        }

        if (0 == i)
        {
          results[i, 0] = results[i, 1];
        }
        else
        {
          results[i, 0] = Double.NaN;
        }
        if (i < pricers.Length - 1)
        {
          double oldFee = cdo.Fee;
          double oldPrem = cdo.Premium;
          try
          {
            BasketPricer basketPricer = pricer.Basket;
            SetFactor(basketPricer, Math.Sqrt(results[i, 1]));
            cdo.Premium = pricers[i + 1].CDO.Premium;
            cdo.Fee = pricers[i + 1].CDO.Fee;
            lastPv = pricer.FlatPrice();
          }
          finally
          {
            cdo.Premium = oldPrem;
            cdo.Fee = oldFee;
            cdo.FeeSettle = oldFeeSettle;
          }
        }
        cdo.FeeSettle = oldFeeSettle;
      }

      return results;
    }

    // Local routine Called by ImpliedCorrelation
    private static double[,]
    ImpliedCorrelationArbitrageFreeTopDown(
        SyntheticCDOPricer[] pricers,
        double toleranceF, double toleranceX)
    {
      int N = pricers.Length;
      double[,] results = new double[N, 2];
      
      if (pricers.Length == 0)
        return results;

      // we use the total principal of the last pricer as the benchmark
      double totalPrincipal = pricers[N - 1].TotalPrincipal;

      // calculate correlations
      results[N - 1, 0] = results[N - 1, 1] = Double.NaN;
      double lastPv = pricers[N - 1].FlatPrice();
      for (int i = N - 2; i >= 0; --i)
      {
        SyntheticCDOPricer pricer = pricers[i];
        SyntheticCDO cdo = pricer.CDO;
        double oldFee = cdo.Fee;
        double oldPrem = cdo.Premium;
        
        // include upfront fee if fee settle is on settle
        Dt oldFeeSettle = cdo.FeeSettle;
        if (oldFeeSettle == pricer.Settle)
          cdo.FeeSettle = Dt.Add(oldFeeSettle, 1);

        try
        {
          // Note that all the tranche notional must be based on the same total principal
          cdo.Premium = pricers[i + 1].CDO.Premium;
          cdo.Fee = pricers[i + 1].CDO.Fee;
          pricer = new SyntheticCDOPricer(
              cdo, pricer.Basket, pricer.DiscountCurve,
              totalPrincipal * cdo.Detachment, false, pricer.RateResets);
          CorrelationCalc calc = new CorrelationCalc(pricer);
          results[i, 1] = calc.ImpliedCorrelationPv(
                lastPv, toleranceF, toleranceX);
          cdo.Fee = oldFee;
          cdo.Premium = oldPrem;
        }
        catch (SolverException e)
        {
          cdo.FeeSettle = oldFeeSettle;
          cdo.Fee = oldFee;
          cdo.Premium = oldPrem;
          for (int j = i; j >= 0; --j)
            results[j, 0] = results[j, 1] = Double.NaN;
          e.Data.Add(BaseCorrelation.ExceptionDataKey, results);
          throw e;
        }

        // tranche correlation = 0
        results[i, 0] = Double.NaN;
        
        if (i < pricers.Length - 1)
        {
          try
          {
            BasketPricer basketPricer = pricer.Basket;
            SetFactor(basketPricer, Math.Sqrt(results[i, 1]));
            lastPv = pricer.FlatPrice();
          }
          finally
          {
            cdo.FeeSettle = oldFeeSettle;
          }
        }
        cdo.FeeSettle = oldFeeSettle;
      }
      return results;
    }

    #endregion //NEW_METHODS
  } // class CorrelationCalc
}
