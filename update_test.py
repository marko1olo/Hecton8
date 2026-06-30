import re

with open('Assets/Candice AI for Games/Tests/CandiceAIControllerTests.cs', 'r') as f:
    content = f.read()

# is3D is public property Is3D
# object col is private (no access modifier) or internal. Reflection with NonPublic | Instance is correct because it's private by default.
# MovePoint is initialized as Vector3.zero by default in Unity, but we can set it explicitly.
# MovePoint has a public property `MovePoint`

content = content.replace('_controller.MainTarget = _targetGo;', '_controller.MainTarget = _targetGo;\n            _controller.MovePoint = Vector3.zero;')

with open('Assets/Candice AI for Games/Tests/CandiceAIControllerTests.cs', 'w') as f:
    f.write(content)
