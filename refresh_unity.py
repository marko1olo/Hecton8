import pyautogui
import time
import psutil
from pywinauto import Application

unity_pid = None
for p in psutil.process_iter(['name', 'pid']):
    if p.info['name'] == 'Unity.exe':
        unity_pid = p.info['pid']
        break

if unity_pid:
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
