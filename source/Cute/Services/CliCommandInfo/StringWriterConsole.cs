using Spectre.Console.Rendering;
using Spectre.Console;

namespace Cute.Services.CliCommandInfo;

// Custom IAnsiConsole implementation
public class StringWriterConsole(StringWriter writer) : IAnsiConsole
{
    private readonly StringWriter _writer = writer;

    public IAnsiConsoleCursor Cursor { get; } = null!;

    public IAnsiConsoleInput Input { get; } = null!;

    public RenderPipeline Pipeline { get; } = null!;

    public IExclusivityMode ExclusivityMode => null!;

    Spectre.Console.Profile IAnsiConsole.Profile => throw new NotImplementedException();

    public void Clear(bool home) => throw new NotImplementedException();

    public void Write(Segment segment)
    {
        _writer.Write(segment.Text);
    }

    public void WriteLine()
    {
        _writer.WriteLine();
    }

    public void Write(IRenderable renderable)
    {
        var segments = renderable.Render(new RenderOptions(null!, new Spectre.Console.Size(1024, 1024)), 1024);

        // Write the segments
        foreach (var segment in segments)
        {
            Write(segment);
        }
    }
}

