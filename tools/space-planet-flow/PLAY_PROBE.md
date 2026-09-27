# Bootstrap Play 的交互与画面辅助

`JourneyPlayProbe.cs` 放在 Assets 外，由已有 Unity `run_script` 在真实 Bootstrap Play 中临时执行。入口为 `JourneyPlayProbe.Run(string operation, string value, int milliseconds)`，三个参数都显式传入。它不切换场景、不建立世界、不传送角色、不直接写入权威状态。

通过现有工具调用形式指定 `--file ../tools/space-planet-flow/JourneyPlayProbe.cs --entry JourneyPlayProbe.Run`；参数沿用当前 Unity CLI 的结构化参数格式。不要在另一个 Editor 操作尚未完成时调用，也不要把此文件复制进 Assets。

| operation | value | milliseconds | 用途 |
| --- | --- | --- | --- |
| `begin` | `""` | 0 | 新建本次 `artifacts/space-planet-flow/play-ui-时间` 证据目录。 |
| `status` | `""` | 0 | 查询当前可见按钮的 label/path、权威投影、Ready、地图、输入焦点和 YYGC 阻塞。 |
| `button` | 精确 label 或 path | 0 | 检查按钮可见且可交互，再调用该现有按钮的 `onClick`。 |
| `keys` | `D`、`A`、`Space`、`S` 或逗号组合 | 1–15000 | 将状态排入现有 Keyboard，等待正常 Update 采样；结束总会释放输入并恢复临时 Editor 输入选项。 |
| `keys-capture` | 同上 | 250–15000 | 按键保持期间截图；输出按住和释放后的冻结数据。 |
| `capture` | 本次图像标签 | 0 | `ScreenCapture.CaptureScreenshot` 捕获实际 GameView 及 Overlay UI。 |
| `confirm-watch` | `""` | 0 | 从已打开的选择页调用“确认目的地并驾驶”，有界观察 45 秒并尝试捕获 Transit、Ready Descent。 |
| `wait` | `""` | 0–15000 | 让真实 Play 继续运行，再记录最新状态。 |

`completed` 只表示工具操作完成，不表示对应验收通过。按键报告的 `before/held/released` 含角色位置、船位、驾驶者、输入可读性和交互阻塞，可用于核对实际结果。没有 Application 焦点时按键直接报告失败；不会修改正式输入门控来制造成功。按钮调用证明现有 UGUI 事件链，不能证明原生 OS 点击、射线命中、鼠标热点或触控行为。

建议按以下顺序分步观察，而非盲目按时长执行整条流程：

1. 已进入 Bootstrap Play 后 `begin`、`status`。在真实主菜单中用查询所得按钮开房，待 Ready；通过 `keys` 自由走动，保存 `capture/01-space-walk`。
2. 按住 D 走向驾驶台，逐次从 status 核对位置和按钮可用性；点击“选择目的地”，保存 `02-destination-picker`。打开期间发送 D／Space，比较 before/held/released 位置及 `gameplayBlocked`，关闭后再确认动作恢复。不要以按键事件成功排队代替移动／互斥断言。
3. 再次打开选择页，运行 `confirm-watch`，得到 `03-star-transit` 和 `04-planet-airborne`。若没捕获阶段、截图前后阶段改变或 Ready 未到，明确记未覆盖，不用另一阶段图替代。
4. 用 `keys/Space` 验证上升、松手缓降；点击离座确认无人驾驶悬停，再点击接管。使用 `keys-capture/S` 保存 `05-manual-descent`；按实际高度适时松手，等待安全自动着陆。
5. 查询 `Journey=Landed`、`PilotId=0`、`Ship.Phase=0`、`DoorClock=0`，保存 `06-platform-landed`。此时直接按 A 走下坡道，确认 `Crew.Boarded=false` 且角色站在地面，保存 `07-ramp-exit`，再向右走回船内。

七时点图应由根代理实际打开检查。分辨率、高 DPI、图像比例与可读性也需要真实画面判断；本工具不修改 GameView 尺寸、不自动勾选 117 项清单，不把单 Editor 画面作为独立 Host＋Client 联机证据。
