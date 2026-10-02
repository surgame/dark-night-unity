# 氧气移除验证进度

2026-10-03。用户最初授权开始验证，随后明确要求“暂时不验证先，列出验证项我自行完成”。已停止后续验证，以下清单交由用户执行。当前只有先前完成的独立编译与纯规则结果，**整批尚未通过**；不能作为可运行交付。源码候选基于`d16a84b`，后续补齐了设备数量回归和新增行为用例。

## 已执行

| 检查 | 结果 | 限制 |
| --- | --- | --- |
| Core、Runtime、View、Entry、Editor、Tests独立C#编译 | 6/6，exit=0 | 使用Local只读响应文件、锁定程序集和原生成器；未写Local缓存，不能代替Unity导入或IL后处理。保留既有Unity弃用API警告 |
| 新增及修改Tests重新编译 | exit=0 | 双人五分钟离船、移除命令／旧协议／旧存档拒绝、三设备撤收、存储测试数据归档；尚未执行用例 |
| 纯航程／冻结快照 | 1903/1903 | 独立.NET进程，实际候选Core及Runtime配置解析；不等同于真实角色通行或联机 |
| Core规则回归 | 1046/1048 | 两项旧软岩／保护格断言失败，基线[工具Definition说明](TOOL_DEFINITION_HARVESTING.md)已记录相同失败；未改变地形规则、冻结夹具或旧期望来消除它们 |
| 依赖输入 | 字节一致 | 候选与Local的manifest、packages-lock及YYGC补丁一致；YYGC未修改 |

机器摘要见[证据JSON](evidence/oxygen-removal-20261003.json)。原始回归报告保留在`artifacts/oxygen-removal-20261003/`；独立编译中间输出保留于`artifacts/待清理/20261003-oxygen-validation/`，原路径、当前路径、体积及处理条件见其清单。归档不作空间释放，JSON中的编译时绝对路径保留用于追溯。

## 验证中修正与准备

- `ExpeditionTests`旧断言仍含四种设备：已改为明确的ship／storage／turret／lamp集合，三设备真实部署与撤收；包含飞船的展开总数为4。
- 新增`OxygenRemovalTests`：通过可信双人输入走到离船220单位处，隔离威胁后推进五分钟，检查位置、HP、登船标记、租约与远征阶段；另检查relay、协议22及v15拒绝且世界保持。
- `SessionStorageScenarios`临时测试存档不再递归删除，改由`StorageTestArtifacts`校验独占目录和链接后移入`artifacts/待清理/YYYYMMDD-session-storage-tests/`并写清单。
- `tools/oxygen-removal/compile_readonly.py`只写候选artifacts。首轮生成源码转储路径写出失败，保留失败日志；取消非必要的源码转储后原生成器参与的C#编译正常完成。
- `OxygenAssetProbe.cs`已准备，只有Editor加载协议23／v16且空闲时才执行注册检查与两套Prefab保存重开；目前未调用。

## 用户自行验证清单（尚未执行）

1. **候选与导入**：在另一对话释放Local后安全接入`ref-20261003-remove-oxygen`，先保留Local全部未提交工作；确认协议23／存档v16／AMP1 schema2，正常导入／代码生成／全程序集编译。新增`OxygenRemovalTests.cs`与`StorageTestArtifacts.cs`的.meta须由Unity正常生成并保留提交，不手工指定GUID。本次保存的是验证准备checkpoint，尚不具备Editor导入或.meta生成证据。
2. 执行原生资源探针，核对正式Definition、Addressables、Prefab绑定、保存重开和其余GUID保持。
3. 合并执行受影响Editor批次：OxygenRemoval、Expedition、Ship、Journey、Session权限／投影／存储和Definition相关用例；失败只重跑修正后的受影响阶段。
4. **玩法与Mono**：一次Mono构建复用同一产物。从正式Bootstrap新开局到星球、着陆、下船；隔离敌人后真实离船超过五分钟，确认HP、位置、控制租约及登船标记保持，不因时间自动返船。分别检查机器人／货舱／船员舱，三种设备位置-50／+30／+110、供电范围360和预算12；矿工持续采集、满包卸货、无矿返程、机器人搬运、侦察机及召回；正常／紧急返航、战斗全员死亡及保存失败回滚仍正常。
5. 独立Host＋Client、晚加入、重连、暂停／epoch／权限、正常及弱网；真实画面另行检查。
6. **存档与兼容**：v16真正写盘、退出进程重启恢复，覆盖地面、空中、结算与失败输入；旧v15明确拒绝且旧文件保留。协议22连接和已移除relay命令拒绝，当前世界不部分改变；更新后的死亡Recovery、Walkway和新增OxygenRemoval用例全部执行。
7. **再次出发**：复现并单独处理已知phase4／Landed再次出发问题；目前仍未修复，不能计作通过。修复后完成第二次目的地选择、到达、着陆与结算全链。
8. **画面**：真实前台观察氧气显示及中继按钮消失，其余HUD、交互、两种面板布局、设备外观和坡道通行正常；多个窗口尺寸下检查空位、遮挡和文字。

IL2CPP须另行明确同意，双机器需要环境；它们不会从Mono或本机结果推断为通过。原Local仍有其他对话的未提交工作，本轮未覆盖、切分支、合并、启动第二Editor或写入其缓存。用户已取消本轮移交与后续自动验证，接下来由用户安排；工具脚本和新增用例属于准备材料，不等同于已运行证据。
