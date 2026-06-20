import cv2
import numpy as np

for i in range(3):
    path = f'Terrain_View_{i}.png'
    img = cv2.imread(path)
    if img is None:
        print(f'{path} not found.')
        continue
    
    magenta_lower = np.array([180, 0, 180])
    magenta_upper = np.array([255, 50, 255])
    
    mask = cv2.inRange(img, magenta_lower, magenta_upper)
    magenta_pixels = cv2.countNonZero(mask)
    total_pixels = img.shape[0] * img.shape[1]
    
    print(f'View {i}: Magenta pixels: {magenta_pixels} / {total_pixels} ({(magenta_pixels/total_pixels)*100:.2f}%)')
