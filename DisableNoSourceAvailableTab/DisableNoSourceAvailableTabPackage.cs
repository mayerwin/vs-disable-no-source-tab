using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace ErwinMayerLabs.DisableNoSourceAvailableTab {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "3.0", IconResourceID = 400)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidDisableNoSourceAvailableTabPkgString)]
    public sealed class DisableNoSourceAvailableTabPackage : AsyncPackage {
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (await this.GetServiceAsync(typeof(IVsShell)) is IVsShell shell) {
                RemoveNoSourceToolWindow(shell);
            }
        }

        #endregion

        private static void RemoveNoSourceToolWindow(IVsShell shell) {
            try {
                var package = GetRazorPackage(shell);

                if (package != null) {
                    var assemblyTypes = package.GetType().Assembly.GetTypes();
                    var adapterType = assemblyTypes.FirstOrDefault(x => x.Name.Equals("NoSourceToolWindowAdapter"));

                    // If the RazorPackage is loaded before a solution exists, the `NoSourceToolWindowAdapter` 
                    // class will add an event handler for the `RazorPackage.SolutionOpened` event. If we remove
                    // the handler for that event, then the debugger event handlers will never be added
                    // and the tool window will never be shown. Try to remove that event handler now.
                    if (adapterType != null) {
                        RemoveSolutionOpenedHandler(package, adapterType);
                    }

                    // If the RazorPackage is loaded when a solution already exists, the debugger 
                    // event handlers are added straight away. If we remove those event handlers, 
                    // then the adapter won't know when the debugger is paused and won't show the 
                    // tool window. These event handlers are also added by the `SolutionOpened` event 
                    // handler. If a solution is already open, but was opened after the RazorPackage 
                    // was loaded, then both the `SolutionOpened` event handler and the debugger 
                    // event handlers will have been added, which is why we always try to remove 
                    // these handlers, even if we removed the `SolutionOpened` event handler above.
                    var debuggerEventsType = assemblyTypes.FirstOrDefault(x => x.Name.Equals("DebuggerEvents"));

                    if (debuggerEventsType != null) {
                        RemoveDebuggerEventHandler(debuggerEventsType, "DebuggerEvent", adapterType);
                        RemoveDebuggerEventHandler(debuggerEventsType, "ModeChanged", adapterType);
                    }
                }
            }
            catch (Exception e) {
                Trace.WriteLine(e.ToString());
            }
        }

        private static IVsPackage GetRazorPackage(IVsShell shell) {
            var guid = new Guid("BEB01DDF-9D2B-435B-A9E7-76557E2B6B52");

            shell.IsPackageLoaded(ref guid, out var package);

            if (package == null) {
                shell.LoadPackage(ref guid, out package);
            }
            return package;
        }


        private static object GetNoSourceToolWindowAdapter(Type packageType) {
            var adapterType = packageType.Assembly.GetTypes().FirstOrDefault(x => x.Name.Equals("NoSourceToolWindowAdapter"));

            if (adapterType != null) {
                return adapterType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty);
            }
            return null;
        }


        private static void RemoveSolutionOpenedHandler(IVsPackage package, Type targetType) {
            var packageType = package.GetType();
            var eventInfo = packageType.GetEvent("SolutionOpened");

            if (eventInfo != null) {
                // Find the backing field so that we can get the handlers
                // and find the handler that comes from the target type.
                var field = packageType.GetField(eventInfo.Name, BindingFlags.NonPublic | BindingFlags.Static);

                if (field != null) {
                    if (field.GetValue(package) is Delegate eventValue) {
                        var handler = eventValue.GetInvocationList().FirstOrDefault(x => x.Target?.GetType() == targetType);

                        if (handler != null) {
                            eventInfo.RemoveEventHandler(package, handler);
                        }
                    }
                }
            }
        }

        private static void RemoveDebuggerEventHandler(Type debuggerEventType, string eventName, Type targetType) {
            var eventInfo = debuggerEventType.GetEvent(eventName);

            if (eventInfo != null) {
                // The event handlers are stored in a "weak event" collection. Look for a backing field
                // that is a `WeakEventDelegateCollection` for the type of the event handler.
                var field = (from f in debuggerEventType.GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                             where f.FieldType.IsGenericType
                             where f.FieldType.Name == "WeakEventDelegateCollection`1"
                             where f.FieldType.GetGenericArguments()[0] == eventInfo.EventHandlerType
                             select f
                    ).FirstOrDefault();

                if (field != null) {
                    var collection = field.GetValue(null);
                    var collectionType = collection.GetType();

                    // The collection has a private `ActiveList` property that gets 
                    // all handler delegates that are still alive. Get that list.
                    var list = collectionType.InvokeMember("ActiveList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty, null, collection, new object[] { }
                    );

                    // Find the handler for the target type and remove 
                    // it by calling the `Remove` method on the collection.
                    var handler = ((IEnumerable)list).OfType<Delegate>().FirstOrDefault(x => x.Target?.GetType() == targetType);

                    if (handler != null) {
                        collectionType.InvokeMember("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null, collection, new object[] { handler });
                    }
                }
            }
        }
    }
}
