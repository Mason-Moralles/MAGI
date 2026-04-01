namespace MAGI.Mobile.Core.Core.Abstractions;

public interface IShareService
{
    Task ShareTextAsync(string title, string text, CancellationToken cancellationToken = default);
}