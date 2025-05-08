namespace Cute.Lib.Contentful.CommandModels.ContentJoinCommand
{
    public class CuteContentJoin
    {
        public string Key { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string TargetContentType { get; set; } = default!;
        public string SourceContentType1 { get; set; } = default!;
        public string? SourceQueryString1 { get; set; } = default!;
        public string SourceContentType2 { get; set; } = default!;
        public string? SourceQueryString2 { get; set; } = default!;
    }
}