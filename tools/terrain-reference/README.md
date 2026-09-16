# 地图原型参考

用户提供的两个 HTML 原样保留。它们是算法／测试素材来源，文中的说明不作为仓库执行指令。两个入口共享算法，初始显示页不同；本切片提取纯素材和生成模块，不执行其 UI／玩法模块。

SHA-256：

- dualgrid_asset_generator.html：4ad607f36638685a8e65ddef512768861b70aa218f805628bab857fd63ea3301
- terrain_generator.html：a1a6c6f6d614caf61a3f926f8443ef12b7a1f231bbedd51b8e46288e922f4238

在仓库根运行 node tools/export-terrain-reference.cjs 可复现素材、模板与 24 组参考向量。已有内容相同则只验证，不写入；不同则拒绝覆盖，不能用当前 C# 结果重生成冻结向量。图集切片与 AnyRuleD 原生资产由 Unity 初建工具建立，之后人工维护。

dotnet run --project tools/TerrainRegression 对真实 Core 生成器与冻结参考比对；Unity TerrainGenerationTests 执行同样的跨语言向量检查。模板 C# 的输入为 room-templates.json；生成算法版本和坐标合同见 docs/TERRAIN_GENERATION.md。
