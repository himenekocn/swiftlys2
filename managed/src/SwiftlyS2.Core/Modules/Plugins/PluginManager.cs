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
            EnableRaisingEvents = true
        };
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
                    ReloadPluginById(pluginId, true);
                }
            }
            catch (Exception ex)
            {
                if (!GlobalExceptionHandler.Handle(ex))
                {
                    return;
                }
                logger.LogError(ex, "Failed to handle plugin change");
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

    public IReadOnlyList<PluginContext> GetPlugins() => plugins.AsReadOnly();

    public PluginContext? LoadPlugin( string dir, bool hotReload, bool silent = false )
    {
        PluginContext? FailWithError( PluginContext context, string message )
        {
            if (!silent)
            {
                logger.LogWarning("{Message}", message);
            }
            context.Status = PluginStatus.Error;
            return null;
        }

        var context = new PluginContext { PluginDirectory = dir, Status = PluginStatus.Loading };
        plugins.Add(context);

        var entrypointDll = Path.Combine(dir, Path.GetFileName(dir) + ".dll");
        if (!File.Exists(entrypointDll))
        {
            return FailWithError(context, $"Failed to find plugin entrypoint DLL: {Path.Combine(dir, Path.GetFileName(dir))}.dll");
        }

        var currentContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
        var loader = PluginLoader.CreateFromAssemblyFile(
            entrypointDll,
            [typeof(BasePlugin), .. sharedTypes],
            config =>
            {
                config.IsUnloadable = config.LoadInMemory = true;
                if (currentContext != null)
                {
                    (config.DefaultContext, config.PreferSharedTypes) = (currentContext, true);
                }
            }
        );

        var pluginType = loader.LoadDefaultAssembly()
            .GetTypes()
            .FirstOrDefault(t => t.IsSubclassOf(typeof(BasePlugin)));
        if (pluginType == null)
        {
            return FailWithError(context, $"Failed to find plugin type: {Path.Combine(dir, Path.GetFileName(dir))}.dll");
        }

        var metadata = pluginType.GetCustomAttribute<PluginMetadata>();
        if (metadata == null)
        {
            return FailWithError(context, $"Failed to find plugin metadata: {Path.Combine(dir, Path.GetFileName(dir))}.dll");
        }

        context.Metadata = metadata;
        dataDirectoryService.EnsurePluginDataDirectory(metadata.Id);

        var pluginDir = Path.GetDirectoryName(entrypointDll)!;
        var dataDir = dataDirectoryService.GetPluginDataDirectory(metadata.Id);
        var core = new SwiftlyCore(metadata.Id, pluginDir, metadata, pluginType, rootProvider, dataDir);

        core.InitializeType(pluginType);
        var plugin = (BasePlugin)Activator.CreateInstance(pluginType, [core])!;
        core.InitializeObject(plugin);

        try
        {
            plugin.Load(hotReload);
            context.Status = PluginStatus.Loaded;
            context.Core = core;
            context.Plugin = plugin;
            context.Loader = loader;
            return context;
        }
        catch (Exception e)
        {
            _ = GlobalExceptionHandler.Handle(e);

            try
            {
                plugin.Unload();
                loader?.Dispose();
                core?.Dispose();
            }
            catch { }

            return FailWithError(context, $"Failed to load plugin: {Path.Combine(dir, Path.GetFileName(dir))}.dll");
        }
    }

    public bool UnloadPluginById( string id, bool silent = false )
    {
        var context = plugins
            .Where(p => p.Status != PluginStatus.Unloaded)
            .FirstOrDefault(p => p.Metadata?.Id == id);

        try
        {
            context?.Dispose();
            context?.Loader?.Dispose();
            context?.Core?.Dispose();
            context!.Status = PluginStatus.Unloaded;
            return true;
        }
        catch
        {
            if (!silent)
            {
                logger.LogWarning("Failed to unload plugin by id: {Id}", id);
            }
            if (context != null)
            {
                context.Status = PluginStatus.Indeterminate;
            }
            return false;
        }
        finally
        {
            RebuildSharedServices();
        }
    }

    public bool LoadPluginById( string id, bool silent = false )
    {
        var context = plugins
            .Where(p => p.Status != PluginStatus.Loading && p.Status != PluginStatus.Loaded)
            .FirstOrDefault(p => p.Metadata?.Id == id);

        try
        {
            if (plugins.Remove(context!))
            {
                _ = LoadPlugin(context!.PluginDirectory!, true, silent);
                return true;
            }
            else
            {
                throw new ArgumentException(string.Empty, string.Empty);
            }
        }
        catch
        {
            if (!silent)
            {
                logger.LogWarning("Failed to load plugin by id: {Id}", id);
            }
            if (context != null)
            {
                context.Status = PluginStatus.Indeterminate;
            }
            return false;
        }
        finally
        {
            RebuildSharedServices();
        }
    }

    public void ReloadPluginById( string id, bool silent = false )
    {
        _ = UnloadPluginById(id, silent);

        if (!LoadPluginById(id, silent))
        {
            logger.LogWarning("Failed to reload plugin by id: {Id}", id);
        }
        else
        {
            logger.LogInformation("Reloaded plugin by id: {Id}", id);
        }
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
            logger.LogInformation("{Graph}\n", resolver.GetDependencyGraphVisualization());
            var loadOrder = resolver.GetLoadOrder();
            // logger.LogInformation("Loading {Count} export assemblies in dependency order", loadOrder.Count);

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

            logger.LogInformation("Loaded {Count} shared types", sharedTypes.Count);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Circular dependency"))
        {
            logger.LogError(ex, "Circular dependency detected in plugin exports, loading manually");
            PopulateSharedManually(rootDirService.GetPluginsRoot());
        }
        catch (Exception ex)
        {
            if (!GlobalExceptionHandler.Handle(ex))
            {
                return;
            }
            logger.LogError(ex, "Failed to load exports");
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
                        logger.LogInformation("Loaded plugin: {Id}", context.Metadata!.Id);
                    }
                }
                catch (Exception e)
                {
                    if (!GlobalExceptionHandler.Handle(e))
                    {
                        continue;
                    }
                    logger.LogWarning(e, "Failed to load plugin: {Path}", pluginDir);
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
}