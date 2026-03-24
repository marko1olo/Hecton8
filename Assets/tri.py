import os

def print_tree(startpath, indent=""):
    """
    Рекурсивная функция для вывода структуры папок в виде дерева.
    """
    # Получаем список всех элементов в папке
    try:
        entries = os.listdir(startpath)
    except PermissionError:
        print(f"{indent}[Нет доступа к этой папке]")
        return
    except FileNotFoundError:
        print(f"{indent}[Папка не найдена]")
        return

    # Сортируем: сначала папки, потом файлы (для удобства чтения)
    # Можно убрать key, если нужно просто по алфавиту
    entries.sort(key=lambda e: (not os.path.isdir(os.path.join(startpath, e)), e.lower()))

    for i, entry in enumerate(entries):
        entry_path = os.path.join(startpath, entry)
        is_last = (i == len(entries) - 1)
        
        # Выбираем символ ветки: ├── для промежуточных, └── для последнего
        connector = "└── " if is_last else "├── "
        
        # Печатаем имя файла или папки
        # Добавляем слэш в конце, если это папка, для наглядности
        display_name = entry + "/" if os.path.isdir(entry_path) else entry
        print(f"{indent}{connector}{display_name}")

        # Если это папка, заходим внутрь (рекурсия)
        if os.path.isdir(entry_path):
            # Добавляем отступ для следующего уровня
            # Если элемент последний, то вертикальной линии | не будет
            extension = "    " if is_last else "│   "
            print_tree(entry_path, indent + extension)

# --- НАСТРОЙКИ ---
# Путь к вашей папке. Обратите внимание на букву 'r' перед кавычками, 
# чтобы обратные слеши не воспринимались как спецсимволы.
target_path = r"C:\hades\Hecton8\Assets\GPUInstancer"

# Запуск
if __name__ == "__main__":
    print(f"Структура папки: {target_path}\n")
    # Печатаем корневую папку
    print(os.path.basename(target_path) + "/") 
    print_tree(target_path)