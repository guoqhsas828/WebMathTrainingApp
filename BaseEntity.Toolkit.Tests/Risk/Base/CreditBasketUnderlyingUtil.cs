/*
 * CreditBasketUnderlyingUtil.cs
 *
 */

using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;


namespace BaseEntity.Risk
{

  /// <summary>
  /// </summary>
  public class CreditBasketUnderlyingUtil
  {
    #region Factory

    /// <summary>
		///   Create a new credit basket underlying
    /// </summary>
		///
		/// <returns>Created CreditBasketUnderlying</returns>
		///
    public static CreditBasketUnderlying CreateInstance()
    {
      return (CreditBasketUnderlying)Entity.CreateInstance();
    }

    /// <summary></summary>
    public static CreditBasketUnderlying CreateInstance(LegalEntity referenceEntity, Currency ccy, Seniority seniority, RestructuringType restructuringType, double percentage)
    {
      CreditBasketUnderlying cbu = CreateInstance();
      cbu.ReferenceEntity = referenceEntity;
      cbu.Currency = ccy;
      cbu.Seniority = seniority;
      cbu.RestructuringType = restructuringType;
      cbu.Percentage = percentage;
      return cbu;
    }

    /// <summary>
		///   Get meta data
    /// </summary>
    public static ClassMeta Entity
    {
      get
      {
        if (entity_ == null)
          entity_ = ClassCache.Find("CreditBasketUnderlying");
        return entity_;
      }
    }

		#endregion Factory

		#region Data

    private static ClassMeta entity_;

    #endregion

  } // class CreditBasketUnderlyingUtil
}  
