using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;
using System.Text;

namespace IslandCaller.Services.NotificationProvidersNew;

[NotificationProviderInfo(
    "9B570BF1-9A32-40C0-9D5D-4FFA69E03A37",
    "IslandCallerServices",
    "\uECEE",
    "用于为IslandCaller提供通知接口")]
public class IslandCallerNotificationProviderNew(ILessonsService lessonsService,CoreService coreService) : NotificationProviderBase
{
    private readonly ILessonsService lessonsService = lessonsService;
    public NotificationRequest Request { get; set; }

    public async void RandomCall(int stunum)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < stunum; i++)
        {
            sb.Append(coreService.GetRandomStudent());

            if (i != stunum-1)
            {
                sb.Append("  ");
            }
        }
        string output = sb.ToString();
        int maskduration = stunum * 2 + 1; // 计算持续时间
        Request = new NotificationRequest()
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(output, factory: x =>
            {
                x.Duration = new TimeSpan(0, 0, maskduration);
                x.IsSpeechEnabled = true;
                x.SpeechContent = output;
            })
        };
        ShowNotification(Request);
    }
}
