namespace Buttercup.Core.Services.Implementation
{
    // These classes would normally be in separate files
    public class ContentTypeEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string DisplayField { get; set; } = default!;
        public List<FieldEntity> Fields { get; set; } = [];
    }
}