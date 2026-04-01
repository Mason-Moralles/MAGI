using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Core.Mapping;

public static class ChannelMapper
{
    public static Channel ToDomain(ChannelDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Link = dto.Link,
        PublishMode = dto.PublishMode,
        IsActive = dto.IsActive,
        TimeZone = dto.TimeZone,
        DelayBetweenPosts = dto.DelayBetweenPosts,
        ArtsRootPath = dto.ArtsRootPath
    };
}