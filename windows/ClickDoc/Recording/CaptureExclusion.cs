using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using ClickDoc.Native;

namespace ClickDoc.Recording;

/// Keeps ClickDoc's own windows, menus and tooltips out of every screenshot
/// (WDA_EXCLUDEFROMCAPTURE, Windows 10 2004+).
static class CaptureExclusion
{
    static readonly HashSet<nint> Applied = [];

    public static void Install()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(OnOpened));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler(OnOpened));
        EventManager.RegisterClassHandler(typeof(MenuItem), MenuItem.SubmenuOpenedEvent, new RoutedEventHandler(OnSubmenuOpened));
        EventManager.RegisterClassHandler(typeof(ComboBox), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnComboLoaded));
    }

    /// Applies to a window this app owns; safe to call repeatedly.
    public static void Apply(nint hwnd)
    {
        if (hwnd != 0 && Applied.Add(hwnd)) Win32.SetWindowDisplayAffinity(hwnd, Win32.WDA_EXCLUDEFROMCAPTURE);
    }

    static void OnLoaded(object sender, RoutedEventArgs e) => Apply(new WindowInteropHelper((Window)sender).Handle);

    static void OnOpened(object sender, RoutedEventArgs e) => ApplyToHost((Visual)sender);

    static void OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        // The submenu lives in the item's popup; its HWND exists once the popup is open.
        if (((MenuItem)sender).Template?.FindName("PART_Popup", (MenuItem)sender) is Popup { Child: Visual child }) ApplyToHost(child);
    }

    static void OnComboLoaded(object sender, RoutedEventArgs e)
    {
        var combo = (ComboBox)sender;
        combo.DropDownOpened += (_, _) =>
        {
            if (combo.Template?.FindName("PART_Popup", combo) is Popup { Child: Visual child }) ApplyToHost(child);
        };
    }

    static void ApplyToHost(Visual visual)
    {
        if (PresentationSource.FromVisual(visual) is HwndSource source) Apply(source.Handle);
    }
}
