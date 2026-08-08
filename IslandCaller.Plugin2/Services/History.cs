using ClassIsland.Shared;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace IslandCaller.Services
{
    public class HistoryService()
    {
        private Dictionary<string, int> historyDict = new();
        private List<string> top20List = new();
        private ILogger<HistoryService>? Logger { get; set; }
        private ProfileService ProfileService {  get; set; }
        private Status Status {  get; set; }

        internal void Initialize()
        {
            Logger = IAppHost.TryGetService<ILogger<HistoryService>>();
            ProfileService = IAppHost.GetService<ProfileService>();
            Status = IAppHost.GetService<Status>();
            Load(Settings.Instance.Profile.DefaultProfile);
            Logger?.LogInformation("HistoryService initialized.");
        }

        private static string GetBasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IslandCaller",
                "History"
            );
        }

        private static string GetFilePath(Guid guid)
        {
            return Path.Combine(GetBasePath(), $"{guid}.txt");
        }

        // 载入长期历史（只加载 Dictionary）
        public void Load(Guid guid)
        {
            Status.HistoryServiceInitialized = false;
            historyDict.Clear();

            string filePath = GetFilePath(guid);

            // 先构建一个 name → count 的临时字典，用于快速查找
            var csvDict = new Dictionary<string, int>();

            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    var parts = line.Split(',');

                    if (parts.Length != 2)
                        continue;

                    string name = parts[0].Trim();

                    if (int.TryParse(parts[1], out int count))
                    {
                        // 只记录，不判断是否在 Members 中
                        csvDict[name] = count;
                    }
                }
            }

            // 以 Members 为基准加载
            foreach (var person in ProfileService.Members)
            {
                string name = person.Name;

                if (csvDict.TryGetValue(name, out int count))
                {
                    historyDict[name] = count;   // CSV 有 → 用 CSV 的值
                }
                else
                {
                    historyDict[name] = 0;       // CSV 没有 → 记为 0
                }
            }
            Status.HistoryServiceInitialized = true;
            Logger?.LogInformation("HistoryService loaded for profile {ProfileGuid}.", guid);
        }

        // 写入历史
        public void Add(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            // 1️ 更新 Dictionary（统计次数）
            if (historyDict.ContainsKey(name))
                historyDict[name]++;
            else
                historyDict[name] = 1;

            // 2️ 更新 top20（允许重复）
            top20List.Insert(0, name);

            if (top20List.Count > 20)
                top20List.RemoveAt(top20List.Count - 1);

            // 3️自动保存
            Guid guid = Settings.Instance.Profile.DefaultProfile;
            Save(guid);
        }

        // 保存长期记录到本地
        private void Save(Guid guid)
        {
            string basePath = GetBasePath();
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            string filePath = GetFilePath(guid);

            StringBuilder sb = new();

            foreach (var pair in historyDict)
            {
                sb.AppendLine($"{pair.Key},{pair.Value}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Logger?.LogInformation("HistoryService saved for profile {ProfileGuid}.", guid);
        }

        // 获取长期记录中某个名字的出现次数
        public int GetLongTermCount(string name)
        {
            if (historyDict.TryGetValue(name, out int count))
                return count;

            return 0;
        }

        // 获取短期记录中的序号（1-based）
        // 不存在返回 -1
        public int GetLastCallIndex(string name)
        {
            int index = top20List.IndexOf(name);
            return index;
        }

        // 获取长期平均次数
        public double GetAverageLongTermCount()
        {
            if (historyDict.Count == 0)
                return 0;
            return historyDict.Values.Average();
        }

        // 清空长期记录
        public void ClearLongTermHistory()
        {
            historyDict.Clear();
            Save(Settings.Instance.Profile.DefaultProfile);
            Logger?.LogInformation("HistoryService long-term history cleared for profile {ProfileGuid}.", Settings.Instance.Profile.DefaultProfile);
        }

        // 清空短期记录
        public void ClearThisLessonHistory()
        {
            top20List.Clear();
            Logger?.LogInformation("HistoryService short-term history cleared.");
        }
    }
}