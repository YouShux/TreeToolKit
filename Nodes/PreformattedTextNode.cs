using KamiToolKit.Nodes;

namespace TreeToolKit;

/// <summary>
/// 由调用方显式排版的文本节点，尺寸变化不触发自动重排。
/// </summary>
public class PreformattedTextNode : TextNode
{
    protected override void OnSizeChanged()
    {
        // 保留调用方已生成的换行、缩进与原生文本缓冲。
    }
}
