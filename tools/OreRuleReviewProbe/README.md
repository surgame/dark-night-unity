# 矿层规则方案审查探针

此工具实际编译并求解独立矿层的候选规则，只读引用现有 AnyRules Core 与 Compiler 源码，不修改依赖源码，不安装 Unity 或启动 Editor。输出中的资源 GUID 是人工测试身份，不是 Unity 资源 GUID；不得拿来制作正式资产。

覆盖五矿加空格的 1,296 种四角组合、3,355 次非空矿种选择、75 组四变体规则、目录编码恢复、未知输入，以及旧 ExactCell 合同、NotThis 空格语义、特殊 surface 通道和缺失 Fill 的反例。它不验证贴图 Alpha、GPU 裁剪、Prefab、YYGC 状态、采矿或网络。

在 Local 的 PowerShell 7 中运行，要求 .NET 8 和已准备的锁定依赖。输出集中到仓库 artifacts；在薄 worktree 中指定 Local 的依赖与输出路径，不在薄 worktree 生成 bin、obj、Library 或 artifacts。

```powershell
$orePackageRoot = (Resolve-Path -LiteralPath '.deps/YYGC-grid-business/AnyRuleD~/Packages/com.tsgame.anyrules').Path
$oreOutputRoot = Join-Path (Get-Location).Path 'artifacts/embedded-ore-rule-review'
New-Item -ItemType Directory -Path $oreOutputRoot -Force | Out-Null
dotnet build tools/OreRuleReviewProbe/OreRuleReviewProbe.csproj "-p:AnyRulesPackageRoot=$orePackageRoot" "-p:BaseIntermediateOutputPath=$oreOutputRoot/obj/" "-p:OutputPath=$oreOutputRoot/bin/" --nologo
if ($LASTEXITCODE -ne 0) { throw '矿层审查探针编译失败。' }
dotnet "$oreOutputRoot/bin/OreRuleReviewProbe.dll" "$oreOutputRoot/rule-review.json"
if ($LASTEXITCODE -ne 0) { throw '矿层审查探针检查失败。' }
```

依赖缺失时先报告，不自动升级或修补 YYGC。完成后按项目规则盘点可重建的 bin／obj，验证归属与占用后移到 `artifacts/待清理/YYYYMMDD-任务名/`，保留相对结构及清单；日志、最终 JSON 与正式证据保留，不永久删除。
