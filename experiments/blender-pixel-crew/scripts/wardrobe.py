"""可组合的头发、帽子、包和工具；每个部件仅建模与绑定一次。"""

import math

from character import variant_collection
from geometry import box, cylinder, goggles, profile, segment, sphere


def hair(rig, m, color, name):
    target = variant_collection(name)
    sphere("Hair.Crown", (0, .015, 1.71), (.397, .318, .224),
           m[color], target, rig, "Head")
    box("Hair.Back", (0, .225, 1.60), (.677, .15, .30),
        m[color], target, rig, "Head", .045)
    for i, (x, length) in enumerate(((-.27, .145), (-.125, .085), (.04, .115), (.24, .14))):
        box("Hair.Fringe." + str(i), (x, -.249, 1.705 - length / 2),
            (.166, .11, length), m[color], target, rig, "Head", .018,
            rotation=(0, math.radians((i - 1) * 7), 0))
    for side in (-1, 1):
        box("Hair.Sideburn", (side * .34, -.11, 1.58), (.09, .26, .22),
            m[color], target, rig, "Head", .018)
    if color == "pink":
        for x in (-.37, .37):
            sphere("Hair.Pigtail", (x, .21, 1.60), (.16, .18, .23),
                   m[color], target, rig, "Head")
            box("Hair.Tie", (x, .23, 1.56), (.15, .17, .055),
                m["ink"], target, rig, "Head", .01)


def cap(rig, m):
    target = variant_collection("cap_red")
    profile("Cap.Crown", [(1.758, .397, .316), (1.88, .369, .305),
                          (1.976, .258, .216), (2.00, .08, .07)],
            m["red"], target, rig, "Head", center_y=.015, power=3.2)
    sphere("Cap.Brim", (0, -.31, 1.777), (.385, .307, .047),
           m["red"], target, rig, "Head")
    cylinder("Cap.Patch", (0, -0.305, 1.893), 0.113, 0.02,
             m["cream"], target, rig, "Head",
             rotation=(math.pi / 2, 0, 0), vertices=12, scale=(1.06, 1, 1))
    box("Cap.Mark", (.015, -.324, 1.893), (.053, .012, .084),
        m["red"], target, rig, "Head", 0)


def worker_face(rig, m):
    target = variant_collection("hair_cap")
    profile("Hair.CapBack", [(1.23, .30, .21), (1.43, .38, 0.30),
                             (1.72, .385, .31), (1.80, .30, .25)],
            m["hair"], target, rig, "Head", center_y=.075, power=3)
    for sign in (-1, 1):
        sphere("Hair.Temple", (sign * .343, -.015, 1.54),
               (.066, .25, .235), m["hair"], target, rig, "Head")
    target = variant_collection("beard")
    profile("Beard.Jaw", [(1.115, .19, .145), (1.16, .29, .239),
                          (1.235, .346, .284), (1.335, .373, .307)],
            m["hair"], target, rig, "Head", center_y=-.021, power=3)
    for sign in (-1, 1):
        sphere("Beard.Cheek", (sign * .318, -.115, 1.374),
               (.079, .23, .16), m["hair"], target, rig, "Head")
        box("Beard.Moustache", (sign * .10, -.331, 1.351),
            (.22, .055, .085), m["hair"], target, rig, "Head", .02,
            rotation=(0, sign * .09, 0))
    box("Beard.Mouth", (0, -.337, 1.262), (.093, .022, .032),
        m["skin"], target, rig, "Head", .006)


def hat(rig, m):
    target = variant_collection("hat_brown")
    cylinder("Hat.Brim", (0, -.013, 1.79), .53, .072,
             m["brown"], target, rig, "Head", vertices=12, scale=(1, .77, 1))
    box("Hat.Crown", (0, .018, 1.931), (.63, .445, .31),
        m["brown"], target, rig, "Head", .068)
    box("Hat.Band", (0, .013, 1.84), (.665, .477, .089),
        m["ink"], target, rig, "Head", .052)
    box("Hat.Pin", (.23, -.224, 1.85), (.056, .03, .07),
        m["gold"], target, rig, "Head", .009)


def rabbit(rig, m):
    target = variant_collection("hood_rabbit")
    box("Hood.Back", (0, .11, 1.51), (.80, .47, .73),
        m["cream"], target, rig, "Head", .095)
    box("Hood.Top", (0, -.07, 1.795), (.79, .46, .19),
        m["cream"], target, rig, "Head", .045)
    for side, sign in (("L", 1), ("R", -1)):
        box("Hood.Cheek." + side, (sign * .357, -.15, 1.45),
            (.145, .34, .51), m["cream"], target, rig, "Head", .04)
        box("Hood.Ear." + side, (sign * .205, .001, 2.015),
            (.185, .17, .48), m["cream"], target, rig, "Head", .043,
            rotation=(0, sign * .10, 0))
        box("Hood.EarInner." + side, (sign * .205, -.095, 2.02),
            (.077, .028, .33), m["pink"], target, rig, "Head", .013,
            rotation=(0, sign * .10, 0))


def accessories(rig, m):
    target = variant_collection("goggles")
    goggles(target, m, rig)
    box("Goggle.Strap", (0, .015, 1.75), (.75, .56, .064),
        m["leather"], target, rig, "Head", .04)
    target = variant_collection("scarf_red")
    box("Scarf.Collar", (0, -.02, 1.166), (.46, .40, .105),
        m["red"], target, rig, "Spine", .04)
    box("Scarf.Tip", (.058, -.215, 1.067), (.18, .05, .22),
        m["red"], target, rig, "Spine", .013,
        rotation=(0, -.27, 0))
    target = variant_collection("backpack")
    box("Backpack.Body", (0, .34, .963), (.50, .32, .475),
        m["leather"], target, rig, "Spine", .052)
    box("Backpack.Flap", (0, .511, 1.06), (.44, .054, .18),
        m["brown"], target, rig, "Spine", .024)
    box("Backpack.Buckle", (0, .546, .975), (.077, .025, .09),
        m["gold"], target, rig, "Spine", .008)
    target = variant_collection("satchel")
    box("Satchel", (.287, .079, .714), (.225, .29, .29),
        m["leather"], target, rig, "Hips", .03)
    box("Satchel.Flap", (.412, .02, .777), (.025, .22, .11),
        m["brown"], target, rig, "Hips", .01)


def wrench(rig, m):
    target = variant_collection("wrench")
    segment("Wrench.Handle", (-.418, -.095, .60), (-.56, -.19, .99),
            .076, .074, m["metal"], target, rig, "Hand.R")
    box("Wrench.HeadBase", (-.56, -.19, .985), (.205, .09, .135),
        m["metal"], target, rig, "Hand.R", .015)
    for sign in (-1, 1):
        box("Wrench.Jaw", (-.56 + sign * .086, -.19, 1.072),
            (.058, .09, .14), m["metal"], target, rig, "Hand.R", .012,
            rotation=(0, sign * .20, 0))


def create_wardrobe(rig, materials):
    worker_face(rig, materials)
    hair(rig, materials, "hair", "hair_brown")
    hair(rig, materials, "pink", "hair_pink")
    cap(rig, materials)
    hat(rig, materials)
    rabbit(rig, materials)
    accessories(rig, materials)
    wrench(rig, materials)
