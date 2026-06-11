🔒 Remove unsafe eval() usage in OOP_Voxel_Scanner.py

🎯 **What:**
The `eval_int_expr` function inside `Tools/OOP_Voxel_Scanner.py` was using Python's built-in `eval()` function to calculate expression strings dynamically. This has been replaced with a secure evaluation function utilizing `ast.parse`.

⚠️ **Risk:**
Although `eval()` was used with an empty `__builtins__` dictionary and regular expressions were utilized to sanitize input strings, using `eval()` on any user-controllable or file-parsed input carries a severe risk of Remote Code Execution (RCE) via bypasses.

🛡️ **Solution:**
Removed `eval()` and introduced `_safe_eval`, which parses the expression into an Abstract Syntax Tree (AST) using `ast.parse(expr_str, mode='eval')`. A customized parser safely evaluates only expected operations (mathematical operations and bitwise operators) directly mapped to the `operator` module while ignoring other potentially hazardous nodes (like function calls). This mitigates any dynamic code execution risks while maintaining compatibility with the existing voxel scanner functionality.
