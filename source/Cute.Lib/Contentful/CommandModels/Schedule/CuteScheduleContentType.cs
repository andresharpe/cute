using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.Schedule
{
    public class CuteScheduleContentType
    {
        private static readonly ContentType _contentType;

        public static ContentType Instance()
        {
            return _contentType;
        }

        static CuteScheduleContentType()
        {
            var contentTypeBuilder = new ContentTypeBuilder(nameof(CuteSchedule).ToCamelCase())
                .WithDescription("Jobs and definitions for synchronising the space with external API's.")
                .WithDisplayField("key")
                .WithFields([

                    new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                    new FieldBuilder("command", FieldType.Text)
                        .IsRequired()
                        .Build(),

                    new FieldBuilder("schedule", FieldType.Symbol)
                        .IsRequired()
                        .Build(),

                    new FieldBuilder("cronSchedule", FieldType.Symbol)
                        .Build(),

                    new FieldBuilder("runAfter", FieldType.Link)
                        .ValidateLinkContentType([nameof(CuteSchedule).ToCamelCase()], LinkType.Entry)
                        .Build(),

                    new FieldBuilder("lastRunStatus", FieldType.Symbol)
                        .Build(),

                    new FieldBuilder("lastRunErrorMessage", FieldType.Text)
                        .Build(),

                    new FieldBuilder("lastRunStarted", FieldType.Date)
                        .Build(),

                    new FieldBuilder("lastRunFinished", FieldType.Date)
                        .Build(),

                    new FieldBuilder("lastRunDuration", FieldType.Symbol)
                        .Build(),

                ]);

            _contentType = contentTypeBuilder.Build();
        }
    }
}
