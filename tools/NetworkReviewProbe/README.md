# YYGC 联机复评探针

独立的 .NET 8 评估工具，不进入 Unity 程序集，不修改 YYGC。它只检查两种依赖调用语义，并加载真实命令生成器 DLL 做命名空间对照；不替代 Unity 编译、FishNet RPC 或实际联机测试。

在 `unity-projects` 使用安装了 .NET 8 SDK 的终端：

```powershell
dotnet restore tools/NetworkReviewProbe --locked-mode
dotnet run --no-restore --project tools/NetworkReviewProbe -- 'D:\Developer\YYGC' artifacts/network-review-probe.json
```

第二个参数是输出报告路径。生成器输出写入报告旁的两个专用目录；复跑会更新报告，旧评估的冻结证据不会改变。查看当前 JSON 的 outputs 字段判断本次输出，不将目录中可能残留的历史生成文件当成本次结果。

锁文件固定 R3 1.3.0 和 MemoryPack 1.21.4，Roslyn 来自执行时 SDK。本次 SDK 为 8.0.409。工具使用 C# 12，因为 MemoryPack 的 net8 生成结果使用 C# 11+ 语法；这是工具宿主要求，不改变游戏目标 C# 9。最初尝试 C# 9 的工具构建失败，调整工具语言版本后运行成功；该失败不属于 Unity 构建证据。

观察项目：

- R3 `Where(non-null).Skip(1)`：初始 null 和已有初值的对照。
- MemoryPack：具体类型可往返，同对象经未注册接口泛型序列化失败；不假设外部 Unity 宿主没有注册 formatter。
- YYGC 命令生成器：读取当前 `NetworkCommandAttribute.cs` 和 DLL；当前命名空间与仅改命名空间的旧版对照。只运行生成，不编译这些输出所需的整个框架。

退出码 0 表示两项依赖语义观察符合本次复现条件；生成器输出和诊断单独记录在 JSON 中。不能把退出码叫作“网络测试通过”。源文件变化、依赖升级或生成器修复后，应重新解释结果。
