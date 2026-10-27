using System.IO;
using System.Windows;
using System.Windows.Threading;
using FlexCanvas.Application;
using FlexCanvas.Domain;
using FlexCanvas.Infrastructure;
using FlexCanvas.Infrastructure.Win32;
using Microsoft.Extensions.DependencyInjection;

namespace FlexCanvas.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private IDiagnosticSink? _diagnostics;
    private int _fatal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnProcessException;
        TaskScheduler.UnobservedTaskException += OnTaskException;
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDiagnosticSink>(_ => new RollingDiagnosticSink(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlexCanvas", "logs")));
            services.AddSingleton<WindowHostDispatcher>();
            services.AddSingleton<IWindowDiscoveryService, Win32WindowDiscoveryService>();
            services.AddSingleton<IWindowCompatibilityService, WindowCompatibilityService>();
            services.AddSingleton<IWindowEmbeddingApi, Win32WindowEmbeddingApi>();
            services.AddSingleton<IWindowEmbeddingService, WindowEmbeddingService>();
            services.AddSingleton<WindowManagerViewModel>();
            services.AddSingleton<MainWindow>();
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            _diagnostics = _services.GetRequiredService<IDiagnosticSink>();
            _diagnostics.Record(new(DiagnosticEvent.Startup, DiagnosticLevel.Information, Guid.NewGuid()));
            MainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception error) { FailAndExit(error); }
    }

    public async Task StopServicesAsync()
    {
        var services = _services;
        if (services is null) return;
        _diagnostics?.Record(new(DiagnosticEvent.Shutdown, DiagnosticLevel.Information, Guid.NewGuid()));
        await services.DisposeAsync();
        _services = null;
    }

    public async Task PrepareShutdownAsync()
    {
        if (_services?.GetService<IWindowEmbeddingService>() is IAsyncDisposable embedding)
            await embedding.DisposeAsync();
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        FailAndExit(e.Exception);
    }
    private void OnProcessException(object sender, UnhandledExceptionEventArgs e)
    {
        _diagnostics?.Record(new(DiagnosticEvent.UnhandledException, DiagnosticLevel.Critical, Guid.NewGuid(),
            DiagnosticCode.InternalError, ExceptionHResult: (e.ExceptionObject as Exception)?.HResult));
        // Terminating CLR exceptions may not permit a UI prompt or a complete log flush.
        if (!e.IsTerminating) Dispatcher.BeginInvoke(() => FailAndExit(e.ExceptionObject as Exception));
    }
    private void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Dispatcher.BeginInvoke(() => FailAndExit(e.Exception));
    }
    private async void FailAndExit(Exception? error)
    {
        if (Interlocked.Exchange(ref _fatal, 1) != 0) return;
        Guid id = Guid.NewGuid();
        _diagnostics?.Record(new(DiagnosticEvent.UnhandledException, DiagnosticLevel.Critical, id,
            DiagnosticCode.InternalError, ExceptionHResult: error?.HResult));
        MessageBox.Show($"发生无法继续运行的错误，应用将关闭。\n诊断 ID：{id:N}", "FlexCanvas", MessageBoxButton.OK, MessageBoxImage.Error);
        if (MainWindow is MainWindow window && window.IsVisible) window.Close();
        else
        {
            try { await StopServicesAsync(); }
            catch (Exception shutdownError)
            {
                MessageBox.Show($"服务关闭失败（0x{shutdownError.HResult:X}）。", "FlexCanvas");
            }
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException -= OnProcessException;
        TaskScheduler.UnobservedTaskException -= OnTaskException;
        base.OnExit(e);
    }
}
