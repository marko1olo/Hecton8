import os

def print_tree(startpath, indent=""):
    """
    Rekursivnaya funktsiya dlya vyvoda struktury papok v vide dereva.
    """
    # Poluchaem spisok vseh elementov v papke
    try:
        entries = os.listdir(startpath)
    except PermissionError:
        print(f"{indent}[Net dostupa k etoy papke]")
        return
    except FileNotFoundError:
        print(f"{indent}[Papka ne naydena]")
        return

    # Sortiruem: snachala papki, potom fayly (dlya udobstva chteniya)
    # Mozhno ubrat key, esli nuzhno prosto po alfavitu
    entries.sort(key=lambda e: (not os.path.isdir(os.path.join(startpath, e)), e.lower()))

    for i, entry in enumerate(entries):
        entry_path = os.path.join(startpath, entry)
        is_last = (i == len(entries) - 1)
        
        # Vybiraem simvol vetki: ├── dlya promezhutochnyh, └── dlya poslednego
        connector = "└── " if is_last else "├── "
        
        # Pechataem imya fayla ili papki
        # Dobavlyaem slesh v kontse, esli eto papka, dlya naglyadnosti
        display_name = entry + "/" if os.path.isdir(entry_path) else entry
        print(f"{indent}{connector}{display_name}")

        # Esli eto papka, zahodim vnutr (rekursiya)
        if os.path.isdir(entry_path):
            # Dobavlyaem otstup dlya sleduyuschego urovnya
            # Esli element posledniy, to vertikalnoy linii | ne budet
            extension = "    " if is_last else "│   "
            print_tree(entry_path, indent + extension)

# --- NASTROYKI ---
# Put k vashey papke. Obratite vnimanie na bukvu 'r' pered kavychkami, 
# chtoby obratnye sleshi ne vosprinimalis kak spetssimvoly.
target_path = r"C:\hades\Hecton8\Assets\GPUInstancer"

# Zapusk
if __name__ == "__main__":
    print(f"Struktura papki: {target_path}\n")
    # Pechataem kornevuyu papku
    print(os.path.basename(target_path) + "/") 
    print_tree(target_path)