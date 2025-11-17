using System.Reflection;
using System.Runtime.Loader;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Core.Services;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Core.Modules.Plugins;

namespace SwiftlyS2.Core.Plugins;

internal class PluginManager
{
    private readonly IServiceProvider rootProvider;
    private readonly RootDirService rootDirService;
    private readonly DataDirectoryService dataDirectoryService;
    private readonly ILogger logger;

    private readonly InterfaceManager interfaceManager;
    private readonly List<Type> sharedTypes;
    private readonly List<PluginContext> plugins;

    private readonly FileSystemWatcher? fileWatcher;

    public PluginManager(
        IServiceProvider provider,
        ILogger<PluginManager> logger,
        RootDirService rootDirService,
        DataDirectoryService dataDirectoryService
    )
    {
        this.rootProvider = provider;
        this.rootDirService = rootDirService;
        this.dataDirectoryService = dataDirectoryService;
        this.logger = logger;

        this.interfaceManager = new();
        this.sharedTypes = [];
        this.plugins = [];

        this.fileWatcher = new FileSystemWatcher {
            Path = rootDirService.GetPluginsRoot(),
            Filter = "*.dll",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite,
        };

        this.fileWatcher.EnableRaisingEvents = true;
        this.fileWatcher.Changed += ( sender, e ) =>
        {
            try
            {
                if (!NativeServerHelpers.UseAutoHotReload() || e.ChangeType != WatcherChangeTypes.Changed)
                {
                    return;
                }

                var directoryName = Path.GetDirectoryName(e.FullPath) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(directoryName))
                {
                    return;
                }

                var pluginId = plugins
                    .FirstOrDefault(p => Path.GetFileName(p?.PluginDirectory) == Path.GetFileName(directoryName))
                    ?.Metadata?.Id;
                if (!string.IsNullOrWhiteSpace(pluginId))
                {
                    ReloadPlugin(pluginId);
                }
            }
            catch (Exception ex)
            {
                if (!GlobalExceptionHandler.Handle(ex))
                {
                    return;
                }
                logger.LogError(ex, "Error handling plugin change");
            }
        };

        AppDomain.CurrentDomain.AssemblyResolve += ( sender, e ) =>
        {
            var loadingAssemblyName = new AssemblyName(e.Name).Name ?? string.Empty;
            return loadingAssemblyName.Equals("SwiftlyS2.CS2", StringComparison.OrdinalIgnoreCase)
                ? Assembly.GetExecutingAssembly()
                : AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => loadingAssemblyName == a.GetName().Name);
        };

        LoadExports();
        LoadPlugins();
    }

    private void LoadExports()
    {
        void PopulateSharedManually( string startDirectory )
        {
            var pluginDirs = Directory.GetDirectories(startDirectory);

            foreach (var pluginDir in pluginDirs)
            {
                var dirName = Path.GetFileName(pluginDir);
                if (dirName.StartsWith('[') && dirName.EndsWith(']'))
                {
                    PopulateSharedManually(pluginDir);
                    continue;
                }

                var exportsPath = Path.Combine(pluginDir, "resources", "exports");
                if (!Directory.Exists(exportsPath))
                {
                    continue;
                }

                Directory.GetFiles(exportsPath, "*.dll")
                    .ToList()
                    .ForEach(exportFile =>
                    {
                        try
                        {
                            var assembly = Assembly.LoadFrom(exportFile);
                            assembly.GetTypes().ToList().ForEach(sharedTypes.Add);
                        }
                        catch (Exception innerEx)
                        {
                            if (!GlobalExceptionHandler.Handle(innerEx))
                            {
                                return;
                            }
                            logger.LogWarning(innerEx, "Failed to load export assembly: {Path}", exportFile);
                        }
                    });
            }
        }

        try
        {
            var resolver = new DependencyResolver(logger);
            resolver.AnalyzeDependencies(rootDirService.GetPluginsRoot());
            logger.LogInformation("{Graph}", resolver.GetDependencyGraphVisualization());
            var loadOrder = resolver.GetLoadOrder();
            logger.LogInformation("Loading {Count} export assemblies in dependency order.", loadOrder.Count);

            loadOrder.ForEach(exportFile =>
            {
                try
                {
                    var assembly = Assembly.LoadFrom(exportFile);
                    var exports = assembly.GetTypes();
                    logger.LogDebug("Loaded {Count} types from {Path}", exports.Length, Path.GetFileName(exportFile));
                    exports.ToList().ForEach(sharedTypes.Add);
                }
                catch (Exception ex)
                {
                    if (!GlobalExceptionHandler.Handle(ex))
                    {
                        return;
                    }
                    logger.LogWarning(ex, "Failed to load export assembly: {Path}", exportFile);
                }
            });

            logger.LogInformation("Successfully loaded {Count} shared types.", sharedTypes.Count);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Circular dependency"))
        {
            logger.LogError(ex, "Circular dependency detected in plugin exports. Loading exports without dependency resolution.");
            PopulateSharedManually(rootDirService.GetPluginsRoot());
        }
        catch (Exception ex)
        {
            if (!GlobalExceptionHandler.Handle(ex)) return;
            logger.LogError(ex, "Unexpected error during export loading");
        }
    }

    private void LoadPlugins()
    {
        void LoadPluginsFromFolder( string directory )
        {
            var pluginDirs = Directory.GetDirectories(directory);

            foreach (var pluginDir in pluginDirs)
            {
                var dirName = Path.GetFileName(pluginDir);
                if (dirName.StartsWith('[') && dirName.EndsWith(']'))
                {
                    LoadPluginsFromFolder(pluginDir);
                    continue;
                }

                try
                {
                    var context = LoadPlugin(pluginDir, false);
                    if (context?.Status == PluginStatus.Loaded)
                    {
                        logger.LogInformation("Loaded plugin {Id}", context.Metadata!.Id);
                    }
                }
                catch (Exception e)
                {
                    if (!GlobalExceptionHandler.Handle(e))
                    {
                        continue;
                    }
                    logger.LogWarning(e, "Error loading plugin: {Path}", pluginDir);
                }
            }
        }

        LoadPluginsFromFolder(rootDirService.GetPluginsRoot());
        RebuildSharedServices();

        plugins
            .Where(p => p.Status == PluginStatus.Loaded)
            .ToList()
            .ForEach(p => p.Plugin!.OnAllPluginsLoaded());
    }

    public List<PluginContext> GetPlugins()
    {
        return plugins;
    }

    private void RebuildSharedServices()
    {
        interfaceManager.Dispose();

        var loadedPlugins = plugins
            .Where(p => p.Status == PluginStatus.Loaded)
            .ToList();

        loadedPlugins.ForEach(p => p.Plugin?.ConfigureSharedInterface(interfaceManager));
        interfaceManager.Build();

        loadedPlugins.ForEach(p => p.Plugin?.UseSharedInterface(interfaceManager));
        loadedPlugins.ForEach(p => p.Plugin?.OnSharedInterfaceInjected(interfaceManager));
    }

    public PluginContext? LoadPlugin( string dir, bool hotReload )
    {
        PluginContext context = new() {
            PluginDirectory = dir,
            Status = PluginStatus.Loading,
        };
        plugins.Add(context);

        var entrypointDll = Path.Combine(dir, Path.GetFileName(dir) + ".dll");

        if (!File.Exists(entrypointDll))
        {
            logger.LogWarning("Plugin entrypoint DLL not found: {Path}", entrypointDll);
            context.Status = PluginStatus.Error;
            return null;
        }

        var loader = PluginLoader.CreateFromAssemblyFile(
            assemblyFile: entrypointDll,
            sharedTypes: [typeof(BasePlugin), .. sharedTypes],
            config =>
            {
                config.IsUnloadable = true;
                config.LoadInMemory = true;
                var currentContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
                if (currentContext != null)
                {
                    config.DefaultContext = currentContext;
                    config.PreferSharedTypes = true;
                }
            }
        );

        var assembly = loader.LoadDefaultAssembly();
        var pluginType = assembly.GetTypes().FirstOrDefault(t => t.IsSubclassOf(typeof(BasePlugin)));
        if (pluginType == null)
        {
            logger.LogWarning("Plugin type not found: {Path}", entrypointDll);
            context.Status = PluginStatus.Error;
            return null;
        }

        var metadata = pluginType.GetCustomAttribute<PluginMetadata>();
        if (metadata == null)
        {
            logger.LogWarning("Plugin metadata not found: {Path}", entrypointDll);
            context.Status = PluginStatus.Error;
            return null;
        }

        context.Metadata = metadata;
        dataDirectoryService.EnsurePluginDataDirectory(metadata.Id);

        var core = new SwiftlyCore(metadata.Id, Path.GetDirectoryName(entrypointDll)!, metadata, pluginType, rootProvider, dataDirectoryService.GetPluginDataDirectory(metadata.Id));
        core.InitializeType(pluginType);
        var plugin = (BasePlugin)Activator.CreateInstance(pluginType, [core])!;
        core.InitializeObject(plugin);

        try
        {
            plugin.Load(hotReload);
        }
        catch (Exception e)
        {
            if (!GlobalExceptionHandler.Handle(e))
            {
                context.Status = PluginStatus.Error;
                return null;
            }
            logger.LogWarning(e, "Error loading plugin {Path}", entrypointDll);
            try
            {
                plugin.Unload();
                loader?.Dispose();
                core?.Dispose();
            }
            catch (Exception)
            {
            }
            context.Status = PluginStatus.Error;
            return null;
        }

        context.Status = PluginStatus.Loaded;
        context.Core = core;
        context.Plugin = plugin;
        context.Loader = loader;
        return context;
    }

    public bool UnloadPlugin( string id )
    {
        var context = plugins
            .Where(p => p.Status == PluginStatus.Loaded)
            .FirstOrDefault(p => p.Metadata?.Id == id);

        if (context == null)
        {
            logger.LogWarning("Plugin not found or not loaded: {Id}", id);
            return false;
        }

        context.Dispose();
        context.Status = PluginStatus.Unloaded;
        return true;
    }

    public bool LoadPluginById( string id )
    {
        var context = plugins
            .Where(p => p.Status == PluginStatus.Unloaded)
            .FirstOrDefault(p => p.Metadata?.Id == id);

        var result = false;
        if (context != null)
        {
            var directory = context.PluginDirectory!;
            _ = plugins.Remove(context);
            _ = LoadPlugin(directory, true);
            result = true;
        }

        RebuildSharedServices();
        return result;
    }

    public void ReloadPlugin( string id )
    {
        logger.LogInformation("Reloading plugin {Id}", id);

        if (!UnloadPlugin(id))
        {
            logger.LogWarning("Plugin not found or not loaded: {Id}", id);
            return;
        }

        if (!LoadPluginById(id))
        {
            logger.LogWarning("Failed to load plugin {Id}", id);
            return;
        }

        logger.LogInformation("Reloaded plugin {Id}", id);
    }
}