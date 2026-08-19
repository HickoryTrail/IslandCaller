using System.Collections;
using System.Reflection;

namespace IslandCaller.Plugin2.Helpers;

internal sealed class ClassIslandSubjectItem(Guid subjectId, string name, object subject)
{
    public Guid SubjectId { get; } = subjectId;
    public string Name { get; } = name;
    public object Subject { get; } = subject;
}

internal static class ClassIslandSubjectHelper
{
    public static IReadOnlyList<ClassIslandSubjectItem> GetSubjects(object? profile)
    {
        try
        {
            if (profile?.GetType().GetProperty("Subjects", BindingFlags.Instance | BindingFlags.Public)?.GetValue(profile)
                is not IEnumerable subjects)
            {
                return [];
            }

            var result = new List<ClassIslandSubjectItem>();
            foreach (object? entry in subjects)
            {
                if (entry is null)
                {
                    continue;
                }

                Type entryType = entry.GetType();
                if (entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry) is not Guid subjectId ||
                    subjectId == Guid.Empty ||
                    entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry) is not { } subject)
                {
                    continue;
                }

                string name = subject.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(subject) as string ?? string.Empty;
                result.Add(new ClassIslandSubjectItem(subjectId, name, subject));
            }

            return result;
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static Guid FindSubjectId(object? profile, object? subject)
    {
        if (subject is null)
        {
            return Guid.Empty;
        }

        return GetSubjects(profile)
            .FirstOrDefault(item => ReferenceEquals(item.Subject, subject))?.SubjectId ?? Guid.Empty;
    }
}
