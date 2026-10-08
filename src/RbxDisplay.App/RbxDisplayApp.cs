using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;

namespace RbxDisplay;

internal sealed class RbxDisplayApp : Application, IXamlMetadataProvider
{
    private MainWindow? window;

    // Code-only WinUI still needs the native controls' metadata for their templates.
    private readonly XamlControlsXamlMetaDataProvider metadata = new();

    public RbxDisplayApp()
    {
        RequestedTheme = ApplicationTheme.Dark;
        UnhandledException += (_, args) =>
        {
            Store.Log(args.Exception.ToString());
            string? error = window?.EmergencyStop();
            NativeWindow.Message(args.Exception.Message + (error == null ? "" : "\nRestoration needs attention: " + error), "RbxDisplay");
            args.Handled = true;
        };
    }

    public IXamlType? GetXamlType(Type type)
    {
        return metadata.GetXamlType(type);
    }

    public IXamlType? GetXamlType(string fullName)
    {
        return metadata.GetXamlType(fullName);
    }

    public XmlnsDefinition[] GetXmlnsDefinitions()
    {
        return metadata.GetXmlnsDefinitions();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Resources = Theme.CreateResources();
        AppSettings loaded;
        string? startupError = null;
        try
        {
            loaded = ProfileRepository.Load();
        }
        catch (Exception ex)
        {
            loaded = new AppSettings();
            startupError = ex.Message;
            Store.Log("Loading settings: " + ex.Message);
        }

        window = new MainWindow(loaded, startupError);
        window.Activate();
    }
}
