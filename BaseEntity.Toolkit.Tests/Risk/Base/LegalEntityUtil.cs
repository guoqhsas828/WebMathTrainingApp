
using System;
using System.Collections;
using System.Linq;
using BaseEntity.Database;
using BaseEntity.Metadata;
using NHibernate.Criterion;
using NHibernate.Transform;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Utility methods for <see cref="BaseEntity.Risk.LegalEntity">Legal Entity</see> class.
  /// </summary>
  public class LegalEntityUtil
  {
    #region Utils

    /// <summary>
    ///   Compares the 2 entities and returns true if they match 
    ///   by Name, Ticker and CLIP.
    /// </summary>
    /// <param name="entity1"></param>
    /// <param name="entity2"></param>
    /// <returns></returns>
    public static bool AreSame(LegalEntity entity1, LegalEntity entity2)
    {
      // Return true if both are null.
      if (entity1 == null && entity2 == null) return true;

      // Return false if only one of them is null
      if (entity1 == null || entity2 == null) return false;

      // If none of them are null, then compare Name, Ticker and CLIP
      if (!entity1.Name.Equals(entity2.Name, StringComparison.OrdinalIgnoreCase)) return false;

      // Return false if only one of the tickers are null 
      if ((entity1.Ticker == null && entity2.Ticker != null) || (entity1.Ticker != null && entity2.Ticker == null))
        return false;

      if (entity1.Ticker != null && entity2.Ticker != null && !entity1.Ticker.Equals(entity2.Ticker, StringComparison.OrdinalIgnoreCase))
        return false;

      // Return false if only one of the CLIP's are null 
      if ((entity1.CLIP == null && entity2.CLIP != null) || (entity1.CLIP != null && entity2.CLIP == null))
        return false;

      if (entity1.CLIP != null && entity2.CLIP != null && !entity1.CLIP.Equals(entity2.CLIP, StringComparison.OrdinalIgnoreCase))
        return false;

      return true;
    }

    /// <summary>
    /// The Sector assigned to a LegalEntity for a given SectorType classification.
    /// </summary>
    /// 
    /// <param name="le">The LegalEntity</param>
    /// <param name="sectorType">The type of Sector classification</param>
    /// 
    /// <returns>Sector</returns>
    /// 
    public static SectorItem Sector(LegalEntity le, SectorType sectorType)
    {
      SectorItem result = null;

      // Find Sector
      foreach (SectorItem item in le.Sectors)
      {
        if (String.CompareOrdinal(item.Sector.SectorType.Name, sectorType.Name) == 0)
        {
          result = item;
          break;
        }
      }

      // Done
      return result;
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Return all entities
    /// </summary>
    public static IList FindAll()
    {
      return Session.Find("from LegalEntity");
    }

    /// <summary>
    ///   Get entity by id
    /// </summary>
    ///
    /// <param name="id">Id for LegalEntity to retrieve</param>
    ///
    /// <returns>LegalEntity</returns>
    ///
    public static LegalEntity FindById(long id)
    {
      var cm = ClassCache.Find(typeof(LegalEntity));
      return (LegalEntity)Session.Get(cm.Type, id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="roles"></param>
    /// <returns></returns>
    public static IList FindByRoles(LegalEntityRoles roles)
    {
      return Session.Find("from LegalEntity l where (l.EntityRoles & ?) > 0", roles, ScalarType.Int32);
    }

    /// <summary>
    ///  Return all LegalEntities that are Counterparties on any Trade 
    /// </summary>
    /// <returns></returns>
    public static IList FindAllCounterparties()
    {
      var lst =
        Session.Find(
          "select distinct l from LegalEntity l where l in (select distinct t.Counterparty from Trade t) or l in (select distinct b.Issuer from Bond b) or l in (select distinct ln.Issuer from Loan ln)");
      var transformer = new DistinctRootEntityResultTransformer();
      return transformer.TransformList(lst);
    }

    /// <summary>
    ///   Return all LegalEntities with role BookingEntity
    /// </summary>
    /// <returns></returns>
    public static IList FindAllBookingEntities()
    {
      return Session.Find("from LegalEntity l where (l.EntityRoles & ?) > 0", LegalEntityRoles.BookingEntity, ScalarType.Int32);
    }

    /// <summary>
    ///   Lookup entity by shortName
    /// </summary>
    public static LegalEntity FindByName(string name)
    {
      IList entities = Session.Find("from LegalEntity e where e.Name = ?", name, ScalarType.String);

      if (entities.Count == 0)
        return null;
      else if (entities.Count == 1)
        return (LegalEntity)entities[0];
      else
        throw new DatabaseException("Invalid entity: " + name);
    }

    /// <summary>
    ///   Lookup entity by shortName
    /// </summary>
    public static LegalEntity FindByLongName(string name)
    {
      IList entities = Session.Find("from LegalEntity e where e.LongName = ?", name, ScalarType.String);

      if (entities.Count == 0)
        return null;
      else if (entities.Count == 1)
        return (LegalEntity)entities[0];
      else
        throw new DatabaseException("Invalid entity: " + name);
    }

    /// <summary>
    ///   Lookup entity by ticker
    /// </summary>
    public static LegalEntity FindByTicker(string ticker)
    {
      IList list = Session.Find("from LegalEntity e where e.Ticker = ?", ticker, ScalarType.String);
      if (list.Count == 0)
        return null;
      else if (list.Count == 1)
        return (LegalEntity)list[0];
      else
        throw new DatabaseException("Invalid ticker: " + ticker);
    }

    /// <summary>
    ///   Lookup entity by Markit RED CLIP
    /// </summary>
    public static LegalEntity FindByCLIP(string clip)
    {
      IList entities = Session.Find("from LegalEntity e where e.CLIP = ?", clip, ScalarType.String);
      if (entities.Count == 0)
        return null;
      else if (entities.Count == 1)
        return (LegalEntity)entities[0];
      else
        throw new DatabaseException("Invalid CLIP: " + clip);
    }

    /// <summary>
    ///   Lookup entities matching tag name and value
    /// </summary>
    /// <param name="tagName"></param>
    /// <param name="tagValue"></param>
    /// <returns></returns>
    public static IList FindByTag(string tagName, string tagValue)
    {
      return Session.CreateCriteria(typeof(LegalEntity))
        .CreateCriteria("Tags").Add(Restrictions.And(Restrictions.Eq("Name", tagName),
          Restrictions.Eq("Value", tagValue)))
        .List();
    }

    ///// <summary>
    /////   Lookup PaymentTerm by name
    ///// </summary>
    //public static PaymentTerm FindPaymentTerm(string name)
    //{
    //  string query = "FROM PaymentTerm where Name = ?";
    //  IList entities = Session.Find(query, name, ScalarType.String);

    //  if (entities.Count == 0)
    //    return null;
    //  else if (entities.Count == 1)
    //    return (PaymentTerm)entities[0];
    //  else
    //    throw new DatabaseException("Duplicate PaymentTerm name: " + name);
    //}

    /// <summary>
    /// Find a set of legal entities given their IDs
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public static IQueryable<LegalEntity> FindAllById(long[] ids)
    {
      return Session.Linq<LegalEntity>().Where(s => ids.Contains(s.ObjectId));
    }
    #endregion Methods

    #region Factory

    /// <summary>
    ///   Create instance of LegalEntity
    /// </summary>
    public static LegalEntity CreateInstance()
    {
      return (LegalEntity)Entity.CreateInstance();
    }

		/// <summary>
		///   Get meta information
		/// </summary>
		public static ClassMeta Entity
		{
			get { 
				if (entity_ == null)
					entity_ = ClassCache.Find("LegalEntity");
				return entity_; 
			}
		}

    #endregion Factory

    #region Data

		private static ClassMeta entity_;

    #endregion Data
  } // class LegalEntityUtil
}  