using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Database;
using BaseEntity.Metadata;
using NHibernate;
using NHibernate.Criterion;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public class SharedBasketUtil : ObjectFactory<SharedBasket>
  {
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static IList FindAll()
    {
      return Session.CreateCriteria(typeof (SharedBasket)).List();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static IList FindAllLCDSBaskets()
    {
      ICriteria sharedBasketCriteria = Session.CreateCriteria(typeof(SharedBasket));
      sharedBasketCriteria.CreateCriteria("Underlyings").Add(
        Restrictions.Not(Restrictions.Eq("Cancellability", Cancellability.None)));
      return sharedBasketCriteria.List();
    }


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static SharedBasket FindByName(string name)
    {
      IList list = Session.CreateCriteria(typeof (SharedBasket)).Add(Restrictions.Eq("Name", name)).List();
      if(list.Count==0) 
        return null;
      if (list.Count == 1)
        return (SharedBasket) list[0];
      throw new RiskException(String.Format("Multiple Shared Baskets found with name [{0}]", name));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static IList<string> FindAllBasketNames()
    {
      return Session.CreateCriteria(typeof (SharedBasket))
        .AddOrder(Order.Asc(Projections.Property("Name")))
        .SetProjection(Projections.Property("Name"))
        .List<string>();
    }

    #region Factory

    /// <summary>
    ///   Create a new Shared Basket
    /// </summary>
    ///
    /// <returns>Created Shared Basket</returns>
    ///
    public static SharedBasket CreateInstance()
    {
      return Create();
    }

    #endregion Factory
  }
}
