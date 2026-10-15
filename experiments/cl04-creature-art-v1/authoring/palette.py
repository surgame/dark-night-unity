"""CL-04 原生像素草案的固定调色板；透明像素只使用零 alpha。"""

from PIL import Image, ImageDraw

COLORS = {
    'outline': '#191714',
    'shadow': '#28231e',
    'rock_dark': '#393027',
    'rock': '#514434',
    'rock_light': '#706047',
    'rock_edge': '#918063',
    'ore_dark': '#69512a',
    'ore': '#a17b39',
    'ore_light': '#c3a45a',
    'ore_glint': '#d9c68a',
    'skin_dark': '#4c382b',
    'skin': '#79553a',
    'skin_light': '#a48055',
    'skin_edge': '#c3aa78',
    'shell_dark': '#574831',
    'shell': '#887149',
    'shell_light': '#b09868',
    'ivory': '#d4c39a',
    'amber_dark': '#a75d2e',
    'amber': '#df9a48',
    'amber_light': '#f1cd79',
    'fiber_dark': '#383a29',
    'fiber': '#5e6041',
    'fiber_light': '#85835b',
}


def canvas(size):
    return Image.new('RGBA', size, (0, 0, 0, 0))


class Pixels:
    """整数像素绘制；所有外形／切角由作者坐标明确指定，不作缩放或柔化。"""

    def __init__(self, image, offset=(0, 0)):
        self.draw = ImageDraw.Draw(image)
        self.ox, self.oy = offset

    def point(self, xy, color):
        x, y = xy
        self.draw.point((x + self.ox, y + self.oy), fill=COLORS[color])

    def rect(self, box, color):
        x1, y1, x2, y2 = box
        self.draw.rectangle((x1 + self.ox, y1 + self.oy,
                             x2 + self.ox, y2 + self.oy), fill=COLORS[color])

    def line(self, points, color, width=1):
        self.draw.line([(x + self.ox, y + self.oy) for x, y in points],
                       fill=COLORS[color], width=width)

    def poly(self, points, color):
        self.draw.polygon([(x + self.ox, y + self.oy) for x, y in points],
                          fill=COLORS[color])
