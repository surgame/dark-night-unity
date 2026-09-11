extends SceneTree

func _init():
	var sequences = []
	for initial in [90127, -1, -9223372036854775807 - 1, 0]:
		var rng = RandomNumberGenerator.new()
		rng.seed = initial
		var sequence = {"seed": str(initial), "seeded_state": str(rng.state), "samples": []}
		for i in range(128):
			var integer = rng.randi_range(-2147483648, 2147483647) if i % 7 == 0 else rng.randi_range(-3, 10)
			var floating = rng.randf_range(-5.0, 5.0)
			sequence.samples.append({"integer": integer, "float": floating, "state": str(rng.state)})
		sequences.append(sequence)
	var file = FileAccess.open("res://rng-vectors.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"engine": Engine.get_version_info(), "sequences": sequences}, "  ", false, true))
	file.close()
	quit()
