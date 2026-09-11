extends SceneTree
## Disposable native UI geometry capture. No game scripts, simulation or player saves are loaded.

func _initialize() -> void:
	call_deferred("capture")

func color_value(value: Color) -> Array:
	return [value.r, value.g, value.b, value.a]

func scan(node: Node, page: Node, rows: Array) -> void:
	if node is Control:
		var rect: Rect2 = node.get_rect()
		var row: Dictionary = {"path": str(page.get_path_to(node)), "type": node.get_class(),
			"rect": [rect.position.x, rect.position.y, rect.size.x, rect.size.y],
			"visible": node.visible, "unique": node.unique_name_in_owner,
			"variation": str(node.theme_type_variation), "mouseFilter": node.mouse_filter}
		if node is Label or node is Button:
			row["text"] = node.text
			row["fontSize"] = node.get_theme_font_size("font_size")
			row["fontColor"] = color_value(node.get_theme_color("font_color"))
			row["fontName"] = node.get_theme_font("font").get_font_name()
			row["alignment"] = node.horizontal_alignment if node is Label else node.alignment
			row["wrap"] = node.autowrap_mode != TextServer.AUTOWRAP_OFF if node is Label else false
		if node is Button:
			row["disabled"] = node.disabled
			row["tooltip"] = node.tooltip_text
		if node is TextureRect:
			row["texture"] = node.texture.resource_path if node.texture else ""
		if node is ColorRect:
			row["color"] = color_value(node.color)
		if node is ProgressBar:
			row["value"] = node.value / node.max_value
		var styles: Dictionary = {}
		var names: Array = ["panel"] if node is PanelContainer else ["normal", "hover", "pressed", "disabled"] if node is Button else ["background", "fill"] if node is ProgressBar else []
		for style_name in names:
			var style: StyleBox = node.get_theme_stylebox(style_name)
			if style is StyleBoxFlat:
				styles[style_name] = {"background": color_value(style.bg_color), "border": color_value(style.border_color),
					"borderWidth": style.border_width_left, "radius": style.corner_radius_top_left}
		row["styles"] = styles
		rows.append(row)
	for child in node.get_children():
		scan(child, page, rows)

func capture() -> void:
	var result: Dictionary = {"engine": Engine.get_version_info().string, "profiles": []}
	for size in [Vector2i(1280, 800), Vector2i(1600, 900)]:
		root.size = size
		var profile: Dictionary = {"width": size.x, "height": size.y, "pages": []}
		for path in ["res://scenes/ui/HudChrome.tscn", "res://scenes/ui/menus/MainMenu.tscn", "res://scenes/ui/menus/PauseMenu.tscn", "res://scenes/ui/menus/Help.tscn", "res://scenes/ui/menus/Result.tscn"]:
			var page: Control = load(path).instantiate()
			root.add_child(page)
			page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			for frame in range(6):
				await process_frame
			var rows: Array = []
			scan(page, page, rows)
			profile.pages.append({"name": page.name, "source": path, "nodes": rows})
			page.queue_free()
			await process_frame
		result.profiles.append(profile)
	var file := FileAccess.open("res://ui-layout.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "\t"))
	file.close()
	print("DARK_NIGHTS_UI_LAYOUT_CAPTURED profiles=2 pages=5")
	quit()
