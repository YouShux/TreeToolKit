using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools.Extensions;

namespace TreeToolKit;

public static unsafe class SelectYesnoConfirmation
{
    // 调用方在游戏线程校验窗口归属后重试调用；每个新窗口将 checkboxSent 重置为 false。
    // true 仅表示已发送确认，业务结果仍由调用方等待；勾选后返回 false，留待后续更新确认。
    public static bool TryConfirmWithCheckbox(AddonSelectYesno* addon, ref bool checkboxSent)
    {
        if (addon == null || !addon->AtkUnitBase.IsAddonAndNodesReady())
            return false;

        var confirm = addon->ConfirmCheckBox;
        if (confirm != null && confirm->AtkResNode != null &&
            confirm->AtkResNode->IsVisible() && !confirm->IsChecked)
        {
            if (checkboxSent || confirm->OwnerNode == null)
                return false;

            checkboxSent = true;
            // 通知窗口前先修改控件状态，原生点击通知本身不会勾选控件。
            confirm->SetChecked(true);
            addon->AtkUnitBase.ClickComponent(confirm->OwnerNode, 3, AtkEventType.ButtonClick);
            return false;
        }

        if (addon->YesButton == null || !addon->YesButton->IsEnabled)
            return false;

        addon->AtkUnitBase.Callback(0);
        return true;
    }
}
