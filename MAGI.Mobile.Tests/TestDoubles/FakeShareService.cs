using MAGI.Mobile.Core.Core.Abstractions;

namespace MAGI.Mobile.Tests.TestDoubles;

internal sealed class FakeShareService : IShareService
{
    public string LastSharedText { get; private set; } = string.Empty;

    public Task ShareTextAsync(string title, string text, CancellationToken cancellationToken = default)
    {
        LastSharedText = $"{title}|{text}";
        return Task.CompletedTask;
    }
}