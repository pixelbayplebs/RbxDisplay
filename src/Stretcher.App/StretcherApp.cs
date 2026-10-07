using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;

namespace Stretcher;

internal sealed class StretcherApp : Application, IXamlMetadataProvider
{
    private MainWindow? window;

    // Code-only WinUI still needs the native controls' metadata for their templates.
    private readonly XamlControlsXamlMetaDataProvider metadata = new();

    public StretcherApp()
    {
        RequestedTheme = ApplicationTheme.Dark;
        UnhandledException += (_, args) =>
        {
            Store.Log(args.Exception.ToString());
            string? error = window?.EmergencyStop();
            NativeWindow.Message(args.Exception.Message + (error == null ? "" : "\nRestoration needs attention: " + error), "Stretcher");
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
        bool migrated = false;
        try
        {
            loaded = ProfileRepository.Load(out migrated);
        }
        catch (Exception ex)
        {
            loaded = new AppSettings();
            startupError = ex.Message;
            Store.Log("Loading settings: " + ex.Message);
        }

        window = new MainWindow(loaded, startupError, migrated);
        window.Activate();
    }
}
