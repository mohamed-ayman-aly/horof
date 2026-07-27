using horof.Models;

namespace horof.Controls;

public class HexBoardView : GraphicsView
{
    /// <summary>Canvas aspect ratio Width:Height = 10000000000000000 : 8023758099352052.</summary>
    private const double AspectRatioWidth = 10_000_000_000_000_000d;
    private const double AspectRatioHeight = 8_023_758_099_352_052d;
    private static double TargetAspectRatio => AspectRatioWidth / AspectRatioHeight;

    private readonly HexBoardDrawable _drawable = new();
    private VisualElement? _sizedParent;
    private bool _applyingAspect;

    public static readonly BindableProperty CellsProperty =
        BindableProperty.Create(nameof(Cells), typeof(IReadOnlyList<HexCell>), typeof(HexBoardView),
            null, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int?), typeof(HexBoardView),
            null, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty CanSelectProperty =
        BindableProperty.Create(nameof(CanSelect), typeof(bool), typeof(HexBoardView), true,
            propertyChanged: OnVisualPropertyChanged);

    public event EventHandler<int>? HexSelected;

    public IReadOnlyList<HexCell>? Cells
    {
        get => (IReadOnlyList<HexCell>?)GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    public int? SelectedIndex
    {
        get => (int?)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public bool CanSelect
    {
        get => (bool)GetValue(CanSelectProperty);
        set => SetValue(CanSelectProperty, value);
    }

    public HexBoardView()
    {
        Drawable = _drawable;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Center;
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);
        SyncDrawable();
    }

    protected override void OnParentSet()
    {
        if (_sizedParent is not null)
            _sizedParent.SizeChanged -= OnParentSizeChanged;

        base.OnParentSet();

        _sizedParent = Parent as VisualElement;
        if (_sizedParent is not null)
        {
            _sizedParent.SizeChanged += OnParentSizeChanged;
            ApplyAspectRatio(_sizedParent.Width, _sizedParent.Height);
        }
    }

    private void OnParentSizeChanged(object? sender, EventArgs e)
    {
        if (_sizedParent is null)
            return;

        ApplyAspectRatio(_sizedParent.Width, _sizedParent.Height);
    }

    /// <summary>
    /// Fits the canvas inside the parent while locking W:H to
    /// 10000000000000000 : 8023758099352052.
    /// </summary>
    private void ApplyAspectRatio(double maxWidth, double maxHeight)
    {
        if (_applyingAspect || maxWidth <= 0 || maxHeight <= 0)
            return;

        var aspect = TargetAspectRatio;
        double width;
        double height;

        if (maxWidth / maxHeight > aspect)
        {
            height = maxHeight;
            width = height * aspect;
        }
        else
        {
            width = maxWidth;
            height = width / aspect;
        }

        if (Math.Abs(WidthRequest - width) < 0.5 && Math.Abs(HeightRequest - height) < 0.5)
            return;

        _applyingAspect = true;
        try
        {
            WidthRequest = width;
            HeightRequest = height;
        }
        finally
        {
            _applyingAspect = false;
        }
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is HexBoardView view)
        {
            view.SyncDrawable();
            view.Invalidate();
        }
    }

    private void SyncDrawable()
    {
        _drawable.Cells = Cells;
        _drawable.SelectedIndex = SelectedIndex;
        _drawable.CanSelect = CanSelect;
        _drawable.InvalidateLayout();
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!CanSelect || Cells is null)
            return;

        var point = e.GetPosition(this);
        if (point is null)
            return;

        var w = (float)(Width > 0 ? Width : 300);
        var h = (float)(Height > 0 ? Height : 300);
        var layoutRect = new RectF(0, 0, w, h);

        var tapX = (float)point.Value.X;
        if (IsEffectiveRtl())
            tapX = w - tapX;

        _drawable.Cells = Cells;
        if (_drawable.HitTest(new PointF(tapX, (float)point.Value.Y), layoutRect, out var index))
            HexSelected?.Invoke(this, index);
    }

    private bool IsEffectiveRtl()
    {
        for (var element = this as VisualElement; element is not null; element = element.Parent as VisualElement)
        {
            if (element.FlowDirection == FlowDirection.RightToLeft)
                return true;
            if (element.FlowDirection == FlowDirection.LeftToRight)
                return false;
        }

        return false;
    }
}
