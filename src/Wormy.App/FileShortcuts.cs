using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private void InitializeFileShortcuts()
    {
        foreach (var (key,modifiers,action) in new (VirtualKey,VirtualKeyModifiers,Action)[]
        {
            (VirtualKey.C,VirtualKeyModifiers.Control,()=>CopySelectedPaths(this,new RoutedEventArgs())),
            (VirtualKey.A,VirtualKeyModifiers.Control,()=>SelectAllShown(this,new RoutedEventArgs())),
            (VirtualKey.Delete,VirtualKeyModifiers.None,()=>RecycleSelected(this,new RoutedEventArgs()))
        })
        {
            var shortcut=new KeyboardAccelerator { Key=key,Modifiers=modifiers };
            shortcut.Invoked += (_,args) =>
            {
                if (previewOpen || busy || Shell.XamlRoot is null) return;
                var focus=FocusManager.GetFocusedElement(Shell.XamlRoot) as DependencyObject;
                while (focus is not null && focus != FileGrid && focus != FileList) focus=VisualTreeHelper.GetParent(focus);
                if (focus is null) return;
                args.Handled=true; action();
            };
            Shell.KeyboardAccelerators.Add(shortcut);
        }
    }
}
