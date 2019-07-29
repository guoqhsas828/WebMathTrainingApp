/*
 * CustomScheduleUtil.cs
 *
 *  -2012. All rights reserved.
 *
 */

using System;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Utility methods to import a custom schedule for a Bond or SwapLeg as a DataTable coming from an excel
  ///   range and transform this data to a PaymentSchedule format, as well as method(s) to extract the custom schedule
  ///   data from a product (in reality, Bond or SwapLeg) and format it as a data table that can be presented in Excel.
  ///   The names of the columns of the data table are enumerated as string constants below.
  /// </summary>
  public static class CustomScheduleUtil
  {
    /// <summary>
    ///   Utility method to import a custom schedule for a Bond or SwapLeg as a DataTable coming from an excel
    ///   range, transform this data to a PaymentSchedule format and apply to the product passed.
    ///   The parameter amortType applies only to a Bond.
    ///   For a swap leg, the value in Notional/Notional Change column is always interpreted as the Remaining Notional level.
    /// </summary>
    public static void ApplyCustomSchedule(ProductWithSchedule pws, DataTable customSchedule, AmortizationType amortType)
    {
      // This function is similar to ApplyCustomCashflowsTable() in CashflowHelper.cs
      // The input data does not include principal payment recods separately, and these items will be
      // inferred from the changes in Notional from one row to the next.

      if (!(pws is Bond || pws is SwapLeg)) return; // Supported for just these two types right now.

      PaymentSchedule ps = CreateCustomScheduleFromDataTable(pws, customSchedule, amortType);
      if (ps != null && ps.Count > 0)
        pws.CustomPaymentSchedule = ps;
    }

    /// <summary>
    ///   Utility method to import a custom schedule for a Bond or SwapLeg as a DataTable coming from an excel
    ///   range and transform this data to a PaymentSchedule format.
    ///   The parameter amortType applies only to a Bond.
    ///   For a swap leg, the value in Notional/Notional Change column is always interpreted as the Remaining Notional level.
    /// </summary>
    public static PaymentSchedule CreateCustomScheduleFromDataTable(ProductWithSchedule pws, DataTable customSchedule, AmortizationType amortType)
    {
      // This function is similar to ApplyCustomCashflowsTable() in CashflowHelper.cs
      // The input data does not include principal payment recods separately, and these items will be
      // inferred from the changes in Notional from one row to the next.

      // First perform some basic validation of the input data:
      string valErr = ValidateCustomSchedule(pws, customSchedule, amortType);
      if (!string.IsNullOrEmpty(valErr))
        throw new ToolkitException("Invalid custom cash flows data: " + valErr);

      var bond = pws as Bond;
      var sLeg = pws as SwapLeg;
      bool isBond = (bond != null);
      // The following item is not really used right now; however, we still introduce it as we might put it to use ...
      const double notionalScalingFactor = 1.0;
      double prevNotional = 0.0;
      if (isBond)
        prevNotional = 1.0;
      int row, count = 0;
      Dt prevPaydate = Dt.Empty;
      Dt prevAccrualPeriodEnd = Dt.Empty;
      bool isChangeOfNotionalImported = IsNotionalChangeColumnApplicable(pws, amortType);
      bool isFloat = IsFloating(pws);
      bool isResetDateCustomizable = IsResetDateCustomizable(pws);
      bool allRedeemed = false;
      var ps = new PaymentSchedule();
      DayCount dayCount = DayCount.None;
      if (bond != null)
        dayCount = bond.DayCount;
      else if (sLeg != null)
        dayCount = sLeg.DayCount;
      InterestRateIndex referenceIndex = null;
      CompoundingConvention compConvention = (sLeg != null ? sLeg.CompoundingConvention : CompoundingConvention.None);

      if (isFloat)
      {
        if (bond != null)
          referenceIndex = bond.ReferenceIndex as InterestRateIndex;
        else if (sLeg != null)
          referenceIndex = sLeg.ReferenceIndex as InterestRateIndex;
      }

      for (row = 0; row < customSchedule.Rows.Count; row++)
      {

        DataRow r = customSchedule.Rows[row];
        try
        {
          double notional;
          Dt accrualStart = new Dt((DateTime)r[AccrualStartColumn]);
          Dt accrualEnd = new Dt((DateTime)r[AccrualEndColumn]);

          if (accrualStart == accrualEnd)
            continue; // We assume that the row with accrual start = accrual end is a principal payment row (no interest) - usually, the first row, and we skip it.

          Dt cycleStartDate = Dt.Empty;
          Dt cycleEndDate = Dt.Empty;
          if (customSchedule.Columns.Contains(CycleStartDateColumn) && r[CycleStartDateColumn] is System.DateTime)
            cycleStartDate = new Dt((DateTime)r[CycleStartDateColumn]);
          if (customSchedule.Columns.Contains(CycleEndDateColumn) && r[CycleEndDateColumn] is System.DateTime)
            cycleEndDate = new Dt((DateTime)r[CycleEndDateColumn]);

          Dt payDate = new Dt((DateTime)r[PayDateColumn]);
          var couponOrSpread = (double)r[CouponColumn];
          if (isChangeOfNotionalImported)
          {
            double importedChangeOfNotional = (double)r[NotionalColumn];
            if (amortType == AmortizationType.PercentOfInitialNotional)
              notional = prevNotional - importedChangeOfNotional;
            else  // AmortizationType.PercentOfCurrentNotional
              notional = prevNotional - prevNotional * importedChangeOfNotional;
          }
          else
          {
            notional = (double)r[NotionalColumn] / notionalScalingFactor;
          }
          if (isBond)
          {
            if (notional < AmortizationUtil.NotionalTolerance)  // Notional reached 0 or even below 0
            {
              if (notional > -AmortizationUtil.NotionalTolerance)
                allRedeemed = true;  // No more notional outstanding
              else // Notional became negative - error in input data
                throw new ToolkitException("Invalid custom cash flows data - outstanding notional becomes negative as of date " + accrualStart.ToString());
            }
          }

          count++; // Count the rows that represent an interest payment, not a principal payment.
          double changeNotional = notional - prevNotional;
          prevNotional = notional;
          if (!allRedeemed)
          {
            InterestPayment fip;
            Dt lastPaymentDate = prevPaydate;
            if (isFloat)
            {
              ForwardRateCalculator rateProjector = new ForwardRateCalculator(payDate, referenceIndex, null);
              fip = new FloatingInterestPayment(lastPaymentDate, payDate, pws.Ccy,
                (cycleStartDate != Dt.Empty ? cycleStartDate : accrualStart),
                (cycleEndDate != Dt.Empty ? cycleEndDate : accrualEnd),
                accrualStart, accrualEnd, Dt.Empty, notional, couponOrSpread, dayCount, pws.Freq,
                compConvention, rateProjector, null)
              {
                AccrueOnCycle = true, // Force this flag for customized cash flow payments so that Accrual Start/End match the period start/end dates, and the accrual factor is computed based on these input dates
                IncludeEndDateInAccrual = false
              };
              if (isResetDateCustomizable)
                ((FloatingInterestPayment)fip).ResetDate = new Dt((DateTime)r[ResetDateColumn]);
            }
            else // Fixed
            {
              fip = new FixedInterestPayment(lastPaymentDate, payDate, pws.Ccy,
                                             (cycleStartDate != Dt.Empty ? cycleStartDate : accrualStart),
                                             (cycleEndDate != Dt.Empty ? cycleEndDate : accrualEnd),
                                             accrualStart, accrualEnd, Dt.Empty, notional, couponOrSpread, dayCount, pws.Freq)
              {
                AccrueOnCycle = true,
                // Force this flag for customized cash flow payments so that Accrual Start/End match the period start/end dates, and the accrual factor is computed based on these input dates
                IncludeEndDateInAccrual = false
              };
            }
            ps.AddPayment(fip);
          }

          PrincipalExchange rbp = null;
          if (count == 1 && (sLeg == null || sLeg.InitialExchange) && Math.Abs(changeNotional) > 0.0)
          {
            rbp = new PrincipalExchange(pws.Effective, -changeNotional, pws.Ccy);
            ps.AddPayment(rbp);
          }
          else if (count > 1 && (sLeg == null || sLeg.IntermediateExchange) && Math.Abs(changeNotional) > 0.0)
          {
            // This is not the first row of interest payments.
            // We interpret the change of notional as the amount borrowed or repayed on the previous pay date.
            rbp = new PrincipalExchange(prevPaydate, -changeNotional, pws.Ccy);

            // For bonds, we also need to record the end of the accrual period this principal payment is associated with
            // so that we can eliminate the cash flows (in case of a payment lag) whose payment date is after the trade settlement,
            // while the end of the corresponding period is before the trade settlement.
            if (bond != null)
              rbp.CutoffDate = prevAccrualPeriodEnd;
            ps.AddPayment(rbp);
          }
          if (row == customSchedule.Rows.Count - 1 && (sLeg == null || sLeg.FinalExchange))
          {
            // In the last row, we will repay all the outstanding principal.
            rbp = new PrincipalExchange(payDate, notional, pws.Ccy);
            if (bond != null)
              rbp.CutoffDate = accrualEnd;
            ps.AddPayment(rbp);
          }
          prevPaydate = payDate;
          prevAccrualPeriodEnd = accrualEnd;
        }
        catch (Exception ex)
        {
          throw new ToolkitException(string.Format("Cannot create payment for pasted row [{0}].", ex.Message));
        }
        if (allRedeemed) break; // Notional reached 0 - ignore the rest of the custom schedule.
      }
      return ps;
    }

    /// <summary>
    ///   Utility method to format a custom schedule of a Bond or SwapLeg as a DataTable.
    ///   The parameter amortType applies only to a Bond.
    ///   For a swap leg, the value in Notional/Notional Change column is always interpreted as the Remaining Notional level.
    /// </summary>
    public static DataTable ExtractCustomSchedule(ProductWithSchedule pws, AmortizationType amortType)
    {
      var bond = pws as Bond;
      var sLeg = pws as SwapLeg;
      if (bond == null && sLeg == null) return null;
      PaymentSchedule ps = pws.CustomPaymentSchedule;
      if (ps == null || ps.Count == 0) return null;
      InterestPayment[] arr = ps.ToArray<InterestPayment>(null);  // These will be sorted by the pay date.
      if (arr == null || arr.Length == 0) return null;
      int i;
      var tb = new DataTable();
      bool isFloat = IsFloating(pws);
      for (i = 0; i < AllColumnNames.Length; i++)
        tb.Columns.Add(new DataColumn(AllColumnNames[i], AllColumnTypes[i]));
      double previousNotional = 1.0;
      for (i = 0; i < arr.Length; i++)
      {
        DataRow tbRow = tb.NewRow();

        tbRow[AccrualStartColumn] = arr[i].AccrualStart.ToDateTime();
        tbRow[AccrualEndColumn] = arr[i].AccrualEnd.ToDateTime();
        if (!arr[i].CycleStartDate.IsEmpty())
          tbRow[CycleStartDateColumn] = arr[i].CycleStartDate.ToDateTime();
        if (!arr[i].CycleEndDate.IsEmpty())
          tbRow[CycleEndDateColumn] = arr[i].CycleEndDate.ToDateTime();
        tbRow[PayDateColumn] = arr[i].PayDt;
        if (isFloat && arr[i] is FloatingInterestPayment)
          tbRow[ResetDateColumn] = ((FloatingInterestPayment)arr[i]).ResetDate.ToDateTime();

        if (bond != null && amortType != AmortizationType.RemainingNotionalLevels)
        {
          double changeNotional = ComputeNotionalChange(arr[i].Notional, previousNotional, amortType);
          tbRow[NotionalColumn] = changeNotional;
        }
        else
          tbRow[NotionalColumn] = arr[i].Notional;

        tbRow[CouponColumn] = arr[i].FixedCoupon; // In FloatingInterestPayment, the FixedCoupon field stores the spread.

        tb.Rows.Add(tbRow);
        previousNotional = arr[i].Notional;
      }

      return tb;
    }

    private static string ValidateCustomSchedule(ProductWithSchedule pws, DataTable tb, AmortizationType amortType)
    {
      var bond = pws as Bond;
      var sLeg = pws as SwapLeg;
      if ((bond == null && sLeg == null) || tb == null || tb.Rows.Count == 0) return string.Empty;
      bool isFloat = IsFloating(pws);
      bool isResetDateCustomizable = IsResetDateCustomizable(pws);
      string eMess = ValidateCustomSchedule(bond != null, isFloat, isResetDateCustomizable, tb, amortType);
      return eMess;
    }

    /// <summary> This function is made public to be re-used in the RiskCustomScheduleUtil class </summary>
    public static string ValidateCustomSchedule(bool isBond, bool isFloat, bool isResetDateCustomizable, DataTable tb, AmortizationType amortType)
    {
      // This function is similar to ValidateCustomCashflowsTable() in CashflowHelper.cs

      if (tb == null || tb.Rows.Count == 0) return string.Empty;
      // This function returns a validation error message, or an empty string if no error reported.
      // First check that all the required columns are present.
      int i;

      for (i = 0; i < AllColumnNames.Length; i++)
      {
        if (AllColumnNames[i] == CycleStartDateColumn || AllColumnNames[i] == CycleEndDateColumn)
          continue; // These two columns are not required
        if (!isResetDateCustomizable && AllColumnNames[i] == ResetDateColumn)
          continue; // This column not needed for fixed coupon product.
        int ind = tb.Columns.IndexOf(AllColumnNames[i]);
        if (ind < 0)
          return ("Not all the required data columns are present.\n" + GetCashflowsInputColumnsFormatMessage(isFloat));
        if (tb.Columns[ind].DataType != AllColumnTypes[i])
          return ("The column " + AllColumnNames[i] + " has incorrect data type. The correct data type is " + AllColumnTypes[i]);
      }

      // Now perform the following basic validations row-by-row:
      // 1. All the required values are present
      // 2. In each row, accrual start is <= accrual end
      // 3. accrual end of the previous row <= accrual start of the next row
      // 4. pay date in previous row < pay date in next row
      for (i = 0; i < tb.Rows.Count; i++)
      {
        DataRow r = tb.Rows[i];

        if (!(r[AccrualStartColumn] is System.DateTime))
          return ("The value in row " + (i + 1) + " in column " + AccrualStartColumn + " is missing.");
        if (!(r[AccrualEndColumn] is System.DateTime))
          return ("The value in row " + (i + 1) + " in column " + AccrualEndColumn + " is missing.");

        Dt accrualStart = new Dt((DateTime)r[AccrualStartColumn]);
        Dt accrualEnd = new Dt((DateTime)r[AccrualEndColumn]);

        if (accrualStart == accrualEnd)
          continue; // We assume that the row with accrual start = accrual end is a principal payment row (no interest) - usually, the first row, and we skip it.

        if (!(r[PayDateColumn] is System.DateTime))
          return ("The value in row " + (i + 1) + " in column " + PayDateColumn + " is missing.");

        if (!(r[NotionalColumn] is System.Double))
          return ("The value in row " + (i + 1) + " in column " + NotionalColumn + " is missing.");
        double notionalImported = (double)r[NotionalColumn];
        if (notionalImported < 0.0 && (isBond && amortType == AmortizationType.RemainingNotionalLevels))
          return ("The value in row " + (i + 1) + " in column " + NotionalColumn + " is negative.");  // Can not import negative remaining notional level for a bond schedule.
        if (!(r[CouponColumn] is System.Double))
          return ("The value in row " + (i + 1) + " in column " + CouponColumn + " is missing.");
        if ((double)r[CouponColumn] < 0.0 && !isFloat)
          return ("The value in row " + (i + 1) + " in column " + CouponColumn + " is negative.");

        if (isResetDateCustomizable)
        {
          if (!(r[ResetDateColumn] is System.DateTime))
            return ("The value in row " + (i + 1) + " in column " + ResetDateColumn + " is missing.");
        }

        Dt payDate = new Dt((DateTime)r[PayDateColumn]);
        if (accrualStart > accrualEnd)
          return ("Accrual start date is after the end date in row " + (i + 1));
        if (i > 0)  // Check row 2, 3, ...
        {
          DataRow rPrev = tb.Rows[i - 1];
          Dt payDatePrev = new Dt((DateTime)rPrev[PayDateColumn]);
          Dt accrualEndPrev = new Dt((DateTime)rPrev[AccrualEndColumn]);
          if (accrualEndPrev > accrualStart)
            return ("Accrual start date in row " + (i + 1) + " is before the end date in the previous row.");
          if (payDate < payDatePrev)
            return ("Pay date in row " + (i + 1) + " is before the pay date in the previous row.");
        }
        if (tb.Columns.Contains(CycleStartDateColumn) && tb.Columns.Contains(CycleEndDateColumn) && r[CycleStartDateColumn] is System.DateTime && r[CycleEndDateColumn] is System.DateTime)
        {
          Dt cycleStartDate = new Dt((DateTime)r[CycleStartDateColumn]);
          Dt cycleEndDate = new Dt((DateTime)r[CycleEndDateColumn]);
          if (cycleStartDate != Dt.Empty && cycleEndDate != Dt.Empty && cycleEndDate < cycleStartDate)
            return ("Cycle start date is after the cycle end date in row " + (i + 1));
        }
      }
      return string.Empty; // No error found
    }

    private static bool IsFloating(ProductWithSchedule pws)
    {
      var bond = pws as Bond;
      var sLeg = pws as SwapLeg;
      if (bond == null && sLeg == null) return false;
      if (bond != null && bond.Floating || sLeg != null && sLeg.Floating) return true;
      return false;
    }

    private static bool IsResetDateCustomizable(ProductWithSchedule pws)
    {
      if (!IsFloating(pws)) return false;
      var sLeg = pws as SwapLeg;
      if (sLeg != null)
      {
        if (sLeg.CompoundingConvention != CompoundingConvention.None)
          return false;
        if (sLeg.ProjectionType != ProjectionType.SimpleProjection && sLeg.ProjectionType != ProjectionType.SwapRate)
          return false;
      }
      return true;
    }

    private static bool IsNotionalChangeColumnApplicable(ProductWithSchedule pws, AmortizationType amortType)
    {
      if (!(pws is Bond)) return false;
      if (amortType == AmortizationType.PercentOfInitialNotional || amortType == AmortizationType.PercentOfCurrentNotional)
        return true;
      return false;
    }

    /// <summary>
    /// This function builds a message about the expected format of the custom cash flows input data.
    /// </summary>
    private static string GetCashflowsInputColumnsFormatMessage(bool isFloat)
    {
      string output = "The imported data must have the following columns (with headers):";
      for (int i = 0; i < AllColumnNames.Length; i++)
      {
        output += NewLine;

        if (AllColumnNames[i] == CycleStartDateColumn || AllColumnNames[i] == CycleEndDateColumn)
          continue; // These two columns are not required
        if (!isFloat && AllColumnNames[i] == ResetDateColumn)
          continue; // This column not needed for fixed coupon product.

        output += (AllColumnNames[i] + " (");
        if (AllColumnTypes[i] == typeof(DateTime))
          output += "Date";
        else
          output += "Numerical";
        output += ")";
      }
      return output;
    }

    /// <summary>
    /// This function is made public to be re-used in the RiskCustomScheduleUtil
    /// </summary>
    public static double ComputeNotionalChange(double currentNotional, double previousNotional, AmortizationType type)
    {
      if (type == AmortizationType.PercentOfInitialNotional)
        return previousNotional - currentNotional;
      else if (type == AmortizationType.PercentOfCurrentNotional)
        return (1.0 - currentNotional / previousNotional);
      return 0;
    }

    // Display column names; also used in imported custom schedule:
    // Make these constants public to be re-used in a "parralel" class RiskCustomScheduleUtil (operating on the Risk level)

    /// <summary></summary>
    public const string AccrualStartColumn = "Accrual Start";
    /// <summary></summary>
    public const string AccrualEndColumn = "Accrual End";
    /// <summary></summary>
    public const string ResetDateColumn = "Reset Date";
    /// <summary></summary>
    public const string PayDateColumn = "Pay Date";
    /// <summary></summary>
    public const string NotionalColumn = "Notional/Notional Change";
    /// <summary></summary>
    public const string CouponColumn = "Cpn/Spread";
    /// <summary></summary>
    public const string CycleStartDateColumn = "Cycle Start Date";
    /// <summary></summary>
    public const string CycleEndDateColumn = "Cycle End Date";

    /// <summary></summary>
    public static readonly string[] AllColumnNames = {AccrualStartColumn, AccrualEndColumn, ResetDateColumn, PayDateColumn,
      NotionalColumn, CouponColumn, CycleStartDateColumn, CycleEndDateColumn };

    /// <summary></summary>
    public static readonly Type[] AllColumnTypes = { typeof(DateTime), typeof(DateTime), typeof(DateTime), typeof(DateTime),
      typeof(double), typeof(double), typeof(DateTime), typeof(DateTime) };

    private const string NewLine = @"
";
  }
}