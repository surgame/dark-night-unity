"""九个短动作烘焙为可编辑 Action；姿势样机，不定义游戏攻击或交互时点。"""

import math

import bpy

from rigging import reset, rotation, select_action, translation


def limbs(rig, left_leg, right_leg, left_knee=0, right_knee=0):
    for side, thigh, knee in (("L", left_leg, left_knee),
                              ("R", right_leg, right_knee)):
        rotation(rig, "Thigh." + side, (thigh, 0, 0))
        rotation(rig, "Shin." + side, (knee, 0, 0))
        rotation(rig, "Foot." + side, (-thigh - knee, 0, 0))


def pose(rig, action, index, count):
    reset(rig)
    t = index / count
    phase = t * math.tau
    wave = math.sin(phase)
    bob = (1 - math.cos(phase * 2)) / 2
    rotation(rig, "UpperArm.L", (-8, -18, 0))
    rotation(rig, "UpperArm.R", (-8, 18, 0))
    rotation(rig, "Forearm.L", (-20, 0, 0))
    rotation(rig, "Forearm.R", (-20, 0, 0))

    if action == "idle":
        translation(rig, "Spine", (0, 0, .009 * wave))
        rotation(rig, "Head", (0, 0, 2 * wave))
        rig["eyes_open"] = 0.0 if index == 6 else 1.0
    elif action in ("walk", "run"):
        running = action == "run"
        swing = 40 if running else 26
        lift = 58 if running else 28
        limbs(rig, -swing * wave, swing * wave,
              lift * max(0, wave), lift * max(0, -wave))
        rotation(rig, "UpperArm.L", (swing * wave, -7, 0))
        rotation(rig, "UpperArm.R", (-swing * wave, 7, 0))
        rotation(rig, "Forearm.L", (-48 if running else -12, 0, 0))
        rotation(rig, "Forearm.R", (-48 if running else -12, 0, 0))
        rotation(rig, "Spine", (13 if running else 4, 0, 3 * wave))
        rotation(rig, "Head", (-8 if running else -3, 0, -2 * wave))
        translation(rig, "Hips", (0, 0, (.06 if running else .028) * bob))
    elif action == "jump":
        height = [0, -.06, .14, .32, .25, .08, -.065, 0][index % count]
        crouch = [0, 1, .30, .55, .40, .15, .85, 0][index % count]
        translation(rig, "Root", (0, 0, height))
        limbs(rig, -42 * crouch, -42 * crouch, 80 * crouch, 80 * crouch)
        rotation(rig, "Spine", (15 * crouch, 0, 0))
        for side, sign in (("L", -1), ("R", 1)):
            rotation(rig, "UpperArm." + side, (-30, sign * 70 * max(height, 0) / .32, 0))
            rotation(rig, "Forearm." + side, (-30, 0, 0))
    elif action == "interact":
        strike = (1 - math.cos(phase)) / 2
        rotation(rig, "Spine", (7 + 12 * strike, 0, -5))
        rotation(rig, "Head", (4, 0, 7))
        rotation(rig, "UpperArm.R", (-25 - 82 * strike, 17, 0))
        rotation(rig, "Forearm.R", (-18 - 25 * strike, 0, 0))
        rotation(rig, "UpperArm.L", (-35, -20, 0))
        rotation(rig, "Forearm.L", (-28, 0, 0))
        limbs(rig, -7, 7, 7, 0)
    elif action == "cheer":
        for side, sign in (("L", -1), ("R", 1)):
            rotation(rig, "UpperArm." + side, (-10, sign * (135 + 12 * wave), 0))
            rotation(rig, "Forearm." + side, (-12, sign * 10, 0))
        translation(rig, "Hips", (0, 0, .055 * bob))
        rotation(rig, "Head", (-7, 0, 5 * wave))
    elif action == "sit":
        translation(rig, "Hips", (0, 0, -.15))
        limbs(rig, -77, -77, 75, 75)
        rotation(rig, "UpperArm.L", (-36, -12, 0))
        rotation(rig, "UpperArm.R", (-36, 12, 0))
        rotation(rig, "Forearm.L", (-32, 0, 0))
        rotation(rig, "Forearm.R", (-32, 0, 0))
        rotation(rig, "Head", (2 * wave, 0, 0))
    elif action == "sleep":
        translation(rig, "Root", (-.80, 0, .65))
        rotation(rig, "Root", (0, 86, 0))
        rotation(rig, "Spine", (0, 0, 2 * wave))
        rotation(rig, "UpperArm.L", (-40, -28, 0))
        rotation(rig, "UpperArm.R", (-28, 14, 0))
        rotation(rig, "Forearm.L", (-65, 0, 0))
        rotation(rig, "Forearm.R", (-40, 0, 0))
        limbs(rig, -17, -10, 25, 18)
        rig["eyes_open"] = 0.0
    elif action == "hurt":
        recoil = [0, .8, 1, .65, .35, .12, 0, 0][index % count]
        translation(rig, "Root", (0, .055 * recoil, 0))
        rotation(rig, "Spine", (-20 * recoil, 8 * recoil, 0))
        rotation(rig, "Head", (-9 * recoil, 0, -8 * recoil))
        rotation(rig, "UpperArm.L", (-25 * recoil, -25 * recoil, 0))
        rotation(rig, "UpperArm.R", (-40 * recoil, 22 * recoil, 0))
        rig["eyes_open"] = 0.0 if recoil > .6 else 1.0


def create_actions(rig, config):
    rig.animation_data_create()
    for name, definition in config["actions"].items():
        action = bpy.data.actions.new("Crew." + name)
        action.use_fake_user = True
        action["sprite_frames"] = definition["frames"]
        action["sprite_fps"] = definition["fps"]
        action["loop"] = definition["loop"]
        action["scope"] = "Shared by all outfits; visual-only prototype"
        rig.animation_data.action = action
        count = definition["frames"]
        for index in range(count + (1 if definition["loop"] else 0)):
            pose(rig, name, index % count, count)
            frame = index + 1
            for bone in rig.pose.bones:
                bone.keyframe_insert("location", frame=frame, group=bone.name)
                bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
            rig.keyframe_insert('["eyes_open"]', frame=frame, group="Face")
        if hasattr(action, "layers"):
            for layer in action.layers:
                for strip in layer.strips:
                    if hasattr(strip, "channelbags"):
                        for bag in strip.channelbags:
                            for curve in bag.fcurves:
                                for key in curve.keyframe_points:
                                    key.interpolation = "LINEAR"
    select_action(rig, "idle")
