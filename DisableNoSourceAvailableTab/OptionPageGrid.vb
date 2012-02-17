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
Imports System.Threading
Imports System.Text.RegularExpressions
Imports System.ComponentModel

<ClassInterface(ClassInterfaceType.AutoDual)>
<CLSCompliant(False), ComVisible(True)>
Public Class OptionPageGrid
    Inherits DialogPage

    Private _ExtraTabTitle As String = ""
    <Category("General")>
    <DisplayName("Extra title of a tab to automatically close")>
    <Description("Use this option to enter another single tab title (e.g. equivalent to 'No Source Available' in English) that should be automatically closed when they appear. Restart for change to take effect.")>
    Public Property ExtraTabTitle() As String
        Get
            Return Me._ExtraTabTitle
        End Get
        Set(ByVal value As String)
            Me._ExtraTabTitle = value
        End Set
    End Property
End Class
