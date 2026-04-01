using MAGI.Mobile.Core.Core.Abstractions;

namespace MAGI.Mobile.Platform;

public sealed class MauiShareService : IShareService
{
    public Task ShareTextAsync(string title, string text, CancellationToken cancellationToken = default)
    {
        return Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = title,
            Text = text
        });
    }
}