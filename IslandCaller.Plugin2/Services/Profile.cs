using ClassIsland.Shared;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace IslandCaller.Services
{
    public class ProfileService
    {
        private ILogger<ProfileService>? Logger { get; set; }
        private Status Status { get; set; }
        public ProfileService(ILogger<ProfileService> logger)
        {
            Logger = logger;
            Logger.LogTrace("ProfileService created.");
        }
        internal void Initialize()
        {
            Status = IAppHost.GetService<Status>();
            LoadSelectedProfile(Settings.Instance.Profile.DefaultProfile);
            Logger?.LogInformation("ProfileService initialized.");
        }
        public class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Gender { get; set; }
            public double ManualWeight { get; set; } = 1.0; // 手动权重，默认为 1.0
        }
        // 名单存储
        public List<Person> Members { get; set; } = new List<Person>();
        public Guid ActiveProfileId { get; private set; }

        private static string GetBasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IslandCaller",
                "Profile"
            );
        }

        private static string GetFilePath(Guid guid)
        {
            return Path.Combine(GetBasePath(), $"{guid}.csv");
        }

        // 读取名单
        public void LoadSelectedProfile(Guid guid)
        {
            Status.ProfileServiceInitialized = false;
            var members = GetMembers(guid);
            Members = members;
            ActiveProfileId = guid;
            Status.ProfileServiceInitialized = true;
        }

        // 获取名单
        public List<Person> GetMembers(Guid guid)
        {
            string filePath = GetFilePath(guid);

            if (!File.Exists(filePath))
            {
                Logger?.LogError($"找不到对应的名单文件: {filePath}");
                throw new FileNotFoundException($"找不到对应的名单文件: {filePath}");
            }

            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length == 0)
            {
                Logger?.LogError("CSV 文件为空");
                throw new Exception("CSV 文件为空");
            }

            if (lines[0].Trim() != "id,name,gender,manualweight")
            {
                Logger?.LogError("CSV 标题格式错误，必须为: id,name,gender,manualweight");
                throw new Exception("CSV 标题格式错误，必须为: id,name,gender,manualweight");
            }

            List<Person> members = new List<Person>();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(',');

                if (parts.Length != 4)
                {
                    Logger?.LogWarning($"第 {i + 1} 行格式错误: {line}");
                    continue;
                }

                members.Add(new Person
                {
                    Id = Convert.ToInt32(parts[0]),
                    Name = parts[1],
                    Gender = Convert.ToInt32(parts[2]),
                    ManualWeight = Convert.ToDouble(parts[3])
                });
            }
            members = members.OrderBy(x => x.Id).ToList();
            return members;
        }

        // 写入名单（覆盖或创建）
        public void SaveProfile(Guid guid, List<Person> members)
        {
            members = members.OrderBy(x => x.Id).ToList();
            string basePath = GetBasePath();

            // 如果目录不存在就创建
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string filePath = GetFilePath(guid);

            StringBuilder sb = new StringBuilder();

            // 写标题
            sb.AppendLine("id,name,gender,manualweight");

            foreach (var person in members)
            {
                sb.AppendLine($"{person.Id},{person.Name},{person.Gender},{person.ManualWeight}");
            }

            // 覆盖写入
            try
            {
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"写入名单文件失败: {filePath}");
                throw new Exception($"写入名单文件失败: {filePath}", ex);
            }
        }

        public void CreateDemoProfile(Guid guid)
        { 
            List<Person> members = new List<Person>();
            members.Add(new Person
            {
                Id = 1,
                Gender = 0,
                Name = "小明",
                ManualWeight = 1.0
            });
            members.Add(new Person
            {
                Id = 2,
                Gender = 0,
                Name = "李明",
                ManualWeight = 1.0
            });
            members.Add(new Person
            {
                Id = 3,
                Gender = 1,
                Name = "李华",
                ManualWeight = 1.0
            });
            SaveProfile(guid, members);
        }
    }
}
