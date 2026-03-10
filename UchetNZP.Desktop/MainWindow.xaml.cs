using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace UchetNZP.Desktop;

public partial class MainWindow : Window
{
    private readonly Uri _homeUri;
    private readonly bool _autoStartBackend;
    private readonly BackendHost _backendHost;
    private readonly CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();

        _homeUri = ResolveHomeUri();
        _autoStartBackend = ShouldAutoStartBackend(_homeUri);
        _backendHost = new BackendHost(_homeUri);

        AddressBox.Text = _homeUri.ToString();
        Loaded += OnLoadedAsync;
        Closing += OnClosingAsync;

        UpdateNavigationButtons();
    }

    private static Uri ResolveHomeUri()
    {
        var configured = Environment.GetEnvironmentVariable("UCHETNZP_DESKTOP_URL");
        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out var fromEnv))
        {
            return fromEnv;
        }

        return new Uri("http://192.168.1.200:8008/");
    }

    private static bool ShouldAutoStartBackend(Uri homeUri)
    {
        var configured = Environment.GetEnvironmentVariable("UCHETNZP_DESKTOP_AUTOSTART_BACKEND");
        if (bool.TryParse(configured, out var explicitValue))
        {
            return explicitValue;
        }

        return homeUri.IsLoopback;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Инициализация браузера...";
        await Browser.EnsureCoreWebView2Async();

        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
        Browser.CoreWebView2.NavigationCompleted += BrowserOnNavigationCompleted;
        Browser.CoreWebView2.HistoryChanged += (_, _) => Dispatcher.Invoke(UpdateNavigationButtons);

        if (_autoStartBackend)
        {
            _ = TryStartBackendAsync();
        }

        Navigate(_homeUri.ToString());
    }

    private async Task TryStartBackendAsync()
    {
        try
        {
            StatusText.Text = "Запуск backend...";
            await _backendHost.StartAsync(_cts.Token);
            StatusText.Text = "Backend запущен.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Backend не запущен. Работа через внешний URL.";
            MessageBox.Show(
                $"Автозапуск backend не удался: {ex.Message}\n\nОкно продолжит работу, можно открыть внешний адрес вручную.",
                "Предупреждение",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void OnClosingAsync(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _cts.Cancel();
        await _backendHost.DisposeAsync();
    }

    private void Navigate(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            if (!Uri.TryCreate($"http://{address}", UriKind.Absolute, out uri))
            {
                return;
            }
        }

        Browser.Source = uri;
        AddressBox.Text = uri.ToString();
        StatusText.Text = $"Переход: {uri}";
    }

    private void BrowserOnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        AddressBox.Text = Browser.Source?.ToString() ?? string.Empty;
        StatusText.Text = e.IsSuccess ? "Готово" : $"Ошибка навигации: {e.WebErrorStatus}";
        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = Browser.CanGoBack;
        ForwardButton.IsEnabled = Browser.CanGoForward;
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack)
        {
            Browser.GoBack();
        }
    }

    private void ForwardButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoForward)
        {
            Browser.GoForward();
        }
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        Browser.Reload();
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        Browser.Stop();
    }

    private void HomeButton_OnClick(object sender, RoutedEventArgs e)
    {
        Navigate(_homeUri.ToString());
    }

    private void GoButton_OnClick(object sender, RoutedEventArgs e)
    {
        Navigate(AddressBox.Text);
    }

    private void AddressBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        Navigate(AddressBox.Text);
        e.Handled = true;
    }
}
