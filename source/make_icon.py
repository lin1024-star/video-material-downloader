"""生成视频素材下载器的应用图标（多尺寸 ICO）。"""
from PIL import Image, ImageDraw
import os

S = 1024  # 超采样画布，保证边缘平滑
OUT_DIR = os.path.dirname(os.path.abspath(__file__))

# ---------- 圆角矩形背景 + 垂直渐变 ----------
grad = Image.new("RGBA", (S, S))
gd = ImageDraw.Draw(grad)
top = (58, 130, 246)    # 亮蓝
bot = (24, 82, 200)     # 深蓝（贴近软件主色 #246EE0 的加深）
for y in range(S):
    t = y / (S - 1)
    gd.line(
        [(0, y), (S, y)],
        fill=(
            int(top[0] + (bot[0] - top[0]) * t),
            int(top[1] + (bot[1] - top[1]) * t),
            int(top[2] + (bot[2] - top[2]) * t),
            255,
        ),
    )

mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)

img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
img.paste(grad, (0, 0), mask)

d = ImageDraw.Draw(img)
W = (255, 255, 255, 255)
cx = S // 2

# ---------- 向下箭头：箭杆 ----------
shaft_w = int(S * 0.155)
d.rounded_rectangle(
    [cx - shaft_w // 2, int(S * 0.195), cx + shaft_w // 2, int(S * 0.58)],
    radius=int(S * 0.032),
    fill=W,
)

# ---------- 向下箭头：箭头尖 ----------
head_w = int(S * 0.425)
head_top = int(S * 0.545)
head_bot = int(S * 0.795)
d.polygon(
    [(cx - head_w // 2, head_top), (cx + head_w // 2, head_top), (cx, head_bot)],
    fill=W,
)

# ---------- 底部横线：表示“落到磁盘” ----------
bar_h = int(S * 0.052)
bar_w = int(S * 0.455)
bar_y = int(S * 0.862)
d.rounded_rectangle(
    [cx - bar_w // 2, bar_y, cx + bar_w // 2, bar_y + bar_h],
    radius=bar_h // 2,
    fill=W,
)

# ---------- 输出 ----------
preview = os.path.join(OUT_DIR, "icon-preview.png")
img.resize((256, 256), Image.LANCZOS).save(preview, format="PNG")
print("预览图: " + preview)

ico_path = os.path.join(OUT_DIR, "app.ico")
img.save(
    ico_path,
    format="ICO",
    sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)],
)
print("图标: " + ico_path)
print("大小: %d 字节" % os.path.getsize(ico_path))
