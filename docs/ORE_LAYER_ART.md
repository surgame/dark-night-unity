# 按格矿层美术候选

2026-09-22，分支 `ft-20260922-embedded-ore-art`。用户要求从分叉矿脉改为按格矿层，并支持 AnyRuleD DualGrid 自动连接。本轮交付铜、铁、金、银、钻石的原生 8px 图集：每矿 16 个四角形状、4 个共边变体，分离矿化岩面与半透明外缘；原三层背景作为离线嵌入样板。

[五矿与三层色样](../experiments/ore-dualgrid-v1/preview/five-ore-dualgrid.png) · [素材与接入说明](../experiments/ore-dualgrid-v1/README.md) · [规则切片合同](../experiments/ore-dualgrid-v1/anyruled-contract.json)

30,720 对共边 RGBA 一致，离线绘制预览 8/8。图集和规则映射已制作，Unity AnyRuleD 资产尚未导入；没有宣称实际 Editor／Play／Player、性能、联机或最终美术验收通过。协议 13／存档 v9 和当前暂时隐藏正式矿图的行为不变。

旧分叉矿脉方向已撤下，唯一 AI 源图及过程素材保留于 `artifacts/ore-art-rejected-seams-20260922`。本轮新图集全部由原生像素绘制产生，不依赖旧生图缩放。
