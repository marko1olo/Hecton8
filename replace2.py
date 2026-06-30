import re

content = open('Assets/MapMagic/Tools/Matrix/Matrix.cs', 'r').read()

search = """				/// Center does not need to be the real center, it's just used to calculate fallof
				/// Hardness is the percent (0-1) of the stamp that has 100% fallof
				/// Used in Locks (seems only)
				/// TODO: switch to Fallof
				{"""

replace = """				/// Center does not need to be the real center, it's just used to calculate fallof
				/// Hardness is the percent (0-1) of the stamp that has 100% fallof
				/// Used in Locks (seems only)
				{"""

if search in content:
    print("Found! Replacing.")
    content = content.replace(search, replace)
    open('Assets/MapMagic/Tools/Matrix/Matrix.cs', 'w').write(content)
else:
    print("Not found.")
