using ClassIsland.Shared;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services
{
    public class CoreService
    {
        private ProfileService ProfileService { get; set; }
        private HistoryService HistoryService { get; set; }
        private ILogger<CoreService>? Logger { get; set; }
        private Status Status {  get; set; }
        Random rand = new();
        public CoreService()
        {
            Logger = IAppHost.TryGetService<ILogger<CoreService>>();
            Logger?.LogTrace("CoreService created.");
        }
        internal class Person
        {
            internal int Id { get; set; }
            internal string Name { get; set; }
            internal int Gender { get; set; }
            internal double ManualWeight { get; set; } = 1.0; // 教师设置的基础权重，默认为 1.0
            internal double Weight { get; set; }
        }
        // 计算学生被点名的权重
        internal List<Person> Persons { get; set; } = new List<Person>();

        /// <summary>当前名单中的可点名人数（0 表示无可用内容）。</summary>
        internal int PersonCount => Persons.Count;

        internal void Initialize()
        {
            ProfileService = IAppHost.GetService<ProfileService>();
            HistoryService = IAppHost.GetService<HistoryService>();
            Status = IAppHost.GetService<Status>();
            Status.CoreServiceInitialized = false;
            Persons.Clear();
            foreach (var person in ProfileService.Members)
            {
                Persons.Add(new Person
                {
                    Id = person.Id,
                    Name = person.Name,
                    Gender = person.Gender,
                    ManualWeight = person.ManualWeight,
                    Weight = 0.0
                });
            }
            ComputeWeightsForAllStudents();
            Status.CoreServiceInitialized = true;
            Logger?.LogInformation($"CoreService initialized with {Persons.Count} students.");
        }

        private double ComputeSingleWeight(
                                double manualWeight,     // W_manual_i
                                int lastHitDistance,     // 距上次被点到的抽取次数（没点过为 -1）
                                int nHist,               // n_hist_i：历史被点次数
                                double avgHist)          // avg_hist：全班历史平均被点次数
        {
            // -----------------------------
            // 1. 本节课防重复因子（Hill 型 S 曲线）
            // -----------------------------
            var algorithm = Settings.Instance.Algorithm;
            double halfRecoveryDistance = algorithm.HalfRecoveryDistance;
            double curvePower = algorithm.CurvePower;

            // 不在短期历史中的学生不应低于已在历史末尾的学生。
            double F_session;
            if (lastHitDistance < 0)
            {
                F_session = 1.0;
            }
            else
            {
                double distance = Math.Max(0, lastHitDistance);
                double distancePower = Math.Pow(distance, curvePower);
                double halfRecoveryPower = Math.Pow(halfRecoveryDistance, curvePower);

                // F_session = d^p / (d^p + h^p)，d = h 时恰为 0.5。
                F_session = distancePower / (distancePower + halfRecoveryPower);
            }

            // -----------------------------
            // 2. 历史均衡因子
            // -----------------------------
            const double eps = 1.0;      // 平滑项
            double gamma = algorithm.Gamma;    // 补偿强度
            double rMin = algorithm.RMin;      // 最小补偿
            double rMax = algorithm.RMax;      // 最大补偿

            // F_history = clip( ((manualWeight * avgHist + eps)/(nHist + eps))^gamma , rMin, rMax )
            double ratio = (manualWeight * avgHist + eps) / (nHist + eps);
            double F_history = Math.Pow(ratio, gamma);
            F_history = Math.Max(rMin, Math.Min(rMax, F_history));

            // -----------------------------
            // 3. 最终权重
            // -----------------------------
            return manualWeight * F_session * F_history;
        }

        private void ComputeWeightsForAllStudents()
        {
            // 计算全班历史平均被点次数
            double avgHist = HistoryService.GetAverageLongTermCount();
            Logger?.LogTrace($"计算全班历史平均被点次数: {avgHist}");
            foreach (var person in Persons)
            {
                int nHist = HistoryService.GetLongTermCount(person.Name);
                int lastHitDistance = HistoryService.GetLastCallIndex(person.Name);
                double weight = ComputeSingleWeight(
                                    person.ManualWeight,
                                    lastHitDistance,
                                    nHist,
                                    avgHist);
                person.Weight = weight;
                Logger?.LogTrace($"计算权重 - 学生: {person.Name}, ManualWeight: {person.ManualWeight}, LastHitDistance: {lastHitDistance}, nHist: {nHist}, Weight: {weight}");
            }
        }

        internal string GetRandomStudent()
        {
            // 计算权重总和
            double totalWeight = Persons.Sum(p => p.Weight);
            Logger?.LogTrace($"计算权重总和: {totalWeight}");
            if (totalWeight == 0) return "Error"; // 避免除以零
            // 生成一个 [0, totalWeight) 的随机数
            double r = rand.NextDouble() * totalWeight;
            Logger?.LogTrace($"生成随机数: {r} (范围: [0, {totalWeight}))");
            // 根据权重选择学生
            double cumulative = 0;
            foreach (var person in Persons)
            {
                cumulative += person.Weight;
                if (r < cumulative)
                {
                    HistoryService.Add(person.Name);
                    Logger?.LogTrace($"抽取到学生：{person.Name}");
                    ComputeWeightsForAllStudents();
                    return person.Name;
                }
            }
            Logger?.LogWarning($"随机选择学生时发生了意外情况，权重总和: {totalWeight}, 随机数: {r}");
            return "Error: 名单人数小于所需人数"; // 理论上不应该到达这里
        }
    }
}
