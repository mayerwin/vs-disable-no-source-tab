Imports Microsoft.VisualBasic
Imports System
Imports System.Diagnostics
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.ComponentModel.Design
Imports Microsoft.Win32
Imports Microsoft.VisualStudio
Imports Microsoft.VisualStudio.Shell.Interop
Imports Microsoft.VisualStudio.OLE.Interop
Imports Microsoft.VisualStudio.Shell
Imports System.Reflection

''' <summary>
''' This is the class that implements the package exposed by this assembly.
'''
''' The minimum requirement for a class to be considered a valid package for Visual Studio
''' is to implement the IVsPackage interface and register itself with the shell.
''' This package uses the helper classes defined inside the Managed Package Framework (MPF)
''' to do it: it derives from the Package class that provides the implementation of the 
''' IVsPackage interface and uses the registration attributes defined in the framework to 
''' register itself and its components with the shell.
''' </summary>
' The PackageRegistration attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class
' is a package.
'
' The InstalledProductRegistration attribute is used to register the information needed to show this package
' in the Help/About dialog of Visual Studio.
'.Debugging_string
<PackageRegistration(UseManagedResourcesOnly:=True), _
InstalledProductRegistration("#110", "#112", "1.0", IconResourceID:=400), _
ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string), _
ProvideMenuResource("Menus.ctmenu", 1), _
Guid(GuidList.guidDisableNoSourceAvailableTab3PkgString)> _
Public NotInheritable Class DisableNoSourceAvailableTab
    Inherits Package

    ''' <summary>
    ''' Default constructor of the package.
    ''' Inside this method you can place any initialization code that does not require 
    ''' any Visual Studio service because at this point the package object is created but 
    ''' not sited yet inside Visual Studio environment. The place to do all the other 
    ''' initialization is the Initialize method.
    ''' </summary>
    Public Sub New()
        Trace.WriteLine(String.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", Me.GetType().Name))
    End Sub

    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    ' Overriden Package Implementation
#Region "Package Members"

    ''' <summary>
    ''' Initialization of the package; this method is called right after the package is sited, so this is the place
    ''' where you can put all the initilaization code that rely on services provided by VisualStudio.
    ''' </summary>
    Protected Overrides Sub Initialize()
        Trace.WriteLine(String.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", Me.GetType().Name))
        MyBase.Initialize()
        DoInitialize()
    End Sub
#End Region

    Private dte As EnvDTE.DTE

    Protected Sub DoInitialize()
        Me.dte = DirectCast(GetGlobalService(GetType(EnvDTE.DTE)), EnvDTE.DTE)
        RemoveNoSourceToolWindow()
    End Sub

    Private Sub RemoveNoSourceToolWindow()
        Dim guid = New Guid("BEB01DDF-9D2B-435B-A9E7-76557E2B6B52")
        Try
            ' Remove the no soure tool window
            ' Get the Razor package
            Dim package As IVsPackage = Nothing
            Dim shell As IVsShell = TryCast(Me.GetService(GetType(IVsShell)), IVsShell)

            If shell IsNot Nothing Then
                shell.IsPackageLoaded(guid, package)
            End If
            If package Is Nothing Then
                shell.LoadPackage(guid, package)
            End If

            If package IsNot Nothing Then
                ' Get the solution opened event handler and remove the NoSourceToolWindowAdapter delegate from it.
                Dim packageType = package.[GetType]()
                Dim eventInfo = packageType.GetEvent("SolutionOpened")
                Dim fieldInfo = packageType.GetField(eventInfo.Name, AllBindings)
                If fieldInfo IsNot Nothing Then
                    Dim eventValue = TryCast(fieldInfo.GetValue(package), [Delegate])
                    If eventValue IsNot Nothing Then
                        Dim list = eventValue.GetInvocationList()
                        For Each eventDelegate In list
                            If eventDelegate.Target Is Nothing Then
                                Continue For
                            End If
                            Dim targetType = eventDelegate.Target.[GetType]()
                            If targetType.Name = "NoSourceToolWindowAdapter" Then
                                eventInfo.RemoveEventHandler(package, eventDelegate)
                            End If
                        Next
                    End If
                End If

            End If
        Catch e As Exception
            Throw e
        End Try
    End Sub

    Private Shared ReadOnly Property AllBindings() As BindingFlags
        Get
            Return BindingFlags.IgnoreCase Or BindingFlags.[Public] Or BindingFlags.NonPublic Or BindingFlags.Instance Or BindingFlags.[Static]
        End Get
    End Property

End Class
