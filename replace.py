import re

content = open('Assets/MapMagic/Tools/Matrix/Matrix.cs', 'r').read()

search = """							float fallof;
							if (transition == 0)
								fallof = dist>radius ? 0 : 1;
							else
							{
								fallof = 1 - (dist-radius) / transition;
								if (fallof>1) fallof = 1; if (fallof<0) fallof = 0;
								if (smoothFallof) fallof = 3*fallof*fallof - 2*fallof*fallof*fallof;
							}"""

replace = """							float totalRadius = radius + transition;
							float hardness = totalRadius == 0 ? 1 : radius / totalRadius;
							float fallof = new Coord(x,z).GetFalloff(new Vector2D(centerX, centerZ), totalRadius, hardness, smoothFallof ? 1 : 0);"""

if search in content:
    print("Found! Replacing.")
    content = content.replace(search, replace)
    open('Assets/MapMagic/Tools/Matrix/Matrix.cs', 'w').write(content)
else:
    print("Not found.")

