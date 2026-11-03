# TreeToolKit

TreeToolKit 集中管理原生窗口贴附、节点处理、浮层、窗口控制和字体资源，避免每个模块重复维护 KamiToolKit 细节。

| 项目 | 当前值 |
| --- | --- |
| 版本 | `1.0.0` |
| 目标 | .NET 10 Windows、Dalamud API 15 |
| 源码依赖 | TreeTools、上游 KamiToolKit 固定提交、OmenTools |
| Omni 中用途 | 原生界面模块、字体和窗口辅助 |

## 它补上了什么

| 能力 | 说明 |
| --- | --- |
| 原生窗口 | 贴附、遮挡判断、Escape 关闭和选择确认 |
| 节点处理 | 安全解除、销毁、Tooltip 和节点列表修复 |
| 浮层 | 多个组件共享 Overlay 控制器，地图浮层支持可配置隐藏键 |
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

挂接到游戏窗口的节点通过 `NativeNodeDetach` 在框架线程解除并释放，随后在同帧重建窗口节点列表。组件节点的原生 `Destroy` 只销毁节点本身；适配递归释放额外托管子节点，并将碰撞根从组件的托管递归与 ULD 对象表移出，保持它存活到组件的 `Deinitialize` 和析构结束，再清除旧父指针与组件指针、释放碰撞根。不能提前释放组件析构仍会访问的内部根，也不能仅清除托管登记表。

`KamiToolKitPluginInterface.Create` 在 Kami 初始化前为其 JSON 选项设置独立的类型信息 resolver。宿主最终通过该适配的 `DisposeAsync` 等待 Kami 释放，并记录剩余节点和本加载上下文的共享节点类型数。以上兼容依赖固定的 Kami 成员布局；不兼容时明确失败，不修改上游源码。

全部绘制入口退出后，`SharedManifestTextures.Release` 定向移除宿主纹理缓存中属于本 collectible 加载上下文的 `(Assembly, 资源名)` 条目，并沿宿主 `ReleaseSelfReference` 释放缓存自身的引用。仍被借用的纹理保留各自引用；不强制销毁共享纹理。适配核对当前 Dalamud 缓存及引用计数方法布局，不兼容时由宿主记录清理跳过原因。

## 依赖边界

| 使用场景 | 需要做什么 |
| --- | --- |
| Omni 内使用 | Omni 已携带 TreeToolKit、TreeTools、KamiToolKit 和 OmenTools |
| 在线 `.cs` 模块 | Omni 自动提供编译引用，作者只导入 `TreeToolKit` 命名空间 |
| 在线 `.dll` 模块 | 使用与 Omni 相同版本的依赖编译，不打包 KamiToolKit 等共享 DLL |
| 独立项目使用 | 当前版必须提供 TreeTools、一个兼容的 KamiToolKit 程序集和 OmenTools |

TreeToolKit 以 KamiToolKit 为核心。Omni 固定使用 [上游 KamiToolKit](https://github.com/MidoriKami/KamiToolKit) 提交构建和发布，不修改子模块源码，也不额外引用其他 KamiToolKit 程序集。地图浮层隐藏键、节点安全处理和窗口兼容等 Omni 差分在本库实现；当前字体与少量辅助功能还使用 OmenTools。

## 系统架构中的位置

| 项目 | 衔接方式 |
| --- | --- |
| [OmniToolbox.Common](https://github.com/YouShux/OmniToolbox.Common) | 为模块提供契约、配置和共享界面 |
| `OmniToolbox.Tree` | 私有功能模块，可按需使用 TreeToolKit |
| [OmniToolbox.TreePublic](https://github.com/YouShux/OmniToolbox.TreePublic) | 公开功能模块，可按需使用 TreeToolKit |
| [TreeTools](https://github.com/YouShux/TreeTools) | 提供生命周期、任务和通用运行能力 |
| [Localizations](https://github.com/YouShux/OmniToolbox.Localizations) | 提供模块显示文字 |
| [TreeHouseOnline](https://github.com/YouShux/OmniToolbox.TreeHouseOnline) | 展示生命周期、配置、命令和发布清单 |
