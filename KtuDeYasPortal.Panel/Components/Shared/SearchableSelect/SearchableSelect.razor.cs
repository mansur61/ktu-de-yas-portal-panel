using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace KtuDeYasPortal.Panel.Components.Shared.SearchableSelect;

public abstract class SearchableSelectBase<TItem> : ComponentBase, IAsyncDisposable
{
    // ── Parameters ────────────────────────────────────────────────────────────
    [Parameter, EditorRequired] public IReadOnlyList<TItem> Items         { get; set; } = [];
    [Parameter, EditorRequired] public Func<TItem, string>  TextSelector  { get; set; } = _ => string.Empty;

    /// <summary>Seçilen eleman. İki yönlü bind için @bind-Value kullanın.</summary>
    [Parameter] public TItem?                  Value        { get; set; }
    [Parameter] public EventCallback<TItem?>   ValueChanged { get; set; }

    [Parameter] public string  Placeholder  { get; set; } = "Ara…";
    [Parameter] public bool    Disabled     { get; set; }
    [Parameter] public bool    AllowClear   { get; set; } = true;
    [Parameter] public bool    IsLoading    { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────
    protected bool   IsOpen          { get; private set; }
    protected string SearchText      { get; set; } = "";
    protected int    HighlightIndex  { get; private set; } = -1;
    protected ElementReference SearchInputRef;

    protected bool HasValue => Value is not null &&
        !(Value is string s && string.IsNullOrEmpty(s));

    protected string TriggerText =>
        HasValue ? TextSelector(Value!) : Placeholder;

    protected IEnumerable<TItem> FilteredItems =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Items
            : Items.Where(i =>
                Normalize(TextSelector(i)).Contains(Normalize(SearchText)));

    protected bool IsSelected(TItem item) =>
        Value is not null && EqualityComparer<TItem>.Default.Equals(item, Value);

    // ── Actions ───────────────────────────────────────────────────────────────
    protected async Task ToggleAsync()
    {
        if (Disabled) return;
        if (IsOpen) { await CloseAsync(); return; }
        IsOpen        = true;
        SearchText    = "";
        HighlightIndex = -1;
        await Task.Delay(50);
        try { await SearchInputRef.FocusAsync(); } catch { }
    }

    protected async Task CloseAsync()
    {
        IsOpen     = false;
        SearchText = "";
    }

    protected async Task SelectItemAsync(TItem item)
    {
        await CloseAsync();
        if (!EqualityComparer<TItem>.Default.Equals(item, Value))
            await ValueChanged.InvokeAsync(item);
    }

    protected async Task ClearAsync()
    {
        await ValueChanged.InvokeAsync(default);
        await CloseAsync();
    }

    protected void OnSearchInput(ChangeEventArgs e)
    {
        SearchText     = e.Value?.ToString() ?? "";
        HighlightIndex = -1;
    }

    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        var list = FilteredItems.ToList();
        switch (e.Key)
        {
            case "Escape":
                await CloseAsync();
                break;
            case "ArrowDown":
                if (!IsOpen) { await ToggleAsync(); break; }
                HighlightIndex = Math.Min(HighlightIndex + 1, list.Count - 1);
                break;
            case "ArrowUp":
                HighlightIndex = Math.Max(HighlightIndex - 1, 0);
                break;
            case "Enter":
                if (IsOpen && HighlightIndex >= 0 && HighlightIndex < list.Count)
                    await SelectItemAsync(list[HighlightIndex]);
                break;
        }
    }

    // ── Türkçe normalizasyon ──────────────────────────────────────────────────
    private static string Normalize(string s) =>
        s.ToLowerInvariant()
         .Replace('ı', 'i').Replace('İ', 'i')
         .Replace('ş', 's').Replace('Ş', 's')
         .Replace('ğ', 'g').Replace('Ğ', 'g')
         .Replace('ü', 'u').Replace('Ü', 'u')
         .Replace('ö', 'o').Replace('Ö', 'o')
         .Replace('ç', 'c').Replace('Ç', 'c');

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
