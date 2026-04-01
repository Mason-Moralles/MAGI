using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Core.Mapping;

public static class ImageMapper
{
    public static ImageItem ToDomain(ImageDto dto) => new()
    {
        FileName = dto.FileName,
        Person = dto.Person ?? string.Empty,
        Caption = dto.Caption,
        IsPosted = dto.Posted == 1,
        ChannelId = dto.ChannelId ?? string.Empty
    };
}