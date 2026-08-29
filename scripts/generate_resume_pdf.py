from pathlib import Path

from reportlab.lib.colors import HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf" / "吴旭洋-C++图形开发-简历.pdf"

FONT_REGULAR = r"C:\Windows\Fonts\Deng.ttf"
FONT_BOLD = r"C:\Windows\Fonts\Dengb.ttf"

NAVY = HexColor("#102A43")
BLUE = HexColor("#147D92")
TEAL = HexColor("#2CB1BC")
INK = HexColor("#243B53")
MUTED = HexColor("#627D98")
PALE = HexColor("#E6F6F8")
RULE = HexColor("#D9E2EC")
PAPER = HexColor("#FBFCFE")


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("Deng", FONT_REGULAR))
    pdfmetrics.registerFont(TTFont("DengBold", FONT_BOLD))


def wrap_text(text: str, font_name: str, font_size: float, max_width: float) -> list[str]:
    lines: list[str] = []
    for raw_line in text.splitlines() or [""]:
        if not raw_line:
            lines.append("")
            continue
        current = ""
        for char in raw_line:
            candidate = current + char
            if current and pdfmetrics.stringWidth(candidate, font_name, font_size) > max_width:
                lines.append(current.rstrip())
                current = char.lstrip()
            else:
                current = candidate
        if current:
            lines.append(current.rstrip())
    return lines


def draw_wrapped(
    c: canvas.Canvas,
    text: str,
    x: float,
    y: float,
    max_width: float,
    *,
    font_name: str = "Deng",
    font_size: float = 8.2,
    leading: float = 10.5,
    color=INK,
) -> float:
    c.setFont(font_name, font_size)
    c.setFillColor(color)
    for line in wrap_text(text, font_name, font_size, max_width):
        c.drawString(x, y, line)
        y -= leading
    return y


def draw_bullet(
    c: canvas.Canvas,
    text: str,
    x: float,
    y: float,
    max_width: float,
    *,
    font_size: float = 7.7,
    leading: float = 9.6,
    color=INK,
    bullet_color=TEAL,
) -> float:
    bullet_gap = 10
    lines = wrap_text(text, "Deng", font_size, max_width - bullet_gap)
    c.setFillColor(bullet_color)
    c.circle(x + 2.2, y + 2.5, 1.55, fill=1, stroke=0)
    c.setFillColor(color)
    c.setFont("Deng", font_size)
    for line in lines:
        c.drawString(x + bullet_gap, y, line)
        y -= leading
    return y - 2.4


def draw_section_title(c: canvas.Canvas, title: str, x: float, y: float, width: float) -> float:
    c.setFillColor(BLUE)
    c.roundRect(x, y - 2, 4, 16, 2, fill=1, stroke=0)
    c.setFillColor(NAVY)
    c.setFont("DengBold", 11.2)
    c.drawString(x + 11, y, title)
    c.setStrokeColor(RULE)
    c.setLineWidth(0.7)
    c.line(x + 72, y + 3, x + width, y + 3)
    return y - 18


def draw_role_header(
    c: canvas.Canvas,
    company: str,
    role: str,
    period: str,
    x: float,
    y: float,
    width: float,
) -> float:
    c.setFillColor(INK)
    c.setFont("DengBold", 9.3)
    c.drawString(x, y, company)
    c.setFillColor(BLUE)
    c.setFont("DengBold", 8.2)
    c.drawRightString(x + width, y, period)
    c.setFillColor(MUTED)
    c.setFont("Deng", 8)
    c.drawString(x, y - 12, role)
    return y - 27


def draw_project_header(
    c: canvas.Canvas,
    title: str,
    period: str,
    stack: str,
    x: float,
    y: float,
    width: float,
) -> float:
    c.setFillColor(INK)
    c.setFont("DengBold", 9.3)
    c.drawString(x, y, title)
    c.setFillColor(BLUE)
    c.setFont("DengBold", 8.1)
    c.drawRightString(x + width, y, period)
    c.setFillColor(MUTED)
    c.setFont("Deng", 7.3)
    c.drawString(x, y - 11, stack)
    return y - 24


def draw_chip(c: canvas.Canvas, text: str, x: float, y: float, width: float) -> None:
    c.setFillColor(white)
    c.roundRect(x, y - 2, width, 20, 10, fill=1, stroke=0)
    c.setFillColor(BLUE)
    c.setFont("DengBold", 7.4)
    c.drawCentredString(x + width / 2, y + 4, text)


def draw_highlight_card(
    c: canvas.Canvas,
    value: str,
    label: str,
    detail: str,
    x: float,
    y: float,
    width: float,
) -> None:
    c.setFillColor(PALE)
    c.roundRect(x, y, width, 86, 7, fill=1, stroke=0)
    c.setFillColor(BLUE)
    c.setFont("DengBold", 18)
    c.drawString(x + 12, y + 54, value)
    c.setFillColor(INK)
    c.setFont("DengBold", 8.2)
    c.drawString(x + 12, y + 37, label)
    draw_wrapped(c, detail, x + 12, y + 23, width - 24, font_size=6.7, leading=8, color=MUTED)


def build_pdf() -> None:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)

    page_w, page_h = A4
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("吴旭洋 - C++ / 图形开发简历")
    c.setAuthor("吴旭洋")
    c.setSubject("软件工程本科生，C++ / OpenGL / 图形开发方向")
    c.setKeywords("C++, OpenGL, 图形开发, 游戏引擎, CAD")

    c.setFillColor(PAPER)
    c.rect(0, 0, page_w, page_h, fill=1, stroke=0)

    # Header
    header_h = 112
    c.setFillColor(NAVY)
    c.rect(0, page_h - header_h, page_w, header_h, fill=1, stroke=0)
    c.setFillColor(TEAL)
    c.rect(0, page_h - header_h, 8, header_h, fill=1, stroke=0)

    c.setFillColor(white)
    c.setFont("DengBold", 27)
    c.drawString(34, page_h - 50, "吴旭洋")
    c.setFillColor(HexColor("#B8E3E8"))
    c.setFont("Deng", 11.2)
    c.drawString(35, page_h - 74, "软件工程本科生  ·  C++ / 图形开发方向")

    contact_x = 356
    c.setFont("Deng", 8.8)
    c.setFillColor(white)
    c.drawString(contact_x, page_h - 45, "邮箱")
    c.setFillColor(HexColor("#D9E2EC"))
    email = "3323157540@qq.com"
    c.drawString(contact_x + 34, page_h - 45, email)
    c.linkURL(f"mailto:{email}", (contact_x + 33, page_h - 48, page_w - 30, page_h - 35))
    c.setFillColor(white)
    c.drawString(contact_x, page_h - 66, "电话")
    c.setFillColor(HexColor("#D9E2EC"))
    phone = "193 1533 8730"
    c.drawString(contact_x + 34, page_h - 66, phone)
    c.linkURL("tel:+8619315338730", (contact_x + 33, page_h - 69, page_w - 30, page_h - 56))
    c.setFillColor(HexColor("#89C9D0"))
    c.setFont("Deng", 7.7)
    c.drawString(contact_x, page_h - 88, "求职方向：C++ / CAD / 图形渲染")

    margin = 32
    gap = 25
    left_w = 180
    right_x = margin + left_w + gap
    right_w = page_w - right_x - margin
    top_y = page_h - header_h - 25

    # Left column
    c.setFillColor(PALE)
    c.roundRect(margin - 8, 37, left_w + 16, top_y - 22, 8, fill=1, stroke=0)
    lx = margin + 3
    ly = top_y - 3
    lw = left_w - 6

    ly = draw_section_title(c, "教育背景", lx, ly, lw)
    c.setFillColor(INK)
    c.setFont("DengBold", 10)
    c.drawString(lx, ly, "安徽大学")
    c.setFillColor(BLUE)
    c.setFont("DengBold", 8.2)
    c.drawRightString(lx + lw, ly, "2022 - 2026")
    c.setFillColor(MUTED)
    c.setFont("Deng", 8.2)
    c.drawString(lx, ly - 14, "软件工程  ·  本科")
    ly -= 37

    ly = draw_section_title(c, "专业技能", lx, ly, lw)
    skill_bullets = [
        "C/C++：理解封装、继承、多态；熟悉 STL 常用容器及 C++11 智能指针等特性。",
        "算法与数据结构：链表、哈希表、二叉树、快速排序、归并排序等。",
        "图形学：熟悉 OpenGL 渲染管线、GLSL 着色器及 Phong 光照模型。",
        "3D 数学：矩阵变换、相机系统，以及 VAO/VBO 等缓冲区管理。",
        "计算机网络：OSI 七层模型、HTTP、TCP/UDP、三次握手与四次挥手。",
        "操作系统：内存管理、进程调度与通信、死锁、零拷贝等。",
        "工程工具：GLFW、ImGui、glm、spdlog、Premake。",
    ]
    for item in skill_bullets:
        ly = draw_bullet(c, item, lx, ly, lw, font_size=7.9, leading=10.2)

    ly -= 2
    ly = draw_section_title(c, "个人优势", lx, ly, lw)
    strengths = (
        "自驱与自学能力强，能够快速检索并吸收新技术；善于独立拆解复杂问题，"
        "重视代码质量、工程规范与项目管理，并能合理使用 AI 工具提升开发效率。"
    )
    ly = draw_wrapped(c, strengths, lx, ly, lw, font_size=7.9, leading=10.2, color=INK)

    ly -= 15
    ly = draw_section_title(c, "技术关键词", lx, ly, lw)
    chips = [
        ("C++17/20", 54),
        ("OpenGL", 48),
        ("GLSL", 40),
        ("CAD", 38),
        ("渲染管线", 54),
        ("批处理", 46),
        ("事件系统", 54),
        ("碰撞检测", 54),
        ("FBO", 38),
        ("FreeType", 52),
        ("GLFW", 42),
        ("ImGui", 44),
    ]
    chip_x = lx
    for label, chip_w in chips:
        if chip_x + chip_w > lx + lw:
            chip_x = lx
            ly -= 28
        draw_chip(c, label, chip_x, ly - 1, chip_w)
        chip_x += chip_w + 6

    # Right column
    rx = right_x
    ry = top_y - 3
    rw = right_w

    ry = draw_section_title(c, "实习经历", rx, ry, rw)
    ry = draw_role_header(c, "上海子虔科技有限公司", "CAD 开发实习", "2025.07 - 2025.09", rx, ry, rw)
    ry = draw_wrapped(
        c,
        "参与跨平台 CAD 工程软件开发。项目采用 C++20 与微服务架构，覆盖 3D 建模、装配设计和工程制图，并支持 4 个主流平台。",
        rx,
        ry,
        rw,
        font_size=8.15,
        leading=10.6,
    )
    ry -= 3
    ry = draw_bullet(c, "完善 CAD 专用交互式渲染功能。", rx, ry, rw, font_size=8.05, leading=10.4)
    ry = draw_bullet(c, "完善拾取系统，支持几何元素的精确选择与高亮显示。", rx, ry, rw, font_size=8.05, leading=10.4)

    ry -= 4
    ry = draw_section_title(c, "项目经历", rx, ry, rw)
    ry = draw_project_header(
        c,
        "LI 游戏引擎",
        "2025.03 - 2025.10",
        "C++17 / OpenGL / GLFW / ImGui / glm / spdlog",
        rx,
        ry,
        rw,
    )
    ry = draw_wrapped(
        c,
        "独立开发的 C++17 / OpenGL 游戏引擎。采用静态库架构，覆盖平台抽象、事件驱动、批处理渲染与资源管理；集成可停靠的运行时调试界面，并使用 Premake 管理多平台工程配置。",
        rx,
        ry,
        rw,
        font_size=7.95,
        leading=10.4,
    )
    ry -= 3
    li_engine = [
        "搭建入口点托管、日志和断言等引擎基础设施。",
        "基于 GLFW 封装跨平台窗口与输入，设计 Layer 架构和模板化事件分发机制。",
        "封装顶点数组、着色器、纹理等渲染资源，以工厂方法隔离底层 OpenGL。",
        "实现基于 VAO 复用的 2D 批处理渲染，集成相机控制与视口自适应。",
    ]
    for item in li_engine:
        ry = draw_bullet(c, item, rx, ry, rw, font_size=7.85, leading=10.2)

    ry -= 2
    ry = draw_project_header(
        c,
        "打砖块游戏",
        "2025.09 - 2026.02",
        "C++17 / OpenGL / GLFW / ImGui / glm / FreeType / irrKlang",
        rx,
        ry,
        rw,
    )
    ry = draw_wrapped(
        c,
        "使用 OpenGL 实现完整的 2D 街机游戏，在经典打砖块玩法上加入粒子系统、帧缓冲后期特效、FreeType 文字渲染、音效与多关卡支持。",
        rx,
        ry,
        rw,
        font_size=7.95,
        leading=10.4,
    )
    ry -= 3
    brick_game = [
        "以正射投影搭建 2D 坐标系，完成精灵着色器及位移、旋转、缩放组合变换。",
        "从文件解析关卡布局，区分普通与实心砖块并实现关卡完成检测。",
        "实现 AABB-AABB 与 AABB-Circle 碰撞检测。",
        "管理 500 个粒子生命周期，以加法混合实现发光拖尾。",
        "使用多重采样 FBO 与普通 FBO 的两阶段管线实现后期特效。",
    ]
    for item in brick_game:
        ry = draw_bullet(c, item, rx, ry, rw, font_size=7.85, leading=10.2)

    # Evidence-based project highlights use only claims present in the source resume.
    card_gap = 8
    card_w = (rw - card_gap * 2) / 3
    card_y = 140
    draw_highlight_card(c, "4", "主流平台", "CAD 工程项目支持跨平台部署。", rx, card_y, card_w)
    draw_highlight_card(c, "500", "粒子生命周期", "以加法混合实现发光拖尾效果。", rx + card_w + card_gap, card_y, card_w)
    draw_highlight_card(c, "2", "阶段 FBO", "多重采样与普通 FBO 完成后期处理。", rx + (card_w + card_gap) * 2, card_y, card_w)

    # Footer and visual balance markers.
    c.setStrokeColor(RULE)
    c.setLineWidth(0.6)
    c.line(right_x, 70, page_w - margin, 70)
    c.setFillColor(MUTED)
    c.setFont("Deng", 6.8)
    c.drawString(right_x, 55, "在线投递版 · 可搜索文本 · 邮箱与电话可点击")
    c.drawRightString(page_w - margin, 55, "更新于 2026.08")

    c.showPage()
    c.save()


if __name__ == "__main__":
    build_pdf()
    print(OUTPUT)
