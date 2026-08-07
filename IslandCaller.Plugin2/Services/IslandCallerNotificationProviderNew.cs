using AvaloniaEdit;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;

namespace IslandCaller.Services.NotificationProvidersNew;

[NotificationProviderInfo(
    "9B570BF1-9A32-40C0-9D5D-4FFA69E03A37",
    "IslandCallerServices",
    "\uECEE",
    "用于为IslandCaller提供通知接口")]
public class IslandCallerNotificationProviderNew() : NotificationProviderBase
{
    public NotificationRequest? Request { get; set; }

    public async void RandomCall(string name,string? speechtext,float second)
    {
        
        Request = new NotificationRequest()
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(name, factory: x =>
            {
                x.Duration = new TimeSpan(0, 0, (int)second,0,(int)((second-(int)second)*1000));
                x.IsSpeechEnabled = !string.IsNullOrWhiteSpace(speechtext);
                x.SpeechContent = speechtext;
            })
        };
        ShowNotification(Request);
    }
}
