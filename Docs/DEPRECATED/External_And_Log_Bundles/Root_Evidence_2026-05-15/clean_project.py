import os
import shutil

def clean_third_party_assets(assets_dir, exclude_dirs=None):
    if exclude_dirs is None:
        exclude_dirs = ["_Project"]

    target_folders = ["Demo", "Example", "Tutorial", "Demos", "Examples", "Tutorials"]
    deleted_count = 0

    print(f"Сканирование папки: {assets_dir}...")
    
    for root, dirs, files in os.walk(assets_dir, topdown=False):
        # Пропускаем папки-исключения
        if any(ex in root for ex in exclude_dirs):
            continue
            
        for d in dirs:
            if d in target_folders:
                dir_path = os.path.join(root, d)
                print(f"Удаление: {dir_path}")
                try:
                    shutil.rmtree(dir_path)
                    # Также удаляем .meta файл папки
                    meta_path = dir_path + ".meta"
                    if os.path.exists(meta_path):
                        os.remove(meta_path)
                    deleted_count += 1
                except Exception as e:
                    print(f"Ошибка при удалении {dir_path}: {e}")

    print(f"\nОчистка завершена. Удалено папок: {deleted_count}")

if __name__ == "__main__":
    current_dir = os.path.dirname(os.path.abspath(__file__))
    assets_dir = os.path.join(current_dir, "Assets")
    
    if os.path.exists(assets_dir):
        clean_third_party_assets(assets_dir)
    else:
        print("Папка Assets не найдена!")
