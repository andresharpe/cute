namespace Cute.Services.ReadLine;

internal record DisplayLine(int Row, int Column, ReadOnlyMemory<char> Line)
{
}