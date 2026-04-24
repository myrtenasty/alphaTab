using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlphaTab.Platform;
using AlphaTab.Platform.CSharp;
using AlphaTab.Platform.Skia.AlphaSkiaBridge;
using AlphaTab.Rendering;
using AlphaTab.Rendering.Utils;
using AlphaTab.Synth;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Image = Avalonia.Controls.Image;

namespace AlphaTab.Avalonia;

internal class AvaloniaUiFacade : ManagedUiFacade<AlphaTab>
{
    private readonly ScrollViewer _scrollViewer;
    private readonly Canvas _renderPanel;
    private readonly Canvas _overlayPanel;
    private readonly Dictionary<string, Image> _resultIdToElementLookup = new Dictionary<string, Image>();
    private event Action? InternalRootContainerBecameVisible;

    public override IContainer RootContainer { get; }
    public override IEventEmitter RootContainerBecameVisible { get; }

    public AvaloniaUiFacade(ScrollViewer scrollViewer, Canvas renderPanel, Canvas overlayPanel)
    {
        _scrollViewer = scrollViewer;
        _renderPanel = renderPanel;
        _overlayPanel = overlayPanel;
        RootContainer = new AvaloniaControlContainer(scrollViewer);
        Environment.HighDpiFactor = 1.0;
        _scrollViewer.SizeChanged += OnRootContainerPossiblyBecameVisible;
        _scrollViewer.PropertyChanged += OnRootContainerPropertyChanged;
        _scrollViewer.AttachedToVisualTree += OnRootContainerAttachedToVisualTree;
        RootContainerBecameVisible = new DelegatedEventEmitter(
            value =>
            {
                if (RootContainer.IsVisible)
                {
                    value();
                }
                else
                {
                    InternalRootContainerBecameVisible += value;
                }
            },
            value => { InternalRootContainerBecameVisible -= value; }
        );
    }

    private void OnRootContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
        {
            NotifyRootContainerBecameVisibleIfNeeded();
        }
    }

    private void OnRootContainerAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        NotifyRootContainerBecameVisibleIfNeeded();
    }

    private void OnRootContainerPossiblyBecameVisible(object? sender, SizeChangedEventArgs e)
    {
        NotifyRootContainerBecameVisibleIfNeeded();
    }

    private void NotifyRootContainerBecameVisibleIfNeeded()
    {
        if (!RootContainer.IsVisible)
        {
            return;
        }

        var visibleHandlers = InternalRootContainerBecameVisible;
        if (visibleHandlers == null)
        {
            return;
        }

        InternalRootContainerBecameVisible = null;
        visibleHandlers();
    }

    protected override Stream? OpenDefaultSoundFont()
    {
        return typeof(NAudioSynthOutput).Assembly.GetManifestResourceStream(
            typeof(NAudioSynthOutput), "default.sf2");
    }

    public override void Initialize(AlphaTabApiBase<AlphaTab> api, AlphaTab control)
    {
        base.Initialize(api, control);
        control.Settings.Core.EnableLazyLoading = false;
        api.Settings = control.Settings;
        control.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(Settings settings)
    {
        settings.Core.EnableLazyLoading = false;
        Api.Settings = settings;
        Api.UpdateSettings();
        Api.Render();
    }

    protected override void RenderTracks()
    {
        SettingsContainer.RenderTracks();
    }

    protected override ISynthOutput CreateSynthOutput()
    {
        return new NAudioSynthOutput();
    }

    public override IAlphaSynth CreateBackingTrackPlayer()
    {
        return new BackingTrackPlayer(
            new NAudioBackingTrackOutput(BeginInvoke),
            Api.Settings.Player.BufferTimeInMilliseconds
        );
    }

    public override void Destroy()
    {
        SettingsContainer.SettingsChanged -= OnSettingsChanged;
        _scrollViewer.SizeChanged -= OnRootContainerPossiblyBecameVisible;
        _scrollViewer.PropertyChanged -= OnRootContainerPropertyChanged;
        _scrollViewer.AttachedToVisualTree -= OnRootContainerAttachedToVisualTree;
        _renderPanel.Children.Clear();
        _overlayPanel.Children.Clear();
        _scrollViewer.Content = null;
    }

    public override IContainer CreateCanvasElement()
    {
        return new AvaloniaControlContainer(_renderPanel);
    }

    public override void InitialRender()
    {
        Api.Renderer.PreRender.On(_ => { _resultIdToElementLookup.Clear(); });
        base.InitialRender();
    }

    public override void TriggerEvent(IContainer container, string eventName,
        object? details = null, IMouseEventArgs? originalEvent = null)
    {
    }

    public override void BeginUpdateRenderResults(RenderFinishedEventArgs? r)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var renderResult = r;
            if (renderResult == null ||
                !_resultIdToElementLookup.TryGetValue(renderResult.Id, out var placeholder))
            {
                return;
            }

            var body = renderResult.RenderResult;
            IImage? source = null;
            if (body is string)
            {
                return;
            }

            if (body is AlphaSkiaImage skiaImage)
            {
                using (skiaImage)
                {
                    source = AlphaSkiaImageSource.Create(skiaImage);
                }
            }
            else if (body is System.Drawing.Bitmap image)
            {
                using (image)
                {
                    source = GdiImageSource.Create(image);
                }
            }

            placeholder.Source = source;
        });
    }

    public override void BeginAppendRenderResults(RenderFinishedEventArgs? r)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var renderResult = r;
            if (renderResult == null)
            {
                if (TotalResultCount.TryDequeue(out var counter))
                {
                    while (_renderPanel.Children.Count > counter.Count)
                    {
                        var removed = _renderPanel.Children[^1];
                        if (removed is Image { Source: IDisposable disposable })
                        {
                            disposable.Dispose();
                        }
                        _renderPanel.Children.RemoveAt(_renderPanel.Children.Count - 1);
                    }
                }
            }
            else if (TotalResultCount.TryPeek(out var counter))
            {
                Image placeholder;
                if (counter.Count < _renderPanel.Children.Count && _renderPanel.Children[counter.Count] is Image image)
                {
                    placeholder = image;
                }
                else
                {
                    placeholder = new Image
                    {
                        Stretch = Stretch.Fill,
                        IsHitTestVisible = false
                    };
                    _renderPanel.Children.Add(placeholder);
                }

                Canvas.SetLeft(placeholder, renderResult.X);
                Canvas.SetTop(placeholder, renderResult.Y);
                placeholder.Width = renderResult.Width;
                placeholder.Height = renderResult.Height;
                _resultIdToElementLookup[renderResult.Id] = placeholder;

                counter.Count++;
            }
        });
    }

    public override void DestroyCursors()
    {
        var cursors = _overlayPanel.Children.OfType<Canvas>()
            .FirstOrDefault(c => "at-cursors".Equals(c.Tag));
        if (cursors != null)
        {
            _overlayPanel.Children.Remove(cursors);
        }
    }

    public override Cursors CreateCursors()
    {
        var cursorWrapper = new Canvas
        {
            Tag = "at-cursors",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
            IsHitTestVisible = false
        };

        var selectionWrapper = new Canvas
        {
            IsHitTestVisible = false
        };

        var barCursor = new global::Avalonia.Controls.Shapes.Rectangle
        {
            Fill = SettingsContainer.BarCursorFill,
            IsHitTestVisible = false
        };
        var beatCursor = new global::Avalonia.Controls.Shapes.Rectangle
        {
            Fill = SettingsContainer.BeatCursorFill,
            IsHitTestVisible = false,
            Width = 3
        };

        cursorWrapper.Children.Add(selectionWrapper);
        cursorWrapper.Children.Add(barCursor);
        cursorWrapper.Children.Add(beatCursor);
        _overlayPanel.Children.Insert(0, cursorWrapper);

        return new Cursors(
            new AvaloniaControlContainer(cursorWrapper),
            new AvaloniaControlContainer(barCursor),
            new AvaloniaControlContainer(beatCursor),
            new AvaloniaControlContainer(selectionWrapper)
        );
    }

    protected override void PostToUIThread(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    public override void RemoveHighlights()
    {
    }

    public override void HighlightElements(string groupId, double masterBarIndex)
    {
    }

    public override IContainer CreateSelectionElement()
    {
        var selection = new global::Avalonia.Controls.Shapes.Rectangle
        {
            Fill = SettingsContainer.SelectionFill,
            IsHitTestVisible = false
        };
        return new AvaloniaControlContainer(selection);
    }

    public override IContainer GetScrollContainer()
    {
        return new AvaloniaControlContainer(_scrollViewer);
    }

    public override Bounds GetOffset(IContainer? relativeTo, IContainer container)
    {
        var containerAvalonia = ((AvaloniaControlContainer)container).Control;
        var position = containerAvalonia.TranslatePoint(new Point(0, 0), _renderPanel) ?? new Point();

        if (relativeTo != null && ((AvaloniaControlContainer)relativeTo).Control is ScrollViewer scrollViewer)
        {
            position = new Point(position.X - scrollViewer.Offset.X, position.Y - scrollViewer.Offset.Y);
        }

        return new Bounds
        {
            X = (float)position.X,
            Y = (float)position.Y,
            W = (float)containerAvalonia.Bounds.Width,
            H = (float)containerAvalonia.Bounds.Height
        };
    }

    public override void ScrollToY(IContainer scrollElement, double offset, double speed)
    {
        if (((AvaloniaControlContainer)scrollElement).Control is ScrollViewer scrollViewer)
        {
            Dispatcher.UIThread.Post(() => { scrollViewer.Offset = new Vector(scrollViewer.Offset.X, offset); });
        }
    }

    public override void ScrollToX(IContainer scrollElement, double offset, double speed)
    {
        if (((AvaloniaControlContainer)scrollElement).Control is ScrollViewer scrollViewer)
        {
            Dispatcher.UIThread.Post(() => { scrollViewer.Offset = new Vector(offset, scrollViewer.Offset.Y); });
        }
    }

    public override void StopScrolling(IContainer scrollElement)
    {
    }

    public override void SetCanvasOverflow(IContainer canvasElement, double overflow, bool isVertical)
    {
        if (!(((AvaloniaControlContainer)canvasElement).Control is Canvas canvas))
        {
            return;
        }

        canvas.Margin = isVertical
            ? new Thickness(0, 0, 0, overflow)
            : new Thickness(0, 0, overflow, 0);
    }
}
