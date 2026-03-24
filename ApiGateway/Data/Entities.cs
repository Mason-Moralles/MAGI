using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MAGI.ApiGateway.Data;

/// <summary>
/// Изображение в базе (бывший images.json).
/// </summary>
[Table("Images")]
public class ImageEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>Имя файла (уникальный идентификатор арта).</summary>
    [Required, MaxLength(500)]
    public string FileName { get; set; } = "";

    /// <summary>Хэштег персонажа (#Asuka_Langley и т.д.).</summary>
    [MaxLength(200)]
    public string? Person { get; set; }

    /// <summary>0 — не опубликован, 1 — опубликован.</summary>
    public int Posted { get; set; }

    /// <summary>Подпись к арту.</summary>
    [MaxLength(1000)]
    public string Caption { get; set; } = "";

    /// <summary>Время публикации (ISO).</summary>
    [MaxLength(50)]
    public string? PostTime { get; set; }

    /// <summary>ID канала, к которому привязан арт (для масштабирования).</summary>
    [MaxLength(50)]
    public string? ChannelId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Опубликованное изображение (бывший posted_images.json).
/// </summary>
[Table("PostedImages")]
public class PostedImageEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public string FileName { get; set; } = "";

    [MaxLength(200)]
    public string? Person { get; set; }

    /// <summary>Время публикации (ISO).</summary>
    [MaxLength(50)]
    public string? PostedAt { get; set; }

    [MaxLength(1000)]
    public string Caption { get; set; } = "";

    [MaxLength(50)]
    public string? ChannelId { get; set; }
}

/// <summary>
/// Слот расписания (бывший schedule.json).
/// </summary>
[Table("ScheduleSlots")]
public class ScheduleSlotEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>ISO datetime ключ (2026-02-17T07:29:00+03:00).</summary>
    [Required, MaxLength(50)]
    public string IsoKey { get; set; } = "";

    [MaxLength(20)]
    public string Date { get; set; } = "";

    [MaxLength(10)]
    public string Time { get; set; } = "";

    /// <summary>pending, scheduled, posted, missed, error.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending";

    [MaxLength(500)]
    public string? File { get; set; }

    [MaxLength(200)]
    public string? Person { get; set; }

    [MaxLength(1000)]
    public string Caption { get; set; } = "";

    [MaxLength(50)]
    public string? ChannelId { get; set; }
}

/// <summary>
/// Telegram-канал с индивидуальными API-креденшалами.
/// </summary>
[Table("Channels")]
public class ChannelEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = "";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    /// <summary>Ссылка на канал (напр. @my_channel или -1001234567890).</summary>
    [Required, MaxLength(200)]
    public string Link { get; set; } = "";

    [MaxLength(50)]
    public string? NetworkId { get; set; }

    /// <summary>Режим публикации: user (Telethon) или bot (Bot API).</summary>
    [MaxLength(20)]
    public string PublishMode { get; set; } = "user";

    public bool IsActive { get; set; } = true;

    // ─── Telegram API Credentials (per-channel) ───

    /// <summary>Telegram API ID (из https://my.telegram.org).</summary>
    public int? ApiId { get; set; }

    /// <summary>Telegram API Hash.</summary>
    [MaxLength(100)]
    public string? ApiHash { get; set; }

    /// <summary>Bot Token (для режима bot).</summary>
    [MaxLength(200)]
    public string? BotToken { get; set; }

    /// <summary>Имя файла Telethon-сессии (без расширения).</summary>
    [MaxLength(200)]
    public string? SessionFile { get; set; }

    /// <summary>Часовой пояс канала (напр. Europe/Moscow).</summary>
    [MaxLength(50)]
    public string TimeZone { get; set; } = "Europe/Moscow";

    /// <summary>Задержка между постами в секундах.</summary>
    public int DelayBetweenPosts { get; set; } = 5;

    /// <summary>Путь к корневой папке артов канала (напр. D:\MAGI-Images\channel_1).</summary>
    [MaxLength(500)]
    public string ArtsRootPath { get; set; } = "";
}

/// <summary>
/// Конфигурация парсера для конкретного канала.
/// </summary>
[Table("ChannelParserConfigs")]
public class ChannelParserConfigEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>ID канала.</summary>
    [Required, MaxLength(50)]
    public string ChannelId { get; set; } = "";

    /// <summary>Хэштеги для поиска (JSON array).</summary>
    public string Hashtags { get; set; } = "[]";

    /// <summary>Негативные хэштеги (JSON array).</summary>
    public string NegativeHashtags { get; set; } = "[]";

    /// <summary>Изображений на хэштег.</summary>
    public int ImagesPerHashtag { get; set; } = 50;

    /// <summary>Задержка скролла (мс).</summary>
    public int ScrollDelayMs { get; set; } = 2000;

    /// <summary>Задержка загрузки изображения (мс).</summary>
    public int ImageLoadDelayMs { get; set; } = 1000;

    /// <summary>Источники через запятую (pinterest,pixiv).</summary>
    [MaxLength(200)]
    public string Sources { get; set; } = "pinterest";
}

/// <summary>
/// Конфигурация теггера для конкретного канала.
/// </summary>
[Table("ChannelTaggerConfigs")]
public class ChannelTaggerConfigEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>ID канала.</summary>
    [Required, MaxLength(50)]
    public string ChannelId { get; set; } = "";

    /// <summary>Шаблон переименования.</summary>
    [MaxLength(500)]
    public string RenameTemplate { get; set; } = "{artist}_{title}_{id}";

    /// <summary>Разделитель тегов.</summary>
    [MaxLength(10)]
    public string Separator { get; set; } = "_";

    /// <summary>Обрабатывать только новые файлы.</summary>
    public bool OnlyNew { get; set; } = true;

    /// <summary>Режим: rename или copy.</summary>
    [MaxLength(20)]
    public string Mode { get; set; } = "rename";
}

/// <summary>
/// Тег по ключевому слову в имени файла (per-channel).
/// Keyword "asuka" → Tag "#Asuka_Langley"
/// </summary>
[Table("FilenameTags")]
public class FilenameTagEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>Ключевое слово для поиска в имени файла (lowercase).</summary>
    [Required, MaxLength(100)]
    public string Keyword { get; set; } = "";

    /// <summary>Тег/хэштег персонажа (#Asuka_Langley).</summary>
    [Required, MaxLength(200)]
    public string Tag { get; set; } = "";

    /// <summary>ID канала.</summary>
    [Required, MaxLength(50)]
    public string ChannelId { get; set; } = "";
}

/// <summary>
/// Сеть каналов.
/// </summary>
[Table("ChannelNetworks")]
public class ChannelNetworkEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = "";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
}

/// <summary>
/// Правило публикации (бывший posting_rules.json → rules[]).
/// </summary>
[Table("PostingRules")]
public class PostingRuleEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>Время публикации HH:MM.</summary>
    [Required, MaxLength(10)]
    public string Time { get; set; } = "";

    /// <summary>Дни недели через запятую (Monday,Tuesday,...).</summary>
    [Required, MaxLength(200)]
    public string Days { get; set; } = "";

    [MaxLength(1000)]
    public string Caption { get; set; } = "";

    [MaxLength(50)]
    public string? ChannelId { get; set; }
}

/// <summary>
/// Запись о скачанном изображении (бывший Pinterest/Pixiv_downloaded_images.json).
/// </summary>
[Table("DownloadRecords")]
public class DownloadRecordEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>Источник: pinterest, pixiv.</summary>
    [Required, MaxLength(50)]
    public string Source { get; set; } = "";

    /// <summary>URL пина или арта (уникальный).</summary>
    [Required, MaxLength(2000)]
    public string SourceUrl { get; set; } = "";

    /// <summary>URL изображения.</summary>
    [MaxLength(2000)]
    public string ImageUrl { get; set; } = "";

    [Required, MaxLength(500)]
    public string FileName { get; set; } = "";

    [MaxLength(200)]
    public string Hashtag { get; set; } = "";

    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>ID канала, для которого скачан арт.</summary>
    [MaxLength(50)]
    public string? ChannelId { get; set; }
}
