"""虫卵、空巢和前兆贴片；装饰透明图，不定义碰撞、掉落或发光。"""

from palette import Pixels, canvas


def fibers(image, active=False):
    p = Pixels(image)
    paths = [([(3, 20), (7, 18), (12, 20), (19, 18), (22, 21)], 'fiber_dark'),
             ([(2, 22), (6, 20), (9, 22), (15, 21), (20, 22)], 'fiber'),
             ([(5, 19), (5, 17), (7, 16)], 'fiber'),
             ([(14, 20), (17, 22), (21, 21)], 'fiber_light')]
    for points, color in paths:
        p.line(points, color)
    if active:
        p.point((4, 20), 'ore')
        p.point((19, 21), 'ore_light')


def egg(image, pos, opened=False, cracked=False, pulse=False):
    p = Pixels(image, pos)
    p.poly([(0, 8), (0, 4), (2, 1), (4, 0), (6, 1), (8, 5),
            (8, 8), (6, 10), (2, 10)], 'outline')
    p.poly([(1, 8), (1, 4), (3, 1), (5, 1), (7, 5),
            (7, 8), (5, 9), (3, 9)], 'shell_dark')
    p.poly([(1, 5), (3, 2), (5, 2), (6, 5), (5, 8),
            (2, 7)], 'shell')
    p.line([(2, 4), (3, 2), (4, 2)], 'shell_light')
    p.point((2, 5), 'ivory')
    p.line([(5, 3), (6, 5), (5, 7)], 'skin_light')
    if pulse:
        p.line([(4, 4), (5, 5), (4, 7)], 'amber_dark')
        p.point((5, 5), 'amber')
    if cracked:
        p.line([(4, 1), (3, 3), (5, 5), (3, 6), (4, 9)], 'outline')
        p.point((4, 5), 'amber_dark')
    if opened:
        p.poly([(1, 4), (3, 5), (4, 3), (6, 5), (7, 4),
                (7, 7), (5, 9), (3, 9), (1, 7)], 'outline')
        p.line([(1, 4), (3, 5), (4, 3), (6, 5), (7, 4)], 'ivory')
        p.line([(2, 8), (4, 9), (6, 8)], 'skin')


def egg_clutch(state):
    im = canvas((24, 24))
    fibers(im, state == 'pulse')
    egg(im, (9, 4), opened=state == 'empty', cracked=state == 'cracked')
    egg(im, (3, 10), opened=state == 'empty', pulse=state == 'pulse')
    egg(im, (13, 11), opened=state == 'empty',
        cracked=state == 'cracked', pulse=state == 'pulse')
    if state == 'empty':
        p = Pixels(im)
        p.line([(2, 19), (1, 21), (4, 21)], 'shell_light')
        p.line([(20, 19), (22, 21), (20, 21)], 'shell')
    return im


def nest(state):
    im = canvas((32, 24))
    p = Pixels(im)
    p.poly([(2, 17), (3, 12), (6, 9), (7, 6), (12, 4),
            (19, 5), (22, 8), (26, 9), (28, 13), (29, 19),
            (25, 21), (9, 22), (3, 20)], 'outline')
    p.poly([(3, 16), (5, 11), (8, 9), (9, 6), (14, 5),
            (18, 6), (21, 9), (25, 10), (27, 14), (27, 19),
            (22, 20), (8, 20), (4, 19)], 'rock_dark')
    p.line([(5, 12), (8, 10), (10, 7), (13, 6)], 'rock_light')
    p.line([(19, 7), (21, 10), (25, 11)], 'rock')
    p.poly([(8, 18), (8, 13), (10, 10), (15, 9), (20, 11),
            (23, 16), (22, 19), (10, 20)], 'outline')
    p.line([(8, 16), (9, 12), (12, 10), (15, 10)], 'skin_dark')
    p.line([(4, 19), (9, 18), (13, 21), (18, 20), (22, 21), (29, 20)], 'fiber')
    p.line([(6, 17), (9, 18), (12, 17)], 'fiber_light')
    p.line([(22, 16), (24, 18), (27, 17)], 'fiber_dark')
    p.line([(12, 21), (14, 22), (17, 21)], 'shell')
    if state == 'occupied':
        p.point((14, 15), 'amber')
        p.point((19, 15), 'amber')
        p.point((14, 14), 'amber_dark')
    return im


def clue(kind):
    im = canvas((16, 16))
    p = Pixels(im)
    if kind == 'scratch':
        for x, y in ((3, 3), (7, 2), (11, 4)):
            p.line([(x, y), (x - 1, y + 3), (x - 1, y + 7)], 'outline')
            p.line([(x + 1, y + 1), (x, y + 4), (x, y + 6)], 'rock_edge')
            p.point((x - 1, y + 8), 'rock_dark')
    elif kind == 'slime':
        p.line([(2, 12), (4, 10), (7, 11), (9, 8), (13, 7)], 'fiber_dark', 2)
        p.line([(3, 11), (5, 11), (7, 12), (10, 9), (12, 9)], 'fiber')
        p.point((5, 10), 'fiber_light')
        p.point((11, 8), 'fiber_light')
        p.point((14, 5), 'fiber')
        p.point((2, 14), 'fiber')
    elif kind == 'molt':
        p.poly([(2, 10), (4, 7), (9, 5), (12, 7), (14, 11),
                (11, 13), (5, 13)], 'outline')
        p.poly([(3, 10), (5, 8), (9, 6), (11, 8), (13, 11),
                (10, 12), (5, 12)], 'shell_dark')
        p.line([(4, 9), (7, 7), (9, 7)], 'shell_light')
        p.line([(7, 8), (8, 10), (8, 12)], 'shell')
        p.line([(10, 8), (11, 10), (11, 11)], 'shell')
        p.line([(3, 12), (2, 14)], 'skin_light')
    return im
