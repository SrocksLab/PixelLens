from __future__ import annotations

import ctypes
import json
import os
import re
import shutil
import sys
import time
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Optional

import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from PIL import Image, ImageChops, ImageGrab, ImageTk


try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)
except Exception:
    pass


ROOT = Path(__file__).resolve().parent


def resource_path(name: str) -> Path:
    """Resolve bundled files both during development and inside a PyInstaller EXE."""
    base = Path(getattr(sys, "_MEIPASS", ROOT))
    return base / name


if getattr(sys, "frozen", False):
    # 单文件 EXE 的临时解包目录不可写，默认项目放到当前用户的本地应用数据目录。
    user_data = Path(os.environ.get("LOCALAPPDATA", Path.home())) / "界面对比"
    DEFAULT_PROJECT = user_data / "ui-compare-project"
else:
    DEFAULT_PROJECT = ROOT / "ui-compare-project"
IMAGE_TYPES = [("图片", "*.png *.jpg *.jpeg *.bmp *.webp"), ("所有文件", "*.*")]


@dataclass
class Page:
    name: str
    size: str
    design_path: str
    capture_path: Optional[str] = None
    capture_time: str = ""
    status: str = "未截屏"


def parse_design_name(path: Path) -> tuple[str, str]:
    match = re.search(r"[_ -]?(\d{3,5})x(\d{3,5})$", path.stem, re.I)
    if match:
        return path.stem[: match.start()].rstrip(" _-"), f"{match.group(1)} × {match.group(2)}"
    try:
        with Image.open(path) as image:
            return path.stem, f"{image.width} × {image.height}"
    except Exception:
        return path.stem, "尺寸未知"


def size_tuple(size: str) -> tuple[int, int]:
    match = re.search(r"(\d+)\s*[×x]\s*(\d+)", size)
    return (int(match.group(1)), int(match.group(2))) if match else (0, 0)


class CaptureOverlay(tk.Toplevel):
    """Frozen-screen selector with fixed-size and free selection modes."""

    def __init__(self, owner: "CompareApp", page: Page):
        super().__init__(owner)
        self.owner = owner
        self.page = page
        try:
            self.iconbitmap(str(resource_path("app.ico")))
        except tk.TclError:
            pass
        self.source, self.virtual_bbox = self._grab_screen()
        self.mode = "fixed"
        self.rect: Optional[list[float]] = None
        self.interaction: Optional[dict] = None
        self.drag_origin: Optional[tuple[float, float]] = None
        self.photo: Optional[ImageTk.PhotoImage] = None
        self.canvas_scale = 1.0
        self.rect_items: list[int] = []
        self.title("选择截图区域")
        self.configure(bg="#07142c")
        self.attributes("-topmost", True)
        self.overrideredirect(True)
        self.resizable(False, False)
        self._build()
        self._position()
        # 将输入焦点交给截图层，避免主窗口拦截选框操作。
        self.after_idle(self.grab_set)
        self.after_idle(self.focus_force)
        self.after(60, self._draw)

    @staticmethod
    def _grab_screen() -> tuple[Image.Image, tuple[int, int, int, int]]:
        """Capture the full Windows virtual desktop, including monitors left of the primary."""
        try:
            user32 = ctypes.windll.user32
            virtual_left = int(user32.GetSystemMetrics(76))
            virtual_top = int(user32.GetSystemMetrics(77))
            virtual_width = int(user32.GetSystemMetrics(78))
            virtual_height = int(user32.GetSystemMetrics(79))
            virtual_bbox = (virtual_left, virtual_top, virtual_width, virtual_height)
        except Exception:
            virtual_bbox = (0, 0, 0, 0)
        try:
            image = ImageGrab.grab(all_screens=True).convert("RGB")
            width, height = image.size
            if not virtual_bbox[2] or not virtual_bbox[3]:
                virtual_bbox = (0, 0, width, height)
            else:
                virtual_bbox = (virtual_bbox[0], virtual_bbox[1], width, height)
            return image, virtual_bbox
        except Exception:
            image = ImageGrab.grab().convert("RGB")
            return image, (0, 0, image.width, image.height)

    def _position(self):
        _, _, screen_w, screen_h = self.virtual_bbox
        virtual_left, virtual_top = self.virtual_bbox[:2]
        self.geometry(f"{screen_w}x{screen_h}{virtual_left:+d}{virtual_top:+d}")

    def _build(self):
        toolbar = tk.Frame(self, bg="#0b1d3b", height=58)
        toolbar.pack(fill="x", side="top")
        toolbar.pack_propagate(False)
        tk.Label(toolbar, text="画面已冻结", bg="#0b1d3b", fg="#f0f6ff", font=("Segoe UI", 11, "bold")).pack(side="left", padx=(16, 6))
        self.subtitle = tk.Label(toolbar, text="移动固定尺寸框，选择截图区域", bg="#0b1d3b", fg="#8ea6d0", font=("Segoe UI", 9))
        self.subtitle.pack(side="left")

        self.mode_var = tk.StringVar(value="fixed")
        mode_frame = tk.Frame(toolbar, bg="#0b1d3b")
        mode_frame.pack(side="left", padx=26)
        for label, value in (("按设计稿尺寸", "fixed"), ("自由框选", "free")):
            tk.Radiobutton(
                mode_frame,
                text=label,
                variable=self.mode_var,
                value=value,
                command=self._change_mode,
                indicatoron=False,
                padx=10,
                pady=5,
                bg="#19345f",
                selectcolor="#4d8cff",
                activebackground="#2b5eaa",
                fg="#eaf2ff",
                font=("Segoe UI", 9),
            ).pack(side="left", padx=2)

        tk.Button(toolbar, text="取消", command=self.destroy, bg="#142b52", fg="#eaf2ff", relief="flat", padx=14).pack(side="right", padx=(6, 16))
        tk.Button(toolbar, text="确认截取", command=self._confirm, bg="#4d8cff", fg="#07142c", relief="flat", padx=14).pack(side="right")

        self.canvas = tk.Canvas(self, bg="#07142c", highlightthickness=0, cursor="crosshair")
        self.canvas.pack(fill="both", expand=True)
        self.canvas.bind("<ButtonPress-1>", self._pointer_down)
        self.canvas.bind("<B1-Motion>", self._pointer_move)
        self.canvas.bind("<ButtonRelease-1>", self._pointer_up)

    def _fit_source(self):
        width = max(1, self.winfo_width())
        height = max(1, self.winfo_height() - 58)
        self.canvas_scale = min(width / self.source.width, height / self.source.height, 1.0)
        draw_w = int(self.source.width * self.canvas_scale)
        draw_h = int(self.source.height * self.canvas_scale)
        return draw_w, draw_h, (width - draw_w) / 2, (height - draw_h) / 2

    def _default_rect(self):
        draw_w, draw_h, left, top = self._fit_source()
        target_w, target_h = size_tuple(self.page.size)
        if self.mode == "fixed" and target_w and target_h:
            width = min(draw_w - 12, max(90, target_w * self.canvas_scale))
            height = min(draw_h - 12, max(60, target_h * self.canvas_scale))
            ratio = target_w / target_h if target_h else 16 / 9
            if width / max(height, 1) > ratio:
                width = height * ratio
            else:
                height = width / ratio
            return [left + (draw_w - width) / 2, top + (draw_h - height) / 2, width, height]
        return [left + draw_w * 0.18, top + draw_h * 0.18, draw_w * 0.64, draw_h * 0.64]

    def _draw(self):
        if not self.winfo_exists():
            return
        draw_w, draw_h, left, top = self._fit_source()
        resized = self.source.resize((draw_w, draw_h), Image.Resampling.LANCZOS)
        self.photo = ImageTk.PhotoImage(resized)
        self.canvas.delete("all")
        self.canvas.create_image(left, top, image=self.photo, anchor="nw")
        if self.rect is None:
            self.rect = self._default_rect()
        self._draw_selection()

    def _draw_selection(self):
        if not self.rect:
            return
        self.canvas.delete("selection")
        x, y, w, h = self.rect
        self.canvas.create_rectangle(x, y, x + w, y + h, outline="#ffffff", width=2, tags="selection")
        self.canvas.create_rectangle(x + 1, y + 1, x + w - 1, y + h - 1, outline="#4d8cff", width=1, tags="selection")
        if self.mode == "free":
            for hx, hy in self._handles():
                self.canvas.create_rectangle(hx - 5, hy - 5, hx + 5, hy + 5, fill="#4d8cff", outline="#ffffff", tags="selection")
        actual_w = max(1, round(w / self.canvas_scale))
        actual_h = max(1, round(h / self.canvas_scale))
        self.canvas.create_rectangle(x, y + h + 8, x + 164, y + h + 32, fill="#07142c", outline="", tags="selection")
        self.canvas.create_text(x + 8, y + h + 20, text=f"{actual_w} × {actual_h} · {'固定尺寸' if self.mode == 'fixed' else '自由区域'}", anchor="w", fill="#ffffff", font=("Segoe UI", 9), tags="selection")

    def _handles(self):
        x, y, w, h = self.rect or (0, 0, 0, 0)
        return [(x, y), (x + w / 2, y), (x + w, y), (x + w, y + h / 2), (x + w, y + h), (x + w / 2, y + h), (x, y + h), (x, y + h / 2)]

    def _handle_at(self, px, py):
        for index, (hx, hy) in enumerate(self._handles()):
            if abs(px - hx) <= 9 and abs(py - hy) <= 9:
                return index
        return None

    def _pointer_down(self, event):
        if not self.rect:
            return
        x, y, w, h = self.rect
        handle = self._handle_at(event.x, event.y) if self.mode == "free" else None
        if handle is not None:
            self.interaction = {"kind": "resize", "handle": handle, "start": (event.x, event.y), "rect": self.rect.copy()}
        elif x <= event.x <= x + w and y <= event.y <= y + h:
            self.interaction = {"kind": "move", "start": (event.x, event.y), "rect": self.rect.copy()}
        elif self.mode == "free":
            self.drag_origin = (event.x, event.y)
            self.rect = [event.x, event.y, 1, 1]
            self.interaction = {"kind": "create", "start": (event.x, event.y), "rect": self.rect.copy()}
        self.canvas.configure(cursor="hand2" if self.interaction and self.interaction["kind"] == "move" else "sizing")

    def _pointer_move(self, event):
        if not self.interaction:
            self._update_cursor(event.x, event.y)
            return
        start_x, start_y = self.interaction["start"]
        base_x, base_y, base_w, base_h = self.interaction["rect"]
        dx, dy = event.x - start_x, event.y - start_y
        draw_w, draw_h, left, top = self._fit_source()
        if self.interaction["kind"] in ("move", "create"):
            if self.interaction["kind"] == "create":
                new_x, new_y = min(start_x, event.x), min(start_y, event.y)
                self.rect = [new_x, new_y, max(20, abs(dx)), max(20, abs(dy))]
            else:
                self.rect = [base_x + dx, base_y + dy, base_w, base_h]
        else:
            index = self.interaction["handle"]
            new_x, new_y, new_w, new_h = base_x, base_y, base_w, base_h
            if index in (0, 6, 7):
                new_x = min(base_x + dx, base_x + base_w - 42)
                new_w = base_w - (new_x - base_x)
            if index in (2, 3, 4):
                new_w = max(42, base_w + dx)
            if index in (0, 1, 2):
                new_y = min(base_y + dy, base_y + base_h - 42)
                new_h = base_h - (new_y - base_y)
            if index in (4, 5, 6):
                new_h = max(42, base_h + dy)
            self.rect = [new_x, new_y, new_w, new_h]
        x, y, w, h = self.rect
        self.rect[0] = min(max(left, x), left + draw_w - w)
        self.rect[1] = min(max(top, y), top + draw_h - h)
        self.rect[2] = min(w, left + draw_w - self.rect[0])
        self.rect[3] = min(h, top + draw_h - self.rect[1])
        self._draw_selection()

    def _update_cursor(self, px, py):
        if not self.rect:
            self.canvas.configure(cursor="crosshair")
            return
        x, y, w, h = self.rect
        if self.mode == "free" and self._handle_at(px, py) is not None:
            self.canvas.configure(cursor="sizing")
        elif x <= px <= x + w and y <= py <= y + h:
            self.canvas.configure(cursor="hand2")
        else:
            self.canvas.configure(cursor="crosshair")

    def _pointer_up(self, _event):
        self.interaction = None
        self._update_cursor(_event.x, _event.y)

    def _change_mode(self):
        self.mode = self.mode_var.get()
        self.rect = None
        self.subtitle.configure(text="拖出区域后，可拖边缘或四角调整" if self.mode == "free" else "移动固定尺寸框，选择截图区域")
        self._draw()

    def destroy(self):
        try:
            self.grab_release()
        except tk.TclError:
            pass
        super().destroy()
        if self.owner.winfo_exists():
            self.owner.after_idle(self.owner._restore_after_capture)

    def _confirm(self):
        if not self.rect:
            return
        _, _, image_left, image_top = self._fit_source()
        x, y, w, h = self.rect
        crop_box = (
            max(0, round((x - image_left) / self.canvas_scale)),
            max(0, round((y - image_top) / self.canvas_scale)),
            min(self.source.width, round((x + w - image_left) / self.canvas_scale)),
            min(self.source.height, round((y + h - image_top) / self.canvas_scale)),
        )
        if crop_box[2] <= crop_box[0] or crop_box[3] <= crop_box[1]:
            return
        crop = self.source.crop(crop_box)
        self.owner.receive_capture(self.page, crop)
        self.destroy()


class CompareApp(tk.Tk):
    BG = "#08152b"
    PANEL = "#0d1e3b"
    PANEL_2 = "#12284d"
    BORDER = "#294771"
    TEXT = "#f2f6ff"
    MUTED = "#91a4ca"
    BLUE = "#4d8cff"
    TEAL = "#58c7d8"

    def __init__(self):
        super().__init__()
        self.title("界面对比")
        try:
            self.iconbitmap(str(resource_path("app.ico")))
        except tk.TclError:
            pass
        self.geometry("1280x760")
        self.minsize(1100, 650)
        self.configure(bg=self.BG)
        self.project_dir = DEFAULT_PROJECT
        self.pages: list[Page] = []
        # 右侧截图是独立的槽位列表，拖动时只调整截图，不会带动左侧设计稿。
        self.capture_slots: list[Optional[str]] = []
        self.capture_slot_times: list[str] = []
        self.capture_slot_names: list[str] = []
        self.capture_rows: list[tk.Frame] = []
        self.selected_index = -1
        self.view_mode = "overlay"
        self.opacity = tk.DoubleVar(value=52)
        self.zoom = tk.DoubleVar(value=96)
        self.compare_offset = [0, 0]
        self.drag_start = None
        self.design_photo = None
        self.result_photo = None
        self.list_images: list[ImageTk.PhotoImage] = []
        self._build_styles()
        self._build_ui()
        self._load_project()

    def _build_styles(self):
        style = ttk.Style(self)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass
        style.configure("Tech.Horizontal.TScale", troughcolor="#1b3764", background=self.PANEL, sliderlength=16)

    def _build_ui(self):
        self.columnconfigure(1, weight=1)
        self.rowconfigure(1, weight=1)
        title = tk.Frame(self, bg="#091a36", height=58)
        title.grid(row=0, column=0, columnspan=3, sticky="nsew")
        title.grid_propagate(False)
        self._label(title, "▰", 15, self.BLUE, side="left", padx=(18, 8), font=("Segoe UI", 16, "bold"))
        self._label(title, "界面对比", 14, self.TEXT, side="left", font=("Microsoft YaHei UI", 13, "bold"))
        actions = tk.Frame(title, bg="#091a36")
        actions.pack(side="right", padx=16)
        self._button(actions, "导入设计稿", self.import_designs, primary=False).pack(side="left", padx=4)
        self._button(actions, "截屏", self.start_capture, primary=True).pack(side="left", padx=4)

        self.left = tk.Frame(self, bg=self.PANEL, width=220, highlightbackground=self.BORDER, highlightthickness=1)
        self.left.grid(row=1, column=0, sticky="nsew")
        self.left.grid_propagate(False)
        self.right = tk.Frame(self, bg=self.PANEL, width=228, highlightbackground=self.BORDER, highlightthickness=1)
        self.right.grid(row=1, column=2, sticky="nsew")
        self.right.grid_propagate(False)
        self._build_left()
        self._build_center()
        self._build_right()

    def _label(self, parent, text, size, color, side=None, padx=0, font=None):
        label = tk.Label(parent, text=text, bg=parent.cget("bg"), fg=color, font=font or ("Microsoft YaHei UI", size))
        if side:
            label.pack(side=side, padx=padx)
        return label

    def _button(self, parent, text, command, primary=False):
        return tk.Button(
            parent,
            text=text,
            command=command,
            width=12,
            height=1,
            relief="flat",
            bd=0,
            bg=(self.BLUE if primary else "#17345f"),
            fg=("#07142d" if primary else self.TEXT),
            activebackground="#68b6ff",
            activeforeground="#07142d",
            font=("Microsoft YaHei UI", 10),
            cursor="hand2",
            padx=10,
            pady=7,
        )

    def _build_left(self):
        header = tk.Frame(self.left, bg=self.PANEL)
        header.pack(fill="x", padx=14, pady=(16, 8))
        self._label(header, "设计稿", 12, self.TEXT, side="left", font=("Microsoft YaHei UI", 12, "bold"))
        self.page_count = self._label(header, "0 张", 10, self.MUTED, side="right")
        self.design_list = tk.Frame(self.left, bg=self.PANEL)
        self.design_list.pack(fill="both", expand=True, padx=8)

    def _build_right(self):
        header = tk.Frame(self.right, bg=self.PANEL)
        header.pack(fill="x", padx=14, pady=(16, 8))
        self._label(header, "当前截图", 12, self.TEXT, side="left", font=("Microsoft YaHei UI", 12, "bold"))
        self._label(header, "⠿", 16, self.MUTED, side="right")
        self.capture_list = tk.Frame(self.right, bg=self.PANEL)
        self.capture_list.pack(fill="both", expand=True, padx=8)

    def _build_center(self):
        self.center = tk.Frame(self, bg=self.BG)
        self.center.grid(row=1, column=1, sticky="nsew")
        self.center.rowconfigure(1, weight=1)
        self.center.columnconfigure(0, weight=1)
        workbar = tk.Frame(self.center, bg="#0b1d3b", height=58)
        workbar.grid(row=0, column=0, sticky="ew")
        workbar.grid_propagate(False)
        self.heading = self._label(workbar, "未选择设计稿", 11, self.TEXT, side="left", padx=(16, 0), font=("Microsoft YaHei UI", 12, "bold"))
        self.heading_size = self._label(workbar, "", 9, self.MUTED, side="left", padx=(8, 0))
        tabs = tk.Frame(workbar, bg="#17345f")
        tabs.place(relx=0.5, rely=0.5, anchor="center")
        self.view_buttons: dict[str, tk.Button] = {}
        for label, mode in (("设计稿", "design"), ("截图", "capture"), ("叠加", "overlay"), ("差异", "diff")):
            button = tk.Button(tabs, text=label, command=lambda m=mode: self.set_view(m), relief="flat", bd=0, bg="#17345f", fg=self.MUTED, padx=10, pady=5, cursor="hand2")
            button.pack(side="left")
            self.view_buttons[mode] = button
        self.canvas = tk.Canvas(self.center, bg="#0a1831", highlightthickness=0, cursor="hand2")
        self.canvas.grid(row=1, column=0, sticky="nsew")
        self.canvas.bind("<Configure>", lambda _event: self.render_compare())
        self.canvas.bind("<ButtonPress-1>", self._compare_down)
        self.canvas.bind("<B1-Motion>", self._compare_move)
        self.canvas.bind("<ButtonRelease-1>", self._compare_up)

        controls = tk.Frame(self.center, bg="#0b1d3b", height=64)
        controls.grid(row=2, column=0, sticky="ew")
        controls.grid_propagate(False)
        self._label(controls, "截图透明度", 10, self.MUTED, side="left", padx=(16, 5))
        ttk.Scale(controls, from_=0, to=100, variable=self.opacity, command=lambda _v: self.render_compare(), style="Tech.Horizontal.TScale", length=110).pack(side="left")
        self.opacity_value = self._label(controls, "52%", 10, self.TEXT, side="left", padx=(5, 18))
        self._label(controls, "截图缩放", 10, self.MUTED, side="left", padx=(0, 5))
        ttk.Scale(controls, from_=50, to=120, variable=self.zoom, command=lambda _v: self.render_compare(), style="Tech.Horizontal.TScale", length=110).pack(side="left")
        self.zoom_value = self._label(controls, "96%", 10, self.TEXT, side="left", padx=(5, 18))
        self._label(controls, "↕↔  可拖动截图对齐设计稿", 10, self.MUTED, side="left")
        self.reset_button = self._button(controls, "重置", self.reset_compare, primary=False)
        self.reset_button.pack(side="right", padx=4)
        self.compare_button = self._button(controls, "开始对比", self.compare, primary=True)
        self.compare_button.pack(side="right", padx=(4, 16))
        self.set_view("overlay")

    def _load_project(self):
        self.project_dir.mkdir(parents=True, exist_ok=True)
        metadata = self.project_dir / "project.json"
        if metadata.exists():
            try:
                payload = json.loads(metadata.read_text(encoding="utf-8"))
                self.pages = [Page(**item) for item in payload.get("pages", [])]
                self.capture_slots = list(payload.get("capture_slots", []))
                self.capture_slot_times = list(payload.get("capture_slot_times", []))
                self.capture_slot_names = list(payload.get("capture_slot_names", []))
            except Exception:
                self.pages = []
                self.capture_slots = []
                self.capture_slot_times = []
                self.capture_slot_names = []
        self._normalize_capture_slots()
        self.refresh_lists()

    def _normalize_capture_slots(self):
        count = len(self.pages)
        if not self.capture_slots:
            self.capture_slots = [page.capture_path for page in self.pages]
        self.capture_slots = (self.capture_slots + [None] * count)[:count]
        legacy_times = [page.capture_time for page in self.pages]
        self.capture_slot_times = (self.capture_slot_times + legacy_times + [""] * count)[:count]
        legacy_names = [f"{page.name}-截图" if page.capture_path else "" for page in self.pages]
        self.capture_slot_names = (self.capture_slot_names + legacy_names + [""] * count)[:count]

    def _save_project(self):
        self.project_dir.mkdir(parents=True, exist_ok=True)
        (self.project_dir / "designs").mkdir(exist_ok=True)
        (self.project_dir / "captures").mkdir(exist_ok=True)
        self._normalize_capture_slots()
        metadata = {
            "version": 2,
            "pages": [asdict(page) for page in self.pages],
            "capture_slots": self.capture_slots,
            "capture_slot_times": self.capture_slot_times,
            "capture_slot_names": self.capture_slot_names,
        }
        (self.project_dir / "project.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")

    def import_designs(self):
        paths = filedialog.askopenfilenames(title="导入设计稿", filetypes=IMAGE_TYPES)
        if not paths:
            return
        if not self.project_dir.exists() or self.project_dir == DEFAULT_PROJECT:
            selected = filedialog.askdirectory(title="选择项目保存位置")
            if selected:
                self.project_dir = Path(selected) / "ui-compare-project"
        (self.project_dir / "designs").mkdir(parents=True, exist_ok=True)
        existing = {(page.name, page.size) for page in self.pages}
        for raw_path in paths:
            source = Path(raw_path)
            name, size = parse_design_name(source)
            destination = self.project_dir / "designs" / source.name
            try:
                shutil.copy2(source, destination)
            except Exception:
                destination = source
            if (name, size) in existing:
                continue
            self.pages.append(Page(name=name, size=size, design_path=str(destination)))
            self.capture_slots.append(None)
            self.capture_slot_times.append("")
            self.capture_slot_names.append("")
            existing.add((name, size))
        if self.pages and self.selected_index < 0:
            self.selected_index = 0
        self._save_project()
        self.refresh_lists()

    def refresh_lists(self):
        for child in self.design_list.winfo_children():
            child.destroy()
        for child in self.capture_list.winfo_children():
            child.destroy()
        self.capture_rows = []
        self._normalize_capture_slots()
        self.list_images.clear()
        self.page_count.configure(text=f"{len(self.pages)} 张")
        for index, page in enumerate(self.pages):
            self._design_row(index, page).pack(fill="x", pady=2)
            capture_row = self._capture_row(
                index,
                self.capture_slots[index],
                self.capture_slot_times[index],
                self.capture_slot_names[index],
            )
            capture_row.pack(fill="x", pady=2)
            self.capture_rows.append(capture_row)
        if self.pages and not (0 <= self.selected_index < len(self.pages)):
            self.selected_index = 0
        self._refresh_selection()

    def _thumbnail(self, path: Optional[str], missing=False):
        if missing or not path or not Path(path).exists():
            image = Image.new("RGB", (84, 52), "#102548")
        else:
            try:
                image = Image.open(path).convert("RGB")
                image.thumbnail((84, 52), Image.Resampling.LANCZOS)
                canvas = Image.new("RGB", (84, 52), "#dce6ef")
                canvas.paste(image, ((84 - image.width) // 2, (52 - image.height) // 2))
                image = canvas
            except Exception:
                image = Image.new("RGB", (84, 52), "#102548")
        photo = ImageTk.PhotoImage(image)
        self.list_images.append(photo)
        return photo

    def _row_frame(self, parent, active=False):
        return tk.Frame(parent, bg=("#193d63" if active else parent.cget("bg")), highlightbackground=("#3b8bd1" if active else parent.cget("bg")), highlightthickness=1, padx=6, pady=5)

    def _design_row(self, index, page):
        frame = self._row_frame(self.design_list, index == self.selected_index)
        frame.bind("<Button-1>", lambda _event, i=index: self.select_page(i))
        photo = self._thumbnail(page.design_path)
        image_label = tk.Label(frame, image=photo, bg=frame.cget("bg"))
        image_label.pack(side="left")
        image_label.bind("<Button-1>", lambda _event, i=index: self.select_page(i))
        copy = tk.Frame(frame, bg=frame.cget("bg"))
        copy.pack(side="left", fill="x", expand=True, padx=8)
        name = tk.Label(copy, text=page.name, bg=frame.cget("bg"), fg=self.TEXT, font=("Microsoft YaHei UI", 10, "bold"), anchor="w")
        name.pack(fill="x")
        if index < len(self.capture_slots) and self.capture_slots[index]:
            row_status = "有差异" if self._capture_matches_design(index) else "尺寸不符"
        else:
            row_status = "未截屏"
        size = tk.Label(copy, text=f"{page.size} · {row_status}", bg=frame.cget("bg"), fg=self.MUTED, font=("Segoe UI", 8), anchor="w")
        size.pack(fill="x", pady=(2, 0))
        dot_color = {"有差异": "#f5b84e", "尺寸不符": "#f06b6b", "已通过": "#42ca9e", "未截屏": "#415574"}.get(row_status, "#415574")
        dot = tk.Label(frame, text="●", bg=frame.cget("bg"), fg=dot_color, font=("Segoe UI", 9))
        dot.pack(side="right")
        return frame

    def _capture_row(self, index, capture_path, capture_time, capture_name):
        frame = self._row_frame(self.capture_list, self.selected_index == index and capture_path is not None)
        frame.bind("<Button-1>", lambda _event, i=index: self.select_capture(i))
        photo = self._thumbnail(capture_path, missing=not capture_path)
        image_label = tk.Label(frame, image=photo, bg=frame.cget("bg"))
        image_label.pack(side="left")
        image_label.bind("<Button-1>", lambda _event, i=index: self.select_capture(i))
        copy = tk.Frame(frame, bg=frame.cget("bg"))
        copy.pack(side="left", fill="x", expand=True, padx=8)
        page = self.pages[index]
        text = capture_name or "未截取"
        capture_size = self._image_size(capture_path)
        size_text = f"{capture_size[0]} × {capture_size[1]}" if capture_size[0] else "等待捕获"
        meta = f"{capture_time} · {size_text}" if capture_path else f"等待捕获 · {page.size}"
        tk.Label(copy, text=text, bg=frame.cget("bg"), fg=self.TEXT, font=("Microsoft YaHei UI", 10, "bold"), anchor="w").pack(fill="x")
        tk.Label(copy, text=meta, bg=frame.cget("bg"), fg=self.MUTED, font=("Segoe UI", 8), anchor="w").pack(fill="x", pady=(2, 0))
        handle = tk.Label(frame, text="⠿", bg=frame.cget("bg"), fg=self.MUTED, font=("Segoe UI", 17), cursor="hand2")
        handle.pack(side="right")
        handle.bind("<ButtonPress-1>", lambda event, i=index: self._start_row_drag(event, i))
        handle.bind("<B1-Motion>", self._row_drag_motion)
        handle.bind("<ButtonRelease-1>", self._finish_row_drag)
        return frame

    def _start_row_drag(self, event, index):
        self.row_drag = {"index": index, "target": index, "start_y": event.y_root}
        self._show_drop_target(index)

    def _row_drag_motion(self, event):
        if not getattr(self, "row_drag", None) or len(self.pages) < 2:
            return
        current = self.row_drag["index"]
        target = len(self.capture_rows) - 1
        for row_index, row in enumerate(self.capture_rows):
            midpoint = row.winfo_rooty() + max(1, row.winfo_height()) / 2
            if event.y_root < midpoint:
                target = row_index
                break
        self.row_drag["target"] = max(0, min(len(self.capture_rows) - 1, target))
        self._show_drop_target(self.row_drag["target"])

    def _finish_row_drag(self, _event):
        if getattr(self, "row_drag", None):
            current = self.row_drag["index"]
            target = self.row_drag["target"]
            if current != target:
                self.capture_slots[current], self.capture_slots[target] = self.capture_slots[target], self.capture_slots[current]
                self.capture_slot_times[current], self.capture_slot_times[target] = self.capture_slot_times[target], self.capture_slot_times[current]
                self.capture_slot_names[current], self.capture_slot_names[target] = self.capture_slot_names[target], self.capture_slot_names[current]
            self._save_project()
            self.refresh_lists()
        self.row_drag = None

    def _show_drop_target(self, target):
        for index, row in enumerate(self.capture_rows):
            row.configure(highlightbackground=("#f5b84e" if index == target else row.cget("bg")))

    def select_page(self, index):
        self.selected_index = index
        self.compare_offset = [0, 0]
        self.zoom.set(96)
        self.opacity.set(52)
        self._refresh_selection()

    def select_capture(self, index):
        self.select_page(index)

    def _refresh_selection(self):
        if 0 <= self.selected_index < len(self.pages):
            page = self.pages[self.selected_index]
            self.heading.configure(text=page.name)
            self.heading_size.configure(text=f"设计稿 {page.size}")
        else:
            self.heading.configure(text="未选择设计稿")
            self.heading_size.configure(text="")
        enabled = self._capture_matches_design(self.selected_index) if 0 <= self.selected_index < len(self.pages) else False
        self.compare_button.configure(state=("normal" if enabled else "disabled"))
        self.render_compare()

    def _image_size(self, path: Optional[str]) -> tuple[int, int]:
        if not path or not Path(path).exists():
            return (0, 0)
        try:
            with Image.open(path) as image:
                return image.size
        except Exception:
            return (0, 0)

    def _capture_matches_design(self, slot_index: int) -> bool:
        if not (0 <= slot_index < len(self.capture_slots)):
            return False
        design_size = self._image_size(self.pages[slot_index].design_path)
        capture_size = self._image_size(self.capture_slots[slot_index])
        return bool(design_size[0] and design_size == capture_size)

    def set_view(self, mode):
        self.view_mode = mode
        for key, button in self.view_buttons.items():
            button.configure(bg=("#4d75cc" if key == mode else "#17345f"), fg=("#ffffff" if key == mode else self.MUTED))
        self.render_compare()

    def _load_image(self, path):
        try:
            return Image.open(path).convert("RGBA")
        except Exception:
            return None

    def render_compare(self):
        if not hasattr(self, "canvas"):
            return
        self.canvas.delete("all")
        self.opacity_value.configure(text=f"{int(self.opacity.get())}%")
        self.zoom_value.configure(text=f"{int(self.zoom.get())}%")
        if not (0 <= self.selected_index < len(self.pages)):
            self.canvas.create_text(
                canvas_w / 2,
                canvas_h / 2,
                text="导入设计稿后开始",
                anchor="center",
                justify="center",
                fill=self.MUTED,
                font=("Microsoft YaHei UI", 14),
            )
            return
        page = self.pages[self.selected_index]
        design = self._load_image(page.design_path)
        if design is None:
            return
        capture_path = self.capture_slots[self.selected_index] if self.selected_index < len(self.capture_slots) else None
        capture = self._load_image(capture_path) if capture_path else None
        canvas_w = max(1, self.canvas.winfo_width())
        canvas_h = max(1, self.canvas.winfo_height())
        scale = min(max(1, canvas_w - 40) / design.width, max(1, canvas_h - 40) / design.height, 1.0)
        display_w, display_h = max(1, int(design.width * scale)), max(1, int(design.height * scale))
        design_small = design.resize((display_w, display_h), Image.Resampling.LANCZOS)
        if self.view_mode == "design" or capture is None:
            composed = design_small
        else:
            zoom = self.zoom.get() / 100
            cap_size = (max(1, int(capture.width * scale * zoom)), max(1, int(capture.height * scale * zoom)))
            cap_small = capture.resize(cap_size, Image.Resampling.LANCZOS)
            if self.view_mode == "capture":
                composed = cap_small
            elif self.view_mode == "diff":
                base = Image.new("RGBA", design_small.size, "#eef3f8")
                base.alpha_composite(design_small)
                diff = ImageChops.difference(design_small.convert("RGB"), cap_small.resize(design_small.size, Image.Resampling.BILINEAR).convert("RGB")).convert("RGBA")
                composed = Image.blend(base, diff, 0.7)
            else:
                canvas_image = Image.new("RGBA", (max(design_small.width, cap_small.width + abs(self.compare_offset[0])), max(design_small.height, cap_small.height + abs(self.compare_offset[1]))), "#eef3f8")
                canvas_image.alpha_composite(design_small, (max(0, -self.compare_offset[0]), max(0, -self.compare_offset[1])))
                overlay = cap_small.copy()
                overlay.putalpha(int(255 * self.opacity.get() / 100))
                canvas_image.alpha_composite(overlay, (max(0, self.compare_offset[0]), max(0, self.compare_offset[1])))
                composed = canvas_image
        composed.thumbnail((max(1, canvas_w - 40), max(1, canvas_h - 40)), Image.Resampling.LANCZOS)
        self.result_photo = ImageTk.PhotoImage(composed)
        x = canvas_w / 2 - composed.width / 2
        y = canvas_h / 2 - composed.height / 2
        self.canvas.create_image(x, y, image=self.result_photo, anchor="nw")
        if capture and self.view_mode == "overlay":
            self.canvas.create_text(16, 16, text=f"截图 {int(self.zoom.get())}% · 透明度 {int(self.opacity.get())}%", anchor="nw", fill="#91a4ca", font=("Segoe UI", 9))
        if capture and not self._capture_matches_design(self.selected_index):
            capture_w, capture_h = self._image_size(capture_path)
            self.canvas.create_text(
                16,
                canvas_h - 18,
                text=f"截图尺寸 {capture_w} × {capture_h}，设计稿 {page.size}，请重新截取",
                anchor="sw",
                fill="#f06b6b",
                font=("Microsoft YaHei UI", 9),
            )

    def _compare_down(self, event):
        if self.view_mode not in ("capture", "overlay") or not (0 <= self.selected_index < len(self.pages)):
            return
        self.drag_start = (event.x, event.y, self.compare_offset[0], self.compare_offset[1])
        self.canvas.configure(cursor="hand2")

    def _compare_move(self, event):
        if not self.drag_start:
            return
        sx, sy, ox, oy = self.drag_start
        self.compare_offset = [ox + event.x - sx, oy + event.y - sy]
        self.render_compare()

    def _compare_up(self, _event):
        self.drag_start = None
        self.canvas.configure(cursor="hand2")

    def reset_compare(self):
        self.compare_offset = [0, 0]
        self.zoom.set(96)
        self.opacity.set(52)
        self.set_view("overlay")

    def compare(self):
        if not (0 <= self.selected_index < len(self.pages)) or not self._capture_matches_design(self.selected_index):
            return
        self.set_view("diff")

    def start_capture(self):
        if not self.pages:
            messagebox.showinfo("需要设计稿", "请先导入至少一张设计稿。")
            return
        if not (0 <= self.selected_index < len(self.pages)):
            self.selected_index = 0
        self.withdraw()
        try:
            CaptureOverlay(self, self.pages[self.selected_index])
        except Exception as exc:
            self._restore_after_capture()
            messagebox.showerror("截屏失败", str(exc))

    def _restore_after_capture(self):
        self.deiconify()
        self.state("normal")
        self.lift()
        self.focus_force()

    def receive_capture(self, page: Page, image: Image.Image):
        self.project_dir.mkdir(parents=True, exist_ok=True)
        capture_dir = self.project_dir / "captures"
        capture_dir.mkdir(exist_ok=True)
        safe_name = re.sub(r"[^\w\-一-龥]+", "_", page.name)
        path = capture_dir / f"{safe_name}_{time.strftime('%Y%m%d_%H%M%S')}.png"
        image.save(path, "PNG")
        self._normalize_capture_slots()
        slot_index = self.selected_index if 0 <= self.selected_index < len(self.pages) else self.pages.index(page)
        capture_time = time.strftime("%H:%M")
        self.capture_slots[slot_index] = str(path)
        self.capture_slot_times[slot_index] = capture_time
        self.capture_slot_names[slot_index] = f"{page.name}-截图"
        page.capture_path = str(path)
        page.capture_time = capture_time
        page.status = "有差异" if self._image_size(page.design_path) == image.size else "尺寸不符"
        self._save_project()
        self.refresh_lists()
        self.set_view("overlay")


if __name__ == "__main__":
    app = CompareApp()
    app.mainloop()
