// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
//

using System;
using System.Collections.Concurrent;
using System.Linq;
using log4net;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;
#if NETSTANDARD2_0 || NETSTANDARD2_1
using Unity;
#endif

namespace BaseEntity.Configuration
{
  /// <summary>
  /// 
  /// </summary>
  public class DefaultContainerFactory : IUnityContainerFactory
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(DefaultContainerFactory));

    private readonly IUnityContainer _rootContainer;
    private readonly UnityConfigurationSection _unitySection;

    private static readonly ConcurrentDictionary<string, IUnityContainer> Containers = 
      new ConcurrentDictionary<string, IUnityContainer>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultContainerFactory"/> class.
    /// </summary>
    /// <param name="rootContainer">The root container.</param>
    /// <param name="unitySection">The unity section.</param>
    public DefaultContainerFactory(IUnityContainer rootContainer, UnityConfigurationSection unitySection)
    {
      _rootContainer = rootContainer;

      _unitySection = unitySection;

      try
      {
        if (_unitySection != null)
          _unitySection.Configure(rootContainer);
      }
      catch (Exception ex)
      {
        Log.Error("Failed to configure root Unity container", ex);
        throw;
      }
    }

    /// <summary>
    /// Resolves the specified container name.
    /// </summary>
    /// <param name="containerName">Name of the container.</param>
    /// <returns></returns>
    public IUnityContainer Resolve(string containerName = null)
    {
      return containerName == null ? _rootContainer : Containers.GetOrAdd(containerName, CreateContainer);
    }

    private IUnityContainer CreateContainer(string containerName)
    {
      if (_unitySection != null && _unitySection.Containers.Any(c => c.Name == containerName))
      {
        // Get the named container (as configured)
        var container = _rootContainer.CreateChildContainer();
        _unitySection.Configure(container, containerName);
        return container;
      }

      return _rootContainer;
    }

    /// <summary>
    /// Disposes the container.
    /// </summary>
    /// <param name="containerName">Name of the container.</param>
    public void DisposeContainer(string containerName)
    {
      IUnityContainer container;
      if (Containers.TryRemove(containerName, out container))
        container.Dispose();
    }
  }
}