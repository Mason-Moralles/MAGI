namespace MAGI.ApiGateway.Models;

/// <summary>
/// Слот расписания публикации.
/// </summary>
public class ScheduleSlotDto
{
    public string IsoKey { get; set; } = "";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending, scheduled, posted, missed, error
    public string? File { get; set; }
    public string? Person { get; set; }
    public string Caption { get; set; } = "";
}

/// <summary>
/// Запрос создания/обновления слота.
/// </summary>
public class ScheduleSlotRequest
{
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    public string? Caption { get; set; }
}

/// <summary>
/// Правило публикации.
/// </summary>
public class PostingRuleDto
{
    public string Time { get; set; } = "";
    public List<string> Days { get; set; } = new();
    public string Caption { get; set; } = "";
}

/// <summary>
/// Данные изображения в базе.
/// </summary>
public class ImageDto
{
    public string FileName { get; set; } = "";
    public string? Person { get; set; }
    public int Posted { get; set; }
    public string? PostTime { get; set; }
    public string Caption { get; set; } = "";
}
