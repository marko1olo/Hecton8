import pygetwindow as gw
import pyautogui
import time
import psutil

unity_pid = None
for p in psutil.process_iter(['name', 'pid']):
    if p.info['name'] == 'Unity.exe':
        unity_pid = p.info['pid']
        break

if unity_pid:
    wins = [w for w in gw.getAllWindows() if getattr(w, '_hWnd', None)]
    # Match pid via win32api or just activate all empty title windows?
    # Better: just use pywinauto
    from pywinauto import Application
    try:
        app = Application().connect(process=unity_pid)
        app.top_window().set_focus()
        time.sleep(1)
        pyautogui.hotkey('ctrl', 'r')
        print("Sent Ctrl+R")
    except Exception as e:
        print(e)
else:
    print("Unity process not found")
