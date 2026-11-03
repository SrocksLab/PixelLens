from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox


APP_NAME = "界面对比"
EXE_NAME = "UICompare.exe"


def resource_path(name: str) -> Path:
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    return base / name


def powershell_quote(value: str) -> str:
    return value.replace("'", "''")


def create_shortcut(shortcut_path: Path, target: Path):
    script = (
        "$s=New-Object -ComObject WScript.Shell;"
        f"$l=$s.CreateShortcut('{powershell_quote(str(shortcut_path))}');"
        f"$l.TargetPath='{powershell_quote(str(target))}';"
        f"$l.WorkingDirectory='{powershell_quote(str(target.parent))}';"
        f"$l.Description='{powershell_quote(APP_NAME)}';$l.Save()"
    )
    try:
        subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script], check=False, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    except Exception:
        pass


def install_files(target_dir: Path):
    source = resource_path(EXE_NAME)
    if not source.exists():
        raise FileNotFoundError("安装包缺少 UICompare.exe。")
    target_dir.mkdir(parents=True, exist_ok=True)
    target = target_dir / EXE_NAME
    shutil.copy2(source, target)
    desktop = Path(os.environ.get("USERPROFILE", Path.home())) / "Desktop"
    start_menu = Path(os.environ.get("APPDATA", Path.home())) / "Microsoft" / "Windows" / "Start Menu" / "Programs"
    create_shortcut(desktop / f"{APP_NAME}.lnk", target)
    create_shortcut(start_menu / f"{APP_NAME}.lnk", target)
    return target


class Installer(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(f"安装 {APP_NAME}")
        try:
            self.iconbitmap(str(resource_path("app.ico")))
        except tk.TclError:
            pass
        # 预留完整的底部操作区，避免按钮被窗口底部裁切。
        self.geometry("500x320")
        self.minsize(500, 320)
        self.resizable(False, False)
        self.configure(bg="#08152b")
        self.install_dir = Path(os.environ.get("LOCALAPPDATA", Path.home())) / "Programs" / APP_NAME
        self._build()

    def _build(self):
        tk.Label(self, text=APP_NAME, bg="#08152b", fg="#f2f6ff", font=("Microsoft YaHei UI", 18, "bold")).pack(anchor="w", padx=30, pady=(28, 4))
        tk.Label(self, text="Windows 桌面版 UI 走查比对工具", bg="#08152b", fg="#91a4ca", font=("Microsoft YaHei UI", 10)).pack(anchor="w", padx=30)
        tk.Label(self, text="安装位置", bg="#08152b", fg="#f2f6ff", font=("Microsoft YaHei UI", 10)).pack(anchor="w", padx=30, pady=(24, 6))
        row = tk.Frame(self, bg="#08152b")
        row.pack(fill="x", padx=30)
        self.path_var = tk.StringVar(value=str(self.install_dir))
        tk.Entry(row, textvariable=self.path_var, bg="#102548", fg="#f2f6ff", insertbackground="#ffffff", relief="flat", font=("Segoe UI", 10)).pack(side="left", fill="x", expand=True, ipady=7)
        tk.Button(row, text="选择", command=self.choose_dir, bg="#17345f", fg="#f2f6ff", relief="flat", padx=14, cursor="hand2").pack(side="left", padx=(8, 0), ipady=4)
        self.status = tk.Label(self, text="安装后会自动创建桌面快捷方式", bg="#08152b", fg="#91a4ca", font=("Microsoft YaHei UI", 9))
        self.status.pack(anchor="w", padx=30, pady=(16, 0))
        action_area = tk.Frame(self, bg="#08152b", height=56)
        action_area.pack(fill="x", padx=30, pady=(12, 24))
        action_area.pack_propagate(False)
        self.install_button = tk.Button(
            action_area,
            text="开始安装",
            command=self.install,
            bg="#4d8cff",
            fg="#07142d",
            relief="flat",
            anchor="center",
            justify="center",
            width=14,
            height=1,
            padx=0,
            pady=0,
            cursor="hand2",
            font=("Microsoft YaHei UI", 10, "bold"),
        )
        self.install_button.pack(side="right", fill="y", ipadx=8)

    def choose_dir(self):
        selected = filedialog.askdirectory(title="选择安装位置", initialdir=str(self.install_dir.parent))
        if selected:
            self.path_var.set(str(Path(selected) / APP_NAME))

    def install(self):
        target_dir = Path(self.path_var.get().strip()).expanduser()
        try:
            target = install_files(target_dir)
            self.status.configure(text=f"已安装到：{target_dir}", fg="#42ca9e")
            self.install_button.configure(text="打开软件", command=lambda: (subprocess.Popen([str(target)]), self.destroy()))
        except Exception as exc:
            messagebox.showerror("安装失败", str(exc))


if __name__ == "__main__":
    if "--silent" in sys.argv:
        target_arg = next((arg.split("=", 1)[1] for arg in sys.argv if arg.startswith("--target=")), "")
        target = Path(target_arg) if target_arg else Path(os.environ.get("LOCALAPPDATA", Path.home())) / "Programs" / APP_NAME
        install_files(target)
    else:
        Installer().mainloop()
