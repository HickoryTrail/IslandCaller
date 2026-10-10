using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;

namespace IslandCaller.Services.NotificationProvidersNew;

[NotificationProviderInfo(
    "9B570BF1-9A32-40C0-9D5D-4FFA69E03A37",
    "IslandCallerServices",
    "\uECEE",
    "用于为IslandCaller提供通知接口")]
public class IslandCallerNotificationProviderNew() : NotificationProviderBase
{
    private static readonly object SyncRoot = new();
    private static NotificationRequest? _activeRequest;

    public NotificationRequest? Request { get; set; }

    public async Task RandomCall(string name, float second, CancellationToken token)
    {
        // 最新优先：先收起上一条还没消失的提醒（可能在排队等待播放，也可能正在播放），
        // 避免连续点名时旧提醒排队堆积，造成"提醒一直不消失、抽多了卡死"。
        NotificationRequest? previous;
        lock (SyncRoot)
        {
            previous = _activeRequest;
            _activeRequest = null;
        }
        try
        {
            previous?.Cancel();
        }
        catch
        {
        }

        var request = new NotificationRequest()
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(name, factory: x =>
            {
                x.Duration = new TimeSpan(0, 0, 0, (int)second, (int)((second - (int)second) * 1000));
                x.IsSpeechEnabled = false;
            })
        };
        lock (SyncRoot)
        {
            _activeRequest = request;
        }

        using var registration = token.Register(() =>
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(_activeRequest, request))
                {
                    _activeRequest = null;
                }
            }
            try
            {
                request.Cancel();
            }
            catch
            {
            }
        });
        Request = request;
        ShowNotification(request);
        try
        {
            await Task.Delay((int)(second * 1000), token);
        }
        catch
        {
        }
        finally
        {
            // 展示时长结束（或被点名打断）后主动收起提醒，
            // 不依赖 ClassIsland 内部计时，确保横幅一定消失、不残留。
            lock (SyncRoot)
            {
                if (ReferenceEquals(_activeRequest, request))
                {
                    _activeRequest = null;
                }
            }
            try
            {
                request.Cancel();
            }
            catch
            {
            }
        }
    }
}
