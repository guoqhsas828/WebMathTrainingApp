/*
 * CreditBasketUtil.cs
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Database;
using BaseEntity.Database.Engine;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Risk
{

  /// <summary>
  /// </summary>
  public static class CreditBasketUtil
  {
    #region IReferenceCreditsOwner Helper Members

    /// <summary>
    ///   Converts a list of CreditBasketUnderlyings' into a list of IReferenceCredit's
    /// </summary>
    /// <returns></returns>
    public static IList<IReferenceCredit> GetReferenceCredits(IList<CreditBasketUnderlying> underlyings)
    {
      return underlyings.Cast<IReferenceCredit>().ToList();
    }

    /// <summary>
    ///   Returns a list of underlyings that reference the given Legal Entity
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="refEntity"></param>
    /// <returns></returns>
    private static IList GetUnderlyingsPointingToEntity(IEnumerable<CreditBasketUnderlying> underlyings, LegalEntity refEntity)
    {
      IList matchingUnderlyings = new ArrayList();
      foreach (CreditBasketUnderlying cbu in underlyings)
      {
        if (cbu.ReferenceEntity.CompareTo(refEntity) == 0)
          matchingUnderlyings.Add(cbu);
      }
      return matchingUnderlyings;
    }

    /// <summary>
    ///   Applies Corporate Actions to matching underlyings
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="corpActionItems"></param>
    public static IList<CreditBasketUnderlyingDelta> ApplyCorporateAction(IList<CreditBasketUnderlying> underlyings, IList<CorporateActionEventItem> corpActionItems)
    {
      IList<CreditBasketUnderlying> origUnderlyings = CloneUtil.CloneToGenericList(underlyings);

      foreach (IGrouping<LegalEntity, CorporateActionEventItem> grpItems in corpActionItems.GroupBy(i => i.OldReferenceEntity))
      {
        LegalEntity oldEntity = grpItems.Key;

        IList matchingUnderlyings = GetUnderlyingsPointingToEntity(underlyings, oldEntity);
        if (matchingUnderlyings.Count == 0)
          throw new BusinessEventApplyException(String.Format("None of the underlyings matched the entity [{0}].", oldEntity.Name));

        foreach (CreditBasketUnderlying originalCbu in matchingUnderlyings)
        {
          double origWeight = originalCbu.Percentage;

          foreach (CorporateActionEventItem item in grpItems)
          {
            double newWeight = origWeight * item.PercentDebtTransferred;
            var newCbu = (CreditBasketUnderlying)originalCbu.Clone();
            newCbu.ReferenceEntity = item.NewReferenceEntity;
            newCbu.Percentage = newWeight;
            underlyings.Add(newCbu);
            originalCbu.Percentage -= newWeight;
          }

          // If the total debt from the original entity is transfered to new entities 
          // then, we should remove the original entity from the basket
          if (grpItems.Sum(i => i.PercentDebtTransferred).ApproximatelyEqualsTo(1.0))
            underlyings.Remove(originalCbu);
        }
      }

      RemoveDuplicateUnderlyings(underlyings);


      var underlyingDeltas = new List<CreditBasketUnderlyingDelta>();

      foreach (CreditBasketUnderlying origCbu in origUnderlyings)
      {
        CreditBasketUnderlying newCbu = GetMatchingUnderlying(underlyings, origCbu);
        if (newCbu == null)
        {
          underlyingDeltas.Add(new CreditBasketUnderlyingDelta
                               {
                                 Type = ObjectChangedType.Deleted,
                                 ReferenceEntity = origCbu.ReferenceEntity,
                                 Seniority = origCbu.Seniority,
                                 RestructuringType = origCbu.RestructuringType,
                                 Currency = origCbu.Currency,
                                 Cancellability = origCbu.Cancellability,
                                 ReferenceObligation = origCbu.ReferenceObligation,
                                 OldPercentage = origCbu.Percentage,
                                 FixedRecoveryRate = origCbu.FixedRecoveryRate
                               });
        }
        else if (!newCbu.Percentage.ApproximatelyEqualsTo(origCbu.Percentage))
        {
          underlyingDeltas.Add(new CreditBasketUnderlyingDelta
                               {
                                 Type = ObjectChangedType.Updated,
                                 ReferenceEntity = origCbu.ReferenceEntity,
                                 Seniority = origCbu.Seniority,
                                 RestructuringType = origCbu.RestructuringType,
                                 Currency = origCbu.Currency,
                                 Cancellability = origCbu.Cancellability,
                                 ReferenceObligation = origCbu.ReferenceObligation,
                                 OldPercentage = origCbu.Percentage,
                                 NewPercentage = newCbu.Percentage,
                                 FixedRecoveryRate = origCbu.FixedRecoveryRate
                               });
        }
      }

      foreach (CreditBasketUnderlying newCbu in underlyings)
      {
        CreditBasketUnderlying origCbu = GetMatchingUnderlying(origUnderlyings, newCbu);
        if (origCbu == null)
        {
          underlyingDeltas.Add(new CreditBasketUnderlyingDelta
          {
            Type = ObjectChangedType.Inserted,
            ReferenceEntity = newCbu.ReferenceEntity,
            Seniority = newCbu.Seniority,
            RestructuringType = newCbu.RestructuringType,
            Currency = newCbu.Currency,
            Cancellability = newCbu.Cancellability,
            ReferenceObligation = newCbu.ReferenceObligation,
            NewPercentage = newCbu.Percentage,
            FixedRecoveryRate = newCbu.FixedRecoveryRate
          });
        }
      }

      return underlyingDeltas;
    }

    /// <summary>
    ///  Finds a matching underlying in the given collection based on keys
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="cbu"></param>
    /// <returns></returns>
    private static CreditBasketUnderlying GetMatchingUnderlying(IEnumerable<CreditBasketUnderlying> underlyings, CreditBasketUnderlying cbu)
    {
      return underlyings.FirstOrDefault(u =>
                                        u.ReferenceEntityId == cbu.ReferenceEntityId &&
                                        u.Currency == cbu.Currency &&
                                        u.RestructuringType == cbu.RestructuringType &&
                                        u.Seniority == cbu.Seniority &&
                                        u.Cancellability == cbu.Cancellability);
    }


    /// <summary>
    ///  Finds a matching underlying in the given collection based on keys
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    private static CreditBasketUnderlying GetMatchingUnderlying(IEnumerable<CreditBasketUnderlying> underlyings, CreditBasketUnderlyingDelta delta)
    {
      return underlyings.FirstOrDefault(u =>
                                        u.ReferenceEntityId == delta.ReferenceEntityId &&
                                        u.Currency == delta.Currency &&
                                        u.RestructuringType == delta.RestructuringType &&
                                        u.Seniority == delta.Seniority &&
                                        u.Cancellability == delta.Cancellability);
    }

    #region UnapplyOldWay

    /// <summary>
    ///   Applies Corporate Actions to matching underlyings 
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="oldEntities"></param>
    /// <param name="newEntity"></param>
    private static void ApplyCorporateAction(IList<CreditBasketUnderlying> underlyings, IList<LegalEntity> oldEntities, LegalEntity newEntity)
    {
      foreach (LegalEntity oldEntity in oldEntities)
      {
        IList matchingUnderlyings = GetUnderlyingsPointingToEntity(underlyings, oldEntity);
        if (matchingUnderlyings.Count == 0)
          throw new RiskException(String.Format("None of the underlyings matched the entity [{0}].", oldEntity.Name));

        foreach (CreditBasketUnderlying matchingCbu in matchingUnderlyings)
        {
          matchingCbu.ReferenceEntity = newEntity;
        }
      }

      RemoveDuplicateUnderlyings(underlyings);
    }

    /// <summary>
    ///   Applies Corporate Actions to matching underlyings
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="oldEntity"></param>
    /// <param name="newEntities"></param>
    private static void ApplyCorporateAction(IList<CreditBasketUnderlying> underlyings, LegalEntity oldEntity, IDictionary<LegalEntity, double> newEntities)
    {
      IList matchingUnderlyings = GetUnderlyingsPointingToEntity(underlyings, oldEntity);
      if (matchingUnderlyings.Count == 0)
        throw new BusinessEventApplyException(String.Format("None of the underlyings matched the entity [{0}].", oldEntity.Name));

      foreach (CreditBasketUnderlying matchingcbu in matchingUnderlyings)
      {
        double origWeight = matchingcbu.Percentage;

        foreach (KeyValuePair<LegalEntity, double> pair in newEntities)
        {
          double newWeight = origWeight * pair.Value;

          if (matchingcbu.Percentage.ApproximatelyEqualsTo(newWeight))
          {
            matchingcbu.ReferenceEntity = pair.Key;
          }
          else
          {
            var newCbu = (CreditBasketUnderlying)matchingcbu.Clone();
            newCbu.ReferenceEntity = pair.Key;
            newCbu.Percentage = newWeight;
            underlyings.Add(newCbu);

            matchingcbu.Percentage -= newWeight;
          }
        }
      }

      RemoveDuplicateUnderlyings(underlyings);
    }

    /// <summary>
    ///   This method unapplies corporate actions applied prior to 10.3
    ///   where the weight transferred was not being saved to the database.
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="corpActionItems"></param>
    private static void UnApplyCorporateActionOldWay(IList<CreditBasketUnderlying> underlyings, IList<CorporateActionEventItem> corpActionItems)
    {
      try
      {
        if (corpActionItems.Count == 1)
        {
          CorporateActionEventItem item = corpActionItems[0];
          LegalEntity oldEntity = item.NewReferenceEntity;
          var newEntities = new Dictionary<LegalEntity, double> { { item.OldReferenceEntity, 1.0 } };
          ApplyCorporateAction(underlyings, oldEntity, newEntities);
        }
        else
        {
          var oldEntities = new List<LegalEntity>();
          var newEntities = new List<LegalEntity>();
          foreach (CorporateActionEventItem eventItem in corpActionItems)
          {
            if (!oldEntities.Contains(eventItem.OldReferenceEntity))
              oldEntities.Add(eventItem.OldReferenceEntity);
            if (!newEntities.Contains(eventItem.NewReferenceEntity))
              newEntities.Add(eventItem.NewReferenceEntity);
          }

          if (oldEntities.Count == 1 && newEntities.Count > 1)
          {
            // Process Demerger as Merger
            ApplyCorporateAction(underlyings, newEntities, oldEntities[0]);
          }
          else if (oldEntities.Count > 1 && newEntities.Count == 1)
          {
            // Process Merger as Demerger
            LegalEntity oldEntity = newEntities[0];
            double percentDebtTransfered = 1.0 / oldEntities.Count;

            Dictionary<LegalEntity, double> newEntityDistribution = oldEntities.ToDictionary(entity => entity,
                                                                                             entity =>
                                                                                             percentDebtTransfered);


            ApplyCorporateAction(underlyings, oldEntity, newEntityDistribution);

          }
          else
          {
            throw new BusinessEventRollbackException(
              "Corporate Action Event cannot have multiple old entities and new entities");
          }
        }
      }
      catch (BusinessEventApplyException ex)
      {
        // We need this because we in order to rollback we apply the corporate action in the 
        // Reverse Order and the Apply process always throws a BusinessEventApplyException
        throw new BusinessEventRollbackException(ex.Message);
      }
    }

    #endregion

    /// <summary>
    ///   Applies Corporate Actions to matching underlyings
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="corpActionItems"></param>
    /// <param name="underlyingDeltas"></param>
    public static void UnApplyCorporateAction(IList<CreditBasketUnderlying> underlyings, IList<CorporateActionEventItem> corpActionItems, IList<CreditBasketUnderlyingDelta> underlyingDeltas)
    {
      // This is to handle un-applying corporate actions applied before 10.3
      // where we did not store the weight that was transferred
      if (underlyingDeltas == null || underlyingDeltas.Count == 0)
      {
        UnApplyCorporateActionOldWay(underlyings, corpActionItems);
        return;
      }

      foreach (CreditBasketUnderlyingDelta delta in underlyingDeltas)
      {
        if (delta.Type == ObjectChangedType.Inserted)
        {
          CreditBasketUnderlying cbu = GetMatchingUnderlying(underlyings, delta);
          if (cbu == null)
            throw new BusinessEventRollbackException(String.Format("Cannot find underlying [{0}] ", delta.Key));

          underlyings.Remove(cbu);
        }
        else if (delta.Type == ObjectChangedType.Deleted)
        {
          var cbu = new CreditBasketUnderlying
                    {
                      ReferenceEntity = delta.ReferenceEntity,
                      Currency = delta.Currency,
                      Seniority = delta.Seniority,
                      RestructuringType = delta.RestructuringType,
                      ReferenceObligation = delta.ReferenceObligation,
                      Cancellability = delta.Cancellability,
                      FixedRecoveryRate = delta.FixedRecoveryRate,
                      Percentage = delta.OldPercentage
                    };

          underlyings.Add(cbu);
        }
        else
        {
          // Must be update to participation
          CreditBasketUnderlying cbu = GetMatchingUnderlying(underlyings, delta);
          if (cbu == null)
            throw new BusinessEventRollbackException(String.Format("Cannot find underlying [{0}]", delta.Key));

          cbu.Percentage = delta.OldPercentage;
        }
      }

      RemoveDuplicateUnderlyings(underlyings);
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Replaces multiple duplicate underlyings with one underlying
    /// </summary>
    public static void RemoveDuplicateUnderlyings(IList<CreditBasketUnderlying> underlyings)
    {
      var cbuCache = new Dictionary<string, List<CreditBasketUnderlying>>();
      foreach (CreditBasketUnderlying cbu in underlyings)
      {
        string key = cbu.Key;
        if (!cbuCache.ContainsKey(key))
          cbuCache[key] = new List<CreditBasketUnderlying>();
        cbuCache[key].Add(cbu);
      }

      foreach (string key in cbuCache.Keys)
      {
        if (cbuCache[key].Count > 1)
        {
          CreditBasketUnderlying cbu1 = cbuCache[key][0];
          for (int i = 1; i < cbuCache[key].Count; i++)
          {
            CreditBasketUnderlying cbuToDelete = cbuCache[key][i];
            cbu1.Percentage += cbuToDelete.Percentage;
            underlyings.Remove(cbuToDelete);
          }
        }
      }
    }

    /// <summary>
    /// Calculates the average notional weighted implied spread of names in a basket.
    /// </summary>
    public static double CalcWeightedAvgImpliedSpread(IRiskSurvivalCurve[] curves, Dt maturity, IList<CreditBasketUnderlying> underlyings)
    {
      int N = underlyings.Count;
      var weights = new double[underlyings.Count];

      for (int i = 0; i < N; ++i)
        weights[i] = underlyings[i].Percentage;

      var survivalCurves = new SurvivalCurve[N];

      for (int i = 0; i < N; ++i)
      {
        survivalCurves[i] = curves[i].SurvivalCurve;
      }

      return BasketUtil.CalcAvgImpliedSpread(maturity, survivalCurves, weights); 
    }

    /// <summary>
    /// Calculates the average Duration Weighted Implied Spread of the names in the basket.
    /// </summary>
    public static double CalcDurationWeightedAvgImpliedSpread(IRiskSurvivalCurve[] curves, Dt maturity, IList<CreditBasketUnderlying> underlyings)
    {
      int N = underlyings.Count;
      double[] weights = new double[underlyings.Count];
    
      for (int i = 0; i < N; ++i)
        weights[i] = underlyings[i].Percentage;
    
      SurvivalCurve[] survivalCurves = new SurvivalCurve[N];
      
      for(int i = 0; i< N; ++i)
      {
        survivalCurves[i] = curves[i].SurvivalCurve;
      }

      return BasketUtil.CalcDurationWeightedAvgImpliedSpread(maturity, survivalCurves, weights); 
    }

    /// <summary>
    /// Calculates the ExpectedLoss for the basket of names.
    /// </summary>
    public static double CalcExpectedLoss(IRiskSurvivalCurve[] curves, Dt maturity, IList<CreditBasketUnderlying> underlyings)
    {
      int N = underlyings.Count;
      double[] weights = new double[underlyings.Count];

      for (int i = 0; i < N; ++i)
        weights[i] = underlyings[i].Percentage;

      SurvivalCurve[] survivalCurves = new SurvivalCurve[N];

      for (int i = 0; i < N; ++i)
      {
        survivalCurves[i] = curves[i].SurvivalCurve;
      }

      return BasketUtil.CalcExpectedLoss(maturity, survivalCurves, weights);
    }


    /// <summary>
    /// Calculates the ExpectedLossPv for the basket of names.
    /// </summary>
    public static double CalcExpectedLossPv(IRiskSurvivalCurve[] curves, Dt maturity, IList<CreditBasketUnderlying> underlyings)
    {
      int N = underlyings.Count;
      double[] weights = new double[underlyings.Count];

      for (int i = 0; i < N; ++i)
        weights[i] = underlyings[i].Percentage;

      SurvivalCurve[] survivalCurves = new SurvivalCurve[N];

      for (int i = 0; i < N; ++i)
      {
        survivalCurves[i] = curves[i].SurvivalCurve;
      }

      return BasketUtil.CalcExpectedLossPv(maturity, survivalCurves, weights);
    }

    /// <summary>
    /// Calculates the specified measure for the basket of names.
    /// </summary>
    ///
    /// <param name="curves"></param>
    /// <param name="maturity"></param>
    /// <param name="underlyings"></param>
    /// <param name="measure"></param>
    ///
    /// <returns></returns>
    ///
    public static double CalcBasketMeasure(IRiskSurvivalCurve[] curves, Dt maturity, IList<CreditBasketUnderlying> underlyings, BasketMeasure measure)
    {
      int N = underlyings.Count;
      double[] weights = new double[underlyings.Count];

      for (int i = 0; i < N; ++i)
        weights[i] = underlyings[i].Percentage;

      SurvivalCurve[] survivalCurves = new SurvivalCurve[N];

      for (int i = 0; i < N; ++i)
      {
        survivalCurves[i] = curves[i].SurvivalCurve;
      }

      return BasketUtil.CalcBasketMeasure(maturity, survivalCurves, weights, measure);
    }

    /// <summary>
    /// Heuristic to determine if basket underlyings are LCDS. 
    /// </summary>
    /// <param name="underlyings"></param>
    /// <returns>True if any underlyings have Cancellability other than None</returns>
    public static bool IsLCDSBasket(IEnumerable<CreditBasketUnderlying> underlyings)
    {
      foreach (CreditBasketUnderlying cbu in underlyings)
      {
        if (cbu.Cancellability != Cancellability.None)
          return true;
      }
      return false; 
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="basketUnderlyings"></param>
    /// <returns></returns>
    public static double[] GetAdjustedBasketWeights(IList<CreditBasketUnderlying> basketUnderlyings)
    {
      var percentages = new double[basketUnderlyings.Count];
      for (int i = 0; i < basketUnderlyings.Count; i++)
      {
        CreditBasketUnderlying ul = basketUnderlyings[i];
        percentages[i] = ul.Percentage;
      }

      double shortfall = 1.0;
      int nZeroes = 0;
      for (int i = 0; i < basketUnderlyings.Count; i++)
      {
        shortfall -= percentages[i];
        if (percentages[i] == 0.0)
        {
          nZeroes++;
        }
      }

      if (shortfall > 0.0 && nZeroes > 0)
      {
        shortfall /= nZeroes;

        for (int i = 0; i < basketUnderlyings.Count; i++)
        {
          if (percentages[i] == 0.0)
          {
            percentages[i] = shortfall;
          }
        }
      }

      return percentages;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="currentBasketUnderlyings"></param>
    /// <param name="originalBasketUnderlyings"></param>
    /// <returns></returns>
    public static double[] GetAdjustedBasketWeights(IList<CreditBasketUnderlying> currentBasketUnderlyings, IList<CreditBasketUnderlying> originalBasketUnderlyings)
    {
      var percentages = new double[currentBasketUnderlyings.Count];

      // first check to see if weights are OK directly from the current basket

      for (int i = 0; i < currentBasketUnderlyings.Count; i++)
      {
        CreditBasketUnderlying ul = currentBasketUnderlyings[i];
        percentages[i] = ul.Percentage;
      }

      // assess any shortfall in sum of basket percentages

      double shortfall = 1.0;
      int nZeroes = 0;
      for (int i = 0; i < currentBasketUnderlyings.Count; i++)
      {
        shortfall -= percentages[i];
        if (percentages[i] == 0.0)
        {
          nZeroes++;
        }
      }

      // if no shortfall then percentages are good

      if (!(shortfall > 0.0) || nZeroes <= 0) return percentages;

      // otherwise look to the original basket if it matches up with the current basket

      if (originalBasketUnderlyings.Count == currentBasketUnderlyings.Count)
      {
        var originalBasketDict = originalBasketUnderlyings.ToDictionary(u => u.Ticker, u => u);

        if (currentBasketUnderlyings.All(u => originalBasketDict.ContainsKey(u.Ticker)))
        {
          percentages = new double[currentBasketUnderlyings.Count];
          for (int i = 0; i < currentBasketUnderlyings.Count; i++)
          {
            CreditBasketUnderlying ul;
            if (originalBasketDict.TryGetValue(currentBasketUnderlyings[i].Ticker, out ul))
            {
              percentages[i] = ul.Percentage;
            }
            else
            {
              // truly exceptional since checked with All() earlier
              throw new RiskException(string.Format("Could not find expected ticker {0} in original basket", currentBasketUnderlyings[i].Ticker));
            }
          }

          // reassess shortfall/nZeroes

          shortfall = 1.0;
          nZeroes = 0;
          for (int i = 0; i < currentBasketUnderlyings.Count; i++)
          {
            shortfall -= percentages[i];
            if (percentages[i] == 0.0)
            {
              nZeroes++;
            }
          }
        }
      } 

      if (shortfall > 0.0 && nZeroes > 0)
      {
        shortfall /= nZeroes;

        for (int i = 0; i < currentBasketUnderlyings.Count; i++)
        {
          if (percentages[i] == 0.0)
          {
            percentages[i] = shortfall;
          }
        }
      }

      return percentages;
    }

    /// <summary>
    ///   Checks for 100% Basket Weight
    /// </summary>
    /// <param name="underlyings"></param>
    /// <param name="errorMsg"></param>
    public static bool TryValidateBasketWeights(IList<CreditBasketUnderlying> underlyings, out string errorMsg)
    {
      double totalPercentage = underlyings.Sum(cbu => cbu.Percentage);
      bool isValid = (Math.Round(totalPercentage, 6) == 1);
      errorMsg = isValid
                   ? String.Empty
                   : String.Format("Total percentage for Underlyings is not 100%. It is [{0:p}]", totalPercentage);
      return isValid;
    }

    #endregion

 } // class CreditBasketUtil
}  
