using System.Reflection;
using System.Runtime.Loader;

namespace DirectorySorter.Core;

/// <summary>
/// Loads every DLL in a Plugins folder into its own collectible AssemblyLoadContext,
/// finds concrete ISortPlugin implementations, and instantiates them.
/// Isolated contexts mean a broken plugin DLL can be dropped/replaced without
/// touching the host process's own assemblies, and (in theory) unloaded again.
/// </summary>
public sealed class PluginLoader
{
    private readonly Logger _log;
    private readonly List<PluginAssemblyContext> _contexts = new();

    public PluginLoader(Logger log) => _log = log;

    public IReadOnlyList<ISortPlugin> LoadFrom(string pluginsFolder)
    {
        var plugins = new List<ISortPlugin>();

        if (!Directory.Exists(pluginsFolder))
        {
            _log.Warn($"Plugins folder not found: {pluginsFolder}");
            return plugins;
        }

        foreach (var dllPath in Directory.EnumerateFiles(pluginsFolder, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var context = new PluginAssemblyContext(dllPath);
                _contexts.Add(context);

                var asm = context.LoadFromAssemblyPath(dllPath);
                var pluginTypes = asm.GetTypes()
                    .Where(t => typeof(ISortPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is ISortPlugin instance)
                    {
                        plugins.Add(instance);
                        _log.Info($"Loaded plugin '{instance.DisplayName}' ({instance.Key}) from {Path.GetFileName(dllPath)}");
                    }
                }
            }
            catch (Exception ex) when (ex is BadImageFormatException or ReflectionTypeLoadException or FileLoadException)
            {
                _log.Error($"Failed to load plugin {dllPath}: {ex.Message}");
            }
        }

        return plugins;
    }

    /// <summary>Unloads every plugin context. Full reclamation depends on the GC collecting collectible contexts.</summary>
    public void UnloadAll()
    {
        foreach (var context in _contexts)
            context.Unload();
        _contexts.Clear();
    }

    private sealed class PluginAssemblyContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginAssemblyContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null;
        }
    }
}
