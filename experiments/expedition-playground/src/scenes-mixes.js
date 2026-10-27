(function (D) {
  "use strict";
  D.register("mix", [
    { id: "m-miner", name: "危险矿工", subtitle: "采矿顺序，就是撤离计划",
      description: "在承重矿柱和塌方区域取矿，使用临时支撑或爆破改变采集节奏。带着收益原路交付。",
      kit: ["pick", "support", "charge", "shotgun"], layout: "miner", tags: ["工业采矿", "原路返回"],
      objective: "取得至少五份货物，回入口交付。", goal: { key: "delivered", count: 5 },
      hint: "先看顶板裂纹，再决定从哪边挖；左侧入口E交付。", focus: "pick" },
    { id: "m-hunt", name: "黑暗猎场", subtitle: "先让它露面",
      description: "巢穴、活茧、吊丝共同出现。诱饵和捕网争取采集窗口，岩壁则提供射钉角度。",
      kit: ["pick", "lure", "net", "nail"], layout: "hunt", tags: ["生物生态", "控制"],
      objective: "取得三份货物，并用环境或控制装备处理生物，再交付。", goal: { key: "huntComplete", count: 1 },
      hint: "可引怪到吊丝下，也可捕网后采矿；全部击杀不是完成前提。", focus: "lure" },
    { id: "m-explore", name: "寻矿探险", subtitle: "回波之外，还有什么",
      description: "探测封岩、搭梯到达高台，再沿遗留标记找到隐藏晶囊。场景以发现和返程为主。",
      kit: ["pick", "scanner", "ladder", "flare"], layout: "explore", tags: ["侦察", "高处探索"],
      objective: "扫描、登上高台并取得至少四份货物后交付。", goal: { key: "exploreComplete", count: 1 },
      hint: "先探测近墙，再到右侧高台左边搭梯；入口可交付。", focus: "scanner" },
    { id: "m-cargo", name: "重货远征", subtitle: "把队友和货物一起带回",
      description: "先救起队友，再使用固定绞盘提升矿箱；挡板能够隔开运输段的幼虫，矿镐可清理障碍。",
      kit: ["pick", "winch", "shield", "medkit"], layout: "cargo", tags: ["救援", "运输"],
      objective: "救起队友、提升矿箱，再回入口确认回收。", goal: { key: "cargoComplete", count: 1 },
      hint: "先救援右侧队友，然后去绞盘底座连接并按住E提升，最后返入口。", focus: "medkit" }
  ]);
})(window.DN);
