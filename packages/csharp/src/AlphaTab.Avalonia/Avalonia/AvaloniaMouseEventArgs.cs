using AlphaTab.Platform;
using Avalonia.Input;

namespace AlphaTab.Avalonia;

internal class AvaloniaMouseEventArgs : IMouseEventArgs
{
    private readonly PointerEventArgs _args;

    public AvaloniaMouseEventArgs(PointerEventArgs args)
    {
        _args = args;
        var properties = args.GetCurrentPoint(null).Properties;
        IsLeftMouseButton = properties.IsLeftButtonPressed ||
                            properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed;
    }

    public bool IsLeftMouseButton { get; }

    public double GetX(IContainer relativeTo)
    {
        var relativeControl = ((AvaloniaControlContainer)relativeTo).Control;
        return (float)_args.GetPosition(relativeControl).X;
    }

    public double GetY(IContainer relativeTo)
    {
        var relativeControl = ((AvaloniaControlContainer)relativeTo).Control;
        return (float)_args.GetPosition(relativeControl).Y;
    }

    public void PreventDefault()
    {
        _args.Handled = true;
    }
}