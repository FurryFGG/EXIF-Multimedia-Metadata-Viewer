using System.Collections.ObjectModel;

namespace ExifMultimediaMetadataViewer.ViewModels;

public sealed class DirectoryTreeNode : ObservableObject
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isLoading;

    public DirectoryTreeNode(string fullPath, string displayName, bool mayHaveChildren = true, bool isPlaceholder = false)
    {
        FullPath = fullPath;
        DisplayName = displayName;
        IsPlaceholder = isPlaceholder;
        Children = [];
        if (mayHaveChildren && !isPlaceholder)
            Children.Add(CreatePlaceholder());
    }

    public string FullPath { get; }
    public string DisplayName { get; }
    public bool IsPlaceholder { get; }
    public ObservableCollection<DirectoryTreeNode> Children { get; }
    public bool ChildrenLoaded { get; private set; }
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    public void BeginLoading()
    {
        IsLoading = true;
        Children.Clear();
    }

    public void CompleteLoading(IEnumerable<DirectoryTreeNode> children)
    {
        Children.Clear();
        foreach (var child in children)
            Children.Add(child);
        ChildrenLoaded = true;
        IsLoading = false;
    }

    public void FailLoading()
    {
        Children.Clear();
        ChildrenLoaded = true;
        IsLoading = false;
    }

    private static DirectoryTreeNode CreatePlaceholder() => new("", "正在读取…", false, true);
}
