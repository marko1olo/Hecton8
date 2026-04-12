             if (_leftFooterText != null && (_lastCargoCells != cargoCells || _lastCargoTotal != cargoTotal || _lastWeightDeci != weightDeci || _lastReadyTools != readyTools || _lastAssignedTools != assignedTools))
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
             }
