/*
 * DataPurger.cs
 *
 * Copyright (c) WebMathTraining Inc 2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Database.Engine;
using BaseEntity.Metadata;

using log4net.Core;

namespace BaseEntity.Database
{
  /// <summary>
	///
	/// </summary>
	public class DataPurger
	{
		#region Constructors

		/// <summary>
		///
		/// </summary>
		public DataPurger()
			: this(Level.Debug)
		{
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="logLevel"></param>
		public DataPurger(Level logLevel)
		{
			logLevel_ = logLevel;
			myReporter_ = ConsoleReporter;
		}

		#endregion

		/// <summary>
		///
		/// </summary>
		/// <param name="po"></param>
		private void DoPurge(PersistentObject po)
		{
			MyReporter(Level.Info, "Purging [{0}]", ((PersistentObject)po).FormKey());
			// We always flush after delete so that when we search for orphaned
			// references we do not see any references from this object.
			Session.Delete(po);
			Session.Flush();
		}

    /// <summary>
    /// Return list of objects that directly reference this object via a many-to-one association
    /// </summary>
    /// <remarks>
    /// This function assumes that components cannot have component properties!
    /// </remarks>
    /// <param name="object"></param>
    /// <returns>IList</returns>
    public IList FindReferences(PersistentObject @object)
    {
      return FindReferences(@object, null);  
    }
    
    /// <summary>
    /// Return list of objects that directly reference this object via a many-to-one association
    /// </summary>
    /// <remarks>
    /// This function assumes that components cannot have component properties!
    /// </remarks>
    /// <param name="object"></param>
    /// <param name="parent">the parent object, ignore references from parent</param>
    /// <returns>IList</returns>
    public IList FindReferences(PersistentObject @object, PersistentObject parent)
    {
			var entity = ClassCache.Find(@object);

      PersistentObject po = @object as PersistentObject;

      // TODO: Put FormKey in the interface
    	MyReporter(Level.Info, "Finding all references to [{0}]", po.FormKey());
    	
			List<Type> skipEntityCache = new List<Type>();
			foreach (PropertyMeta pm in entity.PropertyList)
			{
			  var ompm = pm as OneToManyPropertyMeta;
				if (ompm != null)
				{
					// Create a list of assocations where we cascade deletes.  We ignore the child entities
					// in this case since these references should not prevent the purge from completing.

					// This check should work given the existing (9.0) schema, however it may not work in the
					// future if we have more complex relationships.  More thought needs to be given to this to
					// fully understand the requirements.

					// Needed to change in 9.1 to support MarketShift.
					// The onetoMany may be to a base type eg MarketShift and the entity that references back
					// may be the derived type (survivalcurveshift) so we cant check the name we need to check
					// the type and look at the basetypes.
					if (ompm.Cascade.Contains("all"))
					{
					  skipEntityCache.Add(ompm.ReferencedEntity.Type);
					}
				}
			}

			ArrayList allRefs = new ArrayList();
      foreach (ClassMeta cm in ClassCache.FindAll().Where(cm => cm.IsEntity))
				{
					// Find references to this object from the specified entity
					IList objRefs = purgeHandler_.FindReferences(cm, @object, MyReporter);
					foreach (PersistentObject obj in objRefs)
					{
						bool shouldSkip = false;

						foreach (Type t in skipEntityCache)
						{
							if (cm.IsA(t))
							  shouldSkip = true;
            }

            // if we are recursing down into a child object, ignore references back to the parent
          if (parent != null && obj.ObjectId == parent.ObjectId)
              shouldSkip = true;

            if (shouldSkip)
						{
              // if we are at the top level object (no parent)
              // recurse into any child objects that were in the skip list (i.e. oneToMany references)
              // make sure the child objects are safe to purge
              if (parent == null)
              {
                var childEntityRefs = FindReferences(obj, @object);
                allRefs.AddRange(childEntityRefs);
              }

							MyReporter(Level.Info, "Skipping reference from [{0}]", po.FormKey());
						}
						else
						{
							allRefs.Add(obj);
						}
					}
				}

			return allRefs;
		}

		/// <summary>
    ///   Purges a given Persistent Object if there are no references.
		/// </summary>
		/// <param name="po">The Persistent Object to be deleted.</param>
		/// <returns>True, if no references are found and is able to purge the object; otherwise False</returns>
		public bool Purge(PersistentObject po)
		{
		  return Purge(po, true);
		}


    /// <summary>
    ///   Purges a given Persistent Object. Checks for references if specified
    /// </summary>
    /// <param name="po">The Persistent Object to be deleted </param>
    /// <param name="checkReferences">Whether or not to check for references</param>
    /// <returns>True, if able to purge the object; otherwise False</returns>
    /// <exclude></exclude>
    public bool Purge(PersistentObject po, bool checkReferences)
    {
      if (checkReferences)
      {
        IList allRefs = FindReferences(po);
        if (allRefs.Count > 0)
        {
          MyReporter(Level.Error, "Unable to purge {{{0}}} ({1} references)", ((PersistentObject) po).FormKey(),
                     allRefs.Count);

          foreach (PersistentObject refObj in allRefs)
          {
            MyReporter(Level.Error, "{{{0}}} referenced by {{{1}}}", ((PersistentObject) po).FormKey(),
                       ((PersistentObject) refObj).FormKey());
          }

          return false;
        }
      }

      // Purge this object
      DoPurge(po);

      return true;
    }

		/// <summary>
		///
		/// </summary>
		/// <param name="logLevel"></param>
		/// <param name="format"></param>
		/// <param name="args"></param>
		private void ConsoleReporter(Level logLevel, string format, params object[] args)
		{
			if (logLevel >= logLevel_)
			{
				Console.WriteLine(String.Format(
					System.Globalization.CultureInfo.InvariantCulture, format, args));
			}
		}

		#region Properties

		/// <summary>
		///
		/// </summary>
		public Reporter MyReporter
		{
			get { return myReporter_; }
		}

		/// <summary>
		/// Return true if audit logging is enabled for this entity
		/// </summary>
		private static bool HasAuditTable(ClassMeta cm)
		{
			if (cm.IsA(typeof(PersistentObject)))
				return (cm.AuditPolicy != AuditPolicy.None);

			return false;
		}

		#endregion

		#region Data

		private Level logLevel_;
		private Reporter myReporter_;
		private static IPurgeHandler purgeHandler_ = new DefaultPurgeHandler();

		#endregion

	} // class DataPurger
} // namespace BaseEntity.Database
