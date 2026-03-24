using Microsoft.EntityFrameworkCore;

namespace MAGI.ApiGateway.Data;

/// <summary>
/// EF Core контекст базы данных MAGI.
/// </summary>
public class MagiDbContext : DbContext
{
    public MagiDbContext(DbContextOptions<MagiDbContext> options) : base(options) { }

    public DbSet<ImageEntity> Images => Set<ImageEntity>();
    public DbSet<PostedImageEntity> PostedImages => Set<PostedImageEntity>();
    public DbSet<ScheduleSlotEntity> ScheduleSlots => Set<ScheduleSlotEntity>();
    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();
    public DbSet<ChannelNetworkEntity> ChannelNetworks => Set<ChannelNetworkEntity>();
    public DbSet<PostingRuleEntity> PostingRules => Set<PostingRuleEntity>();
    public DbSet<DownloadRecordEntity> DownloadRecords => Set<DownloadRecordEntity>();
    public DbSet<ChannelParserConfigEntity> ChannelParserConfigs => Set<ChannelParserConfigEntity>();
    public DbSet<ChannelTaggerConfigEntity> ChannelTaggerConfigs => Set<ChannelTaggerConfigEntity>();
    public DbSet<FilenameTagEntity> FilenameTags => Set<FilenameTagEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ─── Unique Indexes ───

        // Images: уникальный FileName
        modelBuilder.Entity<ImageEntity>()
            .HasIndex(e => e.FileName)
            .IsUnique();

        // PostedImages: уникальный FileName
        modelBuilder.Entity<PostedImageEntity>()
            .HasIndex(e => e.FileName)
            .IsUnique();

        // ScheduleSlots: уникальный (IsoKey + ChannelId) — разные каналы могут иметь слоты в одно время
        modelBuilder.Entity<ScheduleSlotEntity>()
            .HasIndex(e => new { e.IsoKey, e.ChannelId })
            .IsUnique();

        // Индекс по IsoKey (не уникальный) для быстрого поиска
        modelBuilder.Entity<ScheduleSlotEntity>()
            .HasIndex(e => e.IsoKey);

        // Индекс по ChannelId для фильтрации
        modelBuilder.Entity<ScheduleSlotEntity>()
            .HasIndex(e => e.ChannelId);

        // DownloadRecords: уникальный SourceUrl
        modelBuilder.Entity<DownloadRecordEntity>()
            .HasIndex(e => e.SourceUrl)
            .IsUnique();

        // ─── FK: Channel → Network (логическая, без каскада) ───

        modelBuilder.Entity<ChannelEntity>()
            .HasIndex(e => e.NetworkId);

        // ─── FK: ChannelParserConfigs → Channels (1:1) ───

        modelBuilder.Entity<ChannelParserConfigEntity>()
            .HasIndex(e => e.ChannelId)
            .IsUnique();

        modelBuilder.Entity<ChannelParserConfigEntity>()
            .HasOne<ChannelEntity>()
            .WithOne()
            .HasForeignKey<ChannelParserConfigEntity>(e => e.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── FK: ChannelTaggerConfigs → Channels (1:1) ───

        modelBuilder.Entity<ChannelTaggerConfigEntity>()
            .HasIndex(e => e.ChannelId)
            .IsUnique();

        modelBuilder.Entity<ChannelTaggerConfigEntity>()
            .HasOne<ChannelEntity>()
            .WithOne()
            .HasForeignKey<ChannelTaggerConfigEntity>(e => e.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── FK: FilenameTags → Channels (1:N) ───

        modelBuilder.Entity<FilenameTagEntity>()
            .HasIndex(e => e.ChannelId);

        modelBuilder.Entity<FilenameTagEntity>()
            .HasOne<ChannelEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── FK: PostingRules → Channels (1:N, optional) ───

        modelBuilder.Entity<PostingRuleEntity>()
            .HasIndex(e => e.ChannelId);

        modelBuilder.Entity<PostingRuleEntity>()
            .HasOne<ChannelEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChannelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
