
import re

# Read the file
with open(r'C:\hades\Hecton8\Assets\_Project\Scripts\UI\PDAShellChrome.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find and replace the left footer section
for i, line in enumerate(lines):
    if 'if (_leftFooterText != null &&' in line and '_lastCargoCells != cargoCells' in lines[i+1]:
        # Found the start of the if statement
        # Replace the entire block
        lines[i] = '              if (_leftFooterText != null && (_lastCargoCells != cargoCells || _lastCargoTotal != cargoTotal || _lastWeightDeci != weightDeci || _lastReadyTools != readyTools || _lastAssignedTools != assignedTools))\n'
        lines[i+1] = '              {\n'
        lines[i+2] = '                  string cargoText = string.Format("CARGO {0}/{1}  |  MASS {2:0.0} kg  |  READY TOOLS {3}/{4}", cargoCells, cargoTotal, weight, readyTools, Mathf.Max(assignedTools, 1));\n'
        lines[i+3] = '                  if (_leftFooterText.text != cargoText)\n'
        lines[i+4] = '                  {\n'
        lines[i+5] = '                      _leftFooterText.text = cargoText;\n'
        lines[i+6] = '                  }\n'
        lines[i+7] = '                  _lastCargoCells = cargoCells;\n'
        lines[i+8] = '                  _lastCargoTotal = cargoTotal;\n'
        lines[i+9] = '                  _lastWeightDeci = weightDeci;\n'
        lines[i+10] = '                  _lastReadyTools = readyTools;\n'
        lines[i+11] = '                  _lastAssignedTools = assignedTools;\n'
        lines[i+12] = '              }\n'
        # Remove the extra lines that were part of the old block
        del lines[i+13:i+13+7]  # Remove 7 extra lines
        break

# Write back the file
with open(r'C:\hades\Hecton8\Assets\_Project\Scripts\UI\PDAShellChrome.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print('Fixed left footer in PDAShellChrome.cs')

