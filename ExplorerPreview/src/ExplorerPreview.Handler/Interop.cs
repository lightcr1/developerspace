using System;
using System.Runtime.InteropServices;
using ExplorerPreview.Native;

namespace ExplorerPreview.Handler;

/// <summary>
/// Handgeschriebene COM-Interop-Deklarationen für die Windows-Shell-Interfaces,
/// die ein Preview-Handler implementieren muss. Diese Interfaces sind Teil von
/// shobjidl_core.h im Windows SDK und existieren nicht als .NET-Typen - deshalb
/// hier per [ComImport] nachgebildet. GUIDs sind feste, von Microsoft dokumentierte
/// Interface-IDs (nicht frei wählbar).
/// </summary>

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
public struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public POINT pt;
}

[ComImport]
[Guid("00000114-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleWindow
{
    void GetWindow(out IntPtr phwnd);
    void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
}

/// <summary>Kern-Interface eines Preview-Handlers - CLSID/IID von Windows fest vorgegeben.</summary>
[ComImport]
[Guid("8895b1c6-b41f-4c1c-a562-0d564250836f")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPreviewHandler
{
    void SetWindow(IntPtr hwnd, ref NativeMethods.RECT rect);
    void SetRect(ref NativeMethods.RECT rect);
    void DoPreview();
    void Unload();
    void SetFocus();
    void QueryFocus(out IntPtr phwnd);
    [PreserveSig]
    int TranslateAccelerator(ref MSG pmsg);
}

[ComImport]
[Guid("fc4801a3-2ba9-11cf-a229-00aa003d7352")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IObjectWithSite
{
    void SetSite([MarshalAs(UnmanagedType.IUnknown)] object? pUnkSite);
    void GetSite(ref Guid riid, out IntPtr ppvSite);
}

/// <summary>Übergibt uns den zu öffnenden Dateipfad - der Weg, den Explorer standardmäßig nutzt.</summary>
[ComImport]
[Guid("b7d14566-0509-4cce-a71f-0a554233bd9b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IInitializeWithFile
{
    void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
}
