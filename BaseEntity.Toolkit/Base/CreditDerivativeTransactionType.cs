//
// CreditDerivativeTransactionType.cs
//   2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   ISDA Credit Derivative Transaction Type
  /// </summary>
  /// <remarks>
  ///   <para>Used to define the standard terms for categories of credit derivative trades as per the
  ///   ISDA Credit Derivatives Physical Settlement Matrix.</para>
  ///   <seealso href="http://www.isda.org/c_and_a/Credit-Derivatives-Physical-Settlement-Matrix.html"/>
  ///   <seealso href="http://www.fpml.org/spec/coding-scheme/fpml-schemes.html#s5.104"/>
  /// </remarks>
  public enum CreditDerivativeTransactionType
  {
    /// <summary>
    ///  None
    /// </summary>
    None,

    /// <summary>
    ///  Asia Corporate
    /// </summary>
    AsiaCorporate,

    /// <summary>
    ///  Asia Financial Corporate
    /// </summary>
    AsiaFinancialCorporate,

    /// <summary>
    ///  Asia Sovereign
    /// </summary>
    AsiaSovereign,

    /// <summary>
    ///  Australia Corporate
    /// </summary>
    AustraliaCorporate,

    /// <summary>
    ///  Australia Financial Corporate
    /// </summary>
    AustraliaFinancialCorporate,

    /// <summary>
    ///  Australia Sovereign
    /// </summary>
    AustraliaSovereign,

    /// <summary>
    ///  Emerging European and Middle Eastern Sovereign
    /// </summary>
    EmergingEuropeanAndMiddleEasternSovereign,

    /// <summary>
    ///  Emerging European Corporate
    /// </summary>
    EmergingEuropeanCorporate,

    /// <summary>
    ///  Emerging European Corporate LPN
    /// </summary>
    EmergingEuropeanCorporateLPN,

    /// <summary>
    ///  Emerging European Financial Corporate
    /// </summary>
    EmergingEuropeanFinancialCorporate,

    /// <summary>
    ///  Emerging European Financial Corporate LPN
    /// </summary>
    EmergingEuropeanFinancialCorporateLPN,

    /// <summary>
    ///  European CoCo Financial Corporate
    /// </summary>
    EuropeanCoCoFinancialCorporate,

    /// <summary>
    ///  European Corporate
    /// </summary>
    EuropeanCorporate,

    /// <summary>
    ///  European Financial Corporate
    /// </summary>
    EuropeanFinancialCorporate,

    /// <summary>
    ///  Japan Corporate
    /// </summary>
    JapanCorporate,

    /// <summary>
    ///  Japan Financial Corporate
    /// </summary>
    JapanFinancialCorporate,

    /// <summary>
    ///  Japan Sovereign
    /// </summary>
    JapanSovereign,

    /// <summary>
    ///  Latin America Corporate
    /// </summary>
    LatinAmericaCorporate,

    /// <summary>
    ///  Latin America Corporate B
    /// </summary>
    LatinAmericaCorporateBond,

    /// <summary>
    ///  Latin America Corporate BL
    /// </summary>
    LatinAmericaCorporateBondOrLoan,

    /// <summary>
    ///  Latin America Financial Corporate B
    /// </summary>
    LatinAmericaFinancialCorporateBond,

    /// <summary>
    ///  Latin America Financial Corporate BL
    /// </summary>
    LatinAmericaFinancialCorporateBondOrLoan,

    /// <summary>
    ///  Latin America Sovereign
    /// </summary>
    LatinAmericaSovereign,

    /// <summary>
    ///  New Zealand Corporate
    /// </summary>
    NewZealandCorporate,

    /// <summary>
    ///  New Zealand Financial Corporate
    /// </summary>
    NewZealandFinancialCorporate,

    /// <summary>
    ///  New Zealand Sovereign
    /// </summary>
    NewZealandSovereign,

    /// <summary>
    ///  North American Corporate
    /// </summary>
    NorthAmericanCorporate,

    /// <summary>
    ///  North American Financial Corporate
    /// </summary>
    NorthAmericanFinancialCorporate,

    /// <summary>
    ///  Singapore Corporate
    /// </summary>
    SingaporeCorporate,

    /// <summary>
    ///  Singapore Financial Corporate
    /// </summary>
    SingaporeFinancialCorporate,

    /// <summary>
    ///  Singapore Sovereign
    /// </summary>
    SingaporeSovereign,

    /// <summary>
    ///  Standard Asia Corporate
    /// </summary>
    StandardAsiaCorporate,

    /// <summary>
    ///  Standard Asia Financial Corporate
    /// </summary>
    StandardAsiaFinancialCorporate,

    /// <summary>
    ///  Standard Asia Sovereign
    /// </summary>
    StandardAsiaSovereign,

    /// <summary>
    ///  Standard Australia Corporate
    /// </summary>
    StandardAustraliaCorporate,

    /// <summary>
    ///  Standard Australia Financial Corporate
    /// </summary>
    StandardAustraliaFinancialCorporate,

    /// <summary>
    ///  Standard Australia Sovereign
    /// </summary>
    StandardAustraliaSovereign,

    /// <summary>
    ///  Standard Emerging European and Middle Eastgern Sovereign
    /// </summary>
    StandardEmergingEuropeanAndMiddleEasternSovereign,

    /// <summary>
    ///  Standard Emerging European Corporate
    /// </summary>
    StandardEmergingEuropeanCorporate,

    /// <summary>
    ///  Standard Emerging European Corporate LPN
    /// </summary>
    StandardEmergingEuropeanCorporateLPN,

    /// <summary>
    ///  Standard Emerging European Financial Corporate
    /// </summary>
    StandardEmergingEuropeanFinancialCorporate,

    /// <summary>
    ///  Standard Emerging European Financial Corporate LPN
    /// </summary>
    StandardEmergingEuropeanFinancialCorporateLPN,

    /// <summary>
    ///   Standard European CoCo Financial Corporate
    /// </summary>
    StandardEuropeanCoCoFinancialCorporate,

    /// <summary>
    ///   Standard European Corporate
    /// </summary>
    StandardEuropeanCorporate,

    /// <summary>
    ///   Standard European Financial Corporate
    /// </summary>
    StandardEuropeanFinancialCorporate,

    /// <summary>
    ///   Standard Japan Corporate
    /// </summary>
    StandardJapanCorporate,

    /// <summary>
    ///   Standard Japan Financial Corporate
    /// </summary>
    StandardJapanFinancialCorporate,

    /// <summary>
    ///   Standard Japan Sovereign
    /// </summary>
    StandardJapanSovereign,

    /// <summary>
    ///   Standard Latin America Corporate B
    /// </summary>
    StandardLatinAmericaCorporateBond,

    /// <summary>
    ///   Standard Latin America Corporate BL
    /// </summary>
    StandardLatinAmericaCorporateBondOrLoan,

    /// <summary>
    ///   Standard Latin America Financial Corporate B
    /// </summary>
    StandardLatinAmericaFinancialCorporateBond,

    /// <summary>
    ///   Standard Latin America Financial Corporate BL
    /// </summary>
    StandardLatinAmericaFinancialCorporateBondOrLoan,

    /// <summary>
    ///   Standard Latin America Sovereign
    /// </summary>
    StandardLatinAmericaSovereign,

    /// <summary>
    ///   Standard New Zealand Corporate
    /// </summary>
    StandardNewZealandCorporate,

    /// <summary>
    ///   Standard New Zealand Financial Corporate
    /// </summary>
    StandardNewZealandFinancialCorporate,

    /// <summary>
    ///   Standard New Zealand Sovereign
    /// </summary>
    StandardNewZealandSovereign,

    /// <summary>
    ///   Standard North American Corporate
    /// </summary>
    StandardNorthAmericanCorporate,

    /// <summary>
    ///   Standard North American Financial Corporate
    /// </summary>
    StandardNorthAmericanFinancialCorporate,

    /// <summary>
    ///   Standard Singapore Corporate
    /// </summary>
    StandardSingaporeCorporate,

    /// <summary>
    ///   Standard Singapore Financial Corporate
    /// </summary>
    StandardSingaporeFinancialCorporate,

    /// <summary>
    ///   Standard Singapore Sovereign
    /// </summary>
    StandardSingaporeSovereign,

    /// <summary>
    ///   Standard Subordinated European Insurance Corporate
    /// </summary>
    StandardSubordinatedEuropeanInsuranceCorporate,

    /// <summary>
    ///   Standard Sukuk Financial Corporate
    /// </summary>
    StandardSukukFinancialCorporate,

    /// <summary>
    ///   Standard US Municipal Full Faith and Credit
    /// </summary>
    StandardUSMunicipalFullFaithAndCredit,

    /// <summary>
    ///   Standard US Municipal General Fund
    /// </summary>
    StandardUSMunicipalGeneralFund,

    /// <summary>
    ///   Standard US Municipal Revenue
    /// </summary>
    StandardUSMunicipalRevenue,

    /// <summary>
    ///   Standard Western European Sovereign
    /// </summary>
    StandardWesternEuropeanSovereign,

    /// <summary>
    ///   Subordinated European Insurance Corporate
    /// </summary>
    SubordinatedEuropeanInsuranceCorporate,

    /// <summary>
    ///   Sukuk Corporate
    /// </summary>
    SukukCorporate,

    /// <summary>
    ///   Sukuk Financial Corporate
    /// </summary>
    SukukFinancialCorporate,

    /// <summary>
    ///   Sukuk Sovereign
    /// </summary>
    SukukSovereign,

    /// <summary>
    ///   US Municipal Full Faith and Credit
    /// </summary>
    USMunicipalFullFaithAndCredit,

    /// <summary>
    ///   US Municipal General Fund
    /// </summary>
    USMunicipalGeneralFund,

    /// <summary>
    ///   US Municipal Revenue
    /// </summary>
    USMunicipalRevenue,

    /// <summary>
    ///   Western European Sovereign
    /// </summary>
    WesternEuropeanSovereign

  }

  /// <summary>
  /// Useful CreditDerivativeTransactionType extension methods
  /// </summary>
  public static class CreditDerivativeTransactionTypeExtensionMethods
  {
    /// <summary>
    /// Convert from transaction type name or abreviation string to CreditDerivativeTransactionType
    /// </summary>
    /// <param name="nameOrAbreviation">nameOrAbreviation (eg. StandardNorthAmericanCorporate or SNAC)</param>
    /// <returns>CreditDerivativeTransactionType</returns>
    public static CreditDerivativeTransactionType ToCreditDerivativeTransactionType(this string nameOrAbreviation)
    {
      CreditDerivativeTransactionType type;
      if (Enum.TryParse(nameOrAbreviation, true, out type))
        return type;
      if (Abreviations.TryGetValue(nameOrAbreviation, out type))
        return type;
      // Not found
      throw new ToolkitException("No CreditDerivativeTransactionType found matching abreviation {0}", nameOrAbreviation);
    }

    /// <summary>
    /// Get CreditDerivativeTransactionType abreviation
    /// </summary>
    /// <param name="type">Credit Derivative Transaction Type</param>
    /// <returns>Abreviation</returns>
    public static string CreditDerivativeTransactionTypeAbreviation(this CreditDerivativeTransactionType type)
    {
      var abreviation = Abreviations.FirstOrDefault(x => x.Value == type).Key;
      return abreviation ?? type.ToString();
    }

    // List of abreviations
    private static readonly Dictionary<string, CreditDerivativeTransactionType> Abreviations = new Dictionary<string, CreditDerivativeTransactionType>()
    {
      {"SNAC", CreditDerivativeTransactionType.StandardNorthAmericanCorporate},
      {"STEC", CreditDerivativeTransactionType.StandardEuropeanCorporate},
      {"SLAC", CreditDerivativeTransactionType.StandardLatinAmericaCorporateBond},
      {"SEEC", CreditDerivativeTransactionType.StandardEmergingEuropeanCorporate},
      {"STAC", CreditDerivativeTransactionType.StandardAsiaCorporate},
      {"SAUC", CreditDerivativeTransactionType.StandardAustraliaCorporate},
      {"SNZC", CreditDerivativeTransactionType.StandardNewZealandCorporate},
      {"STJC", CreditDerivativeTransactionType.StandardJapanCorporate}
    };
  }
}
