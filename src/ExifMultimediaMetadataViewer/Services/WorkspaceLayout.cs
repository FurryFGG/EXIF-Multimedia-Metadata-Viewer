namespace ExifMultimediaMetadataViewer.Services;

public enum LayoutMode { Original, Columns, Stacked }

// Star weights are fractions of the usable area (splitters excluded), never screen pixels.
public sealed record PanelSizes(double Browser, double Preview, double Tree, bool Expanded = true)
{
    public static PanelSizes Defaults(LayoutMode mode) => mode switch
    {
        LayoutMode.Columns => new(.22, .56 / .78, .32),
        LayoutMode.Stacked => new(.25, .64, .32),
        _ => new(300, .49, 260)
    };
    public PanelSizes Checked(LayoutMode mode)
    {
        var defaults = Defaults(mode);
        static double Bound(double n, double min, double max, double fallback) =>
            double.IsFinite(n) ? Math.Clamp(n, min, max) : fallback;
        return this with { Browser = Bound(Browser, mode == LayoutMode.Original ? 150 : .08,
            mode == LayoutMode.Original ? 2400 : .8, defaults.Browser),
            Tree = Bound(Tree, mode == LayoutMode.Original ? 70 : .08, mode == LayoutMode.Original ? 1800 : .8, defaults.Tree),
            Preview = Bound(Preview,.08,.92,defaults.Preview) };
    }
}
public sealed record WorkspaceLayout(LayoutMode Mode, PanelSizes Original, PanelSizes Columns, PanelSizes Stacked)
{
    public static WorkspaceLayout Default => new(LayoutMode.Original, PanelSizes.Defaults(LayoutMode.Original),
        PanelSizes.Defaults(LayoutMode.Columns), PanelSizes.Defaults(LayoutMode.Stacked));
    public PanelSizes Active => Mode switch { LayoutMode.Columns => Columns, LayoutMode.Stacked => Stacked, _ => Original };
    public WorkspaceLayout WithActive(PanelSizes p) => Mode switch { LayoutMode.Columns => this with { Columns = p },
        LayoutMode.Stacked => this with { Stacked = p }, _ => this with { Original = p } };
    public WorkspaceLayout Checked() => new(Enum.IsDefined(Mode) ? Mode : LayoutMode.Original,
        Original?.Checked(LayoutMode.Original) ?? Default.Original, Columns?.Checked(LayoutMode.Columns) ?? Default.Columns,
        Stacked?.Checked(LayoutMode.Stacked) ?? Default.Stacked);
}
