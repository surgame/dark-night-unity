# Unity与YYGC依赖准备

本页描述现行可重现输入及准备入口，版本与执行结果见[DEVELOPMENT](DEVELOPMENT.md)。最终来源为 `Game/Packages/manifest.json`、`packages-lock.json`、NuGet配置、准备脚本及其补丁锁；历史接入和授权见[YYGC账本](YYGC_CHANGES.md)，不按旧阶段段落选择依赖版本。

## 当前输入

| 项目 | 锁定配置及职责 |
| --- | --- |
| Unity Editor | ProjectVersion锁定6000.4.9f1，Linear，C#9／.NET Standard 2.1 |
| YYGC | com.tsgame.gamecore 0.3.0-preview.1；UPM为 `file:../../.deps/YYGC-grid-business` |
| YYGC源码基线 | `fee18645c997ed7529c4592917de6c412033c84e`，原网格补丁再串行Debugging overlay |
| AnyRuleD四包 | 同一 `.deps/YYGC-grid-business/AnyRuleD~/Packages/` 下anyrules、networking、fishnet及yygc，不混入外部脏工作区 |
| FishNet | `file:../../.deps/FishNet/Assets/FishNet`，tools/prepare-fishnet.ps1锁定来源及既有生命周期补丁 |
| Addressables／Input System | 2.10.1／1.19.0，声明于manifest与lock |
| URP／UGUI | 17.4.0／2.0.0及宿主TMP，沿现有管线和主题 |
| Newtonsoft JSON | com.unity.nuget.newtonsoft-json 3.2.2，Runtime显式字段映射，Core无JSON依赖 |
| Pipeline | com.unity.pipeline 0.6.0-exp.1，连接已打开Editor；机器级CLI另核对实际版本 |
| UniTask／R3／MemoryPack／ZLinq | UPM固定tag 2.5.11／1.3.1／1.21.4／1.5.6；NuGet及生成器输入按提交配置核对 |
| VitalRouter | 原2.7.1固定源码wait-all修正及DLL来源保留，不由普通恢复悄悄换回未修正版 |
| Odin／DOTween／Smart Console | 既有导入payload、许可及元数据保留；不是可随意清理的普通缓存 |

YYGC package.json的Unity声明与宿主锁定Editor是不同概念，不能仅凭预览版本号假定兼容。完整依赖与原素材来源保留在[历史评估证据](archive/evidence/assessment-2026-09-10.json)、[样板依赖证据](archive/evidence/lan-sample-dependencies.json)和[第三方声明](third-party/)。

## 锁与准备入口

| 文件／入口 | 作用 |
| --- | --- |
| `tools/grid-business/dependency.lock.json`／yygc.patch | YYGC基线、网格补丁摘要、目标及原批次核验 |
| `tools/debug-hub/dependency.lock.json`／yygc-debug-hub.patch | 前置补丁摘要、Debugging overlay、首次导入meta和原批次静态验证 |
| `tools/grid-business/prepare-dependency.ps1` | 核对基线／补丁、拒绝未知修改，串行应用基础补丁与overlay |
| `tools/prepare-fishnet.ps1` | 精确恢复FishNet锁定输入，保留用户原工作区 |

需要补齐现行依赖时，在仓库根目录按当前锁执行：

```powershell
pwsh -NoProfile -File tools/grid-business/prepare-dependency.ps1
pwsh -NoProfile -File tools/prepare-fishnet.ps1
```

准备现有锁定输入属于使用已锁定依赖；若检查发现未知差异、基线不匹配或本地维护内容，保留现场并调查，不reset、覆盖或混入用户YYGC／AnyRule工作区。上述准备不表示Unity编译、Player或新补丁行为已验收。

锁中的game_protocol／save_version及validation还保留制作批次信息，不能代替运行源码常量或当前DEVELOPMENT。文档整理没有修改锁字段、补丁或框架；若需改变锁语义／接入，按AGENTS另明确范围。基线和补丁完整SHA及逐文件原因在账本和实际锁文件，不在多份指南复制维护。

## 运行库与生成器

Core只存纯计算、只读配置和冻结合同，不引用Unity、GameCore、网络库、JSON或文件系统。Runtime用JObject显式转冻结类型；64位RNG及snake_case字段按存档合同处理，不默认使用.NET8 JSON API或加载net8.0游戏程序集。

沿锁定ViewBinding、DI、BehaviourRegistry、NetworkCommand／StateData及Singleton生成器和类型注册。生成DLL、RoslynAnalyzer标签、作用域／平台导入、`.meta`和已批准宿主补丁都是构建输入；不能手改Library/Bee输出。需要重建生成器先核对来源及AfterBuild复制目标，框架源码／补丁升级仍需具体授权。

UPM lock不单独覆盖NuGet、VitalRouter修正DLL或既有插件payload。普通NuGet恢复后核对固定输入和多版本程序集警告；未知DLL不覆盖，实际兼容性按当前组合验证。精确字段、类型注册及AOT入口必须明确，不能用整程序集preserve=all掩盖未定位问题。

## 官方 Unity MCP 开发工具

本工程沿机器级Unity CLI及锁定Pipeline连接Local单一Editor，命令／任务续接见[Unity CLI](UNITY_CLI_WORKFLOW.md)。Pipeline包含Runtime／Roslyn DLL，enableInBuilds=false仅表示Player服务未启用，不承诺所有DLL已剔除。已有Sample证据不能签署正式AppStartup／Addressables或发布体积。

新机器先核对Editor、提交配置及现行依赖入口，再按所选CLI版本配置连接。日常不运行强制升级或重新安装Pipeline；机器代理、Codex本机配置及个人路径不进入项目。TLS／UPM网络受限的原处理留在历史Git基线，不关闭TLS或复制脏Library绕过恢复。

## 独立样板与历史接入

[LAN Sample](samples/LAN_SAMPLE.md)及其旧prepare-lan-sample路线用于明确的独立模板输入，不作为当前Game的默认依赖步骤。旧 `.deps/YYGC`／YYGC-unified／AnyRules隔离目录、M0–M5及历次framework提交继续保留来源，不把多个旧“当前版本”并列为现行方案。

旧Sample排除、友元访问、VitalRouter、场景Loader、输入和地图补丁的授权／逐文件结果由YYGC账本及原证据承接。原文可从[顶层整理基线](archive/README.md#2026-10-09顶层职责整理)恢复；历史IL2CPP通过不授权新一轮后端构建。

## 变更与核验

依赖变更提交manifest／packages-lock、ProjectVersion、准备输入及正常生成meta，不提交.deps、Library、密钥和本机配置。修改YYGC源码／补丁／版本及接入须事先具体授权；未知漂移不自动修复。

实际验收按受影响Editor导入、生成器／DTO往返、Addressables、当前Mono独立运行和联机配置安排，构建不运行会保存人工资产的初始化。新Editor／管线／平台／大批导入变化先评估Local缓存失效，遵循单一通道和[内存保护](UNITY_CLI_WORKFLOW.md#内存门控)。
