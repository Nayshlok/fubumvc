using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Bottles;
using FubuCore;
using FubuCore.Util;
using FubuMVC.Core.Caching;
using FubuMVC.Core.Http;
using FubuMVC.Core.Registration;
using FubuMVC.Core.Registration.Conventions;
using FubuMVC.Core.Registration.Services;
using FubuMVC.Core.Runtime.Files;
using FubuMVC.Core.Security;
using FubuCore.Reflection;

namespace FubuMVC.Core
{
    /// <summary>
    ///   Orders and governs the construction of a BehaviorGraph
    /// </summary>
    public class ConfigurationGraph
    {
        private readonly Cache<string, IList<IConfigurationAction>> _configurations
            = new Cache<string, IList<IConfigurationAction>>(x => new List<IConfigurationAction>());

        private readonly List<RegistryImport> _imports = new List<RegistryImport>();
        private readonly FubuRegistry _registry;
        private readonly TypePool _types = new TypePool(FindTheCallingAssembly());

        public ConfigurationGraph(FubuRegistry registry)
        {
            _registry = registry;

            // TODO-- track more
            new DiscoveryActionsConfigurationPack().WriteTo(this);
        }

        public TypePool Types
        {
            get { return _types; }
        }

        public void AddConfiguration(IConfigurationAction action, string defaultType = null)
        {
            string type = DetermineConfigurationType(action) ?? defaultType;
            if (type == null)
            {
                throw new ArgumentOutOfRangeException(
                    "No Type specified and unable to determine what the configuration type for " +
                    action.GetType());
            }

            _configurations[type].FillAction(action);
        }

        private bool _sealed;

        // TODO -- make this *seal* itself so that you can't do it twice.
        public BehaviorGraph Build()
        {
            if (_sealed)
            {
                throw new InvalidOperationException("A ConfigurationGraph can only \"Build()\" once");
            }

            new DefaultConfigurationPack().WriteTo(this);

            var graph = new BehaviorGraph {Types = Types};



            AllServiceRegistrations().Each(services => {
                graph.Log.RunAction(_registry, services);

                // TODO -- temporary.  I think this will need to get better so that you can trace the source all the way through
                graph.Log.EventsOfType<ServiceEvent>().Where(x => x.RegistrationSource == null).Each(
                    x => x.RegistrationSource = services.GetType().Name);
            });

            AllConfigurationActions().Each(x => { graph.Log.RunAction(_registry, x); });

            graph.Services.AddService(this);

            _sealed = true;

            return graph;
        }

        public BehaviorGraph BuildForImport(BehaviorGraph parent)
        {
            IEnumerable<IConfigurationAction> lightweightActions = _configurations[ConfigurationType.Settings]
                .Union(_configurations[ConfigurationType.Discovery])
                .Union(_imports)
                .Union(_configurations[ConfigurationType.Explicit])
                .Union(_configurations[ConfigurationType.Policy])
                .Union(_configurations[ConfigurationType.Reordering]);

            BehaviorGraph graph = BehaviorGraph.ForChild(parent);

            lightweightActions.Each(x => graph.Log.RunAction(_registry, x));

            return graph;
        }

        private IEnumerable<IConfigurationAction> allChildrenImports()
        {
            foreach (RegistryImport import in _imports)
            {
                foreach (RegistryImport action in import.Registry.Configuration._imports)
                {
                    yield return action;

                    foreach (
                        IConfigurationAction descendentAction in
                            _imports.SelectMany(x => x.Registry.Configuration.allChildrenImports()))
                    {
                        yield return descendentAction;
                    }
                }
            }
        }

        public IEnumerable<IConfigurationAction> UniqueImports()
        {
            List<IConfigurationAction> children = allChildrenImports().ToList();

            return _imports.Where(x => !children.Contains(x));
        }

        public IEnumerable<IConfigurationAction> AllConfigurationActions()
        {
            return _configurations[ConfigurationType.Settings]
                .Union(_configurations[ConfigurationType.Discovery])
                .Union(UniqueImports())
                .Union(_configurations[ConfigurationType.Explicit])
                .Union(_configurations[ConfigurationType.Policy])
                .Union(_configurations[ConfigurationType.Navigation])
                .Union(_configurations[ConfigurationType.ByNavigation])
                .Union(_configurations[ConfigurationType.Attributes])
                .Union(_configurations[ConfigurationType.ModifyRoutes])
                .Union(_configurations[ConfigurationType.InjectNodes])
                .Union(_configurations[ConfigurationType.Conneg])
                .Union(_configurations[ConfigurationType.Attachment])

                .Union(_configurations[ConfigurationType.Reordering])
                .Union(_configurations[ConfigurationType.Instrumentation]);
        }

        public IEnumerable<IConfigurationAction> ConfigurationsByType(string configurationType)
        {
            return _configurations[configurationType];
        }

        public IEnumerable<IConfigurationAction> AllServiceRegistrations()
        {
            return serviceRegistrations()
                .Union(systemServices());
        }


        private static IEnumerable<ServiceRegistry> systemServices()
        {
            yield return new ModelBindingServicesRegistry();
            yield return new SecurityServicesRegistry();
            yield return new HttpStandInServiceRegistry();
            yield return new CoreServiceRegistry();
            yield return new CachingServiceRegistry();
        }

        private IEnumerable<IConfigurationAction> serviceRegistrations()
        {
            foreach (RegistryImport import in _imports)
            {
                foreach (IConfigurationAction action in import.Registry.Configuration.serviceRegistrations())
                {
                    yield return action;
                }
            }

            foreach (IConfigurationAction action in _configurations[ConfigurationType.Services])
            {
                yield return action;
            }

            yield return new FileRegistration();
        }


        public void AddImport(RegistryImport import)
        {
            if (HasImported(import.Registry)) return;

            _imports.Add(import);
        }

        public bool HasImported(FubuRegistry registry)
        {
            if (_imports.Any(x => x.Registry.GetType() == registry.GetType()))
            {
                return true;
            }

            if (_imports.Any(x => x.Registry.Configuration.HasImported(registry)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///   Finds the currently executing assembly.
        /// </summary>
        /// <returns></returns>
        public static Assembly FindTheCallingAssembly()
        {
            var trace = new StackTrace(false);

            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            Assembly fubuCore = typeof (ITypeResolver).Assembly;
            Assembly bottles = typeof (IPackageLoader).Assembly;

            Assembly callingAssembly = null;
            for (int i = 0; i < trace.FrameCount; i++)
            {
                StackFrame frame = trace.GetFrame(i);
                Assembly assembly = frame.GetMethod().DeclaringType.Assembly;
                if (assembly != thisAssembly && assembly != fubuCore && assembly != bottles)
                {
                    callingAssembly = assembly;
                    break;
                }
            }
            return callingAssembly;
        }

        public static string DetermineConfigurationType(IConfigurationAction action)
        {
            if (action is ReorderBehaviorsPolicy) return ConfigurationType.Reordering;
            if (action is ServiceRegistry) return ConfigurationType.Services;

            if (action.GetType().HasAttribute<ConfigurationTypeAttribute>())
            {
                return action.GetType().GetAttribute<ConfigurationTypeAttribute>().Type;
            }

            return null;
        }

        #region Nested type: FileRegistration

        internal class FileRegistration : IConfigurationAction
        {
            #region IConfigurationAction Members

            public void Configure(BehaviorGraph graph)
            {
                graph.Services.Clear(typeof (IFubuApplicationFiles));
                graph.Services.AddService(graph.Files);
            }

            #endregion
        }

        #endregion
    }
}