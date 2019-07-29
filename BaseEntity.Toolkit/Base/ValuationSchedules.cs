// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// A pair of Value date and Payment date
  /// </summary>
  public struct ValueAndPaymentDatePair
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueAndPaymentDatePair"/> struct.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <param name="paymentDate">The payment date.</param>
    public ValueAndPaymentDatePair(Dt valueDate, Dt paymentDate)
    {
      ValueDate = valueDate;
      PaymentDate = paymentDate;
    }

    /// <summary>
    /// Gets the value date.
    /// </summary>
    /// <value>The value date.</value>
    public Dt ValueDate { get; private set; }
 
    /// <summary>
    /// Gets the payment date.
    /// </summary>
    /// <value>The payment date.</value>
    public Dt PaymentDate { get; private set; }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return $"{ValueDate}, {PaymentDate}";
    }
  }

  /// <summary>
  /// Valuation schedule with explicit dates.
  /// </summary>
  [Serializable]
  internal class ValuationScheduleWithExplicitDates : IReadOnlyList<ValueAndPaymentDatePair>
  {
    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    /// <value>The schedule.</value>
    private List<ValueAndPaymentDatePair> Schedule { get; set; }

    /// <summary>
    /// Adds the specified value date.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <param name="paymentDate">The payment date.</param>
    public void Add(Dt valueDate, Dt paymentDate)
    {
      var list = Schedule ?? (Schedule = new List<ValueAndPaymentDatePair>());
      list.Add(new ValueAndPaymentDatePair(valueDate, paymentDate));
    }

    /// <summary>
    /// Gets the number of (ValueDate, PaymentDate) pairs.
    /// </summary>
    /// <value>The count.</value>
    public int Count => Schedule?.Count ?? 0;

    /// <summary>
    /// Gets the <see cref="ValueAndPaymentDatePair"/> at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>ValueAndPaymentDatePair.</returns>
    public ValueAndPaymentDatePair this[int index]
    {
      get { return Schedule[index];}
    }


    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> 
    /// that can be used to iterate through the collection.</returns>
    public IEnumerator<ValueAndPaymentDatePair> GetEnumerator()
    {
      for (int i = 0, n = Count; i < n; ++i)
        yield return this[i];
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" /> 
    /// object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }

  /// <summary>
  /// Valuation schedule defined as payment dates relative to value dates.
  /// </summary>
  [Serializable]
  internal class ValuationScheduleRelativeToValueDates : IReadOnlyList<ValueAndPaymentDatePair>
  {
    /// <summary>
    /// Gets or sets the value dates.
    /// </summary>
    /// <value>The value dates.</value>
    internal IList<Dt> ValueDates { get; set; }

    /// <summary>
    /// Gets or sets the payment lag.
    /// </summary>
    /// <value>The payment lag.</value>
    internal int PaymentLag { get; set; }

    /// <summary>
    /// Gets or sets the calendar.
    /// </summary>
    /// <value>The calendar.</value>
    internal Calendar Calendar { get; set; }

    /// <summary>
    /// Gets the number of (ValueDate, PaymentDate) pairs.
    /// </summary>
    /// <value>The count.</value>
    public int Count => ValueDates?.Count ?? 0;


    /// <summary>
    /// Gets the <see cref="ValueAndPaymentDatePair"/> at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>ValueAndPaymentDatePair.</returns>
    public ValueAndPaymentDatePair this[int index]
    {
      get
      {
        Dt valueDate = ValueDates[index];
        return new ValueAndPaymentDatePair(valueDate, GetPaymentDate(valueDate));
      }
    }

    /// <summary>
    /// Gets the payment date at the specified index.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The payment date</returns>
    private Dt GetPaymentDate(Dt valueDate)
    {
      return PaymentLag == 0 ? valueDate : Dt.AddDays(valueDate, PaymentLag, Calendar);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> 
    /// that can be used to iterate through the collection.</returns>
    public IEnumerator<ValueAndPaymentDatePair> GetEnumerator()
    {
      for (int i = 0, n = Count; i < n; ++i)
        yield return this[i];
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" /> 
    /// object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }


}
