Imports Microsoft.VisualStudio
Imports Microsoft.VisualStudio.Shell
Imports Microsoft.VisualStudio.Shell.Interop
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Threading


<PackageRegistration(UseManagedResourcesOnly:=True, AllowsBackgroundLoading:=True),
InstalledProductRegistration("#110", "#112", "1.0", IconResourceID:=400),
ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad),
ProvideMenuResource("Menus.ctmenu", 1),
Guid(GuidList.guidDisableNoSourceAvailableTab3PkgString)>
Public NotInheritable Class DisableNoSourceAvailableTab
    Inherits AsyncPackage


    Protected Overrides Async Function InitializeAsync(cancellationToken As CancellationToken, progress As IProgress(Of ServiceProgressData)) As Tasks.Task
        Dim shell As IVsShell

        ' Switch to the UI thread so we can consume some UI services.
        Await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken)

        shell = TryCast(Await GetServiceAsync(GetType(IVsShell)), IVsShell)

        If shell IsNot Nothing Then
            RemoveNoSourceToolWindow(shell)
        End If
    End Function


    Private Shared Sub RemoveNoSourceToolWindow(shell As IVsShell)
        Try
            Dim package As IVsPackage

            package = GetRazorPackage(shell)

            If package IsNot Nothing Then
                Dim assemblyTypes() As Type
                Dim adapterType As Type
                Dim debuggerEventsType As Type

                assemblyTypes = package.GetType().Assembly.GetTypes()
                adapterType = assemblyTypes.FirstOrDefault(Function(x) x.Name.Equals("NoSourceToolWindowAdapter"))

                ' If the RazorPackage is loaded before a solution exists, the `NoSourceToolWindowAdapter` 
                ' class will add an event handler for the `RazorPackage.SolutionOpened` event. If we remove
                ' the handler for that event, then the debugger event handlers will never be added
                ' and the tool window will never be shown. Try to remove that event handler now.
                If adapterType IsNot Nothing Then
                    RemoveSolutionOpenedHandler(package, adapterType)
                End If

                ' If the RazorPackage is loaded when a solution already exists, the debugger 
                ' event handlers are added straight away. If we remove those event handlers, 
                ' then the adapter won't know when the debugger is paused and won't show the 
                ' tool window. These event handlers are also added by the `SolutionOpened` event 
                ' handler. If a solution is already open, but was opened after the RazorPackage 
                ' was loaded, then both the `SolutionOpened` event handler and the debugger 
                ' event handlers will have been added, which is why we always try to remove 
                ' these handlers, even if we removed the `SolutionOpened` event handler above.
                debuggerEventsType = assemblyTypes.FirstOrDefault(Function(x) x.Name.Equals("DebuggerEvents"))

                If debuggerEventsType IsNot Nothing Then
                    RemoveDebuggerEventHandler(debuggerEventsType, "DebuggerEvent", adapterType)
                    RemoveDebuggerEventHandler(debuggerEventsType, "ModeChanged", adapterType)
                End If
            End If

        Catch e As Exception
            Throw
        End Try
    End Sub


    Private Shared Function GetRazorPackage(shell As IVsShell) As IVsPackage
        Dim guid = New Guid("BEB01DDF-9D2B-435B-A9E7-76557E2B6B52")
        Dim package As IVsPackage = Nothing

        shell.IsPackageLoaded(guid, package)

        If package Is Nothing Then
            shell.LoadPackage(guid, package)
        End If

        Return package
    End Function


    Private Shared Function GetNoSourceToolWindowAdapter(packageType As Type) As Object
        Dim adapterType As Type

        adapterType = packageType.Assembly.GetTypes().FirstOrDefault(Function(x) x.Name.Equals("NoSourceToolWindowAdapter"))

        If adapterType IsNot Nothing Then
            Return adapterType.GetProperty("Instance", BindingFlags.Public Or BindingFlags.Static Or BindingFlags.GetProperty)
        End If

        Return Nothing
    End Function


    Private Shared Sub RemoveSolutionOpenedHandler(package As IVsPackage, targetType As Type)
        Dim packageType As Type
        Dim eventInfo As EventInfo

        packageType = package.GetType()
        eventInfo = packageType.GetEvent("SolutionOpened")

        If eventInfo IsNot Nothing Then
            Dim field As FieldInfo

            ' Find the backing field so that we can get the handlers
            ' and find the handler that comes from the target type.
            field = packageType.GetField(eventInfo.Name, BindingFlags.NonPublic Or BindingFlags.Static)

            If field IsNot Nothing Then
                Dim eventValue As [Delegate]

                eventValue = TryCast(field.GetValue(package), [Delegate])

                If eventValue IsNot Nothing Then
                    Dim handler As [Delegate]

                    handler = eventValue.GetInvocationList().FirstOrDefault(Function(x) x.Target?.GetType() = targetType)

                    If handler IsNot Nothing Then
                        eventInfo.RemoveEventHandler(package, handler)
                    End If
                End If
            End If
        End If
    End Sub


    Private Shared Sub RemoveDebuggerEventHandler(debuggerEventType As Type, eventName As String, targetType As Type)
        Dim eventInfo As EventInfo

        eventInfo = debuggerEventType.GetEvent(eventName)

        If eventInfo IsNot Nothing Then
            Dim field As FieldInfo

            ' The event handlers are stored in a "weak event" collection. Look for a backing field
            ' that is a `WeakEventDelegateCollection` for the type of the event handler.
            field = (
                From f In debuggerEventType.GetFields(BindingFlags.NonPublic Or BindingFlags.Static)
                Where f.FieldType.IsGenericType
                Where f.FieldType.Name = "WeakEventDelegateCollection`1"
                Where f.FieldType.GetGenericArguments()(0) = eventInfo.EventHandlerType
            ).FirstOrDefault()

            If field IsNot Nothing Then
                Dim collection As Object
                Dim collectionType As Type
                Dim list As Object
                Dim handler As [Delegate]

                collection = field.GetValue(Nothing)
                collectionType = collection.GetType()

                ' The collection has a private `ActiveList` property that gets 
                ' all handler delegates that are still alive. Get that list.
                list = collectionType.InvokeMember(
                    "ActiveList",
                    BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.GetProperty,
                    Nothing,
                    collection,
                    New Object() {}
                )

                ' Find the handler for the target type and remove 
                ' it by calling the `Remove` method on the collection.
                handler = DirectCast(list, IEnumerable).OfType(Of [Delegate]).FirstOrDefault(Function(x) x.Target?.GetType() = targetType)

                If handler IsNot Nothing Then
                    collectionType.InvokeMember(
                        "Remove",
                        BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.InvokeMethod,
                        Nothing,
                        collection,
                        New Object() {handler}
                    )
                End If
            End If
        End If
    End Sub

End Class
