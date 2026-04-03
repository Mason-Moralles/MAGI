-- MAGI: SQL validation queries for KT4
-- Target DB: data/magi.db (SQLite)
-- Replace placeholder values before running manually.

-- ==========================================
-- 0. PARAMETERS
-- ==========================================
-- Replace these placeholders in your SQL IDE:
-- :channel_id   -> test channel id
-- :channel_name -> expected channel name
-- :slot_caption -> expected schedule slot caption
-- :file_name    -> expected image file name
-- :source_url   -> expected download source URL

-- ==========================================
-- 1. CHANNEL CREATED
-- ==========================================

SELECT COUNT(*) AS channel_count
FROM Channels
WHERE Id = :channel_id;

SELECT Id, Name, Link, PublishMode, IsActive, TimeZone
FROM Channels
WHERE Id = :channel_id;

-- ==========================================
-- 2. DEFAULT CONFIGS CREATED
-- ==========================================

SELECT COUNT(*) AS parser_config_count
FROM ChannelParserConfigs
WHERE ChannelId = :channel_id;

SELECT COUNT(*) AS tagger_config_count
FROM ChannelTaggerConfigs
WHERE ChannelId = :channel_id;

SELECT ChannelId, ImagesPerHashtag, Sources
FROM ChannelParserConfigs
WHERE ChannelId = :channel_id;

SELECT ChannelId, RenameTemplate, Separator, Mode
FROM ChannelTaggerConfigs
WHERE ChannelId = :channel_id;

-- ==========================================
-- 3. FILENAME TAGS
-- ==========================================

SELECT COUNT(*) AS filename_tags_count
FROM FilenameTags
WHERE ChannelId = :channel_id;

SELECT Keyword, Tag, ChannelId
FROM FilenameTags
WHERE ChannelId = :channel_id
ORDER BY Keyword;

-- ==========================================
-- 4. IMAGE INSERTED
-- ==========================================

SELECT COUNT(*) AS image_count
FROM Images
WHERE FileName = :file_name
  AND ChannelId = :channel_id;

SELECT FileName, Person, Caption, ChannelId, Posted
FROM Images
WHERE FileName = :file_name
  AND ChannelId = :channel_id;

-- ==========================================
-- 5. DOWNLOAD RECORD INSERTED
-- ==========================================

SELECT COUNT(*) AS download_record_count
FROM DownloadRecords
WHERE SourceUrl = :source_url
  AND ChannelId = :channel_id;

SELECT Source, SourceUrl, FileName, Hashtag, ChannelId
FROM DownloadRecords
WHERE SourceUrl = :source_url
  AND ChannelId = :channel_id;

-- Duplicate control for one URL
SELECT SourceUrl, COUNT(*) AS duplicate_count
FROM DownloadRecords
WHERE SourceUrl = :source_url
GROUP BY SourceUrl;

-- ==========================================
-- 6. SCHEDULE SLOT CREATED
-- ==========================================

SELECT COUNT(*) AS schedule_slot_count
FROM ScheduleSlots
WHERE ChannelId = :channel_id
  AND Caption = :slot_caption;

SELECT IsoKey, Date, Time, Status, Caption, ChannelId
FROM ScheduleSlots
WHERE ChannelId = :channel_id
  AND Caption = :slot_caption;

-- Channel isolation check
SELECT ChannelId, COUNT(*) AS slot_count
FROM ScheduleSlots
GROUP BY ChannelId
ORDER BY ChannelId;

-- ==========================================
-- 7. CASCADE DELETE CHECKS
-- ==========================================

SELECT COUNT(*) AS channels_after_delete
FROM Channels
WHERE Id = :channel_id;

SELECT COUNT(*) AS parser_configs_after_delete
FROM ChannelParserConfigs
WHERE ChannelId = :channel_id;

SELECT COUNT(*) AS tagger_configs_after_delete
FROM ChannelTaggerConfigs
WHERE ChannelId = :channel_id;

SELECT COUNT(*) AS filename_tags_after_delete
FROM FilenameTags
WHERE ChannelId = :channel_id;

SELECT COUNT(*) AS schedule_slots_after_delete
FROM ScheduleSlots
WHERE ChannelId = :channel_id;

SELECT COUNT(*) AS posting_rules_after_delete
FROM PostingRules
WHERE ChannelId = :channel_id;

SELECT COUNT(*) AS images_after_delete
FROM Images
WHERE ChannelId = :channel_id;

SELECT COUNT(*) AS downloads_after_delete
FROM DownloadRecords
WHERE ChannelId = :channel_id;