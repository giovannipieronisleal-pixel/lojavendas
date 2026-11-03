namespace lojavendas.Views;

public sealed class ResponsiveGrid : Grid
{
    public static readonly BindableProperty MaximumColumnsProperty =
        BindableProperty.Create(
            nameof(MaximumColumns),
            typeof(int),
            typeof(ResponsiveGrid),
            4);

    public static readonly BindableProperty MinimumColumnWidthProperty =
        BindableProperty.Create(
            nameof(MinimumColumnWidth),
            typeof(double),
            typeof(ResponsiveGrid),
            220d);

    private int _configuredColumns;
    private int _configuredChildren = -1;

    public int MaximumColumns
    {
        get => (int)GetValue(MaximumColumnsProperty);
        set => SetValue(MaximumColumnsProperty, value);
    }

    public double MinimumColumnWidth
    {
        get => (double)GetValue(MinimumColumnWidthProperty);
        set => SetValue(MinimumColumnWidthProperty, value);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Children.Count == 0 || MaximumColumns <= 0 || MinimumColumnWidth <= 0)
            return;

        var columns = Math.Clamp(
            (int)Math.Floor((width + ColumnSpacing) / (MinimumColumnWidth + ColumnSpacing)),
            1,
            MaximumColumns);
        if (columns == _configuredColumns && Children.Count == _configuredChildren)
            return;

        _configuredColumns = columns;
        _configuredChildren = Children.Count;
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();

        for (var column = 0; column < columns; column++)
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var rows = (int)Math.Ceiling(Children.Count / (double)columns);
        for (var row = 0; row < rows; row++)
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var index = 0; index < Children.Count; index++)
        {
            if (Children[index] is not BindableObject child)
                continue;

            Grid.SetRow(child, index / columns);
            Grid.SetColumn(child, index % columns);
        }
    }
}
