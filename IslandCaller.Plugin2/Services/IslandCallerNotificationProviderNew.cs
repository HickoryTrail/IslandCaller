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
    public NotificationRequest? Request { get; set; }

    public async Task RandomCall(string name, float second, CancellationToken token)
    {
        using var registration = token.Register(() =>
        {
            Request?.Cancel();
        });
        Request = new NotificationRequest()
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(name, factory: x =>
            {
                x.Duration = new TimeSpan(0, 0, 0, (int)second, (int)((second - (int)second) * 1000));
                x.IsSpeechEnabled = false;
            })
        };
        ShowNotification(Request);
        try
        {
            await Task.Delay((int)(second * 1000), token);
        }
        catch { }
    }
}
