//
// OverlapTreatment.cs
//   2014. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Class containing instructions on overlap treatment
  /// </summary>
  [Serializable]
  public class OverlapTreatment : BaseEntityObject
  {
    private bool futuresOverMoneyMarket_;
    private bool futuresOverSwaps_;
    private bool all_;
    private readonly InstrumentType[] overlapTreatmentOrder_;

    private static readonly InstrumentType[] _collectInstrumentTypes =
    {
      InstrumentType.Swap, InstrumentType.FUT, InstrumentType.None
    };

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="futuresOverMoneyMarket">Select ed futures over money market instruments for overlapping securities </param>
    /// <param name="futuresOverSwaps">Select futures over swaps instruments for overlapping securities</param>
    /// <param name="all">True to attempt calibration to all tenors </param>
    public OverlapTreatment(bool futuresOverMoneyMarket, bool futuresOverSwaps, bool all)
    {
      futuresOverMoneyMarket_ = futuresOverMoneyMarket;
      futuresOverSwaps_ = futuresOverSwaps;
      all_ = all;
      if (!all_)
      {
        overlapTreatmentOrder_ = new InstrumentType[3];
        if (futuresOverSwaps_ && futuresOverMoneyMarket_ )
        {
          overlapTreatmentOrder_[0] = InstrumentType.FUT;
          overlapTreatmentOrder_[1] = InstrumentType.MM;
          overlapTreatmentOrder_[2] = InstrumentType.Swap;
        } 
        else if (futuresOverSwaps_)
        {
          overlapTreatmentOrder_[1] = InstrumentType.FUT;
          overlapTreatmentOrder_[0] = InstrumentType.MM;
          overlapTreatmentOrder_[2] = InstrumentType.Swap;
        }
        else
        {
          overlapTreatmentOrder_[1] = InstrumentType.FUT;
          overlapTreatmentOrder_[2] = InstrumentType.MM;
          overlapTreatmentOrder_[0] = InstrumentType.Swap;
        }
      }
    }

    /// <summary>
    /// Constructor to accept new overlapping treatment based on priority order
    /// </summary>
    /// <param name="overlapTreatmentPriorities">The array indicating overlap treatment priorities.</param>
    public OverlapTreatment(InstrumentType[] overlapTreatmentPriorities)
    {
      if (overlapTreatmentPriorities == null || overlapTreatmentPriorities.Length == 0)
      {
        all_ = true;
        overlapTreatmentOrder_ = EmptyArray<InstrumentType>.Instance;
      }
      else
      {
        all_ = false;
        // Removed duplicated instrument types
        overlapTreatmentOrder_ = overlapTreatmentPriorities
          .Where((t, i) => Array.IndexOf(
            overlapTreatmentPriorities, t, 0, i) < 0)
          .ToArray();
      }
    }

    /// <summary>
    /// True to select futures over money market instruments
    /// </summary>
    public bool FuturesOverMoneyMarket
    {
      get { return futuresOverMoneyMarket_; }
      set { futuresOverMoneyMarket_ = value; }
    }

    /// <summary>
    /// True to select futures over swaps if there is overlap in maturities
    /// </summary>
    public bool FuturesOverSwaps
    {
      get { return futuresOverSwaps_; }
      set { futuresOverSwaps_ = value; }
    }

    /// <summary>
    /// True to attempt calibration to all securities. This requires that all dates are distinct
    /// </summary>
    public bool All
    {
      get { return all_; }
      set { all_ = value; }
    }

    /// <summary>
    /// Resolves overlapping among the calibration instruments
    /// </summary>
    /// <param name="curveTenors">Curve tenors</param>
    /// <param name="cloneIndividualTenors">if set to <c>true</c>, clone individual tenors.</param>
    /// <returns>A new curve tenor collection resolved with the specified method</returns>
    public CurveTenorCollection ResolveTenorOverlap(
      CurveTenorCollection curveTenors, bool cloneIndividualTenors = true)
    {
      var addedTenorCollection = new CurveTenorCollection();
      // Set curve dates before sorting for it to be based on the correct curve dates.
      DiscountCurveCalibrationUtils.SetCurveDates(curveTenors);
      curveTenors.Sort(); 

      if (all_)
      {
        return cloneIndividualTenors ? (CurveTenorCollection)
          curveTenors.Clone() : curveTenors;
      }
      Dt shortStart = Dt.Empty;
      Dt shortEnd = Dt.Empty;
      Dt middleStart = Dt.Empty;
      Dt middleEnd = Dt.Empty;
      Dt longStart = Dt.Empty;
      InstrumentType middleStartHolder = InstrumentType.None;
      InstrumentType middleEndHolder = InstrumentType.None;

      IList<InstrumentType> order = overlapTreatmentOrder_;
      foreach (var ty in _collectInstrumentTypes)
      {
        if (order.Contains(ty)) continue;
        if (ReferenceEquals(order, overlapTreatmentOrder_))
        {
          var list = new List<InstrumentType>();
          list.AddRange(order);
          order = list;
        }
        order.Add(ty);
      }

      foreach (InstrumentType prioritySelection in order)
      {
        switch (prioritySelection)
        {
          case InstrumentType.Swap:
            foreach (CurveTenor ten in curveTenors)
            {
              if ((ten.Product is SwapLeg || ten.Product is Swap) &&
                  (middleEnd == Dt.Empty || ten.Product.Maturity > middleEnd))
              {
                addedTenorCollection.Add(ten);

                if (longStart == Dt.Empty || longStart > ten.Product.Maturity )
                {
                  longStart = ten.Product.Maturity;
                }
              }
            }
            break;
          case InstrumentType.FUT:
          case InstrumentType.FRA:
            foreach (CurveTenor ten in curveTenors)
            {
              Dt underlyingMaturity;
              if (prioritySelection != GetInstrumentType(ten))
                continue;

              if (ten.Product is StirFuture)
                underlyingMaturity = ((StirFuture)ten.Product).DepositMaturity;
              else if (ten.Product is FRA)
                underlyingMaturity = ((FRA) ten.Product).ContractMaturity;
              else
                continue;

              if ((shortEnd == Dt.Empty || ten.Product.Maturity >= shortEnd) &&
                  (longStart == Dt.Empty || underlyingMaturity < longStart))
              {
                if (middleEnd == Dt.Empty || middleEnd <= ten.Product.Maturity || middleEndHolder == GetInstrumentType(ten))
                {
                  addedTenorCollection.Add(ten);
                  middleEnd = underlyingMaturity;
                  middleEndHolder = GetInstrumentType(ten);
                  middleStart = (middleStart == Dt.Empty ? ten.Product.Maturity : middleStart);
                  middleStartHolder = GetInstrumentType(ten);
                }
                else if (middleStart == Dt.Empty || middleStart >= underlyingMaturity || middleStartHolder == GetInstrumentType(ten))
                {
                  addedTenorCollection.Add(ten);
                  middleEnd = (middleEnd == Dt.Empty ? underlyingMaturity : middleEnd);
                  middleEndHolder = GetInstrumentType(ten);
                  middleStart = ten.Product.Maturity;
                  middleStartHolder = GetInstrumentType(ten);
                }
              }
            }
            break;
          default:
            foreach (CurveTenor ten in curveTenors)
            {
              if (ten.Product is Note && (middleStart == Dt.Empty || ten.Product.Maturity <= middleStart))
              {
                if (shortStart == Dt.Empty || shortStart > ten.Product.Maturity)
                {
                  addedTenorCollection.Add(ten);
                  shortStart = ten.Product.Maturity;
                  shortEnd = (shortEnd == Dt.Empty ? ten.Product.Maturity : shortEnd);
                }
                else if (shortEnd == Dt.Empty || shortEnd < ten.Product.Maturity)
                {
                  addedTenorCollection.Add(ten);
                  shortEnd = ten.Product.Maturity;
                  shortStart = (shortStart == Dt.Empty ? ten.Product.Maturity : shortStart);
                }
              }
            }
            break;
        }
      }

      addedTenorCollection.Sort();
      return cloneIndividualTenors ? (CurveTenorCollection)
        addedTenorCollection.Clone() : addedTenorCollection;
    }

    private InstrumentType GetInstrumentType(CurveTenor ten)
    {
      if (ten.Product is SwapLeg || ten.Product is Swap)
        return InstrumentType.Swap;
      else if (ten.Product is StirFuture)
        return InstrumentType.FUT;
      else if (ten.Product is Note)
        return InstrumentType.MM;
      else if (ten.Product is FRA)
        return InstrumentType.FRA;
      else
        return InstrumentType.None;
    }

    /// <summary>
    /// Resolves overlap for overlapping securities
    /// </summary>
    /// <param name="curveTenors">Curve tenors</param>
    /// <returns>A new curve tenor collection without overlap</returns>
    public CurveTenorCollection ResolveOverlap(CurveTenorCollection curveTenors)
    {
      CurveTenorCollection retVal = new CurveTenorCollection();
      CurveTenorCollection swps = new CurveTenorCollection();
      CurveTenorCollection edfuts = new CurveTenorCollection();
      CurveTenorCollection mm = new CurveTenorCollection();
      curveTenors.Sort();
      if (all_)
      {
        retVal = (CurveTenorCollection)curveTenors.Clone();
        return retVal;
      }
      foreach (CurveTenor ten in curveTenors)
      {
        if (ten.Product is SwapLeg || ten.Product is Swap)
          swps.Add(ten);
        if (ten.Product is StirFuture)
          edfuts.Add(ten);
        else if (ten.Product is Note)
          mm.Add(ten);
      }
      if ((mm.Count == 0 && swps.Count == 0)||(edfuts.Count == 0 && swps.Count == 0)||(mm.Count== 0 && edfuts.Count ==0)) 
          return (CurveTenorCollection)curveTenors.Clone();
      if (futuresOverMoneyMarket_ == true && futuresOverSwaps_ == true)
      {
        Dt end = (edfuts.Count != 0)? edfuts[0].Maturity : swps[0].Maturity;
        for (int i = 0; i < mm.Count; i++)
        {
          if (mm[i].Maturity < end)
            retVal.Add(mm[i]);
        }
        for (int i = 0; i < edfuts.Count; i++)
          retVal.Add(edfuts[i]);
        Dt start = (edfuts.Count != 0) ? ((StirFuture) edfuts[edfuts.Count - 1].Product).DepositMaturity
                     : Dt.Add(swps[0].Maturity, -1, TimeUnit.Years);  
        for (int i = 0; i < swps.Count; i++)
        {
          if (swps[i].Maturity > start)
            retVal.Add(swps[i]);
        }
      }
      else if (futuresOverMoneyMarket_ == true && futuresOverSwaps_ == false)
      {
        Dt end = (edfuts.Count != 0) ? edfuts[0].Maturity : swps[0].Maturity;
        for (int i = 0; i < mm.Count; i++)
        {
          if (mm[i].Maturity < end)
            retVal.Add(mm[i]);
        }
        end = (swps.Count != 0) ? swps[0].Maturity : Dt.Add(edfuts[edfuts.Count - 1].Maturity, 1, TimeUnit.Years);
        for (int i = 0; i < edfuts.Count; i++)
        {
          if (edfuts[i].Maturity < end)
            retVal.Add(edfuts[i]);
        }
        for (int i = 0; i < swps.Count; i++)
          retVal.Add(swps[i]);
      }
      else if (futuresOverMoneyMarket_ == false && futuresOverSwaps_ == true)
      {
        Dt end = (swps.Count != 0) ? swps[0].Maturity : Dt.Add(mm[mm.Count - 1].Maturity, 1, TimeUnit.Years);
        for (int i = 0; i < mm.Count; i++)
        {
          if (mm[i].Maturity < end)
            retVal.Add(mm[i]);
        }
        Dt start = (mm.Count != 0) ? mm[mm.Count - 1].Maturity : Dt.Add(edfuts[0].Maturity, -1, TimeUnit.Years);
        for (int i = 0; i < edfuts.Count; i++)
        {
          if (edfuts[i].Maturity > start)
            retVal.Add(edfuts[i]);
        }
        start = (edfuts.Count != 0) ? edfuts[edfuts.Count - 1].Maturity : Dt.Add(swps[0].Maturity, -1, TimeUnit.Years);
        for (int i = 0; i < swps.Count; i++)
        {
          if (swps[i].Maturity > start)
            retVal.Add(swps[i]);
        }
      }
      else
      {
        Dt end = (swps.Count != 0) ? swps[0].Maturity : Dt.Add(mm[mm.Count - 1].Maturity, 1, TimeUnit.Years);
        for (int i = 0; i < mm.Count; i++)
        {
          if(mm[i].Maturity < end)
          retVal.Add(mm[i]);
        }
        Dt start = (mm.Count != 0)? mm[mm.Count - 1].Maturity
                  : Dt.Add(edfuts[0].Maturity, -1, TimeUnit.Years);
        end = (swps.Count != 0)
                ? swps[0].Maturity
                : Dt.Add(edfuts[edfuts.Count - 1].Maturity, 1, TimeUnit.Years);
        for (int i = 0; i < edfuts.Count; i++)
        {
          if (edfuts[i].Maturity > start && edfuts[i].Maturity < end)
            retVal.Add(edfuts[i]);
        }
        for (int i = 0; i < swps.Count; i++)
          retVal.Add(swps[i]);
      }
      return retVal;
    }
  }
}
