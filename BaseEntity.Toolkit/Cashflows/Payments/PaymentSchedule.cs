
/*
 * PaymentSchedule.cs
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Payment Schedule that represent a set of payments made through time
  /// </summary>
  [Serializable]
  public class PaymentSchedule : IEnumerable<Payment>,
    IEnumerable<KeyValuePair<Dt,IList<Payment>>>, ISchedule
  {
    /// <summary>
    /// Create empty payment schedule
    /// </summary>
    public PaymentSchedule()
    {
      Payments = new SortedDictionary<Dt, IList<Payment>>();
    }

    private SortedDictionary<Dt, IList<Payment>> Payments { get; set; }

    /// <summary>
    /// Total number of payments
    /// </summary>
    public int Count
    {
      get { return Payments.Aggregate(0, (count, pair) => count + pair.Value.Count); }
    }

    internal int DateCount => Payments.Count;

    /// <summary>
    /// Clear a payment schedule
    /// </summary>
    public void Clear()
    {
      Payments.Clear();
    }

    /// <summary>
    /// First payment in a schedule. 
    /// </summary>
    /// <returns>First payment or null if PaymentSchedule is empty</returns>
    public Payment First()
    {
      if (Payments == null || Payments.Count == 0)
        return null;
      return Payments.First().Value.FirstOrDefault();
    }

    /// <summary>
    /// Get all Payments dates
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Dt> GetPaymentDates()
    {
      return Payments.Keys;
    }

    /// <summary>
    /// Get Payments that happen on given date
    /// </summary>
    /// <param name="d">Date for payments</param>
    /// <returns></returns>
    public IList<Payment> GetPaymentsOnDate(Dt d)
    {
      IList<Payment> retVal;
      if (Payments.TryGetValue(d, out retVal))
        return retVal;
      return new List<Payment>();
    }

    /// <summary>
    /// Get last (maturity) payments
    /// </summary>
    /// <typeparam name="T">Payment type</typeparam>
    /// <returns></returns>
    public IEnumerable<T> GetLastPaymentsByType<T>() where T : Payment
    {
      return Payments.Last().Value.OfType<T>();
    }

    /// <summary>
    /// Get all payments of a given type
    /// </summary>
    /// <typeparam name="T">Type of Payment</typeparam>
    /// <returns></returns>
    public IEnumerable<T> GetPaymentsByType<T>() where T : Payment
    {
      return GetByType<T>(this);
    }

    /// <summary>
    /// Get all payments of a given type and date
    /// </summary>
    /// <typeparam name="T">Type of payments</typeparam>
    /// <param name="d">Date of payments</param>
    /// <returns></returns>
    public IEnumerable<T> GetPaymentsByType<T>(Dt d) where T : Payment
    {
      if (Payments.ContainsKey(d))
        return GetByType<T>(Payments[d]);

      return new List<T>();
    }

    /// <summary>
    /// Get all payments of a given type and date
    /// </summary>
    /// <typeparam name="T">Type of payments</typeparam>
    /// <param name="d">Date of payments</param>
    /// <param name="match">Predicate</param>
    /// <returns>Payments of type T that satisfy the given condition</returns>
    public IEnumerable<T> GetPaymentsByType<T>(Dt d, Predicate<T> match) where T : Payment
    {
      IList<Payment> paymentsOnDate;
      if (Payments.TryGetValue(d, out paymentsOnDate))
      {
        return paymentsOnDate.Where(p => (p is T) && match((T)p)).Cast<T>();
      }
      return new List<T>();
    }

    private static IEnumerable<T> GetByType<T>(IEnumerable e) where T : Payment
    {
      return e.OfType<T>().ToList();
    }

    /// <summary>
    /// Add Payment
    /// </summary>
    /// <param name="payment"></param>
    public void AddPayment(Payment payment)
    {
      InternalAdd(payment);
    }

    /// <summary>
    /// Add Payments of a given type
    /// </summary>
    /// <typeparam name="T">Type of payment</typeparam>
    /// <param name="payments"></param>
    public void AddPayments<T>(IEnumerable<T> payments) where T : Payment
    {
      foreach (T payment in payments)
      {
        InternalAdd(payment);
      }
    }

    /// <summary>
    /// Add Payments of a given type
    /// </summary>
    /// <param name="payments"></param>
    public void AddPayments(PaymentSchedule payments)
    {
      foreach (Payment payment in payments)
      {
        InternalAdd(payment);
      }
    }

    /// <summary>
    /// Concatenate a payment schedule
    /// </summary>
    /// <param name="ps">Payment schedule</param>
    public void AddPaymentSchedule(PaymentSchedule ps)
    {
      foreach (Payment payment in ps)
      {
        InternalAdd(payment);
      }
    }

    private void InternalAdd<T>(T payment) where T : Payment
    {
      if (!Payments.ContainsKey(payment.PayDt))
      {
        Payments[payment.PayDt] = new List<Payment>();
      }
      Payments[payment.PayDt].Add(payment);
    }

    /// <summary>
    /// Remove Payments for a date
    /// </summary>
    /// <param name="dt">date</param>
    public void RemovePayments(Dt dt)
    {
      Payments.Remove(dt);
    }

    /// <summary>
    /// Remove Payments By Type for a date
    /// </summary>
    /// <param name="dt">date</param>
    public void RemovePaymentsByType<T>(Dt dt) where T : Payment
    {
      IList<Payment> paymentsOnDate;
      if (Payments.TryGetValue(dt, out paymentsOnDate))
      {
        ((List<Payment>)paymentsOnDate).RemoveAll(p => p is T);
        if (paymentsOnDate.Count == 0)
          Payments.Remove(dt);
      }
    }

    /// <summary>
    /// Remove Payments satisfying a given conditions
    /// </summary>
    /// <param name="match">Predicate</param>
    public void RemovePaymentsByType<T>(Predicate<T> match) where T : Payment
    {
      foreach (var keyVal in Payments.Where(pair =>
                                            {
                                              ((List<Payment>)pair.Value).RemoveAll(p => p is T && match((T)p));
                                              return !pair.Value.Any();
                                            }).ToList())
        Payments.Remove(keyVal.Key);

    }

    /// <summary>
    /// Convert all payments of type T to an array
    /// </summary>
    /// <typeparam name="T">Payment type</typeparam>
    /// <param name="predicate">Predicate</param>
    /// <returns>Array of payments of type T</returns>
    public T[] ToArray<T>(Func<T, bool> predicate) where T : Payment
    {
      return (predicate == null)
               ? Payments.SelectMany(pair => pair.Value.OfType<T>().ToArray()).ToArray()
               : Payments.SelectMany(pair => pair.Value.OfType<T>().Where(predicate)).ToArray();
    }

    /// <summary>
    /// Transform in place payments of type T
    /// </summary>
    /// <typeparam name="T">Payment type</typeparam>
    /// <param name="action">action</param>
    public IEnumerable<T> ConvertAll<T>(Action<T> action) where T : Payment
    {
      var retVal = new List<T>();
      foreach (var pList in Payments)
      {
        foreach (var p in pList.Value.OfType<T>())
        {
          action(p);
          retVal.Add(p);
        }
      }
      return retVal;
    }

    /// <summary>
    /// Produces data table representation of this schedule
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable(double pricerNotional = 1.0, DiscountCurve dc = null, SurvivalCurve sc = null)
    {
      var dataTable = new DataTable("PaymentSchedule table");
      const string fmt = "%d-%b-%Y";
      foreach (Payment p in this)
      {
        p.AddDataColumns(dataTable.Columns);

        // Include discount factor, survival probability and discounted payment columns
        if (dc != null && !dataTable.Columns.Contains("Discount Factor"))
        {
          dataTable.Columns.Add("Discount Factor", typeof(double));
          dataTable.Columns.Add("Survival Prob", typeof(double));
          dataTable.Columns.Add("Discounted Payment", typeof(double), "Amount * [Discount Factor]");
        }

        var row = dataTable.NewRow();
        p.AddDataValues(row, fmt);

        // Rescale for pricer notional
        if (pricerNotional != 1.0)
        {
          row["Notional"] = (double)row["Notional"] * pricerNotional;
          row["Amount"] = (double)row["Amount"] * pricerNotional;
        }

        // Include discount factor, survival probability and discounted payment
        if (dc != null)
        {
          row["Discount Factor"] = dc.DiscountFactor(p.PayDt);
          row["Survival Prob"] = sc != null ? sc.Interpolate(p.PayDt) : 1.0;
        }
        dataTable.Rows.Add(row);
      }
      return dataTable;
    }

    /// <summary>
    /// Produces data table representation of this schedule
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable(string columnName, IDictionary<Dt, double> principals)
    {
      var dataTable = new DataTable("PaymentSchedule table");
      dataTable.Columns.Add(new DataColumn(columnName, typeof (double)));
      const string fmt = "%d-%b-%Y";

      foreach (Payment p in this)
      {
        p.AddDataColumns(dataTable.Columns);

        var row = dataTable.NewRow();
        p.AddDataValues(row, fmt);
        if (principals != null)
        {
          row[columnName] = (principals.ContainsKey(p.PayDt) ? principals[p.PayDt] : 0.0);
          principals[p.PayDt] = 0.0; // in case there are > 1 interest payments on same day
        }
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    #region Implementation of IEnumerable

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    public IEnumerator<Payment> GetEnumerator()
    {
      return new PaymentEnumerator(Payments);
    }

    #endregion

    #region Nested type: PaymentEnumerator

    private class PaymentEnumerator : IEnumerator<Payment>
    {
      public PaymentEnumerator(SortedDictionary<Dt, IList<Payment>> payments)
      {
        Dates = payments.GetEnumerator();
      }

      #region Implementation of IEnumerator

      public bool MoveNext()
      {
        if (PaymentsForDate == null || !PaymentsForDate.MoveNext())
        {
          return MoveNextList();
        }
        return true;
      }

      public void Reset()
      {
        Dates.Reset();
        PaymentsForDate = null;
      }

      object IEnumerator.Current
      {
        get { return Current; }
      }

      public Payment Current
      {
        get
        {
          if (PaymentsForDate != null)
            return PaymentsForDate.Current;
          return null;
        }
      }

      private bool MoveNextList()
      {
        if (Dates.MoveNext())
        {
          PaymentsForDate = Dates.Current.Value.GetEnumerator();
          PaymentsForDate.MoveNext();
          return true;
        }
        return false;
      }

      void IDisposable.Dispose()
      {}

      #endregion

      private IEnumerator<KeyValuePair<Dt, IList<Payment>>> Dates { get; set; }
      private IEnumerator<Payment> PaymentsForDate { get; set; }
    }

    #endregion

    #region IEnumerable<KeyValuePair<Dt,IList<Payment>>> Members
    /// <summary>
    /// Enumerate over the payment lists
    /// </summary>
    /// <returns></returns>
    IEnumerator<KeyValuePair<Dt, IList<Payment>>> IEnumerable<KeyValuePair<Dt, IList<Payment>>>.GetEnumerator()
    {
      return Payments.GetEnumerator();
    }
    #endregion

    #region ISchedule members
    IList<Schedule.CouponPeriod> ISchedule.Periods
    {
      get
      {
        return this.OfType<InterestPayment>()
          .Select(p => new Schedule.CouponPeriod
          {
            AccrualBegin = p.AccrualStart,
            AccrualEnd = p.AccrualEnd,
            CycleBegin = p.CycleStartDate,
            CycleEnd = p.CycleEndDate,
            Payment = p.PayDt
          }).ToList();
      }
    }

    Dt ISchedule.GetNextCouponDate(Dt date)
    {
      var payment = this.OfType<InterestPayment>()
        .FirstOrDefault(p => p.AccrualEnd > date);
      return payment != null ? payment.AccrualEnd : Dt.Empty;
    }

    Dt ISchedule.GetPrevCouponDate(Dt date)
    {
      var payment = this.OfType<InterestPayment>()
        .LastOrDefault(p => p.AccrualEnd <= date);
      return payment != null ? payment.AccrualEnd : Dt.Empty;
    }
    #endregion
  }
}