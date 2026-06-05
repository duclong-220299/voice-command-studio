using System;
using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;

namespace VoiceCommandApp
{
    public class AudioRecorder : IDisposable
    {
        private WaveInEvent waveIn;
        private List<float> recordedSamples = new List<float>();
        private bool isRecording = false;

        public int SampleRate { get; } = 16000;
        public int Channels { get; } = 1;

        public event EventHandler<float[]> WaveformData;
        public event EventHandler<float[]> RecordingComplete;

        public bool IsRecording => isRecording;

        public AudioRecorder()
        {
            InitDevice();
        }

        private void InitDevice()
        {
            try
            {
                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SampleRate, 16, Channels),
                    BufferMilliseconds = 50
                };
                waveIn.DataAvailable += OnDataAvailable;
            }
            catch (Exception ex)
            {
                throw new Exception($"Không thể khởi tạo microphone: {ex.Message}");
            }
        }

        public void StartRecording()
        {
            if (isRecording) return;
            recordedSamples.Clear();
            isRecording = true;
            waveIn.StartRecording();
        }

        public void StopRecording()
        {
            if (!isRecording) return;
            isRecording = false;
            waveIn.StopRecording();

            // Small delay to flush buffers
            Thread.Sleep(100);

            float[] samples = recordedSamples.ToArray();
            RecordingComplete?.Invoke(this, samples);
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            int sampleCount = e.BytesRecorded / 2;
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                samples[i] = sample / 32768f;
            }

            if (isRecording)
                recordedSamples.AddRange(samples);

            WaveformData?.Invoke(this, samples);
        }

        public static int GetDeviceCount() => WaveIn.DeviceCount;

        public static string GetDeviceName(int index)
        {
            if (index >= WaveIn.DeviceCount) return "Không có thiết bị";
            return WaveIn.GetCapabilities(index).ProductName;
        }

        public void Dispose()
        {
            waveIn?.Dispose();
        }
    }

    /// <summary>
    /// Audio player for playback of recorded samples
    /// </summary>
    public class AudioPlayer : IDisposable
    {
        private WaveOutEvent waveOut;
        private BufferedWaveProvider buffer;

        public AudioPlayer(int sampleRate = 16000)
        {
            waveOut = new WaveOutEvent();
            buffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 16, 1))
            {
                DiscardOnBufferOverflow = true
            };
            waveOut.Init(buffer);
        }

        public void Play(float[] samples)
        {
            buffer.ClearBuffer();
            byte[] bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)(Math.Max(-1f, Math.Min(1f, samples[i])) * 32767);
                bytes[i * 2] = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            buffer.AddSamples(bytes, 0, bytes.Length);
            waveOut.Play();
        }

        public void Dispose() => waveOut?.Dispose();
    }
}