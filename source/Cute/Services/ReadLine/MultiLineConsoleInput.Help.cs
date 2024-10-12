namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void DisplayHelp(InputState state, InputOptions options)
    {
        var help = new InputState
        {
            BufferLines = new("""
                Keyboard Shortcuts: (Press any key to return...)

                <Ctrl+C>:Copy   <Arrow Keys>:Move Cursor     <Shift+Arrows>:Select           <Ctrl+Up>:Previous History
                <Ctrl+X>:Cut    <Home>:Start of Line         <Ctrl+A>:Select All             <Ctrl+Down>:Next History
                <Ctrl+B>:Paste  <Ctrl+Home>:Start of Prompt  <Backspace>:Delete ←            <Enter>: New Line
                <Ctrl+Z>:Undo   <End>:End of Line            <Ctrl+Backspace>:Delete Word ←  <Escape>:Clear/Cancel Input
                <Ctrl+Y>:Redo   <Ctrl+End>:End of Prompt     <Delete>:Delete →               <Tab>/<Ctrl+Enter>:Finish Input
                                                             <Ctrl+Delete>:Delete Word →
                """),
            RenderStartRow = state.RenderStartRow,
            RenderStartColumn = state.RenderStartColumn,
        };
        Render(help, options);
        Console.ReadKey(true);
        state.RenderStartRow = help.RenderStartRow;
        state.RenderEndRow = help.RenderEndRow;
        state.IsDisplayValid = false;
    }
}