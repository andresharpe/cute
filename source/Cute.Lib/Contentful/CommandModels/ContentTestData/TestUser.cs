using Contentful.Core.Models;

namespace Cute.Lib.Contentful.CommandModels.ContentTestData;

public class TestUser
{
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int Age { get; set; } = default!;
    public Location Location { get; set; } = new();
    public DateTime BirthDate { get; set; } = default!;
}