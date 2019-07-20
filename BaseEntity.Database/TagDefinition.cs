using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Database
{
  /// <summary>
  /// Provides access to optional definition of available tags on entities
  /// Currently this is an optional xml file whose path is specified in WebMathTraining.xml
  /// Expectation is to move this into a required database configuration in the future.
  /// AJT 20090219 - Just cut and paste from GUI library so it can be re-used by trade blotter. Leaving refactoring for another day 
  /// </summary>
  public class TagDefinitionHandler
  {
    /// <summary>
    /// Read All Tag Definitons.
    /// These are predefined tags that entities should always have
    /// Returns a dictionary keyed by entity name with a list of tags for that entity
    /// </summary>
    /// <returns>Dictionary keyed on EntityName of EntityTagDefinitions</returns>
    public static Dictionary<string, EntityTagDefinition> ReadTagDefinitions()
    {
      using (new SessionBinder())
      {
        return Session.Linq<EntityTagDefinition>().ToDictionary(e => e.Name, e => e.ResolveAll());
      }
    }

    /// <summary>
    /// Obtain any tag definitions for a specific entity
    /// This will handle checking base types and returns a merged tag definition
    /// 
    /// So if you ask for tags for CDSTrade and there are some tags defined for CDSTrade
    /// and others for Trade you will get the all (union) of them.
    /// 
    /// If the same tag name is defined multiple times in the hierarchy the most specific 
    /// type definiton is taken (eg use the one from CDSTrade to override the one in Trade)
    /// 
    /// isFixed defaults to false and the most specific definition found is used. Same with name.
    /// So in the example above you would get an EntityTagDefinition named CDSTrade with the isFixed
    /// value coming from CDSTrade and overriding any setting in the Trade tag definitions.
    /// </summary>
    /// <param name="entityType">Type to look for tag definitions for</param>
    /// <returns>null if none found</returns>
    public static EntityTagDefinition GetEntityTagDefinition(Type entityType)
    {
      IDictionary<string, EntityTagDefinition> tagDefs = ReadTagDefinitions();
      if (tagDefs == null || tagDefs.Count == 0)
        return null;

      // Create a list of the hierarchy of types for the one requested
      var types = new List<Type>();
      var bt = entityType;
      while (bt != typeof(Object))
      {
        types.Add(bt);
        bt = bt.BaseType;
      }

      EntityTagDefinition entityTagDefinition = null;

      // Now we look for any tag defintions from the basetype all the way to the specific type
      // This way if there is a setting on the Trade level and the same name tag on the CDSTrade level we
      // will use the CDSTrade tag as an override for the trade tag
      for (var x = types.Count - 1; x >= 0; x--)
      {
        var t = types[x];

        EntityTagDefinition temp;

        if (tagDefs.TryGetValue(t.Name, out temp))
        {
          entityTagDefinition = temp;
        }
      }

      return entityTagDefinition;
    }

    /// <summary>
    /// Query the database for all distinct tag names in the database for trades
    /// </summary>
    /// <returns>unique tag names from the trade tag table</returns>
    public static IEnumerable<string> GetTradeTagNames()
    {
      return GetDistinctTagNames("TradeTag");
    }

    /// <summary>
    /// Query the database for all distinct tag names in the database for products
    /// </summary>
    /// <returns>unique tag names from the product tag table</returns>
    public static IEnumerable<string> GetProductTagNames()
    {
      return GetDistinctTagNames("ProductTag");
    }

    /// <summary>
    /// Query the database for all distinct tag names in the database for Legal Entities
    /// </summary>
    /// <returns>unique tag names from the LegalEntityTag table</returns>
    public static IEnumerable<string> GetLegalEntityTagNames()
    {
      return GetDistinctTagNames("LegalEntityTag");
    }

    /// <summary>
    /// Select distinct tag names from the table specified
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    private static IEnumerable<string> GetDistinctTagNames(string tableName)
    {
      IDbConnection conn = null;

      var uniqueNames = new Dictionary<string, object>();

      try
      {
        conn = SessionFactory.OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "select distinct Name from " + tableName;
        cmd.CommandTimeout = SessionFactory.CommandTimeout;
        IDataReader reader = null;

        try
        {
          reader = cmd.ExecuteReader();

          while (reader.Read())
          {
            var tagName = reader["Name"] as string;

            if (tagName != null)
              uniqueNames[tagName] = null;
          }
        }
        finally
        {
          reader?.Close();
        }
      }
      finally
      {
        if (conn != null)
        {
          SessionFactory.CloseConnection(conn);
        }
      }

      return uniqueNames.Keys;
    }
  }

  /// <summary>
  /// Each entity may have a list of predefined tags.
  /// </summary>
  [Entity(EntityId = 150, Key = new[] { "Name" }, AuditPolicy = AuditPolicy.History)]
  public class EntityTagDefinition : AuditedObject
  {
    private IList<TagDefinition> _tags;

    /// <summary>
    /// Name of entity that these tags apply to
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string Name { get; set; }

    /// <summary>
    /// User is limited to only the defined tags
    /// </summary>
    [BooleanProperty]
    public bool Fixed { get; set; }

    /// <summary>
    /// List of all tags for this entity.
    /// </summary>
    [ComponentCollectionProperty(CollectionType = "list")]
    public IList<TagDefinition> Tags
    {
      get { return _tags ?? (_tags = new List<TagDefinition>()); }
      set { _tags = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tagName"></param>
    /// <returns></returns>
    public TagDefinition FindTag(string tagName)
    {
      return Tags.SingleOrDefault(t => t.Name == tagName);
    }
  }

  /// <summary>
  /// One day perhaps this will be in the db and thus an entity
  /// Each instance of this represents a single tag that is predefined to exist in an entity
  /// </summary>
  [Component(ChildKey = new[] { "Name" })]
  public class TagDefinition : BaseEntityObject
  {
    /// <summary>
    /// Tag name to always have in the tag list
    /// </summary>
    [StringProperty(MaxLength = 64)]
    public string Name { get; set; }

    /// <summary>
    /// Data Type used by the GUI. Actual storage of values is all string name/value pairs
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string DataType { get; set; }

    /// <summary>
    /// Default value used when adding this tag to an entity for the first time
    /// Note its a string because that is how the values are truly stored
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string DefaultValue { get; set; }

    /// <summary>
    /// Semicolon delimeted list of user choices
    /// </summary>
    [StringProperty(MaxLength = 65535)]
    public string Choices { get; set; }


    /// <summary>
    /// Safely gets delimited Choices
    /// </summary>
    /// <returns>array of choices or empty array</returns>
    public string[] GetChoices()
    {
      if (string.IsNullOrWhiteSpace(Choices))
        return new string[0];

      return Choices.Split(';');
      
      // todo: check how/if we haddled empty choices
      //return Choices.Split(new []{';'} , StringSplitOptions.RemoveEmptyEntries);
    }


    /// <summary>
    ///   Copy tag value from lead trade to unwind trade.
    /// </summary>
    [BooleanProperty]
    public bool CopyToUnwindTrade { get; set; }

    /// <summary>
    ///   Copy tag value from lead trade to assign trade.
    /// </summary>
    [BooleanProperty]
    public bool CopyToAssignTrade { get; set; }

    /// <summary>
    ///   Allows users to enter tag values that are not pre-defined.
    /// </summary>
    [BooleanProperty]
    public bool AllowItemsNotInChoices { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Type GetDataType()
    {
      return Type.GetType(DataType, false) ?? typeof(String);
    }
  }
}
