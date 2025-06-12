namespace Buttercup.Core.Services.Implementation
{
    public class FieldEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Type { get; set; } = default!;
        public bool Required { get; set; }
        public bool Localized { get; set; }
    }
}