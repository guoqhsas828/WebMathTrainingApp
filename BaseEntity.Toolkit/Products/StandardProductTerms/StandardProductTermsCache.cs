//
//   2015-2017. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  #region Config

  /// <summary>
  ///   Configuration settings for <see cref="IStandardProductTerms">Standard Product Terms</see>
  /// </summary>
  /// <exclude />
  [Serializable]
  public class StandardProductTermsConfig
  {
    /// <exclude />
    [ToolkitConfig("Name of Xml file containing defined Standard Product terms")]
    public readonly string DefinitionsFilename = "Data/StandardProductTerms.XML";
  }

  #endregion Config

  /// <summary>
  ///   Cache for defined <see cref="IStandardProductTerms">Standard Product Terms</see>
  /// </summary>
  /// <remarks>
  ///   <para>By default, standard product terms are defined in an XML file specified by the toolkit configuration.
  ///   An example of the configuration file entry is:</para>
  ///   <code language="XML">
  ///    &lt;!-- Name of XML file containing RateIndex terms --/&gt;
  ///    &lt;StandardProductTerms DefinitionsFilename="Data/StandardProducts.XML" /&gt;
  ///   </code>
  ///   <para>If no file is specified or the file is not found, pre-defined standard products are specified in <see cref="StandardProductTermsDefaults"/>.</para>
  ///   <para>Examples of standard product terms and usage are <see cref="SwapTerms"/>, <see cref="FxFutureTerms"/>, and <see cref="CdsTerms"/>.</para>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  public sealed class StandardProductTermsCache : StandardTermsCache<IStandardProductTerms>
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public StandardProductTermsCache()
      : base(DefaultInitialise)
    { }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Find product terms matching the specified key and type
    /// </summary>
    /// <remarks>
    ///   <para>Finds the terms matching the specified key. Throws exception if not found.</para>
    /// </remarks>
    /// <param name="key">Key to search for</param>
    /// <exception cref="ArgumentException">Specified key not found</exception>
    /// <returns>Product terms found or throws exception</returns>
    public IStandardProductTerms GetValue(string key)
    {
      return GetValue<IStandardProductTerms>(key);
    }

    /// <summary>
    /// Load standard product terms based on specified file
    /// </summary>
    /// <remarks>
    ///   <para>Useful for overriding the initialisation process for testing.</para>
    /// </remarks>
    /// <param name="path">File path</param>
    public void LoadFromPath(string path)
    {
      StandardTermsXmlSerializer.LoadFromFile(this, path);
    }

    /// <summary>
    ///  Save current standard product terms to a XML file
    /// </summary>
    /// <param name="xmlFile">Name of XML file to save to</param>
    public void SaveXmlTerms(string xmlFile)
    {
      var values = Terms.Values.OrderBy(t => t.GetType().FullName).ThenBy(t => t.Key);
      StandardTermsXmlSerializer.SaveToFile(xmlFile, values);
    }

    #endregion

    #region Local Methods

    /// <summary>
    ///  Default initialisation method calls xml reader
    /// </summary>
    private static void DefaultInitialise(StandardTermsCache<IStandardProductTerms> cache)
    {
      if (!StandardTermsXmlSerializer.LoadFromFile(cache, ToolkitConfigurator.Settings.ReferenceRate.DefinitionsFilename))
        // Load from file failed so use in-memory defaults
        StandardProductTermsDefaults.Initialise(cache);
      return;
    }

    #endregion Local Methods
  }
}
