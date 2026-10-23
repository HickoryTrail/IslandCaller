using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ReactiveUI;

namespace IslandCaller.Services;

/// <summary>
/// 由 RemoteCI 插件设置下发的管控策略：公平模式与锁定设置页。
/// 策略单独保存在注册表 RemoteCI 子键中，不进入 <see cref="Models.SettingsModel"/>，
/// 因此不会随数据包导入导出，也不能通过导入旧数据包绕过。
/// </summary>
public sealed class RemotePolicyService : ReactiveObject
{
    private const string RegistryPath = @"Software\IslandCaller\RemoteCI";
    private const string FairModeValueName = "FairMode";
    private const string LockSettingsPageValueName = "LockSettingsPage";

    private readonly ILogger<RemotePolicyService> _logger;
    private bool _fairMode;
    private bool _lockSettingsPage;
    private bool _isRemoteManaged;

    public RemotePolicyService(ILogger<RemotePolicyService> logger)
    {
        _logger = logger;
        Load();
    }

    /// <summary>RemoteCI 下发的公平模式开关（持久化值）。</summary>
    public bool FairMode
    {
        get => _fairMode;
        set
        {
            if (_fairMode == value)
            {
                return;
            }

            Save(value, _lockSettingsPage);
            this.RaiseAndSetIfChanged(ref _fairMode, value);
            this.RaisePropertyChanged(nameof(IsFairModeActive));
        }
    }

    /// <summary>RemoteCI 下发的锁定设置页开关（持久化值）。</summary>
    public bool LockSettingsPage
    {
        get => _lockSettingsPage;
        set
        {
            if (_lockSettingsPage == value)
            {
                return;
            }

            Save(_fairMode, value);
            this.RaiseAndSetIfChanged(ref _lockSettingsPage, value);
            this.RaisePropertyChanged(nameof(IsSettingsLocked));
        }
    }

    /// <summary>
    /// 是否已向 RemoteCI 注册插件设置分组。只有处于 RemoteCI 管理下策略才生效：
    /// 卸载或停用 RemoteCI 后无人能再远程解除锁定，此时策略自动失效，避免本机被永久锁死。
    /// </summary>
    public bool IsRemoteManaged
    {
        get => _isRemoteManaged;
        internal set
        {
            if (_isRemoteManaged == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isRemoteManaged, value);
            this.RaisePropertyChanged(nameof(IsFairModeActive));
            this.RaisePropertyChanged(nameof(IsSettingsLocked));
        }
    }

    /// <summary>公平模式是否正在生效：所有学生手动权重固定为 1 且不可编辑。</summary>
    public bool IsFairModeActive => IsRemoteManaged && FairMode;

    /// <summary>设置页是否被锁定为只读。</summary>
    public bool IsSettingsLocked => IsRemoteManaged && LockSettingsPage;

    private void Load()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            _fairMode = Convert.ToBoolean(key?.GetValue(FairModeValueName) ?? false);
            _lockSettingsPage = Convert.ToBoolean(key?.GetValue(LockSettingsPageValueName) ?? false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 RemoteCI 管控策略失败，已按未启用处理");
        }
    }

    private void Save(bool fairMode, bool lockSettingsPage)
    {
        bool wroteFairMode = false;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
            key.SetValue(FairModeValueName, fairMode);
            wroteFairMode = true;
            key.SetValue(LockSettingsPageValueName, lockSettingsPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 RemoteCI 管控策略失败");
            if (wroteFairMode)
            {
                try
                {
                    using var rollbackKey = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
                    rollbackKey.SetValue(FairModeValueName, _fairMode);
                    rollbackKey.SetValue(LockSettingsPageValueName, _lockSettingsPage);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(rollbackException, "回滚 RemoteCI 管控策略失败");
                }
            }

            throw;
        }
    }
}
