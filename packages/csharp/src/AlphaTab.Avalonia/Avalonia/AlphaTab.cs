using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using AlphaTab.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaPropertyChangedEventArgs = Avalonia.AvaloniaPropertyChangedEventArgs;
using MediaColor = Avalonia.Media.Color;

namespace AlphaTab.Avalonia;

/// <summary>
/// An Avalonia UI control to display an instance of alphaTab via <see cref="Image"/> controls.
/// </summary>
public class AlphaTab : ContentControl
{
    private readonly ScrollViewer _scrollView;
    private readonly Canvas _layoutPanel;
    private readonly Canvas _renderPanel;
    private readonly Canvas _overlayPanel;

    /// <summary>
    /// Identifies the <see cref="Tracks"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<Track>?> TracksProperty =
        AvaloniaProperty.Register<AlphaTab, IEnumerable<Track>?>(nameof(Tracks));

    /// <summary>
    /// Identifies the <see cref="Settings"/> styled property.
    /// </summary>
    public static readonly StyledProperty<Settings> SettingsProperty =
        AvaloniaProperty.Register<AlphaTab, Settings>(nameof(Settings), new Settings());

    /// <summary>
    /// Identifies the <see cref="BarCursorFill"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IBrush> BarCursorFillProperty =
        AvaloniaProperty.Register<AlphaTab, IBrush>(nameof(BarCursorFill),
            new SolidColorBrush(MediaColor.FromArgb(64, 255, 242, 0)));

    /// <summary>
    /// Identifies the <see cref="BeatCursorFill"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IBrush> BeatCursorFillProperty =
        AvaloniaProperty.Register<AlphaTab, IBrush>(nameof(BeatCursorFill),
            new SolidColorBrush(MediaColor.FromArgb(191, 64, 64, 255)));

    /// <summary>
    /// Identifies the <see cref="SelectionFill"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectionFillProperty =
        AvaloniaProperty.Register<AlphaTab, IBrush>(nameof(SelectionFill),
            new SolidColorBrush(MediaColor.FromArgb(25, 64, 64, 255)));

    /// <summary>
    /// Identifies the <see cref="Api"/> direct property.
    /// </summary>
    public static readonly DirectProperty<AlphaTab, AlphaTabApiBase<AlphaTab>?> ApiProperty =
        AvaloniaProperty.RegisterDirect<AlphaTab, AlphaTabApiBase<AlphaTab>?>(
            nameof(Api),
            o => o.Api);

    private AlphaTabApiBase<AlphaTab>? _api;

    /// <see cref="AlphaTabApiBase{TSettings}.Tracks"/>
    public IEnumerable<Track>? Tracks
    {
        get => GetValue(TracksProperty);
        set => SetValue(TracksProperty, value);
    }

    /// <see cref="AlphaTabApiBase{TSettings}.Settings"/>
    public Settings Settings
    {
        get => GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for filling the bar cursor.
    /// </summary>
    public IBrush BarCursorFill
    {
        get => GetValue(BarCursorFillProperty);
        set => SetValue(BarCursorFillProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for filling the beat cursor.
    /// </summary>
    public IBrush BeatCursorFill
    {
        get => GetValue(BeatCursorFillProperty);
        set => SetValue(BeatCursorFillProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used to fill the selected area highlights.
    /// </summary>
    public IBrush SelectionFill
    {
        get => GetValue(SelectionFillProperty);
        set => SetValue(SelectionFillProperty, value);
    }

    /// <summary>
    /// Gets the alphaTab API object.
    /// </summary>
    public AlphaTabApiBase<AlphaTab>? Api
    {
        get => _api;
        private set => SetAndRaise(ApiProperty, ref _api, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaTab"/> class.
    /// </summary>
    public AlphaTab()
    {
        Settings = new Settings
        {
            Player =
            {
                PlayerMode = PlayerMode.EnabledAutomatic,
                EnableCursor = true
            }
        };

        _renderPanel = new Canvas();
        _overlayPanel = new Canvas
        {
            IsHitTestVisible = false
        };
        _layoutPanel = new Canvas();
        _scrollView = new ScrollViewer
        {
            Content = _layoutPanel,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        Content = _scrollView;

        Api = new AlphaTabApiBase<AlphaTab>(new AvaloniaUiFacade(_scrollView, _layoutPanel, _renderPanel, _overlayPanel), this);
        _layoutPanel.Children.Add(_overlayPanel);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TracksProperty)
        {
            OnTracksChanged((IEnumerable<Track>?)change.NewValue);
        }
        else if (change.Property == SettingsProperty && change.NewValue is Settings settings)
        {
            OnSettingsChanged(settings);
        }
        else if (change.Property == BarCursorFillProperty ||
                 change.Property == BeatCursorFillProperty ||
                 change.Property == SelectionFillProperty)
        {
            Api?.Render();
        }
    }

    private IEnumerable<Track>? _registeredTracks;

    private void OnTracksChanged(IEnumerable<Track>? tracks)
    {
        if (_registeredTracks is INotifyCollectionChanged oldObservable)
        {
            oldObservable.CollectionChanged -= OnTrackCollectionChanged;
        }

        _registeredTracks = tracks;
        if (_registeredTracks is INotifyCollectionChanged newObservable)
        {
            newObservable.CollectionChanged += OnTrackCollectionChanged;
        }

        RenderTracks();
    }

    private void OnTrackCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderTracks();
    }

    private void OnSettingsChanged(Settings settings)
    {
        SettingsChanged?.Invoke(settings);
    }

    /// <summary>
    /// Initiates a rendering of the currently configured tracks if the API object is ready.
    /// </summary>
    public void RenderTracks()
    {
        if (Api == null || Tracks == null)
        {
            return;
        }

        Score? score = null;
        var trackIndexes = new List<double>();
        foreach (var track in Tracks)
        {
            if (score == null)
            {
                score = track.Score;
            }

            if (score == track.Score)
            {
                trackIndexes.Add(track.Index);
            }
        }

        if (score != null)
        {
            Api.RenderScore(score, trackIndexes);
        }
    }

    /// <summary>
    /// Fired when the settings object changed.
    /// </summary>
    public event Action<Settings>? SettingsChanged;
}
