#if UNITY_EDITOR
using Hecton8.Core;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Hecton8.UI.Editor
{
    /// <summary>
    /// Utility for generating placeholder audio clips for UI feedback.
    /// Creates simple sine wave clips for testing before final audio assets.
    /// </summary>
    public static class UIAudioPlaceholderGenerator
    {
        private const int SampleRate = 44100;
        private const int Channels = 1;

        [MenuItem("Hecton8/UI/Generate Placeholder Audio Clips")]
        public static void GeneratePlaceholderClips()
        {
            string folderPath = "Assets/_Project/Audio/UI/Placeholders";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Button clicks
            GenerateClick(folderPath, "UI_Click_Primary", 800f, 0.05f, 0.5f);
            GenerateClick(folderPath, "UI_Click_Secondary", 600f, 0.05f, 0.4f);
            GenerateClick(folderPath, "UI_Click_Destructive", 400f, 0.08f, 0.6f);
            GenerateClick(folderPath, "UI_Hover", 1000f, 0.03f, 0.3f);

            // Slider/Toggle
            GenerateTick(folderPath, "UI_Slider_Tick", 1200f, 0.02f, 0.3f);
            GenerateTone(folderPath, "UI_Toggle_On", 1500f, 0.05f, 0.4f);
            GenerateTone(folderPath, "UI_Toggle_Off", 800f, 0.05f, 0.4f);

            // Panel sounds
            GenerateWhoosh(folderPath, "UI_Panel_Open", 200f, 800f, 0.15f, 0.5f);
            GenerateWhoosh(folderPath, "UI_Panel_Close", 800f, 200f, 0.15f, 0.5f);

            AssetDatabase.Refresh();
            Debug.Log($"[UIAudioPlaceholderGenerator] Generated placeholder audio clips in {folderPath}");
        }

        private static void GenerateClick(string folderPath, string name, float frequency, float duration, float volume)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = 1f - (t / duration); // Decay envelope
                samples[i] = MathLodApproximation.ApproxSinBhaskara(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            SaveAudioClip(folderPath, name, samples);
        }

        private static void GenerateTick(string folderPath, string name, float frequency, float duration, float volume)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = MathLodApproximation.ApproxExpNegPade33Wide40(t * 50f); // Fast decay
                samples[i] = MathLodApproximation.ApproxSinBhaskara(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            SaveAudioClip(folderPath, name, samples);
        }

        private static void GenerateTone(string folderPath, string name, float frequency, float duration, float volume)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = MathLodApproximation.ApproxSinBhaskara(Mathf.PI * t / duration);
                samples[i] = MathLodApproximation.ApproxSinBhaskara(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            SaveAudioClip(folderPath, name, samples);
        }

        private static void GenerateWhoosh(string folderPath, string name, float startFreq, float endFreq, float duration, float volume)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;
                float frequency = Mathf.Lerp(startFreq, endFreq, progress);
                float envelope = MathLodApproximation.ApproxSinBhaskara(Mathf.PI * progress);
                samples[i] = MathLodApproximation.ApproxSinBhaskara(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            SaveAudioClip(folderPath, name, samples);
        }

        private static void SaveAudioClip(string folderPath, string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, Channels, SampleRate, false);
            clip.SetData(samples, 0);

            string filePath = $"{folderPath}/{name}.wav";
            SavWav.Save(filePath, clip);
        }

        // Simple WAV file writer
        private static class SavWav
        {
            public static void Save(string filepath, AudioClip clip)
            {
                using (FileStream fileStream = CreateEmpty(filepath))
                {
                    ConvertAndWrite(fileStream, clip);
                    WriteHeader(fileStream, clip);
                }
            }

            private static FileStream CreateEmpty(string filepath)
            {
                FileStream fileStream = new FileStream(filepath, FileMode.Create);
                byte emptyByte = 0;

                for (int i = 0; i < 44; i++) // WAV header is 44 bytes
                {
                    fileStream.WriteByte(emptyByte);
                }

                return fileStream;
            }

            private static void ConvertAndWrite(FileStream fileStream, AudioClip clip)
            {
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                short[] intData = new short[samples.Length];
                byte[] bytesData = new byte[samples.Length * 2];

                int rescaleFactor = 32767;

                for (int i = 0; i < samples.Length; i++)
                {
                    intData[i] = (short)(samples[i] * rescaleFactor);
                    byte[] byteArr = System.BitConverter.GetBytes(intData[i]);
                    byteArr.CopyTo(bytesData, i * 2);
                }

                fileStream.Write(bytesData, 0, bytesData.Length);
            }

            private static void WriteHeader(FileStream fileStream, AudioClip clip)
            {
                int hz = clip.frequency;
                int channels = clip.channels;
                int samples = clip.samples;

                fileStream.Seek(0, SeekOrigin.Begin);

                byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
                fileStream.Write(riff, 0, 4);

                byte[] chunkSize = System.BitConverter.GetBytes(fileStream.Length - 8);
                fileStream.Write(chunkSize, 0, 4);

                byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
                fileStream.Write(wave, 0, 4);

                byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
                fileStream.Write(fmt, 0, 4);

                byte[] subChunk1 = System.BitConverter.GetBytes(16);
                fileStream.Write(subChunk1, 0, 4);

                ushort one = 1;
                byte[] audioFormat = System.BitConverter.GetBytes(one);
                fileStream.Write(audioFormat, 0, 2);

                byte[] numChannels = System.BitConverter.GetBytes(channels);
                fileStream.Write(numChannels, 0, 2);

                byte[] sampleRate = System.BitConverter.GetBytes(hz);
                fileStream.Write(sampleRate, 0, 4);

                byte[] byteRate = System.BitConverter.GetBytes(hz * channels * 2);
                fileStream.Write(byteRate, 0, 4);

                ushort blockAlign = (ushort)(channels * 2);
                fileStream.Write(System.BitConverter.GetBytes(blockAlign), 0, 2);

                ushort bps = 16;
                byte[] bitsPerSample = System.BitConverter.GetBytes(bps);
                fileStream.Write(bitsPerSample, 0, 2);

                byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
                fileStream.Write(datastring, 0, 4);

                byte[] subChunk2 = System.BitConverter.GetBytes(samples * channels * 2);
                fileStream.Write(subChunk2, 0, 4);
            }
        }
    }
}
#endif
