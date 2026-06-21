import re

with open("Assets/Shapes/Scripts/Runtime/Utils/ShapesMath.cs", "r") as f:
    math_content = f.read()

print("CalcBezierPointCount in ShapesMath: ", "CalcBezierPointCount" in math_content)
