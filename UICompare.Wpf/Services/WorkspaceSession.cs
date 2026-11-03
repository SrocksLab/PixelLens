using System.Collections.ObjectModel;
using UICompare.Wpf.Models;
namespace UICompare.Wpf.Services;
// Stable groups are the single source of truth for both lists and comparison.
public sealed class WorkspaceSession
{
    public ObservableCollection<ComparisonGroup> Groups { get; } = [];
    public ComparisonGroup? Selected { get; private set; }
    public void Select(ComparisonGroup? group)
    {
        if (group != null && !Groups.Contains(group)) throw new ArgumentException("分组不存在");
        Selected = group;
    }
    public void Import(IReadOnlyList<ImageAsset> images, bool design, bool replaceSelected = false)
    {
        if (images.Count == 0) return;
        ComparisonGroup? first = null;
        foreach (var image in images)
        {
            var target = images.Count == 1 && replaceSelected ? Selected : null;
            target ??= Selected != null && (design ? Selected.Design == null : Selected.Shot == null) ? Selected : null;
            if (!(design && Selected == null)) target ??= Groups.FirstOrDefault(g => design ? g.Design == null : g.Shot == null);
            if (target == null) { target = new ComparisonGroup(); Groups.Add(target); }
            if (design) target.Design = image; else target.Shot = image;
            target.AssetsChanged(); first ??= target;
        }
        Selected = first;
    }
    public void MoveShot(ComparisonGroup source, ComparisonGroup target)
    {
        if (source == target || source.Shot == null || !Groups.Contains(source) || !Groups.Contains(target)) return;
        (source.Shot, target.Shot) = (target.Shot, source.Shot);
        source.AssetsChanged(); target.AssetsChanged(); Selected = target;
    }
    public void ClearShot(ComparisonGroup group)
    {
        if(!Groups.Contains(group))return;
        group.Shot=null;group.AssetsChanged();Selected=group;
    }
    public void Delete(ComparisonGroup group)
    {
        var index = Groups.IndexOf(group); if (index < 0) return;
        Groups.RemoveAt(index);
        if (Selected == group) Selected = Groups.Count == 0 ? null : Groups[Math.Min(index, Groups.Count - 1)];
    }
}
