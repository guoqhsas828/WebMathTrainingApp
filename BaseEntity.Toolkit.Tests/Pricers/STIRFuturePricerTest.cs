//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test cases for RateFuturePricer.
  /// </summary>
  /// <remarks>
  /// Results manually calculated in STIRFuturePricerTest.xls
  /// </remarks>
  [TestFixture]
  public class STIRFuturePricerTest : ToolkitTestBase
  {

    #region Tests

    /// <summary>
    /// Model Rate
    /// </summary>
    // CEM 3M ED Futures.
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.04962713)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.04962372)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.04962372)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.04962713)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.04962713)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.04962372)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.04948296)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.04931845)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.04931845)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.04931845)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.04931845)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = 0.05030949)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = 0.05030949)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = 0.05030949)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = 0.05030949)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.05031346)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.05030997)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.05030997)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.05031346)]
    public double ModelRate(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).ModelRate(), 8);
    }

    /// <summary>
    /// Model Value
    /// </summary>
    // CEM 3M ED Futures
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98759321.74)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98759406.89)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98759406.89)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98759321.74)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98759321.74)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98759406.89)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 298779750.22)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 298783807.11)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 298783807.11)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 298783807.11)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 298783807.11)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = 98774692.08)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = 98774692.08)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = 98774692.08)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = 98774692.08)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 98759270.16)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 98759356.11)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 98759356.11)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 98759270.16)]
    public double Pv(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).Pv(), 2);
    }

    /// <summary>
    /// Value
    /// </summary>
    // CEM 3M ED Futures
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98646875.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98642500.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98670000.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98703750.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98728750.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 98740000.00)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 299383500.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 299383500.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 299383500.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 299385966.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 299385966.00)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = 99341083.00)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = 99321620.00)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = 99302164.00)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = 99280286.00)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 99389665.00)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 99397063.00)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 99397063.00)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 99384733.00)]
    public double Value(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).Value(), 2);
    }

    /// <summary>
    /// Contract Margin Value
    /// </summary>
    // CEM 3M ED Futures
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 986468.75)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 986425.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 986700.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 987037.50)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 987287.50)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 987400.00)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 2993835.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 2993835.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 2993835.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 2993859.66)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 2993859.66)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = 993410.83)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = 993216.20)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = 993021.64)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = 992802.86)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 993896.65)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 993970.63)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 993970.63)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 993847.33)]
    public double ContractMarginValue(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).ContractMarginValue(price / 100.0), 2);
    }

    /// <summary>
    /// Percentage Margin Value
    /// </summary>
    // CEM 3M ED Futures
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.98646875)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.98642500)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.98670000)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.98703750)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.98728750)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 0.98740000)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.99794500)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.99794500)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.99794500)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.99795322)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 0.99795322)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = 0.99341083)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = 0.99321620)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = 0.99302164)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = 0.99280286)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.99389665)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.99397063)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.99397063)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 0.99384733)]
    public double PercentageMarginValue(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).PercentageMarginValue(price / 100.0), 8);
    }

    /// <summary>
    /// Tick Value
    /// </summary>
    // CEM 3M ED Futures
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 12.50)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 12.50)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 12.50)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 12.50)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 12.50)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 12.50)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 12.33)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 12.33)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 12.33)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 12.33)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 12.33)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = 24.33)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = 24.33)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = 24.31)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = 24.30)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 12.33)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 12.33)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 12.33)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 12.33)]
    public double TickValue(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).TickValue(), 2);
    }

    /// <summary>
    /// PV01
    /// </summary>
    // CEM 3M ED Futures
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 25.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 25.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 25.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 25.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 25.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = 25.00)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 24.66)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 24.66)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 24.66)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 24.66)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = 24.66)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = 24.33)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = 24.32)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = 24.31)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = 24.30)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 24.66)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 24.66)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 24.66)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = 24.66)]
    public double PointValue(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).PointValue(), 2);
    }

    /// <summary>
    /// Point Value
    /// </summary>
    // CEM 3M ED Futures
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110613, 20110615, 20110615, 20110915, "3M", 0.05, 94.5875, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = -2500.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20110919, 20110921, 20110921, 20111221, "3M", 0.05, 94.5700, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = -2500.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20111219, 20111221, 20111221, 20120321, "3M", 0.05, 94.6800, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = -2500.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120319, 20120321, 20120321, 20120621, "3M", 0.05, 94.8150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = -2500.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120618, 20120620, 20120620, 20120920, "3M", 0.05, 94.9150, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = -2500.00)]
    [NUnit.Framework.TestCase(RateFutureType.MoneyMarketCashRate, 20110509, 20120917, 20120919, 20120919, 20121219, "3M", 0.05, 94.9600, DayCount.Actual360, 1000000.0, 0.5 / 1e4, 12.5, 2, ExpectedResult = -2500.00)]
    // ASX 30D Deposit Futures
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110531, 20110531, 20110502, 20110531, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = -2466.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110630, 20110630, 20110601, 20110630, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = -2466.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110729, 20110729, 20110701, 20110729, "1M", 0.05, 97.5000, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = -2466.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110831, 20110831, 20110801, 20110831, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = -2466.00)]
    [NUnit.Framework.TestCase(RateFutureType.ArithmeticAverageRate, 20110509, 20110930, 20110930, 20110901, 20110930, "1M", 0.05, 97.5100, DayCount.Actual360, 3000000.0, 0.5 / 1e4, 12.33, 0, ExpectedResult = -2466.00)]
    // ASX 90D Bill Futures
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110609, 20110609, 20110609, 20110907, "90D", 0.05, 97.3100, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.33, 2, ExpectedResult = -2433.00)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20110908, 20110908, 20110908, 20111207, "90D", 0.05, 97.2300, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.32, 2, ExpectedResult = -2432.00)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20111208, 20111208, 20111208, 20120307, "90D", 0.05, 97.1500, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.31, 2, ExpectedResult = -2431.00)]
    [NUnit.Framework.TestCase(RateFutureType.ASXBankBill, 20110509, 20120308, 20120308, 20120308, 20120606, "90D", 0.05, 97.0600, DayCount.Actual365Fixed, 1000000.0, 1.0 / 1e4, 24.30, 2, ExpectedResult = -2430.00)]
    // ASX OIS Futures
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110608, 20110609, 20110609, 20110909, "3M", 0.05, 97.5250, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = -2466.00)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20110907, 20110908, 20110908, 20111208, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = -2466.00)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20111207, 20111208, 20111208, 20120308, "3M", 0.05, 97.5550, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = -2466.00)]
    [NUnit.Framework.TestCase(RateFutureType.GeometricAverageRate, 20110509, 20120307, 20120308, 20120308, 20120608, "3M", 0.05, 97.5050, DayCount.Actual365Fixed, 1000000.0, 0.5 / 1e4, 12.33, 1, ExpectedResult = -2466.00)]
    public double Pv01(RateFutureType type, int asOf, int last, int delivery, int accrStart, int accrEnd, string tenor, double ir, double price, DayCount dc, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      return Math.Round(Pricer(type, asOf, last, delivery, accrStart, accrEnd, tenor, ir, price, dc, contractSize, tickSize, tickValue, daysToSettle).Pv01(), 2);
    }

    #endregion Tests

    #region Utils

    /// <summary>
    /// Create a pricer
    /// </summary>
    private StirFuturePricer Pricer(RateFutureType type, int asOfDate, int lastDate, int lastDeliveryDate,
      int accrualStartDate, int accrualEndDate, string tenorString, double ir, double price,
      DayCount daycount, double contractSize, double tickSize, double tickValue, int daysToSettle)
    {
      ToolkitConfigurator.Init();
      var asOf = new Dt(asOfDate);
      var lastTrading = new Dt(lastDate);
      var lastDelivery = new Dt(lastDeliveryDate);
      var accrualStart = new Dt(accrualStartDate);
      var accrualEnd = new Dt(accrualEndDate);
      var discountCurve = new DiscountCurve(asOf, ir);
      double caVol = 0.0;
      var roll = BDConvention.None;
      var calendar = Calendar.None;
      var tenor = Tenor.Parse(tenorString);
      var indexTenor = (type == RateFutureType.GeometricAverageRate || type == RateFutureType.ArithmeticAverageRate) ? new Tenor(1, TimeUnit.Days) : tenor;
      var ccy = Currency.USD;
      var index = new InterestRateIndex(String.Empty, indexTenor, ccy, daycount, calendar, roll, daysToSettle);
      if (asOf > accrualStart)
      {
        var d = accrualStart;
        var rr = new RateResets();
        while (d < asOf)
        {
          rr.Add(new RateReset(d, ir));
          d = Dt.Roll(Dt.Add(d, indexTenor), BDConvention.Following, calendar);
        }
        index.HistoricalObservations = rr;
      }
      var future = new StirFuture(type, lastDelivery, accrualStart, accrualEnd, index, contractSize, tickSize, tickValue) { LastTradingDate = lastTrading };
      RateModelParameters modelPars = null;
      if (type == RateFutureType.MoneyMarketCashRate)
      {
        var caCurve = new VolatilityCurve(asOf, caVol);
        modelPars = new RateModelParameters(RateModelParameters.Model.Hull, new[] { RateModelParameters.Param.Sigma },
          new IModelParameter[] { caCurve }, future.ReferenceIndex.IndexTenor, future.Ccy);
      }
      var pricer = new StirFuturePricer(future, asOf, asOf, 100, discountCurve, discountCurve) { QuotedPrice = price/100.0, RateModelParameters = modelPars};
      return pricer;
    }

    #endregion Utils
  }
}
