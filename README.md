# TreeToolKit

TreeToolKit 集中管理原生窗口贴附、节点处理、浮层、窗口控制和字体资源，避免每个模块重复维护 KamiToolKit 细节。

| 项目 | 当前值 |
| --- | --- |
| 版本 | `1.0.0` |
| 目标 | .NET 10 Windows、Dalamud API 15 |
| 源码依赖 | TreeTools、Omni 固定 KamiToolKit 分支、OmenTools |
| Omni 中用途 | 原生界面模块、字体和窗口辅助 |

## 它补上了什么

| 能力 | 说明 |
| --- | --- |
| 原生窗口 | 贴附、遮挡判断、Escape 关闭和选择确认 |
| 节点处理 | 安全解除、销毁、Tooltip 和节点列表修复 |
| 浮层 | 多个组件共享一套 Overlay 控制器 |
| 缩放恢复 | 保留 Kami 窗口的用户缩放设置 |
| 字体 | 游戏字体、安装字体和 Omni UI 字体句柄 |

TreeToolKit 与 [TreeTools](https://github.com/YouShux/TreeTools) 的职责不同：TreeTools 管理生命周期和任务，TreeToolKit 管理 Kami 原生界面资源。模块自己的窗口、节点和业务仍留在模块内。

## 原生界面链路

```text
Omni 模块
├── TreeTools：启用作用域、任务、退出等待
└── TreeToolKit：窗口、节点、浮层、字体
    ├── KamiToolKit
    └── OmenTools
```

先完成 KamiToolKit 初始化，再在框架线程创建 TreeToolKit 上下文：

```csharp
TreeToolKit.Runtime? toolkit = null;

await framework.RunOnFrameworkThread(() =>
{
    toolkit = TreeToolKit.Runtime.Initialize(
        framework,
        pluginInterface.UiBuilder,
        addonLifecycle,
        name => name.StartsWith("MyPlugin", StringComparison.Ordinal),
        exception => pluginLog.Error(exception, "TreeToolKit 初始化失败。"));
});
```

`ownsAddon` 用于筛选本插件拥有的原生窗口。退出时先释放模块窗口，再等待 TreeToolKit，最后释放 KamiToolKit：

```csharp
if (toolkit is not null)
    await toolkit.DisposeAsync();
```

## 依赖边界

| 使用场景 | 需要做什么 |
| --- | --- |
| Omni 内使用 | Omni 已携带 TreeToolKit、TreeTools、KamiToolKit 和 OmenTools |
| 在线 `.cs` 模块 | Omni 自动提供编译引用，作者只导入 `TreeToolKit` 命名空间 |
| 在线 `.dll` 模块 | 使用与 Omni 相同版本的依赖编译，不打包 KamiToolKit 等共享 DLL |
| 独立项目使用 | 当前版必须提供 TreeTools、一个兼容的 KamiToolKit 程序集和 OmenTools |

TreeToolKit 以 KamiToolKit 为核心。Omni 使用自己固定的 [KamiToolKit](https://github.com/YouShux/KamiToolKit) 分支构建和发布，不额外引用上游 KamiToolKit 程序集。当前字体与少量辅助功能还使用 OmenTools。

## 系统架构中的位置

| 项目 | 衔接方式 |
| --- | --- |
| [OmniToolbox.Common](https://github.com/YouShux/OmniToolbox.Common) | 为模块提供契约、配置和共享界面 |
| `OmniToolbox.Tree` | 私有功能模块，可按需使用 TreeToolKit |
| [OmniToolbox.TreePublic](https://github.com/YouShux/OmniToolbox.TreePublic) | 公开功能模块，可按需使用 TreeToolKit |
| [TreeTools](https://github.com/YouShux/TreeTools) | 提供生命周期、任务和通用运行能力 |
| [Localizations](https://github.com/YouShux/OmniToolbox.Localizations) | 提供模块显示文字 |
| [TreeHouseOnline](https://github.com/YouShux/OmniToolbox.TreeHouseOnline) | 展示生命周期、配置、命令和发布清单 |
