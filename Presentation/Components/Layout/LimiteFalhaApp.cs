using Microsoft.AspNetCore.Components.Web;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.Components.Layout;

public sealed class LimiteFalhaApp : ErrorBoundary
{
    protected override Task OnErrorAsync(Exception exception)
    {
        FalhasApp.Tratar(exception, "Interface Blazor");
        return Task.CompletedTask;
    }
}
