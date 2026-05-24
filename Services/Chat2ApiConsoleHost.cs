using System.Windows;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace DeepSeekBrowser.Services;

/// <summary>单例管理 Chat2API 管理台窗口（WebView2 + Chat2API-main UI）。</summary>
public sealed class Chat2ApiConsoleHost
{
    private readonly LocalOpenAiServer _localApi;
    private readonly WebInjectService _web;
    private CoreWebView2Environment? _env;
    private Chat2ApiConsoleWindow? _window;

    public Chat2ApiConsoleHost(LocalOpenAiServer localApi, WebInjectService web)
    {
        _localApi = localApi;
        _web = web;
    }

    public void SetEnvironment(CoreWebView2Environment env)
    {
        _env = env;
    }

    /// <summary>已弃用：API 管理台改为 Agent 内嵌 iframe，不再预热独立窗口。</summary>
    public void ScheduleWarmup(int delayMs = 0)
    {
        // no-op: embedded panel loads on demand
    }

    private async Task WarmupAfterDelayAsync(int delayMs)
    {
        try
        {
            if (delayMs > 0)
                await Task.Delay(delayMs);
            await WarmupAsync();
        }
        catch
        {
            // 预热失败不影响主流程，打开时再初始化
        }
    }

    public Task WarmupAsync()
    {
        if (_env is null) return Task.CompletedTask;
        if (_window is { IsPageReady: true }) return Task.CompletedTask;

        return Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (_window is { IsPageReady: true }) return;
            await EnsureHiddenWindowAsync();
        }).Task.Unwrap();
    }

    public async Task ShowAsync(Window owner)
    {
        if (_env is null)
            throw new InvalidOperationException("WebView2 environment not set");

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (_window is null)
            {
                _window = CreateWindow();
                _window.Owner = owner;
            }
            else
            {
                _window.Owner = owner;
            }

            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;

            if (_window.IsPageReady)
            {
                _window.SetContentVisible(true);
                _window.SetLoadingVisible(false);
            }
            else
            {
                _window.SetLoadingVisible(true);
                _window.SetContentVisible(false);
            }

            _window.Show();
            _window.Activate();

            if (!_window.IsWebViewReady)
                await _window.EnsureInitializedAsync(_env);

            if (!_window.IsPageReady)
            {
                try
                {
                    await _window.WaitForPageReadyAsync(TimeSpan.FromSeconds(15));
                }
                catch (TimeoutException)
                {
                    _window.SetContentVisible(true);
                    _window.SetLoadingVisible(false);
                }
            }

            _window.RefreshConfig(ConfigStore.Load());
        }).Task.Unwrap();
    }

    private Chat2ApiConsoleWindow CreateWindow()
    {
        var w = new Chat2ApiConsoleWindow(_localApi, _web);
        w.Closing += (_, e) =>
        {
            e.Cancel = true;
            w.Hide();
        };
        return w;
    }

    private async Task EnsureHiddenWindowAsync()
    {
        if (_window is null)
        {
            _window = CreateWindow();
            _window.Opacity = 0;
            _window.ShowActivated = false;
            _window.Show();
        }

        if (!_window.IsWebViewReady)
            await _window.EnsureInitializedAsync(_env!);

        if (!_window.IsPageReady)
        {
            try
            {
                await _window.WaitForPageReadyAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                // 预热超时仍保留窗口，打开时再试
            }
        }

        _window.Hide();
        _window.Opacity = 1;
    }
}
