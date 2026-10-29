using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TreeToolKit;

public static unsafe class NativeImageIcon
{
    public static uint GetIconID(AtkImageNode* image)
    {
        if (image is null || image->PartsList is null || image->PartsList->Parts is null ||
            image->PartId >= image->PartsList->PartCount)
            return 0;

        var asset = image->PartsList->Parts[image->PartId].UldAsset;
        if (asset is null)
            return 0;

        ref var texture = ref asset->AtkTexture;
        return texture.TextureType == TextureType.Resource && texture.Resource is not null
            ? texture.Resource->IconId
            : 0;
    }
}
