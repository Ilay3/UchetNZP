using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace UchetNZP.Desktop;

internal sealed class MainForm : Form
{
    private readonly ToolStripButton _backButton = new("Назад");
    private readonly ToolStripButton _forwardButton = new("Вперед");
    private readonly ToolStripButton _refreshButton = new("Обновить");
    private readonly ToolStripButton _homeButton = new("Домой");
    private readonly ToolStripButton _stopButton = new("Стоп");
    private readonly ToolStripTextBox _addressBox = new() { AutoSize = false, Width = 600 };
    private readonly ToolStripButton _goButton = new("Перейти");
    private readonly ToolStripStatusLabel _statusLabel = new("Запуск...");
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };

    private readonly Uri _homeUri = new("http://localhost:5127/");
    private readonly BackendHost _backendHost;
    private readonly CancellationTokenSource _startupCts = new();

    public MainForm()
    {
        Text = "Учет НЗП — Desktop";
        Width = 1440;
        Height = 900;

        _backendHost = new BackendHost(_homeUri);

        var toolStrip = new ToolStrip();
        toolStrip.Items.AddRange([
            _backButton,
            _forwardButton,
            _refreshButton,
            _stopButton,
            _homeButton,
            new ToolStripSeparator(),
            _addressBox,
            _goButton
        ]);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(_webView);
        Controls.Add(statusStrip);
        Controls.Add(toolStrip);

        _backButton.Click += (_, _) => { if (_webView.CanGoBack) _webView.GoBack(); };
        _forwardButton.Click += (_, _) => { if (_webView.CanGoForward) _webView.GoForward(); };
        _refreshButton.Click += (_, _) => _webView.Reload();
        _homeButton.Click += (_, _) => Navigate(_homeUri.ToString());
        _stopButton.Click += (_, _) => _webView.Stop();
        _goButton.Click += (_, _) => Navigate(_addressBox.Text);
        _addressBox.KeyDown += AddressBoxOnKeyDown;

        Load += OnLoadAsync;
        FormClosing += OnFormClosingAsync;

        UpdateNavigationButtons();
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        try
        {
            _statusLabel.Text = "Запуск backend...";
            await _backendHost.StartAsync(_startupCts.Token);

            _statusLabel.Text = "Инициализация WebView2...";
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.NavigationCompleted += WebViewOnNavigationCompleted;
            _webView.CoreWebView2.HistoryChanged += (_, _) => UpdateNavigationButtons();

            Navigate(_homeUri.ToString());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось запустить приложение: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
    {
        _startupCts.Cancel();
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

        _webView.Source = uri;
        _addressBox.Text = uri.ToString();
        _statusLabel.Text = $"Переход: {uri}";
    }

    private void AddressBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        Navigate(_addressBox.Text);
        e.SuppressKeyPress = true;
    }

    private void WebViewOnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _addressBox.Text = _webView.Source?.ToString() ?? string.Empty;
        _statusLabel.Text = e.IsSuccess ? "Готово" : $"Ошибка навигации: {e.WebErrorStatus}";
        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        _backButton.Enabled = _webView.CanGoBack;
        _forwardButton.Enabled = _webView.CanGoForward;
    }
}
