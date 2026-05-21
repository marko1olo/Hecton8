using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Hecton8.Audio.Synthesis.Editor
{
    public sealed class DigitalVoiceForgeWindow : EditorWindow
    {
        private const string DefaultCsv = "Docs/Audio/dialogue_script.csv";
        private const string DefaultOut = "Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin";
        private const uint MockHash = 0x05203E88u;

        private TextField _csvField;
        private TextField _outField;
        private TextField _xttsField;
        private EnumField _codecField;
        private ProgressBar _progress;
        private Label _status;
        private Label _stateLabel;
        private VisualElement _waveform;
        private Process _process;
        private readonly StringBuilder _stdout = new StringBuilder(4096);
        private readonly StringBuilder _stderr = new StringBuilder(4096);
        private readonly object _processOutputLock = new object();
        private double _startedAt;

        private enum CodecMode
        {
            h8adpcm,
            pcm16
        }

        [MenuItem("HECTON-8/Audio/Digital Voice Forge")]
        public static void Open()
        {
            DigitalVoiceForgeWindow window = GetWindow<DigitalVoiceForgeWindow>();
            window.titleContent = new GUIContent("Digital Voice Forge");
            window.minSize = new Vector2(520f, 360f);
        }

        private void OnEnable()
        {
            BuildUi();
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (_process != null && !_process.HasExited)
                _process.Kill();
            if (_process != null)
            {
                _process.OutputDataReceived -= OnProcessOutputData;
                _process.ErrorDataReceived -= OnProcessErrorData;
                _process.Dispose();
            }
            _process = null;
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _csvField = new TextField("CSV") { value = DefaultCsv };
            _outField = new TextField("Output") { value = DefaultOut };
            _xttsField = new TextField("XTTS Command") { value = string.Empty };
            _codecField = new EnumField("Codec", CodecMode.h8adpcm);
            Button bake = new Button(StartBake) { text = "Bake Bank" };
            Button validate = new Button(ValidateAbi) { text = "Validate ABI" };
            Button mock = new Button(PushMockCue) { text = "Push Mock Cue" };
            _progress = new ProgressBar { title = "Idle", value = 0f };
            _status = new Label("No bake running.");
            _stateLabel = new Label("Phrase 00000000 | speed 0.00 | volume 0.00 | q 0.00");
            _waveform = new VisualElement();
            _waveform.style.height = 120;
            _waveform.style.marginTop = 8;
            _waveform.style.borderBottomWidth = 1;
            _waveform.style.borderTopWidth = 1;
            _waveform.style.borderLeftWidth = 1;
            _waveform.style.borderRightWidth = 1;
            _waveform.generateVisualContent += DrawWaveform;

            rootVisualElement.Add(_csvField);
            rootVisualElement.Add(_outField);
            rootVisualElement.Add(_codecField);
            rootVisualElement.Add(_xttsField);
            VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(bake);
            row.Add(validate);
            row.Add(mock);
            rootVisualElement.Add(row);
            rootVisualElement.Add(_progress);
            rootVisualElement.Add(_status);
            rootVisualElement.Add(_stateLabel);
            rootVisualElement.Add(_waveform);
        }

        private void StartBake()
        {
            if (_process != null && !_process.HasExited)
                return;

            string repo = Directory.GetCurrentDirectory();
            string script = Path.Combine(repo, "Tools", "voice_baker.py");
            if (!File.Exists(script))
            {
                _status.text = "voice_baker.py missing.";
                return;
            }

            string codec = _codecField.value.ToString();
            string args = "\"" + script + "\" --csv \"" + _csvField.value + "\" --out \"" + _outField.value + "\" --codec " + codec;
            if (!string.IsNullOrWhiteSpace(_xttsField.value))
                args += " --xtts-command \"" + _xttsField.value.Replace("\"", "\\\"") + "\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = ResolvePythonExecutable(),
                Arguments = args,
                WorkingDirectory = repo,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            lock (_processOutputLock)
            {
                _stdout.Length = 0;
                _stderr.Length = 0;
            }
            _process = Process.Start(psi);
            if (_process != null)
            {
                _process.OutputDataReceived += OnProcessOutputData;
                _process.ErrorDataReceived += OnProcessErrorData;
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            _startedAt = EditorApplication.timeSinceStartup;
            _progress.title = "Baking";
            _progress.value = 12f;
            _status.text = "voice_baker.py running.";
        }

        private void Tick()
        {
            if (_process != null)
            {
                double elapsed = EditorApplication.timeSinceStartup - _startedAt;
                _progress.value = (float)math.min(95.0, 12.0 + elapsed * 18.0);
                if (_process.HasExited)
                {
                    string stdout;
                    string stderr;
                    lock (_processOutputLock)
                    {
                        stdout = _stdout.ToString();
                        stderr = _stderr.ToString();
                    }
                    int code = _process.ExitCode;
                    _process.OutputDataReceived -= OnProcessOutputData;
                    _process.ErrorDataReceived -= OnProcessErrorData;
                    _process.Dispose();
                    _process = null;
                    _progress.value = code == 0 ? 100f : 0f;
                    _progress.title = code == 0 ? "Baked" : "Failed";
                    _status.text = code == 0 ? stdout.Trim() : stderr.Trim();
                    AssetDatabase.Refresh();
                }
            }

            if (VocalBankPlaybackRuntime.TryGetEditorState(out VocalStateDTO state, out VocalCodecStateDTO codec))
            {
                _stateLabel.text = string.Concat(
                    "Phrase ",
                    state.PhraseHashID.ToString("X8"),
                    " | speed ",
                    state.PlaybackSpeed.ToString("0.00"),
                    " | volume ",
                    state.VolumeScalar.ToString("0.00"),
                    " | q ",
                    codec.QualityWeight01.ToString("0.00"));
            }
            else
            {
                _stateLabel.text = "Phrase 00000000 | speed 0.00 | volume 0.00 | q 0.00";
            }

            _waveform?.MarkDirtyRepaint();
        }

        private void OnProcessOutputData(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;
            lock (_processOutputLock)
                _stdout.AppendLine(e.Data);
        }

        private void OnProcessErrorData(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;
            lock (_processOutputLock)
                _stderr.AppendLine(e.Data);
        }

        private static string ResolvePythonExecutable()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ? "python" : "python3";
        }

        private static void ValidateAbi()
        {
            VocalStateLayoutValidator.ValidateOrThrow();
            Debug.Log("[SHINOBU_260] Digital Voice Forge ABI validation passed.");
        }

        private static void PushMockCue()
        {
            VocalCueSignal cue = default;
            cue.PhraseHashID = MockHash;
            cue.Priority = 16;
            cue.VolumeScalar = 1f;
            cue.PlaybackSpeed = 1f;
            cue.RadioDistortion01 = 0.42f;
            cue.SpatialBlend01 = 0f;
            SignalBus<VocalCueSignal>.TryPush(in cue);
        }

        private static void DrawWaveform(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            Painter2D p = ctx.painter2D;
            p.lineWidth = 1.4f;
            p.strokeColor = new Color(0.42f, 0.94f, 0.82f, 1f);
            float mid = r.y + r.height * 0.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x, mid));
            int points = 128;
            for (int i = 0; i < points; i++)
            {
                float sample;
                if (!VocalBankPlaybackRuntime.TryGetEditorWaveformSample(points - 1 - i, out sample))
                    sample = 0f;
                float x = r.x + (r.width * i) / math.max(1, points - 1);
                float y = mid - sample * r.height * 0.45f;
                p.LineTo(new Vector2(x, y));
            }
            p.Stroke();
        }
    }
}
