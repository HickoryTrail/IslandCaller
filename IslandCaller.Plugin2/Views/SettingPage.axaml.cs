using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using IslandCaller.Services;
using IslandCaller.ViewModels;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using static IslandCaller.Services.ProfileService;
using static IslandCaller.ViewModels.SettingPageViewModel;

namespace IslandCaller.Views;

[SettingsPageInfo("plugins.IslandCaller", "IslandCaller 设置", "\uED39", "\uECF8", SettingsPageCategory.External)]
public partial class SettingPage : SettingsPageBase
{
    private SettingPageViewModel vm;
    private HistoryService HistoryService;
    private ILogger<SettingPage> logger;

    public SettingPage()
    {
        InitializeComponent();
        vm = this.DataContext as SettingPageViewModel;
        HistoryService = IAppHost.GetService<HistoryService>();
        logger = IAppHost.GetService<ILogger<SettingPage>>();
        logger.LogInformation("SettingPage 初始化完成");
    }

    private void AddButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int nextId = vm.ProfileList.Any() ? vm.ProfileList.Max(s => s.ID) + 1 : 1;
        vm.ProfileList.Add(new SettingPageViewModel.StudentModel
        {
            ID = nextId,
            Name = "",
            Gender = 0,
            ManualWeight = 1.0
        });
        logger.LogInformation("手动新增名单项，ID: {Id}", nextId);
    }

    private async void ImportButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        List<Person> newlist = new List<Person>();
        logger.LogInformation("开始导入名单流程");

        await CommonTaskDialogs.ShowDialog("导入提示", "导入的名单仅支持下列格式: \n\n" +
            "文本名单 (*.txt): 名单仅包含姓名，使用空格，逗号，或换行分隔\n\n" +
            "SecRandom 名单 (\\list\\rool_call_list\\*.json)\n\n" +
            "CSV 名单 (*.csv): 名单包含姓名,性别可选，不能含有标题");

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            logger.LogError("导入名单失败：无法获取 TopLevel");
            await CommonTaskDialogs.ShowDialog("导入失败", "无法获取窗口上下文，请重试。");
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要导入的名单",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("文本名单")
                {
                    Patterns = new[] { "*.txt" }
                },
                new FilePickerFileType("SecRandom 名单")
                {
                    Patterns = new[] { "*.json" }
                },
                new FilePickerFileType("CSV 名单")
                {
                    Patterns = new[] { "*.csv" }
                }
            }
        });

        if (files.Count == 0)
        {
            logger.LogInformation("用户取消了名单导入");
            return;
        }

        IStorageFile file = files[0];
        string extension = Path.GetExtension(file.Name).ToLowerInvariant();
        logger.LogInformation("已选择导入文件：{FileName}，扩展名：{Extension}", file.Name, extension);

        try
        {
            switch (extension)
            {
                case ".txt":
                    newlist = await new TextFilePraseHelper().ParseTextFileAsync(file);
                    break;
                case ".json":
                    var resultJson = await new SecRandomImport().ShowDialog<(bool isGender, string male, string female)>(this.VisualRoot as Window);
                    logger.LogDebug("SecRandom 导入参数：isGender={IsGender}", resultJson.isGender);
                    newlist = await new SecRandomParseHelper().ParseSecRandomProfileAsync(file, resultJson.isGender, resultJson.male, resultJson.female);
                    break;
                case ".csv":
                    var resultCsv = await new CsvImport().ShowDialog<(int nameRow, int genderRow, bool isGender, string male, string female)>(this.VisualRoot as Window);
                    resultCsv.nameRow -= 1;
                    resultCsv.genderRow -= 1;
                    if (!resultCsv.isGender) resultCsv.genderRow = -1;
                    logger.LogDebug("CSV 导入参数：nameRow={NameRow}, genderRow={GenderRow}, isGender={IsGender}", resultCsv.nameRow, resultCsv.genderRow, resultCsv.isGender);
                    newlist = await new CsvParseHelper().ParseCsvFileAsync(file, resultCsv.nameRow, resultCsv.genderRow, resultCsv.male, resultCsv.female);
                    break;
                default:
                    logger.LogError("导入名单失败：不支持的文件类型 {Extension}", extension);
                    await CommonTaskDialogs.ShowDialog("导入失败", $"不支持的文件类型：{extension}");
                    return;
            }

            var orderedProfile = newlist
                .OrderBy(m => m.Id)
                .Select(m => new StudentModel
                {
                    ID = m.Id,
                    Name = m.Name,
                    Gender = m.Gender,
                    ManualWeight = m.ManualWeight
                });

            vm.ProfileList = new ObservableCollection<StudentModel>(orderedProfile);
            logger.LogInformation("名单导入成功，共导入 {Count} 人", vm.ProfileList.Count);
            await CommonTaskDialogs.ShowDialog("导入完成", $"成功导入 {vm.ProfileList.Count} 条名单。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "导入名单过程中发生异常，文件：{FileName}", file.Name);
            await CommonTaskDialogs.ShowDialog("导入失败", "导入名单时发生错误，请检查文件格式后重试。");
        }
    }

    private void ClearButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        logger.LogInformation("清空点名历史记录");
        HistoryService.ClearThisLessonHistory();
        HistoryService.ClearLongTermHistory();
    }
}
