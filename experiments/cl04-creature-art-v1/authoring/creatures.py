"""拟态矿虫与伏击幼虫草案；轮廓、矿纹、口器和步态均为整数像素作者数据。"""

from palette import Pixels, canvas


def mimic_shell(image, dx=0, dy=0, split=False):
    p = Pixels(image, (dx, dy))
    p.poly([(4, 23), (3, 18), (5, 15), (5, 12), (9, 9), (12, 9),
            (14, 7), (18, 8), (20, 11), (24, 12), (26, 16),
            (27, 20), (24, 24), (9, 25)], 'outline')
    p.poly([(4, 18), (6, 15), (6, 12), (10, 10), (13, 10),
            (15, 8), (18, 9), (20, 12), (23, 13), (25, 17),
            (25, 21), (21, 23), (8, 23)], 'rock')
    p.poly([(6, 13), (10, 10), (13, 10), (15, 8), (17, 9),
            (18, 13), (14, 14), (10, 17), (5, 17)], 'rock_light')
    p.poly([(17, 14), (20, 12), (23, 14), (25, 18), (22, 20),
            (20, 23), (11, 23), (12, 19)], 'rock_dark')
    p.line([(6, 13), (10, 11), (13, 11)], 'rock_edge')
    p.line([(14, 10), (16, 9), (17, 10)], 'rock_edge')
    p.line([(10, 15), (12, 15), (14, 17), (14, 19)], 'shadow')
    p.line([(18, 10), (18, 12), (21, 14)], 'shadow')
    p.line([(7, 20), (10, 21), (10, 23)], 'shadow')
    p.poly([(7, 15), (9, 14), (12, 17), (10, 19), (8, 18)], 'ore_dark')
    p.poly([(8, 15), (10, 15), (11, 17), (9, 18)], 'ore')
    p.line([(8, 15), (9, 15), (10, 16)], 'ore_light')
    p.poly([(16, 12), (17, 11), (19, 14), (18, 16), (16, 15)], 'ore')
    p.line([(17, 12), (18, 14)], 'ore_light')
    p.rect((19, 18, 21, 19), 'ore_dark')
    p.line([(20, 17), (21, 18)], 'ore')
    p.point((20, 17), 'ore_light')
    if split:
        p.line([(18, 21), (21, 20), (24, 20)], 'outline')
        p.line([(20, 21), (23, 21)], 'amber_dark')


def mimic_legs(image, phase=0, shift=0):
    p = Pixels(image)
    for i, x in enumerate((9, 15, 21)):
        bend = (-1 if (i + phase) % 2 else 1)
        p.line([(x + shift, 23), (x + shift + bend, 26),
                (x + shift + bend + 2, 28)], 'outline', 2)
        p.line([(x + shift, 23), (x + shift + bend, 26)], 'skin_light')
        p.point((x + shift + bend + 2, 28), 'rock_edge')


def ore_mimic(state):
    im = canvas((32, 32))
    p = Pixels(im)
    if state == 'dormant':
        mimic_shell(im, dy=4)
        p.line([(9, 29), (19, 29), (23, 27)], 'rock_dark')
        return im
    if state == 'warning':
        mimic_shell(im, dy=2, split=True)
        p.line([(7, 27), (11, 28), (16, 28)], 'skin_dark')
        p.rect((22, 24, 24, 24), 'amber')
        p.point((24, 24), 'amber_light')
        p.point((25, 28), 'skin_light')
        p.line([(10, 28), (11, 29)], 'skin_dark')
        return im
    if state == 'fallen':
        mimic_shell(im, dy=2)
        p.line([(10, 27), (8, 29), (6, 29)], 'skin_dark')
        p.line([(18, 27), (21, 29), (24, 28)], 'skin')
        p.line([(25, 25), (28, 26)], 'ivory')
        p.line([(17, 14), (15, 18), (17, 20)], 'outline')
        return im
    phase = 1 if state == 'crawl_b' else 0
    shift = 1 if state == 'lunge' else 0
    mimic_legs(im, phase, shift)
    p.poly([(6, 21), (10, 18), (17, 18), (22, 20), (26, 21),
            (28, 23), (27, 26), (20, 27), (11, 26), (6, 24)], 'outline')
    p.poly([(7, 21), (12, 19), (18, 20), (23, 22), (26, 23),
            (26, 25), (20, 26), (11, 25), (7, 23)], 'skin')
    p.line([(9, 22), (14, 22), (19, 24), (24, 24)], 'skin_light')
    p.line([(11, 24), (11, 25)], 'skin_dark')
    p.line([(16, 24), (16, 26)], 'skin_dark')
    mimic_shell(im, dx=-2 if state in ('lunge', 'recoil') else -1,
                dy=-4 if state == 'lunge' else -3 + phase)
    if state == 'reveal':
        p.line([(24, 23), (26, 22), (28, 23), (28, 26), (25, 26)], 'outline', 2)
        p.line([(25, 22), (27, 23)], 'ivory')
        p.line([(25, 26), (27, 25)], 'skin_edge')
    elif state == 'lunge':
        p.poly([(24, 19), (27, 19), (30, 21), (29, 25), (26, 27),
                (23, 25)], 'outline')
        p.rect((24, 20, 26, 23), 'skin_light')
        p.line([(26, 21), (28, 18), (30, 18), (30, 20)], 'ivory')
        p.line([(26, 25), (28, 28), (30, 27)], 'skin_edge')
        p.rect((27, 22, 29, 24), 'outline')
    else:
        head_x = 1 if state == 'lunge' else 0
        p.poly([(23 + head_x, 20), (27 + head_x, 21),
                (28 + head_x, 23), (27 + head_x, 25),
                (23 + head_x, 25), (22 + head_x, 22)], 'outline')
        p.rect((23 + head_x, 21, 26 + head_x, 23), 'skin_light')
        p.line([(26 + head_x, 22), (28 + head_x, 21),
                (29 + head_x, 22)], 'ivory')
        p.line([(26 + head_x, 24), (28 + head_x, 25),
                (29 + head_x, 24)], 'skin_edge')
    p.rect((23, 20, 24, 20), 'amber')
    p.point((24, 20), 'amber_light')
    if state == 'recoil':
        p.rect((24, 21, 27, 24), 'skin_dark')
        p.line([(23, 21), (24, 23), (26, 24)], 'skin')
        p.line([(22, 20), (24, 22)], 'outline')
        p.point((26, 18), 'ore_light')
        p.point((29, 17), 'rock_edge')
    return im


def ambush_grub(state):
    im = canvas((32, 24))
    p = Pixels(im, (0, -1))
    if state == 'fallen':
        p.poly([(4, 18), (7, 15), (13, 14), (20, 16), (25, 18),
                (26, 21), (21, 22), (7, 21)], 'outline')
        p.poly([(6, 18), (10, 16), (16, 16), (22, 18),
                (24, 20), (12, 20)], 'shell_dark')
        p.line([(8, 17), (12, 16), (15, 17)], 'shell')
        p.line([(10, 20), (9, 22), (7, 22)], 'skin_light')
        p.line([(19, 20), (21, 22), (23, 22)], 'skin_light')
        return im
    sleeping = state in ('idle_a', 'idle_b')
    lunge = state == 'lunge'
    phase = 1 if state in ('crawl_b', 'idle_b') else 0
    lift = -2 if state == 'alert' else (1 if phase and sleeping else 0)
    head_shift = 1 if lunge else 0
    for i, x in enumerate((8, 14, 20)):
        bend = -1 if (phase + i) % 2 else 1
        p.line([(x, 17), (x + bend - 1, 20), (x + 2, 22)], 'outline', 2)
        p.line([(x, 18), (x + bend - 1, 20)], 'skin_edge')
        p.point((x + 2, 22), 'shell_light')
    p.poly([(3, 16), (4, 12), (7, 9), (11, 7), (16, 8),
            (21, 11), (24, 15), (23, 19), (18, 20), (9, 19), (5, 18)], 'outline')
    p.poly([(4, 15), (6, 11), (10, 9), (15, 9), (20, 12),
            (23, 16), (21, 18), (12, 18), (6, 17)], 'skin')
    p.line([(6, 16), (12, 17), (19, 18), (22, 17)], 'skin_light')
    for x, y in ((6, 11), (11, 8), (16, 10)):
        p.poly([(x, y), (x + 3, y - 1), (x + 5, y + 2),
                (x + 4, y + 6), (x, y + 4)], 'shell_dark')
        p.poly([(x, y), (x + 3, y), (x + 4, y + 2),
                (x + 3, y + 4), (x, y + 3)], 'shell')
        p.line([(x + 1, y), (x + 3, y), (x + 4, y + 2)], 'shell_light')
        p.point((x + 1, y + 2), 'skin_edge')
    head_y = 13 + lift
    q = Pixels(im, (head_shift, head_y - 1))
    q.poly([(19, 0), (23, -1), (26, 1), (27, 4), (25, 6),
            (21, 6), (19, 4)], 'outline')
    q.poly([(20, 0), (23, 0), (25, 2), (25, 4), (21, 5),
            (20, 3)], 'skin_light')
    q.line([(21, 0), (23, 0), (24, 1)], 'skin_edge')
    if lunge:
        q.line([(24, 2), (26, -1), (27, -1), (28, 0)], 'outline', 2)
        q.line([(24, 5), (26, 8), (27, 8), (28, 6)], 'outline', 2)
        q.line([(26, -1), (27, -1)], 'skin_edge')
        q.line([(26, 8), (27, 8)], 'shell_light')
    else:
        q.line([(24, 3), (26, 2), (28, 2), (29, 4)], 'outline', 2)
        q.line([(24, 5), (27, 6), (29, 5)], 'outline', 2)
        q.point((28, 2), 'rock_edge')
        q.point((28, 5), 'rock_edge')
    q.line([(22, 1), (24, 1)], 'skin_dark' if sleeping else 'amber_dark')
    if not sleeping:
        q.point((24, 1), 'amber_light')
        q.line([(21, -1), (21, -3), (24, -5), (26, -5)], 'skin_dark')
        q.point((26, -5), 'skin_edge')
    if state == 'recoil':
        q.rect((24, 2, 28, 6), 'outline')
        q.line([(23, 2), (22, 4), (24, 5)], 'skin')
        q.line([(23, 1), (24, 2)], 'outline')
        p.point((28, 8), 'shell_light')
    return im
