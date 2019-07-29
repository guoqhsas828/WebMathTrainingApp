//
// NamespaceDoc.cs
// Namespace documentation for BaseEntity.Toolkit.Cashflows namespace
//  -2011. All rights reserved.
//

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Generalised Contingent Payment classes and methods
  /// </summary>
  /// 
  /// <remarks>
  /// <para>Payments are base block in financial products and they are made by one party to another. 
  /// A payment usually has three dimensions: currency, the time when the payment occurs and the 
  /// nominal amount paid. For the paying time, it can be either historical which is known
  /// or it can be projected in the future. For the amount paid, it can either be known or
  /// uncertain amount to be determined on some future dates. The financial models boil down
  /// to evaluating payments or options on the products.</para>
  /// 
  /// <para>
  /// Historically, we developed object model of cash flows for the credit risks evaluations. 
  /// It groups all the payment amounts at the date t into two categories: 1) the amount 
  /// when there is no default; 2) the amount when the credit event happens. This method 
  /// is very efficient for evaluating CDS type cash flows, but it also suffers some 
  /// shortcomings. 1). It only tie to the computational logic, not the business logic. 
  /// 2) Not all type of payments can be clearly grouped into two categories. 
  /// 3) Implicitly assume one payment at one time, not composable and hard to customize. 
  /// </para>
  /// 
  /// <para>
  /// These shortcomings can be solved by the payment schedule method, which 
  /// was initially developed for the rate products. The object of the payment schedule 
  /// directly maps to the business logics, and it is very convenient to customize 
  /// the payment schedule since single payment object is the basic building block 
  /// and it doesn’t tie to the specific computation model 
  /// such that we can apply the different models the calculate. 
  /// </para>
  /// 
  /// <para>
  /// With the payment schedule method, it is easy and straight forward to calculate
  /// many quanlities. For example, the pv function can be described as 
  /// 
  /// <math>Pv = \sum_i Amount_i * RiskyDiscount_i</math>.
  /// 
  /// And the whole calculation process can be simplied to:
  /// (1) Generate payment schedule which is a collection of series payments.
  /// (2) Feed in market data like curves, pricing dates and so on.
  /// (3) Calculate the risky discount based on the different type of the payment.
  /// (4) Calculate the Pv using above formula
  /// </para>
  /// 
  /// <para>
  /// We support a bunch of different types of payments to meet the various financial
  /// transactions. 
  /// <list type="bullet">
  /// <item>FixedInterestPayment</item> 
  /// <item>FloatingInterestPayment</item> 
  /// <item>ContingentPayment</item> 
  /// <item>OneTimePayment, including
  /// <list type="bullet">
  /// <item> BasicPayment </item>
  /// <item> PrincipleExchange </item>
  /// <item> UpfrontFee </item>
  /// <item> BulletBonusPayment </item>
  /// <item> DefaultSettlement </item>
  /// <item> FloatingPrincipleExchange </item>
  /// <item> DividendPayment </item>
  /// </list>
  /// </item> 
  /// <item>CapletPayment</item> 
  /// <item>CommodityPricerPayment</item> 
  /// <item>PaymentAnnotation</item> 
  /// <item>PriceReturnPayment</item> 
  /// <item>ScaledPayment</item> 
  /// <item>VariancePayment</item> 
  /// </list>>
  /// </para>
  /// 
  /// </remarks>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  internal class NamespaceDoc
  {
  }
}