
import re

# Read the file
with open(r'C:\hades\Hecton8\Assets\_Project\Scripts\UI\PDAShellChrome.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Fix left footer - replace the exact pattern
left_pattern = r'''              if \(_leftFooterText != null \&\& \(_lastCargoCells != cargoCells \|\| _lastCargoTotal != cargoTotal \|\| _lastWeightDeci != weightDeci \|\| _lastReadyTools != readyTools \|\| _lastAssignedTools != assignedTools\)\)\n              {\n                  _leftFooterText.SetText\(\n                      "CARGO \{0\}/{1\}  \|  MASS \{2:0\.0\} kg  \|  READY TOOLS \{3\}/{4\}",\n                      cargoCells, cargoTotal, weight, readyTools, Mathf\.Max\(assignedTools, 1\)\);\n                  _lastCargoCells = cargoCells;\n                  _lastCargoTotal = cargoTotal;\n                  _lastWeightDeci = weightDeci;\n                  _lastReadyTools = readyTools;\n                  _lastAssignedTools = assignedTools;\n              }'''

left_replacement = '''              if (_leftFooterText != null && (_lastCargoCells != cargoCells || _lastCargoTotal != cargoTotal || _lastWeightDeci != weightDeci || _lastReadyTools != readyTools || _lastAssignedTools != assignedTools))
              {
                  string cargoText = string.Format("CARGO {0}/{1}  |  MASS {2:0.0} kg  |  READY TOOLS {3}/{4}", cargoCells, cargoTotal, weight, readyTools, Mathf.Max(assignedTools, 1));
                  if (_leftFooterText.text != cargoText)
                  {
                      _leftFooterText.text = cargoText;
                  }
                  _lastCargoCells = cargoCells;
                  _lastCargoTotal = cargoTotal;
                  _lastWeightDeci = weightDeci;
                  _lastReadyTools = readyTools;
                  _lastAssignedTools = assignedTools;
              }'''

content = re.sub(left_pattern, left_replacement, content)

# Write back the file
with open(r'C:\hades\Hecton8\Assets\_Project\Scripts\UI\PDAShellChrome.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Fixed left footer in PDAShellChrome.cs')

