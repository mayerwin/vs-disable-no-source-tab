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

<PackageRegistration(UseManagedResourcesOnly:=True), _
InstalledProductRegistration("#110", "#112", "1.0", IconResourceID:=400), _
ProvideAutoLoad(VSConstants.UICONTEXT.Debugging_string), _
ProvideMenuResource("Menus.ctmenu", 1), _
Guid(GuidList.guidDisableNoSourceAvailableTab3PkgString)> _
<ProvideOptionPage(GetType(OptionPageGrid), _
    "Disable No Source Available Tab", "General", 0, 0, True)>
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
    Public BadCaptions As New List(Of String)

    Protected Sub DoInitialize()
        Me.dte = DirectCast(GetGlobalService(GetType(EnvDTE.DTE)), EnvDTE.DTE)
        Dim captions = New String() {"No Source Available", "Es ist keine Quelle verfügbar", "Aucune source disponible", "No hay código fuente disponible"}
        For Each c In captions
            Me.BadCaptions.Add(c)
        Next
        If Not String.IsNullOrWhiteSpace(Me.Settings.ExtraTabTitle) Then
            Me.BadCaptions.Add(Me.Settings.ExtraTabTitle)
        End If
        AddHandler Me.dte.Events.WindowEvents.WindowActivated, AddressOf DTE_WindowActivated
        AddHandler Me.dte.Events.WindowEvents.WindowCreated, AddressOf DTE_WindowCreated
        'AddHandler Me.dte.Events.DocumentEvents.DocumentOpened, AddressOf DTE_DocumentOpened 'not triggered
        'AddHandler Me.dte.Events.DebuggerEvents.OnEnterBreakMode, AddressOf DTE_OnEnterBreakMode
        'AddHandler Me.dte.Events.OutputWindowEvents.PaneAdded, AddressOf DTE_PaneAdded
        'AddHandler Me.dte.Events.WindowEvents.WindowClosing, AddressOf DTE_WindowClosing
        'AddHandler Me.dte.Events.WindowEvents.WindowMoved, AddressOf DTE_WindowMoved
    End Sub

    Private Sub DTE_WindowCreated(ByVal CreatedWindow As EnvDTE.Window)
        If CreatedWindow IsNot Nothing _
            AndAlso (Me.dte.Mode = EnvDTE.vsIDEMode.vsIDEModeDebug) _
            AndAlso BadCaptions.Any(Function(s) CreatedWindow.Caption.Contains(s)) Then 'The guid should be {1820bae5-c385-4492-9de5-e35c9cf17b18}
            Threading.ThreadPool.QueueUserWorkItem(New Threading.WaitCallback(AddressOf CleanTabs), CreatedWindow)
        End If
    End Sub

    Private Sub DTE_WindowActivated(ByVal GotFocusWindow As EnvDTE.Window, ByVal LostFocusWindow As EnvDTE.Window)
        'Trace.WriteLine("Deactivated a window: " & Convert.ToString(LostFocusWindow.Caption))
        'Trace.WriteLine("Activated a new window: " & Convert.ToString(GotFocusWindow.Caption))
        'AndAlso GotFocusWindow.Type = EnvDTE.vsWindowType.vsWindowTypeToolWindow _
        DTE_WindowCreated(CreatedWindow:=GotFocusWindow)
        'LostFocusWindow.Activate()
    End Sub



    Private Sub CleanTabs(ByVal e As Object)
        Try
            Dim Window = TryCast(e, EnvDTE.Window)
            If Window IsNot Nothing Then
                Window.Visible = False
                Window.Close()
            End If
        Catch ex As Exception
            Trace.WriteLine(ex.Message)
        End Try
    End Sub

    'Private Sub DTE_WindowCreated(ByVal Window As EnvDTE.Window)
    '    MsgBox("Added a new window: " & Convert.ToString(Window.Document.Name))
    'End Sub

    'Private Sub DTE_DocumentOpened(ByVal Document As EnvDTE.Document)
    '    Trace.WriteLine("Opened a new document: " & Convert.ToString(Document.Name))
    'End Sub

    'Private Sub DTE_OnEnterBreakMode(ByVal Reason As EnvDTE.dbgEventReason, ByRef ExecutionAction As EnvDTE.dbgExecutionAction)
    '    Trace.WriteLine("DTE_OnEnterBreakMode: " & Convert.ToString(Reason))
    'End Sub

    'Private Sub DTE_PaneAdded(ByVal Pane As EnvDTE.OutputWindowPane)
    '    Trace.WriteLine("DTE_PaneAdded: " & Convert.ToString(Pane.Name))
    'End Sub

    'Private Sub DTE_WindowClosing(ByVal Window As EnvDTE.Window)
    '    Trace.WriteLine("DTE_WindowClosing: " & Convert.ToString(Window.Document.Name))
    'End Sub

    'Private Sub DTE_WindowMoved(ByVal Window As EnvDTE.Window, ByVal top As Integer, ByVal left As Integer, ByVal width As Integer, ByVal height As Integer)
    '    Trace.WriteLine("DTE_WindowMoved: " & Convert.ToString(Window.Caption))
    'End Sub

    Protected ReadOnly Property Settings() As OptionPageGrid
        Get
            Return CType(GetDialogPage(GetType(OptionPageGrid)), OptionPageGrid)
        End Get
    End Property
End Class
