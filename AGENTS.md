# MeloongCore 仓库指南

MeloongCore 是 PCLA 的基础库，提供日志、配置、文件路径、并发与 WPF 对话框等通用能力。`PCLA` 与 `PCLCS` 都引用它，本仓库保持独立，不依赖引用方的业务代码、私有目录或开发流程。

## 运行事实

| 项目 | 目标框架 | NuGet 依赖 |
| --- | --- | --- |
| `Shared` | .NET Standard 2.0 | Microsoft.Win32.Registry、System.Text.Json、ThrottleDebounce、PolySharp |
| `Wpf` | .NET Framework 4.8 | Newtonsoft.Json、Ookii.Dialogs.Wpf、PolySharp |
| `Tests` | .NET Framework 4.8 | TUnit、System.IO.Compression、PolySharp |

- `Shared` 承载跨产品通用逻辑，不依赖 WPF；WPF 与 Windows 专属能力放在 `Wpf`。
- 目标框架是当前事实；迁移平台时同步更新本文件与 `.agents/Tests/架构分析.md`。
- 使用中文沟通。文本文件使用 UTF-8 无 BOM，PowerShell 读写显式指定 UTF-8。

## 硬规则

- 修改公共 API 前先读 `.agents/仓库指示.md` 的 API 契约分层。
- 不新增 NuGet 依赖。确需新增时，在提交说明写明用途并取得用户确认。
- PCLA 依赖面只增不删。破坏性变更在同一批工作中更新 PCLA 的 submodule 指针与适配代码。
- 测试框架是 TUnit。不删除或跳过既有测试；新行为必须带对应测试。
- 构建保持零警告。格式以根 `.editorconfig` 为准，存量代码不一次性重排。
- 提交信息使用中文 `类型：说明`，一个逻辑变更一个提交。

## 按任务查阅

| 任务 | 文档 |
| --- | --- |
| 编写 C#、处理注释、修改公共 API | [仓库指示](.agents/仓库指示.md) |
| 调整 Shared 结构、跨平台边界 | [Shared 架构分析](.agents/Shared/架构分析.md) |
| 修改 WPF 能力或构建 WPF 工程 | [Wpf 架构分析](.agents/Wpf/架构分析.md) |
| 修改测试、构建或运行方式 | [Tests 架构分析](.agents/Tests/架构分析.md) |

## 文档同步

| 变化 | 必须同步 |
| --- | --- |
| 公共 API 增删或语义变化 | `.agents/仓库指示.md` 的稳定面清单，并确认 PCLA 编译 |
| 目标框架、SDK、构建或测试命令变化 | 本文件与 `.agents/Tests/架构分析.md` |
| 目录职责或模块边界变化 | 对应的 `.agents/<模块>/架构分析.md`，并检查本文件路由表 |
| 新增 NuGet 依赖 | 本文件依赖表与提交说明中的用途 |

文档与代码放在同一提交，禁止先合并代码再补文档。提交前自检：零警告、测试不低于基线、受影响文档已修改、稳定面未被破坏或已同批适配 PCLA。

## 协作与发布

- `main` 只做上游镜像，只接受 fast-forward。
- `pcla` 是长期分支。功能分支从 `pcla` 切出，命名为 `类型/主题`，完成后合并回 `pcla`。
- 禁止直接向 `main` 提交或强推。
- `upstream` 指向 `https://github.com/Meloong-Git/MeloongCore.git`。同步时把 `upstream/main` 合入 `pcla`，解决冲突后运行完整构建与测试。`pcla` 不 rebase。
- 合入 `pcla` 的改动打 `pcla-vX.Y.Z` 标签：破坏稳定面升主版本，新增能力升次版本，修复升补丁版本。
- 发布流程：打标签 → 将 PCLA 的 submodule 指针指向该提交 → PCLA 重建并通过测试。
- 合入门槛：三个项目 Release 构建零警告，TUnit 不低于基线，PCLA 能编译。
