
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System;
using EnvDTE;
using EnvDTE80;

public static class Globals {
    public static DTE2 DTE;

    public static void InvokeOnUIThread(Action action) {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        dispatcher?.Invoke(action);
    }

    public static void BeginInvokeOnUIThread(Action action) {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        dispatcher?.BeginInvoke(action);
    }
}