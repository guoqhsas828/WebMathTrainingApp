using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Database;
using BaseEntity.Shared;
using NHibernate.Criterion;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Utility methods for <see cref="BusinessCenter"/>
  /// </summary>
  public class BusinessCenterUtil
  {
    #region Query

    /// <summary>
    ///   Gets all Business Centers.
    /// </summary>
    /// <returns></returns>
    public static IList FindAll()
    {
      return Session.CreateCriteria(typeof (BusinessCenter)).List();
    }

    /// <summary>
    ///   Gets all Business Centers with valid FpmlCode.
    /// </summary>
    /// <returns></returns>
    public static IList FindAllFpmlBusinessCenters()
    {
      IList lst = Session.CreateCriteria(typeof (BusinessCenter))
        .Add(Restrictions.IsNotNull("FpmlCode"))
        .List();

      return lst.Cast<BusinessCenter>().Where(bc => StringUtil.HasValue(bc.FpmlCode)).ToList();
    }

    #endregion
  }
}
