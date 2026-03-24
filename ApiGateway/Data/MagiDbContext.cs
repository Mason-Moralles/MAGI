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
        // Images: уникальный FileName
        modelBuilder.Entity<ImageEntity>()
            .HasIndex(e => e.FileName)
            .IsUnique();

        // PostedImages: уникальный FileName
        modelBuilder.Entity<PostedImageEntity>()
            .HasIndex(e => e.FileName)
            .IsUnique();

        // ScheduleSlots: уникальный IsoKey
        modelBuilder.Entity<ScheduleSlotEntity>()
            .HasIndex(e => e.IsoKey)
            .IsUnique();

        // DownloadRecords: уникальный SourceUrl
        modelBuilder.Entity<DownloadRecordEntity>()
            .HasIndex(e => e.SourceUrl)
            .IsUnique();

        // Channels: индекс по NetworkId
        modelBuilder.Entity<ChannelEntity>()
            .HasIndex(e => e.NetworkId);

        // PostingRules: индекс по ChannelId
        modelBuilder.Entity<PostingRuleEntity>()
            .HasIndex(e => e.ChannelId);

        // ChannelParserConfigs: уникальный ChannelId (1:1)
        modelBuilder.Entity<ChannelParserConfigEntity>()
            .HasIndex(e => e.ChannelId)
            .IsUnique();

        // ChannelTaggerConfigs: уникальный ChannelId (1:1)
        modelBuilder.Entity<ChannelTaggerConfigEntity>()
            .HasIndex(e => e.ChannelId)
            .IsUnique();

        // FilenameTags: индекс по ChannelId (1:many)
        modelBuilder.Entity<FilenameTagEntity>()
            .HasIndex(e => e.ChannelId);
    }
}
