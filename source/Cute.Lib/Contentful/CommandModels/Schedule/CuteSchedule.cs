using Contentful.Core.Models;

namespace Cute.Lib.Contentful.CommandModels.Schedule
{
    public class CuteSchedule : IContent
    {
        public SystemProperties Sys { get; set; } = default!;
        public string Id => Sys != null ? Sys.Id : "";
        public string Key { get; set; } = default!;
        public string Command { get; set; } = default!;
        public string Schedule { get; set; } = default!;

        public string? CronSchedule { get; set; } = default!;

        public CuteSchedule? RunAfter { get; set; } = default!;

        public string? LastRunStatus { get; set; }

        public string? LastRunErrorMessage { get; set; }

        public DateTime? LastRunStarted { get; set; }
        public DateTime? LastRunFinished { get; set; }
        public string? LastRunDuration { get; set; }
        public bool IsRunAfter => RunAfter != null;
        public bool IsTimeScheduled => !IsRunAfter;
    }
}
