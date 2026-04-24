using AlphaTab.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AlphaTab.Avalonia;

internal class AvaloniaControlContainer : IContainer
{
    private double _targetX;

    public Control Control { get; }

    public AvaloniaControlContainer(Control control)
    {
        Control = control;

        Resize = new DelegatedEventEmitter(
            value => { Control.SizeChanged += (sender, args) => value(); },
            value => { }
        );

        MouseDown = new DelegatedEventEmitter<IMouseEventArgs>(
            value => { Control.PointerPressed += (sender, args) => value(new AvaloniaMouseEventArgs(args)); },
            value => { }
        );

        MouseMove = new DelegatedEventEmitter<IMouseEventArgs>(
            value => { Control.PointerMoved += (sender, args) => value(new AvaloniaMouseEventArgs(args)); },
            value => { }
        );

        MouseUp = new DelegatedEventEmitter<IMouseEventArgs>(
            value => { Control.PointerReleased += (sender, args) => value(new AvaloniaMouseEventArgs(args)); },
            value => { }
        );
    }

    public double Width
    {
        get => Control.Bounds.Width;
        set => Dispatcher.UIThread.Post(() => { Control.Width = value; });
    }

    public double Height
    {
        get => Control.Bounds.Height;
        set => Dispatcher.UIThread.Post(() => { Control.Height = value; });
    }

    public bool IsVisible => Control is { IsVisible: true, Bounds.Width: > 0 };

    public double ScrollLeft
    {
        get => Control is ScrollViewer scroll ? scroll.Offset.X : 0;
        set => Dispatcher.UIThread.Post(() =>
        {
            if (Control is ScrollViewer scroll)
            {
                scroll.Offset = new Vector(value, scroll.Offset.Y);
            }
        });
    }

    public double ScrollTop
    {
        get => Control is ScrollViewer scroll ? scroll.Offset.Y : 0;
        set => Dispatcher.UIThread.Post(() =>
        {
            if (Control is ScrollViewer scroll)
            {
                scroll.Offset = new Vector(scroll.Offset.X, value);
            }
        });
    }

    public void AppendChild(IContainer child)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var childControl = ((AvaloniaControlContainer)child).Control;
            if (Control is Panel panel)
            {
                panel.Children.Add(childControl);
            }
            else if (Control is ContentControl contentControl)
            {
                contentControl.Content = childControl;
            }
            else if (Control is Decorator decorator)
            {
                decorator.Child = childControl;
            }
        });
    }

    public void StopAnimation()
    {
        Dispatcher.UIThread.Post(() => { Canvas.SetLeft(Control, _targetX); });
    }

    public void TransitionToX(double duration, double x)
    {
        _targetX = x;
        Dispatcher.UIThread.Post(() => { Canvas.SetLeft(Control, x); });
    }

    public void Clear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Control is Panel panel)
            {
                panel.Children.Clear();
            }
            else if (Control is ContentControl contentControl)
            {
                contentControl.Content = null;
            }
            else if (Control is Decorator decorator)
            {
                decorator.Child = null;
            }
        });
    }

    public void SetBounds(double x, double y, double w, double h)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Canvas.SetLeft(Control, x);
            Canvas.SetTop(Control, y);
            Control.Width = w;
            Control.Height = h;
        });
    }

    public IEventEmitter Resize { get; set; }
    public IEventEmitterOfT<IMouseEventArgs> MouseDown { get; set; }
    public IEventEmitterOfT<IMouseEventArgs> MouseMove { get; set; }
    public IEventEmitterOfT<IMouseEventArgs> MouseUp { get; set; }
}
