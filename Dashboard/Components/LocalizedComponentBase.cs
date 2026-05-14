using Dashboard.Services;
using Microsoft.AspNetCore.Components;

namespace Dashboard.Components;

public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected LanguageService Language { get; set; } = null!;

    protected override void OnInitialized()
    {
        Language.OnChange += Refresh;
    }

    private void Refresh() => InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        Language.OnChange -= Refresh;
        GC.SuppressFinalize(this);
    }
}
