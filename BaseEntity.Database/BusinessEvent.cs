
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Configuration;
using BaseEntity.Database.Engine;
using BaseEntity.Shared;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  ///   Abstract parent class for all Business Event Implementations
  /// </summary>
  /// <remarks>
  /// <para>  
  ///   Business Events chnage the state of an object as of an Effective
  ///   date and can be rolled back to restore the object to its old state.
  /// </para>
  /// </remarks>
  [Serializable]
  [DataContract]
  [KnownType("EstablishKnownTypes")]
  [Entity(TableName = "BusinessEvent", SubclassMapping = SubclassMappingStrategy.TablePerSubclass, AuditPolicy = AuditPolicy.None)]
  public abstract class BusinessEvent : PersistentObject, IComparable
  {
    #region Constructors


    /// <summary>
    ///   Initializes a new instance of the <see cref="BusinessEvent"/> class.
    /// </summary>
    protected BusinessEvent()
    {
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessEvent"/> class.
    /// </summary>
    /// <param name="effectiveDate">The effective date.</param>
    protected BusinessEvent(DateTime effectiveDate)
    {
      EffectiveDate = effectiveDate;
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Generates an Event Order and sets it on the Business Event
    /// </summary>
    private void SetEventOrder()
    {
      EventOrder = BusinessEventOrderGenerator.Generate();
    }

    /// <summary>
    ///   Apply this event to the underlying object(s)
    /// </summary>
    protected abstract void DoApply();

    /// <summary>
    ///   Apply this event to the underlying object(s)   
    /// </summary>
    /// <remarks>
    ///   This will set the EventOrder. Use this when
    ///   applying the event for the first time.
    /// </remarks>
    public void Apply()
    {
      DoApply();
      SetEventOrder();
    }

    /// <summary>
    ///   Re-apply this event to the underlying object(s)
    /// </summary>
    /// <remarks>
    ///   This will not set the EventOrder. Use this when 
    ///   re-applying the event after rolling back. 
    /// </remarks>
    public void ReApply()
    {
      DoApply();
    }

    /// <summary>
    /// Undo the effect of this event
    /// </summary>
    public abstract void Rollback();

    /// <summary>
    ///  
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      if (EffectiveDate == DateTime.MinValue)
        InvalidValue.AddError(errors, this, "EffectiveDate", "Value cannot be empty!");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the effective date.
    /// </summary>
    /// <value>The effective date.</value>
    [DataMember]
    [DateTimeProperty(AllowNull = false, IsTreatedAsDateOnly = true)]
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the event order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The EventOrder determines the order of the
    /// events occuring on the same date and time.
    /// </para>
    /// </remarks>
    /// <value>The event order.</value>
    [DataMember]
    [Browsable(false)]
    [NumericProperty(AllowNull = false)]
    public long EventOrder { get; set; }


    /// <summary>
    /// Gets or sets the target object id.
    /// </summary>
    /// <value>The target id.</value>
    [DataMember]
    [Browsable(false)]
    [NumericProperty(AllowNull = false)]
    public long TargetId { get; set; }


    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    [DataMember]
    [StringProperty(MaxLength = 512)]
    public string Description { get; set; }

    #endregion

    #region IComparable Members


    /// <summary>
    /// Compares the current instance with another object of the same type.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>
    /// A 32-bit signed integer that indicates the relative order of the objects being compared. 
    /// The return value has these meanings: 
    /// Value Meaning Less than zero This instance is less than <paramref name="obj"/>. 
    /// Zero This instance is equal to <paramref name="obj"/>. 
    /// Greater than zero This instance is greater than <paramref name="obj"/>.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">
    /// 	<paramref name="obj"/> is not the same type as this instance. </exception>
    public int CompareTo(object obj)
    {
      var other = obj as BusinessEvent;
      if (other == null)
        return 1;

      int result = EffectiveDate.CompareTo(other.EffectiveDate);
      return (result == 0) ? EventOrder.CompareTo(other.EventOrder) : result;
    }

    #endregion

    private static readonly Lazy<Type[]> KnownTypes = new Lazy<Type[]>(LoadKnownTypes);

    private static Type[] LoadKnownTypes()
    {
      var knownTypes = Configurator.GetPlugins(PluginType.EntityModel)
        .SelectMany(p => p.Assembly.GetTypes().Where(IsKnownType))
        .ToArray();

      return knownTypes;
    }

    private static bool IsKnownType(Type t)
    {
      return t.IsClass && !t.IsAbstract && typeof(BusinessEvent).IsAssignableFrom(t) && t.GetCustomAttributes(typeof(DataContractAttribute), false).Any();
    }

    /// <summary>
    /// Establishes the known types.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<Type> EstablishKnownTypes()
    {
      return KnownTypes.Value;
    }
  }
}
