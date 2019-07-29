//
//   2015-2017. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  #region Config

  /// <summary>
  ///   Configuration settings for <see cref="IReferenceRate">Reference Rate</see> terms
  /// </summary>
  /// <exclude />
  [Serializable]
  public class ReferenceRateConfig
  {
    /// <exclude />
    [ToolkitConfig("Name of Xml file containing defined Reference Rate terms")]
    public readonly string DefinitionsFilename = "Data/ReferenceRates.xml";
  }

  #endregion Config

  /// <summary>
  ///   Cache for defined <see cref="IReferenceRate">Reference Rates</see>
  /// </summary>
  /// <remarks>
  ///   <para>By default, reference rates are defined in an xml file specified by the toolkit configuration.
  ///   An example of the configuration file entry is:</para>
  ///   <code language="xml">
  ///    &lt;!-- Name of xml file containing RateIndex terms --/&gt;
  ///    &lt;ReferenceRate DefinitionsFilename="Data/ReferenceRates.xml" /&gt;
  ///   </code>
  ///   <para>If no file is specified or the file is not found, pre-defined reference rates are specified in <see cref="ReferenceRateDefaults"/>.</para>
  ///   <para>Examples of standard product terms and usage are <see cref="InterestReferenceRate"/>, <see cref="SwapReferenceRate"/>, and
  ///   <see cref="FxReferenceRate"/>.</para>
  /// </remarks>
  /// <seealso cref="IReferenceRate"/>
  public sealed class ReferenceRateCache : StandardTermsCache<IReferenceRate>
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="initFunct">Default initialisation function</param>
    public ReferenceRateCache(Action<StandardTermsCache<IReferenceRate>> initFunct = null)
      : base(initFunct ?? DefaultInitialise)
    { }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Load standard reference rate terms based on specified file
    /// </summary>
    /// <remarks>
    ///   <para>Useful for overriding the initialisation process for testing.</para>
    /// </remarks>
    /// <param name="path">File path</param>
    public void LoadFromPath(string path)
    {
      StandardTermsXmlSerializer.LoadFromFile<IReferenceRate>(this, path);
    }

    /// <summary>
    ///  Save current reference rate terms in XML to a file
    /// </summary>
    /// <param name="xmlFile">Name of xml file to save to</param>
    public void SaveXmlTerms(string xmlFile)
    {
      var savedDisableNameLookup = ReferenceRateSerializer.DisableNameLookup;
      ReferenceRateSerializer.DisableNameLookup = true;
      StandardTermsXmlSerializer.SaveToFile(xmlFile, Terms.Values.OrderBy(t => $"{t.GetType()}{t.Currency}{t.Key}"));
      ReferenceRateSerializer.DisableNameLookup = savedDisableNameLookup;
    }

    /// <summary>
    /// True if cache contains terms
    /// </summary>
    /// <param name="terms">Reference rate terms to find</param>
    /// <returns>True if cache contains terms</returns>
    public bool Contains(IReferenceRate terms)
    {
      return (Terms.Count(p => p.Value == terms) > 0);
    }

    #endregion

    #region Local Methods

    /// <summary>
    ///  Default initialisation method calls xml reader
    /// </summary>
    private static void DefaultInitialise(StandardTermsCache<IReferenceRate> cache)
    {
      if (!StandardTermsXmlSerializer.LoadFromFile(cache, ToolkitConfigurator.Settings.ReferenceRate.DefinitionsFilename))
        // Load from file failed so use in-memory defaults
        ReferenceRateDefaults.Initialise(cache);
      return;
    }

    #endregion Local Methods
  }
}
