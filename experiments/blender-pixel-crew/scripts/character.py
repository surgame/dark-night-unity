"""中性底模、脸部和身体服装；不复制骨架，也不复制衣服专用动作。"""

import bpy

from geometry import box, collection, eye_driver, profile, segment, sphere


def create_base(rig, materials):
    target = collection("00 · Base body / shared face")
    m = materials
    box("Body.Skin", (0, 0, .96), (.43, .30, .44),
        m["skin"], target, rig, "Spine", .04)
    profile("Head.Skin", [
        (1.135, .23, .16), (1.18, .31, .24), (1.29, .365, .284),
        (1.53, .375, .284), (1.69, .35, .25), (1.79, .25, .19),
        (1.805, .09, .09)], m["skin"], target, rig, "Head",
        center_y=-.018, power=3.5)
    box("Chin.Line", (0, -.083, 1.14), (.535, .383, .038),
        m["ink"], target, rig, "Head", .014)
    for side, sign in (("L", 1), ("R", -1)):
        box("Ear." + side, (sign * .374, -.006, 1.45), (.095, .155, .17),
            m["skin"], target, rig, "Head", .028)
        sphere("Arm." + side, (sign * .33, 0, .967), (.125, .118, .176),
               m["skin"], target, rig, "UpperArm." + side)
        sphere("Forearm." + side, (sign * .39, -.018, .75), (.115, .11, .16),
               m["skin"], target, rig, "Forearm." + side)
        sphere("Hand." + side, (sign * .405, -.043, .625), (.123, .124, .123),
               m["skin"], target, rig, "Hand." + side)
        eye = box("Eyes.Open." + side, (sign * .145, -.314, 1.49),
                  (.078, .031, .125), m["ink"], target, rig, "Head", 0)
        eye_driver(eye, rig, False)
        closed = box("Eyes.Closed." + side, (sign * .145, -.322, 1.465),
                     (.078, .028, .026), m["ink"], target, rig, "Head", 0)
        eye_driver(closed, rig, True)
        sphere("Boot." + side, (sign * .14, -.082, .097), (.126, .19, .11),
               m["brown"], target, rig, "Foot." + side)
        box("Boot.Sole." + side, (sign * .14, -.066, .033), (.23, .34, .06),
            m["ink"], target, rig, "Foot." + side, .01)
    box("Nose", (0, -.312, 1.397), (.075, .055, .065),
        m["skin"], target, rig, "Head", .008)
    box("Mouth", (.02, -.291, 1.319), (.072, .022, .022),
        m["brown"], target, rig, "Head", 0)


def variant_collection(name):
    target = collection("Part · " + name)
    target["crew_variant"] = name
    return target


def create_clothes(rig, m, color):
    target = variant_collection("clothes_" + color)
    cloth = m[color]
    profile("Shirt", [( .80, .21, .165), (.92, .25, .19),
                     (1.09, .247, .18), (1.20, .135, .125)],
            m["cream"], target, rig, "Spine", power=3)
    box("Trousers.Hips", (0, 0, .68), (.46, .34, .20),
        cloth, target, rig, "Hips", .022)
    profile("Overalls.Body", [(.70, .229, .176), (.79, .254, .204),
                             (.91, .255, .213), (1.025, .238, .201)],
            cloth, target, rig, "Spine", power=3)
    box("Overalls.Bib", (0, -.195, 1.01), (.277, .047, .26),
        cloth, target, rig, "Spine", .008)
    box("Overalls.Pocket", (0, -.224, .971), (.155, .03, .11),
        m["leather"], target, rig, "Spine", .008)
    for side, sign in (("L", 1), ("R", -1)):
        box("Strap." + side, (sign * .137, -.171, 1.096), (.061, .05, .20),
            cloth, target, rig, "Spine", .005)
        box("Button." + side, (sign * .13, -.205, 1.059), (.04, .018, .043),
            m["gold"], target, rig, "Spine", 0)
        sphere("Sleeve." + side, (sign * .30, 0, 1.054), (.133, .135, .11),
               m["cream"], target, rig, "UpperArm." + side)
        segment("Trouser.Thigh." + side, (sign * .135, 0, .65),
                (sign * .135, 0, .385), .212, .282,
                cloth, target, rig, "Thigh." + side)
        segment("Trouser.Shin." + side, (sign * .135, 0, .395),
                (sign * .135, 0, .18), .184, .23,
                cloth, target, rig, "Shin." + side)


def create_character(rig, materials):
    create_base(rig, materials)
    create_clothes(rig, materials, "blue")
    create_clothes(rig, materials, "brown")
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj["authoring"] = "Editable mesh; rigid vertex group -> CrewRig"
