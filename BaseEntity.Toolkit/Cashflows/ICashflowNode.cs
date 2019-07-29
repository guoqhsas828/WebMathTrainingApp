using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.Simulations;

namespace BaseEntity.Toolkit.Cashflows
{
  #region ICashflowNode
  /// <summary>
  /// CashflowNode interface. 
  /// Objects implementing this interface should hold a reference to the Objects necessary to compute the coupon amount, 
  /// which will be evolved during simulation. 
  /// Objects implementing this interface should be marked as [Serializable]
  /// </summary>
  public interface ICashflowNode
  {
    #region Properties
    /// <summary>
    /// Reset date for floating rate
    /// </summary>
    Dt ResetDt { get; }

    /// <summary>
    /// Cashflow payment date
    /// </summary>
    Dt PayDt { get; }
    #endregion

    #region Methods

    /// <summary>
    /// Discount back from PayDt to asOf. <m>RiskyDiscount() = \beta_T I_{\{\tau \lt T\}} </m>
    /// </summary>
    /// <remarks>
    /// If the payment is credit contingent, RiskyDiscount should incorporate the indicator 
    /// of default and accrued to default fraction.
    /// </remarks>
    double RiskyDiscount();

    /// <summary>
    /// FxRate between cashflow denomination currency and numeraire (discounting) currency 
    /// </summary>
    double FxRate();

    /// <summary>
    /// Realized cashflow amount at ResetDt 
    /// </summary>
    double RealizedAmount();

    /// <summary>
    /// Cashflows are likely to be used across several paths. 
    /// Reset mechanism will re-initialize path dependent 
    /// accumulated quantities to their time 0 value
    /// </summary>
    void Reset();

    #endregion
  }

  #endregion

  #region ICashflowNodesGenerator
  /// <summary>
  /// Generate a list of cashflows that can be simulated in the LeastSquaresMonteCarloPricingEngine
  /// </summary>
  public interface ICashflowNodesGenerator
  {
    /// <summary>
    /// Cashflow
    /// </summary>
    IList<ICashflowNode> Cashflow { get; }
  }
  #endregion

  /// <summary>
  ///  Utility methods with cash flow nodes
  /// </summary>
  public static class CashflowNodeUtility
  {
    /// <summary>
    ///  Get all event dates inside the specified cash flow node
    /// </summary>
    /// <param name="node">The cash flow node</param>
    /// <returns>All the event dates</returns>
    public static IEnumerable<Dt> GetAllEventDates(this ICashflowNode node)
    {
      var payDt = node.PayDt;
      if (node is IMultiResetCashflowNode multi)
      {
        foreach (var resetNode in multi.ResetNodes)
        {
          if (payDt != resetNode.Date)
            yield return resetNode.Date;
        }
      }
      else if (payDt != node.ResetDt)
      {
        yield return node.ResetDt;
      }

      yield return payDt;
    }

    /// <summary>
    ///  Convert cash flow nodes to data table
    /// </summary>
    /// <param name="nodes">A list of cash flow nodes</param>
    /// <returns>The data table</returns>
    public static DataTable ToDataTable(this IEnumerable<ICashflowNode> nodes)
    {
      if (nodes == null) return null;

      const string PayDt = "Pay Date", ResetDt = "Reset Date",
        RealizedAmount = "Realized Amount", RiskyDiscount = "Risky Discount",
        FxRate = "FX Rate";
      var table = new DataTable();
      table.Columns.Add(PayDt, typeof(Dt));
      table.Columns.Add(ResetDt, typeof(Dt));
      table.Columns.Add(RealizedAmount, typeof(double));
      table.Columns.Add(RiskyDiscount, typeof(double));
      table.Columns.Add(FxRate, typeof(double));
      foreach(var node in nodes)
      {
        if (node == null) continue;
        var row = table.NewRow();
        row[PayDt] = node.PayDt;
        row[ResetDt] = node.ResetDt;
        row[RealizedAmount] = node.RealizedAmount();
        row[RiskyDiscount] = node.RiskyDiscount();
        row[FxRate] = node.FxRate();
        table.Rows.Add(row);
      }
      return table;
    }
  }
}
