namespace Cute.Lib.Contentful.CommandModels.ContentJoinCommand
{
    public class CuteContentJoin
    {
        public string Key { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string TargetContentType { get; set; } = default!;
        public string SourceContentType1 { get; set; } = default!;
        public List<string> SourceKeys1 { get; set; } = default!;
        public string SourceContentType2 { get; set; } = default!;
        public List<string> SourceKeys2 { get; set; } = default!;
    }
}