using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Languages available for Risk scripting
  /// </summary>
  public enum ScriptLanguage
  {
    /// <summary>
    /// C#
    /// </summary>
    CSharp,
    /// <summary>
    /// VB.Net
    /// </summary>
    VisualBasic
  };

  /// <summary>
  /// Database entity for storing a code snippet
  /// </summary>
  [Serializable]
  [Component(ChildKey = new[] { "Name" },DisplayName = "Script")]
  public class CodeSnippet: BaseEntityObject
  {
    /// <summary>
    /// Default constructor. 
    /// </summary>
    public CodeSnippet()
    {
      ReadOnly = true; 
    }

    /// <summary>
    /// Name of Calculation
    /// </summary>
    [StringProperty(AllowNullValue = false, MaxLength = 64, DisplayName = "Calculation Name")]
    public string Name { get; set; }

    /// <summary>
    /// Name of Calculation
    /// </summary>
    [StringProperty(AllowNullValue = false, MaxLength = 128)]
    public string Description { get; set; }

    /// <summary>
    /// the code snippet. Body of a function
    /// </summary>
    [StringProperty(AllowNullValue = false, MaxLength = 1073741820)]
    public string Source { get; set; }

    /// <summary>
    /// Language of script
    /// </summary>
    [EnumProperty(AllowNullValue = false)]
    public ScriptLanguage ScriptLanguage { get; set; }

    /// <summary>
    /// Allow user edit
    /// </summary>
    [BooleanProperty(AllowNullValue = false)]
    public bool ReadOnly { get; set; }

    ///<summary>
    /// The Risk Measure produced by this script
    ///</summary>
    public ResultReferenceType ResultType
    {
      get{return ResultReferenceType.NettingSet;}
    }

    
  }
}
