using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TreeToolKit;

public readonly record struct NativeTextureRegion(nint Handle, Vector2 UVMinimum, Vector2 UVMaximum)
{
    public ImTextureID TextureID => new(Handle);

    // 纹理由游戏持有，返回值仅供当前绘制帧使用。
    public static unsafe bool TryRead(AtkUldPart* part, Vector2 atlasSize, out NativeTextureRegion region)
    {
        region = default;
        if (part is null || part->UldAsset is null || part->Width == 0 || part->Height == 0 ||
            atlasSize.X <= 0f || atlasSize.Y <= 0f)
            return false;

        var texture = &part->UldAsset->AtkTexture;
        if (!texture->IsTextureReady() || texture->TextureType != TextureType.Resource || texture->Resource is null)
            return false;

        var nativeTexture = texture->Resource->KernelTextureObject;
        if (nativeTexture is null || nativeTexture->D3D11ShaderResourceView is null ||
            nativeTexture->ActualWidth == 0 || nativeTexture->ActualHeight == 0 ||
            nativeTexture->ActualWidth * atlasSize.Y != nativeTexture->ActualHeight * atlasSize.X)
            return false;

        var minimum = new Vector2(part->U, part->V);
        var maximum = minimum + new Vector2(part->Width, part->Height);
        if (maximum.X > atlasSize.X || maximum.Y > atlasSize.Y)
            return false;

        region = new((nint)nativeTexture->D3D11ShaderResourceView, minimum / atlasSize, maximum / atlasSize);
        return true;
    }
}
