using ClassIsland.Shared;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace IslandCaller.Services
{
    /// <summary>公平模式重置手动权重的结果：实际被修改的名单数量，以及未能处理的名单。</summary>
    internal readonly record struct ManualWeightResetResult(int ChangedCount, IReadOnlyList<Guid> FailedProfileIds);

    public class ProfileService
    {
        private ILogger<ProfileService>? Logger { get; set; }
        private Status Status { get; set; }
        private RemotePolicyService Policy { get; }

        /// <summary>名单文件写入完成后触发，参数为名单 Guid。</summary>
        public event EventHandler<Guid>? ProfileSaved;

        public ProfileService(ILogger<ProfileService> logger, RemotePolicyService policy)
        {
            Logger = logger;
            Policy = policy;
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
        public List<Person> GetMembers(Guid guid) => GetMembers(guid, out _);

        /// <param name="skippedRows">因格式错误被跳过的行数；按解析结果重写整个文件会丢失这些行。</param>
        internal List<Person> GetMembers(Guid guid, out int skippedRows)
        {
            skippedRows = 0;
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
                    skippedRows++;
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
            if (Policy.IsFairModeActive)
            {
                // 公平模式下手动权重一律为 1，任何入口写入的名单都不能带入其他权重。
                members = members.Select(person => new Person
                {
                    Id = person.Id,
                    Name = person.Name,
                    Gender = person.Gender,
                    ManualWeight = 1.0
                }).ToList();
            }
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

            ProfileSaved?.Invoke(this, guid);
        }

        /// <summary>
        /// 把所有名单中的手动权重重置为 1（公平模式）。单个名单失败只记录日志、不影响其余名单，
        /// 失败的名单会在结果中返回，由调用方决定如何向管理员反馈。名单文件不存在时没有权重可清除，不算失败。
        /// </summary>
        internal ManualWeightResetResult ResetAllManualWeights()
        {
            int changedCount = 0;
            var failedProfileIds = new List<Guid>();
            foreach (Guid profileId in Settings.Instance.Profile.ProfileList.Keys.ToList())
            {
                try
                {
                    // GetMembers 同时校验文件格式；无法解析的名单在这里抛出并计为失败。
                    List<Person> members = GetMembers(profileId);
                    if (members.All(person => person.ManualWeight == 1.0))
                    {
                        continue;
                    }

                    ResetManualWeightsInFile(profileId);
                    changedCount++;
                }
                catch (FileNotFoundException ex)
                {
                    Logger?.LogWarning(ex, "名单 {ProfileGuid} 的文件不存在，跳过手动权重重置", profileId);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "重置名单 {ProfileGuid} 的手动权重失败", profileId);
                    failedProfileIds.Add(profileId);
                }
            }

            return new ManualWeightResetResult(changedCount, failedProfileIds);
        }

        /// <summary>
        /// 只把名单文件中学生行的权重列改为 1，其余内容原样保留。GetMembers 会跳过格式错误的行，
        /// 若按解析结果重写整个文件，开启公平模式就会把这些行永久删除。
        /// </summary>
        private void ResetManualWeightsInFile(Guid guid)
        {
            string filePath = GetFilePath(guid);
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 1; i < lines.Length; i++)
            {
                // 与 GetMembers 相同的判定：恰好 4 列的行才是学生行。
                string[] parts = lines[i].Trim().Split(',');
                if (parts.Length == 4)
                {
                    parts[3] = "1";
                    lines[i] = string.Join(',', parts);
                }
            }

            try
            {
                File.WriteAllLines(filePath, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"写入名单文件失败: {filePath}");
                throw new Exception($"写入名单文件失败: {filePath}", ex);
            }

            ProfileSaved?.Invoke(this, guid);
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
