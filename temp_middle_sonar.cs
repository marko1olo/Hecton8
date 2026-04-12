             if (_sonarStatusLabel != null)
             {
                 string sonarText = active == SpectrumMode.Sonar
                     ? $"СОНАР АКТИВЕН — РАДИУС: {(sys != null ? "100" : "—")}М"
                     : string.Empty;
                 if (_sonarStatusLabel.text != sonarText)
                 {
                     _sonarStatusLabel.text = sonarText;
                 }
             }
