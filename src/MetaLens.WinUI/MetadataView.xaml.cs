using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ExifMetadata.Core.Models;
using ExifMultimediaMetadataViewer.Services;

namespace MetaLens.WinUI;
public sealed record SummaryRow(string Label, string Value, string Source);
public sealed partial class MetadataView : UserControl
{
    internal MediaAnalysis? Data;
    string preview = "", raw = "";
    int page, searchRev;
    const int Chunk = 6000;
    public MetadataView()
    {
        InitializeComponent();
    }

    internal void Set(MediaAnalysis? value, string status)
    {
        Data = value;
        preview = status;
        raw = "";
        page = 0;
        Summary.ItemsSource = null;
        Fields.ItemsSource = null;
        RawText.Text = "";
        Update();
    }

    internal void PreviewStatus(string status)
    {
        preview = status;
        if (Tabs.SelectedIndex == 0)
            Update();
    }

    internal string FieldLine(MetadataField f) => string.Join("\t", f.Source, f.FullPath, f.TagName, f.Group, f.TagId, f.DataType, f.OccurrenceIndex, f.FormattedValue, f.RawValue, f.RawJson);
    internal string Selection() => Tabs.SelectedIndex switch
    {
        0 when Summary.SelectedItem is SummaryRow r => $"{r.Label}\t{r.Value}\t{r.Source}",
        1 when Fields.SelectedItem is MetadataField f => FieldLine(f),
        2 => RawText.SelectedText,
        _ => ""
    };
    internal string All() => Data == null ? "" : Tabs.SelectedIndex == 2 ? Raw() : string.Join(Environment.NewLine, Data.AllFields.Select(FieldLine));
    void TabChange(object s, SelectionChangedEventArgs e)
    {
        if (RawPage != null)
            Update();
    }

    void Update()
    {
        Summary.Visibility = Tabs.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        FieldPage.Visibility = Tabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        RawPage.Visibility = Tabs.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        if (Data == null)
            return;
        switch (Tabs.SelectedIndex)
        {
            case 0:
                Summary.ItemsSource = MetadataSummaryBuilder.Build(Data, preview).SelectMany(sec => sec.Fields.Select(f => new SummaryRow(sec.Title + " · " + f.LabelZh + " / " + f.LabelEn, f.Value, f.SourcePath))).ToArray();
                break;
            case 1:
                FilterFields();
                break;
            case 2:
                ShowRaw();
                break;
        }
    }

    async void SearchChanged(object s, TextChangedEventArgs e)
    {
        int r = ++searchRev;
        await Task.Delay(150);
        if (r == searchRev && Tabs.SelectedIndex == 1)
            FilterFields();
    }

    async void FilterFields()
    {
        int r = ++searchRev;
        var data = Data;
        if (data == null)
            return;
        var q = Search.Text.Trim();
        if (q.Length == 0)
        {
            Fields.ItemsSource = data.AllFields;
            return;
        }

        var items = await Task.Run(() => data.AllFields.Where(f => f.TagName.Contains(q, StringComparison.OrdinalIgnoreCase) || f.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase) || f.Source.Contains(q, StringComparison.OrdinalIgnoreCase) || f.RawValue.Contains(q, StringComparison.OrdinalIgnoreCase) || f.FormattedValue.Contains(q, StringComparison.OrdinalIgnoreCase) || (f.TagId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToArray());
        if (r == searchRev && Data == data)
            Fields.ItemsSource = items;
    }

    string Raw()
    {
        if (raw.Length > 0 || Data == null)
            return raw;
        return raw = string.Join("\n\n", new[] { Data.ExifTool, Data.FFprobe }.Select(p => $"{p.ParserName} {p.Version} · {p.Status} · exit={p.ExitCode} · {p.Elapsed}\n{p.Message}\n{p.StandardError}\n{p.RawJson}"));
    }

    void ShowRaw()
    {
        var text = Raw();
        page = Math.Clamp(page, 0, Math.Max(0, (text.Length - 1) / Chunk));
        RawText.Text = text.Substring(page * Chunk, Math.Min(Chunk, text.Length - page * Chunk));
        PageNo.Text = $"{page + 1} / {Math.Max(1, (text.Length + Chunk - 1) / Chunk)}";
    }

    void PrevRaw(object s, RoutedEventArgs e)
    {
        page--;
        ShowRaw();
    }

    void NextRaw(object s, RoutedEventArgs e)
    {
        page++;
        ShowRaw();
    }
}
