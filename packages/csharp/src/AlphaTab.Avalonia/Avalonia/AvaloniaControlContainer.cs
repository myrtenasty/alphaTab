using System;
using System.Threading;
using AlphaTab.Platform;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AlphaTab.Avalonia;

internal class AvaloniaControlContainer : IContainer
{
    private double _targetX;
    private CancellationTokenSource? _animationCts;

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
                else if (Control is ScrollViewer { Content: Panel scrollPanel })
                {
                    scrollPanel.Children.Add(childControl);
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
        Dispatcher.UIThread.Post(() =>
        {
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = null;
            Canvas.SetLeft(Control, _targetX);
        });
    }

    public void TransitionToX(double duration, double x)
    {
        _targetX = x;
        Dispatcher.UIThread.Post(() =>
        {
            // Cancel and clean up any previous animation
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = null;

            // Invalid, NaN, Infinity, zero, or negative duration: place immediately
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
            {
                Canvas.SetLeft(Control, x);
                return;
            }

            double currentX = Canvas.GetLeft(Control);
            if (double.IsNaN(currentX)) currentX = 0;

            // Already at target — snap and skip animation
            if (Math.Abs(currentX - x) < 0.5)
            {
                Canvas.SetLeft(Control, x);
                return;
            }

            var cts = new CancellationTokenSource();
            _animationCts = cts;
            var token = cts.Token;

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(duration),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(Canvas.LeftProperty, currentX) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(Canvas.LeftProperty, x) }
                    }
                }
            };

            _ = animation.RunAsync(Control, token);
        });
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
