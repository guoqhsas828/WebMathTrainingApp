/*
 * IPricerUtil.cs
 *
 */

using System;
using System.Reflection;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{

  ///
  /// <summary>
  ///   Utility methods for IPricer class
  /// </summary>
  /// <exclude />
  public class IPricerUtil
  {
    /// <summary>
    ///   Return true if pricer depends on a particular curve
    /// </summary>
    ///
    /// <param name="pricer">IPricer</param>
    /// <param name="curve">Calibrated curve to test</param>
    /// 
    /// <returns>true if IPricer depends on curve</returns>
    /// <exclude />
    static public bool
    PricerDependsOn(IPricer pricer, CalibratedCurve curve)
    {
      IPricer[] pricers = new IPricer[] { pricer };

      if (curve is SurvivalCurve)
      {
        List<SurvivalCurve> curves = PricerSurvivalCurves(pricers, false);
        return curves.Contains((SurvivalCurve)curve);
      }
      else if (curve is DiscountCurve)
      {
        List<DiscountCurve> curves = PricerDiscountCurves(pricers);
        return curves.Contains((DiscountCurve)curve);
      }
      else
        throw new ToolkitException("Internal Error: Unsupported curve type");
    }

    /// <summary>
    ///   Return unique set of SurvivalCurves for list of IPricers
    /// </summary>
    /// 
    /// <param name="pricers">List of IPricers</param>
    /// <param name="mustExist">Indicate that the result cannot be empty</param>
    /// 
    /// <returns>List of SurvivalCurves that IPricers depend on</returns>
    /// <exclude />
    static public List<SurvivalCurve>
    PricerSurvivalCurves(IPricer[] pricers, bool mustExist)
    {
      List<SurvivalCurve> curves = new List<SurvivalCurve>();
      for (int i = 0; i < pricers.Length; i++)
      {
        if (pricers[i] != null)
        {
          IPricer pricer = pricers[i];
          SurvivalCurve[] sc;

#if OLD_WAY

          if (pricer is SyntheticCDOPricer)
            sc = ((SyntheticCDOPricer)pricer).SurvivalCurves;
          else if (pricer is FTDPricer)
            sc = ((FTDPricer)pricer).SurvivalCurves;
          else if (pricer is CashflowStreamPricer)
          {
            if (((CashflowStreamPricer)pricer).SurvivalCurve == null)
              sc = new SurvivalCurve[0];
            else if (((CashflowStreamPricer)pricer).CounterpartyCurve == null)
              sc = new SurvivalCurve[] { ((CashflowStreamPricer)pricer).SurvivalCurve };
            else
              sc = new SurvivalCurve[] { ((CashflowStreamPricer)pricer).SurvivalCurve, ((CashflowStreamPricer)pricer).CounterpartyCurve };
          }
          else if (pricer is CashflowPricer)
          {
            if (((CashflowPricer)pricer).SurvivalCurve == null)
              sc = new SurvivalCurve[0];
            else if (((CashflowPricer)pricer).CounterpartyCurve == null)
              sc = new SurvivalCurve[] { ((CashflowPricer)pricer).SurvivalCurve };
            else
              sc = new SurvivalCurve[] { ((CashflowPricer)pricer).SurvivalCurve, ((CashflowPricer)pricer).CounterpartyCurve };
          }
          else if (pricer is CDSOptionPricer)
            sc = new SurvivalCurve[] { ((CDSOptionPricer)pricer).SurvivalCurve };
          else if (pricer is CDXPricer)
            sc = ((CDXPricer)pricer).SurvivalCurves;
          else if (pricer is CDXOptionPricer)
            sc = ((CDXOptionPricer)pricer).SurvivalCurves;
          else if (pricer is CDOOptionPricer)
            sc = ((CDOOptionPricer)pricer).SurvivalCurves;
          else
            throw new System.ArgumentOutOfRangeException(String.Format("Unsupported pricer {0}", pricer.GetType()));

#else

          PropertyInfo info = pricer.GetType().GetProperty("SurvivalCurves", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
          if (info != null)
            sc = (SurvivalCurve[])info.GetValue(pricer, null);
          else
          {
            info = pricer.GetType().GetProperty("SurvivalCurve", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
            if (info != null)
            {
              SurvivalCurve c = (SurvivalCurve)info.GetValue(pricer, null);
              PropertyInfo ccInfo = pricer.GetType().GetProperty("CounterpartyCurve", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
              SurvivalCurve cc = (ccInfo != null) ? (SurvivalCurve)ccInfo.GetValue(pricer, null) : null;
              if (cc != null && c != null)
                sc = new SurvivalCurve[] { c, cc };
              else if (c != null)
                sc = new SurvivalCurve[] { c };
              else if (cc != null)
                sc = new SurvivalCurve[] { cc };
              else
                sc = new SurvivalCurve[0];
            }
            else if (!mustExist)
              continue;
            else
              throw new ArgumentException(String.Format("Unsupported pricer {0} - Does not have SurvivalCurve or SurvivalCurves property", pricer.GetType()));
          }

#endif

          // Add without repitition
          for (int j = 0; j < sc.Length; j++)
          {
            if (!curves.Contains(sc[j]))
              curves.Add(sc[j]);
          }
        }
      }

      return curves;
    }

    /// <summary>
    ///   Return unique set of DiscountCurves for a list of IPrices
    /// </summary>
    /// 
    /// <param name="pricers">List of IPricers</param>
    /// 
    /// <returns>List of DiscountCurves that IPricers depend on</returns>
    /// <exclude />
    static public List<DiscountCurve>
    PricerDiscountCurves(IPricer[] pricers)
    {
      List<DiscountCurve> curves = new List<DiscountCurve>();
      for (int i = 0; i < pricers.Length; i++)
      {
        if (pricers[i] != null)
        {
          IPricer pricer = pricers[i];
          DiscountCurve[] dc;

#if OLD_WAY

          if (pricer is SyntheticCDOPricer)
            dc = new DiscountCurve[] { ((SyntheticCDOPricer)pricer).DiscountCurve };
          else if (pricer is FTDPricer)
            dc = new DiscountCurve[] { ((FTDPricer)pricer).DiscountCurve };
          else if (pricer is CashflowStreamPricer)
          {
            if (((CashflowStreamPricer)pricer).ReferenceCurve == null)
              dc = new DiscountCurve[] { ((CashflowStreamPricer)pricer).DiscountCurve };
            else
              dc = new DiscountCurve[] { ((CashflowStreamPricer)pricer).DiscountCurve, ((CashflowStreamPricer)pricer).ReferenceCurve };
          }
          else if (pricer is CashflowPricer)
          {
            if (((CashflowPricer)pricer).ReferenceCurve == null)
              dc = new DiscountCurve[] { ((CashflowPricer)pricer).DiscountCurve };
            else
              dc = new DiscountCurve[] { ((CashflowPricer)pricer).DiscountCurve, ((CashflowPricer)pricer).ReferenceCurve };
          }
          else if (pricer is CDSOptionPricer)
            dc = new DiscountCurve[] { ((CDSOptionPricer)pricer).DiscountCurve };
          else if (pricer is CDXPricer)
            dc = new DiscountCurve[] { ((CDXPricer)pricer).DiscountCurve };
          else if (pricer is CDXOptionPricer)
            dc = new DiscountCurve[] { ((CDXOptionPricer)pricer).DiscountCurve };
          else if (pricer is CDOOptionPricer)
            dc = new DiscountCurve[] { ((CDOOptionPricer)pricer).DiscountCurve };
          else
            throw new System.ArgumentOutOfRangeException(String.Format("Unsupported pricer {0}", pricer.GetType()));

#else

          PropertyInfo info = pricer.GetType().GetProperty("DiscountCurves", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
          if (info != null)
            dc = (DiscountCurve[])info.GetValue(pricer, null);
          else
          {
            info = pricer.GetType().GetProperty("DiscountCurve", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
            if (info != null)
            {
              DiscountCurve c = (DiscountCurve)info.GetValue(pricer, null);
              PropertyInfo rcInfo = pricer.GetType().GetProperty("ReferenceCurve", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
              DiscountCurve rc = (rcInfo != null && rcInfo.GetValue(pricer,null) is DiscountCurve) ? (DiscountCurve)rcInfo.GetValue(pricer, null) : null;
              if (rc != null && c != null)
                dc = new DiscountCurve[] { c, rc };
              else if (c != null)
                dc = new DiscountCurve[] { c };
              else if (rc != null)
                dc = new DiscountCurve[] { rc };
              else
                dc = new DiscountCurve[0];
            }
            else
              throw new ArgumentException(String.Format("Unsupported pricer {0} - Does not have DiscountCurve or DicountCurves property", pricer.GetType()));
          }

#endif

          // Add without repitition
          for (int j = 0; j < dc.Length; j++)
          {
            if (!curves.Contains(dc[j]))
              curves.Add(dc[j]);
          }
        }
      }

      return curves;
    }

    /// <summary>
    ///   Return unique list of RecoveryCurves that a list of IPricers depend on
    /// </summary>
    /// 
    /// <param name="pricers">List of IPricers</param>
    /// <param name="mustExist">Indicate that the result cannot be empty</param>
    /// 
    /// <returns>List of RecoveryCurves that IPricers depend on</returns>
    /// <exclude />
    static public List<RecoveryCurve>
    PricerRecoveryCurves(IPricer[] pricers, bool mustExist)
    {
      List<RecoveryCurve> curves = new List<RecoveryCurve>();
      for (int i = 0; i < pricers.Length; i++)
      {
        if (pricers[i] != null)
        {
          IPricer pricer = pricers[i];
          RecoveryCurve[] rc = null;

#if OLD_WAY

          if (pricer is SyntheticCDOPricer)
            rc = ((SyntheticCDOPricer)pricer).RecoveryCurves;
          else if (pricer is FTDPricer)
            rc = ((FTDPricer)pricer).RecoveryCurves;
          else if (pricer is CashflowStreamPricer)
            rc = new RecoveryCurve[] { ((CashflowStreamPricer)pricer).RecoveryCurve };
          else if (pricer is CashflowPricer)
            rc = new RecoveryCurve[] { ((CashflowPricer)pricer).RecoveryCurve };
          else if (pricer is CDSOptionPricer)
          {
            SurvivalCalibrator sc = ((CDSOptionPricer)pricer).SurvivalCurve.SurvivalCalibrator;
            if (sc == null)
              throw new System.ArgumentOutOfRangeException("No calibrator for this survival curve");
            rc = new RecoveryCurve[] { sc.RecoveryCurve };
          }
          else if (pricer is CDXPricer)
            rc = ((CDXPricer)pricer).RecoveryCurves;
          else if (pricer is CDXOptionPricer)
            rc = ((CDXOptionPricer)pricer).RecoveryCurves;
          else if (pricer is CDOOptionPricer)
            rc = ((CDOOptionPricer)pricer).RecoveryCurves;
          else
            throw new System.ArgumentOutOfRangeException(String.Format("Unsupported pricer {0}", pricer.GetType()));

#else

          PropertyInfo info = pricer.GetType().GetProperty("RecoveryCurves", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
          if (info != null)
            rc = (RecoveryCurve[])info.GetValue(pricer, null);
          else
          {
            info = pricer.GetType().GetProperty("RecoveryCurve", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
            if (info != null)
            {
              RecoveryCurve c = (RecoveryCurve)info.GetValue(pricer, null);
              if (c != null)
                rc = new RecoveryCurve[] { c };
              else
                rc = new RecoveryCurve[0];
            }
            else
            {
              // Just for CDSOptionPricer backward compatability for now. RTD Feb'07
              PropertyInfo cInfo = pricer.GetType().GetProperty("SurvivalCurve", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
              SurvivalCurve c = (cInfo != null) ? (SurvivalCurve)cInfo.GetValue(pricer, null) : null;
              if (c != null && c.SurvivalCalibrator != null)
              {
                RecoveryCurve cc = c.SurvivalCalibrator.RecoveryCurve;
                if (cc != null)
                  rc = new RecoveryCurve[] { cc };
                else
                  rc = new RecoveryCurve[0];
              }
              else if (mustExist)
                throw new ArgumentException(String.Format("Unsupported pricer {0} - Does not have RecoveryCurve or RecoveryCurves property or Calibrator to extract RecoveryCurve", pricer.GetType()));
            }
          }

#endif

          // Add without repitition
          if (rc != null)
            for (int j = 0; j < rc.Length; j++)
            {
              if (!curves.Contains(rc[j]))
                curves.Add(rc[j]);
            }
        }
      }

      return curves;
    }

    /// <summary>
    ///   Return unique list of CorrelationObjects IPricer depends on
    /// </summary>
    ///
    /// <param name="pricers">List of IPricers</param>
    ///
    /// <returns>List of CorrelationObjects that IPricers depend on</returns>
    /// <exclude />
    static public List<CorrelationObject>
    PricerCorrelations(IPricer[] pricers)
    {
      List<CorrelationObject> corrs = new List<CorrelationObject>();
      for (int i = 0; i < pricers.Length; i++)
      {
        if (pricers[i] != null)
        {
          IPricer pricer = pricers[i];
          CorrelationObject c;

#if OLD_WAY

          if (pricer is SyntheticCDOPricer)
          {
            SyntheticCDOPricer p = (SyntheticCDOPricer)pricer;
            if (p.Basket is BaseCorrelationBasketPricer)
              c = ((BaseCorrelationBasketPricer)p.Basket).BaseCorrelation;
            else
              c = p.Correlation;
          }
          else if (pricer is FTDPricer)
            c = ((FTDPricer)pricer).Correlation;
          else if (pricer is CDOOptionPricer)
          {
            CDOOptionPricer p = (CDOOptionPricer)pricer;
            if (p.Basket is BaseCorrelationBasketPricer)
              c = ((BaseCorrelationBasketPricer)p.Basket).BaseCorrelation;
            else
              c = p.Correlation;
          }
          else
            throw new System.ArgumentOutOfRangeException(String.Format("Unsupported pricer {0}", pricer.GetType()));

#else

          PropertyInfo info = pricer.GetType().GetProperty("Correlation", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
          if (info != null)
            c = (CorrelationObject)info.GetValue(pricer, null);
          else
          {
            info = pricer.GetType().GetProperty("Basket", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
            if (info != null)
            {
              BasketPricer bp = (BasketPricer)info.GetValue(pricer, null);
              c = bp.Correlation;
            }
            else
              throw new ArgumentException(String.Format("Unsupported pricer {0} - Does not have Correlation or BasketPricer to extract Correlation", pricer.GetType()));
          }

#endif

          if (c != null && !corrs.Contains(c))
            corrs.Add(c);
        }
      }

      return corrs;
    }

  } // IPricerUtil
}