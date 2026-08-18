using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Controls;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.Helpers;

public static class ProfileImportHelper
{
    public static async Task<List<Person>?> ImportAsync(Control owner, ILogger? logger = null)
    {
        logger?.LogInformation("开始导入名单流程");

        await CommonTaskDialogs.ShowDialog("导入提示", "导入的名单仅支持下列格式: \n\n" +
            "文本名单 (*.txt): 名单仅包含姓名，使用空格，逗号，或换行分隔\n\n" +
            "SecRandom 名单 (\\list\\rool_call_list\\*.json)\n\n" +
            "CSV 名单 (*.csv): 名单包含姓名,性别可选，不能含有标题");

        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            logger?.LogError("导入名单失败：无法获取 TopLevel");
            await CommonTaskDialogs.ShowDialog("导入失败", "无法获取窗口上下文，请重试。");
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要导入的名单",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("文本名单") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("SecRandom 名单") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("CSV 名单") { Patterns = new[] { "*.csv" } }
            }
        });

        if (files.Count == 0)
        {
            logger?.LogInformation("用户取消了名单导入");
            return null;
        }

        IStorageFile file = files[0];
        string extension = Path.GetExtension(file.Name).ToLowerInvariant();
        logger?.LogInformation("已选择导入文件：{FileName}，扩展名：{Extension}", file.Name, extension);

        try
        {
            List<Person> members = extension switch
            {
                ".txt" => await new TextFilePraseHelper().ParseTextFileAsync(file),
                ".json" => await ImportSecRandomAsync(file, topLevel),
                ".csv" => await ImportCsvAsync(file, topLevel),
                _ => throw new NotSupportedException($"不支持的文件类型：{extension}")
            };

            var orderedMembers = members.OrderBy(m => m.Id).ToList();
            logger?.LogInformation("名单导入成功，共导入 {Count} 人", orderedMembers.Count);
            await CommonTaskDialogs.ShowDialog("导入完成", $"成功导入 {orderedMembers.Count} 条名单。");
            return orderedMembers;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "导入名单过程中发生异常，文件：{FileName}", file.Name);
            await CommonTaskDialogs.ShowDialog("导入失败", ex is NotSupportedException
                ? ex.Message
                : "导入名单时发生错误，请检查文件格式后重试。");
            return null;
        }
    }

    private static async Task<List<Person>> ImportSecRandomAsync(IStorageFile file, TopLevel topLevel)
    {
        var owner = topLevel as Window ?? throw new InvalidOperationException("无法获取窗口上下文，请重试。");
        var result = await new SecRandomImport().ShowDialog<(bool isGender, string male, string female)>(owner);
        return await new SecRandomParseHelper().ParseSecRandomProfileAsync(file, result.isGender, result.male, result.female);
    }

    private static async Task<List<Person>> ImportCsvAsync(IStorageFile file, TopLevel topLevel)
    {
        var owner = topLevel as Window ?? throw new InvalidOperationException("无法获取窗口上下文，请重试。");
        var result = await new CsvImport().ShowDialog<(int nameRow, int genderRow, bool isGender, string male, string female)>(owner);
        result.nameRow -= 1;
        result.genderRow -= 1;
        if (!result.isGender)
        {
            result.genderRow = -1;
        }

        return await new CsvParseHelper().ParseCsvFileAsync(file, result.nameRow, result.genderRow, result.male, result.female);
    }
}
