# Unity与YYGC依赖准备

本页描述现行可重现输入及准备入口，版本与执行结果见[DEVELOPMENT](DEVELOPMENT.md)。最终来源为 `Game/Packages/manifest.json`、`packages-lock.json`、NuGet配置、准备脚本及其补丁锁；历史接入和授权见[YYGC账本](YYGC_CHANGES.md)，不按旧阶段段落选择依赖版本。

## 当前输入

| 项目 | 锁定配置及职责 |
| --- | --- |
| Unity Editor | ProjectVersion锁定6000.4.9f1，Linear，C#9／.NET Standard 2.1 |
| YYGC | com.tsgame.gamecore 0.3.0-preview.1；UPM为 `file:../../.deps/YYGC-spine-renderers` |
| YYGC源码基线 | `fee18645c997ed7529c4592917de6c412033c84e`，原网格补丁、Debugging overlay再串行获批混合Renderer修正 |
| AnyRuleD四包 | 同一 `.deps/YYGC-spine-renderers/AnyRuleD~/Packages/` 下anyrules、networking、fishnet及yygc，不混入外部脏工作区 |
| FishNet | `file:../../.deps/FishNet/Assets/FishNet`，tools/prepare-fishnet.ps1锁定来源及既有生命周期补丁 |
| Addressables／Input System | 2.10.1／1.19.0，声明于manifest与lock |
| URP／UGUI | 17.4.0／2.0.0及宿主TMP，沿现有管线和主题 |
| Spine | 官方spine-csharp 4.3.42／spine-unity 4.3.119，同一提交`175a1e05073a406f305bde2621c065b8c2256c42`；UPM引用`.deps/spine-runtimes-175a1e0`内未修改源码，来源与归档SHA-256由`tools/spine/dependency.lock.json`锁定 |
| Newtonsoft JSON | com.unity.nuget.newtonsoft-json 3.2.2，Runtime显式字段映射，Core无JSON依赖 |
| Pipeline | com.unity.pipeline 0.6.0-exp.1，连接已打开Editor；机器级CLI另核对实际版本 |
| UniTask／R3／MemoryPack／ZLinq | UPM固定tag 2.5.11／1.3.1／1.21.4／1.5.6；NuGet及生成器输入按提交配置核对 |
| VitalRouter | 原2.7.1固定源码wait-all修正及DLL来源保留，不由普通恢复悄悄换回未修正版 |
| Odin／DOTween／Smart Console | 既有导入payload、许可及元数据保留；不是可随意清理的普通缓存 |

YYGC package.json的Unity声明与宿主锁定Editor是不同概念，不能仅凭预览版本号假定兼容。完整依赖与原素材来源保留在[历史评估证据](archive/evidence/assessment-2026-09-10.json)、[样板依赖证据](archive/evidence/lan-sample-dependencies.json)和[第三方声明](third-party/)。

Spine准备：在仓库根执行`python tools/spine/prepare_dependency.py`，下载锁定官方归档并校验，只展开两个原生包；已有落点逐文件核验，拒绝覆盖不明或修改过的依赖。归档可用`--archive <本地归档>`复用，`.deps`及下载产物不提交；提交UPM清单、锁文件、准备脚本和源锁。当前本机Git传输失败后采用官方HTTPS归档，两个包均未修改，既有依赖的版本／来源／依赖关系不变。项目首次加入4.3运行库，不执行旧版Spine组件升级；保持Linear，旧PMA／缺图导出改来源文本后缀、保留字节与GUID，新主角使用单独straight-alpha派生资源。许可原文保存在[第三方声明](third-party/SPINE-RUNTIMES-LICENSE.txt)，源文件中的许可头保持。

## 锁与准备入口

| 文件／入口 | 作用 |
| --- | --- |
| `tools/grid-business/dependency.lock.json`／yygc.patch | YYGC基线、网格补丁摘要、目标及原批次核验 |
| `tools/debug-hub/dependency.lock.json`／yygc-debug-hub.patch | 前置补丁摘要、Debugging overlay、首次导入meta和原批次静态验证 |
| `tools/spine/framework.lock.json`／yygc-mixed-renderers.patch | 现行YYGC目标、三份补丁摘要及39份派生文件身份；本批框架只改ObjectView.cs |
| `tools/spine/prepare_framework_dependency.py` | 从本机框架只读克隆独立候选，串行应用三份补丁并逐文件核验；不改旧脏检出、不启动Unity |
| `tools/grid-business/prepare-dependency.ps1` | 历史网格＋Debugging候选准备入口，保留原目标；当前Game改用上述Spine框架入口 |
| `tools/prepare-fishnet.ps1` | 精确恢复FishNet锁定输入，保留用户原工作区 |

需要补齐现行依赖时，在仓库根目录按当前锁执行：

```powershell
python tools/spine/prepare_framework_dependency.py
pwsh -NoProfile -File tools/prepare-fishnet.ps1
python tools/spine/prepare_dependency.py
```

准备现有锁定输入属于使用已锁定依赖；若检查发现未知差异、基线不匹配或本地维护内容，保留现场并调查，不reset、覆盖或混入用户YYGC／AnyRule工作区。上述准备不表示Unity编译、Player或新补丁行为已验收。

混合Renderer候选于2026-11-03获得本次具体授权，11-04续接同批操作。新检出固定LF；原38份补丁内容与旧依赖一致，其中34份仅换行不同，原`.deps/YYGC-grid-business`及作者框架工作区保持原字节。UPM通过Client.AddAndRemove一次切换GameCore与四个AnyRuleD包，版本号和其余依赖语义保持。准备脚本默认只读克隆`D:/Developer/YYGC`的Git对象，其他位置可传`--framework-repository`；本地shared clone仍依赖该源Git对象库，不能清理／替换源库后假定离线检出完整。现行锁对未知额外修改或缺失文件拒绝复用。

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
