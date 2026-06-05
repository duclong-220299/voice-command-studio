using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoiceCommandApp
{
    /// <summary>
    /// Trích xuất đặc trưng MFCC (Mel-Frequency Cepstral Coefficients) từ âm thanh thô
    /// </summary>
    public static class MFCCExtractor
    {
        private const int SampleRate = 16000;
        private const int NumMFCC = 13;
        private const int NumFilterBanks = 26;
        private const int FFTSize = 512;
        private const int HopSize = 160;   // 10ms hop
        private const int FrameSize = 400; // 25ms frame

        public static double[][] Extract(float[] samples)
        {
            var frames = Framing(samples);
            var result = new List<double[]>();

            foreach (var frame in frames)
            {
                var windowed = ApplyHamming(frame);
                var spectrum = FFT(windowed);
                var filterBankEnergies = MelFilterBank(spectrum);
                var mfcc = DCT(filterBankEnergies);
                result.Add(mfcc);
            }

            return result.ToArray();
        }

        private static List<float[]> Framing(float[] samples)
        {
            var frames = new List<float[]>();
            int start = 0;
            while (start + FrameSize <= samples.Length)
            {
                var frame = new float[FrameSize];
                Array.Copy(samples, start, frame, 0, FrameSize);
                frames.Add(frame);
                start += HopSize;
            }
            if (frames.Count == 0 && samples.Length > 0)
            {
                var frame = new float[FrameSize];
                int copyLen = Math.Min(samples.Length, FrameSize);
                Array.Copy(samples, 0, frame, 0, copyLen);
                frames.Add(frame);
            }
            return frames;
        }

        private static double[] ApplyHamming(float[] frame)
        {
            var result = new double[frame.Length];
            for (int i = 0; i < frame.Length; i++)
            {
                double w = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (frame.Length - 1));
                result[i] = frame[i] * w;
            }
            return result;
        }

        private static double[] FFT(double[] frame)
        {
            int n = FFTSize;
            var complex = new Complex[n];
            for (int i = 0; i < Math.Min(frame.Length, n); i++)
                complex[i] = new Complex(frame[i], 0);
            for (int i = frame.Length; i < n; i++)
                complex[i] = Complex.Zero;

            FFTRecursive(complex);

            var power = new double[n / 2 + 1];
            for (int i = 0; i < power.Length; i++)
                power[i] = (complex[i].Real * complex[i].Real + complex[i].Imaginary * complex[i].Imaginary) / n;

            return power;
        }

        private static void FFTRecursive(Complex[] x)
        {
            int n = x.Length;
            if (n <= 1) return;

            var even = new Complex[n / 2];
            var odd = new Complex[n / 2];
            for (int i = 0; i < n / 2; i++)
            {
                even[i] = x[2 * i];
                odd[i] = x[2 * i + 1];
            }

            FFTRecursive(even);
            FFTRecursive(odd);

            for (int k = 0; k < n / 2; k++)
            {
                Complex t = Complex.FromPolarCoordinates(1, -2 * Math.PI * k / n) * odd[k];
                x[k] = even[k] + t;
                x[k + n / 2] = even[k] - t;
            }
        }

        private static double[] MelFilterBank(double[] powerSpectrum)
        {
            double melMin = HzToMel(0);
            double melMax = HzToMel(SampleRate / 2.0);

            var melPoints = new double[NumFilterBanks + 2];
            for (int i = 0; i < melPoints.Length; i++)
                melPoints[i] = MelToHz(melMin + (melMax - melMin) * i / (NumFilterBanks + 1));

            int specLen = powerSpectrum.Length;
            var freqBins = new double[specLen];
            for (int i = 0; i < specLen; i++)
                freqBins[i] = (double)i * SampleRate / (2.0 * (specLen - 1));

            var energies = new double[NumFilterBanks];
            for (int m = 0; m < NumFilterBanks; m++)
            {
                double sum = 0;
                for (int k = 0; k < specLen; k++)
                {
                    double f = freqBins[k];
                    double w = 0;
                    if (f >= melPoints[m] && f <= melPoints[m + 1])
                        w = (f - melPoints[m]) / (melPoints[m + 1] - melPoints[m]);
                    else if (f >= melPoints[m + 1] && f <= melPoints[m + 2])
                        w = (melPoints[m + 2] - f) / (melPoints[m + 2] - melPoints[m + 1]);
                    sum += w * powerSpectrum[k];
                }
                energies[m] = Math.Log(sum + 1e-10);
            }
            return energies;
        }

        private static double[] DCT(double[] x)
        {
            int n = x.Length;
            var result = new double[NumMFCC];
            for (int k = 0; k < NumMFCC; k++)
            {
                double sum = 0;
                for (int i = 0; i < n; i++)
                    sum += x[i] * Math.Cos(Math.PI * k * (2 * i + 1) / (2 * n));
                result[k] = sum;
            }
            return result;
        }

        private static double HzToMel(double hz) => 2595 * Math.Log10(1 + hz / 700.0);
        private static double MelToHz(double mel) => 700 * (Math.Pow(10, mel / 2595.0) - 1);

        /// <summary>
        /// Tính khoảng cách DTW giữa 2 chuỗi MFCC
        /// </summary>
        public static double DTWDistance(double[][] seq1, double[][] seq2)
        {
            if (seq1 == null || seq2 == null || seq1.Length == 0 || seq2.Length == 0)
                return double.MaxValue;

            int n = seq1.Length;
            int m = seq2.Length;
            var dtw = new double[n + 1, m + 1];

            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= m; j++)
                    dtw[i, j] = double.MaxValue / 2;

            dtw[0, 0] = 0;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    double cost = EuclideanDistance(seq1[i - 1], seq2[j - 1]);
                    dtw[i, j] = cost + Math.Min(dtw[i - 1, j],
                                        Math.Min(dtw[i, j - 1], dtw[i - 1, j - 1]));
                }
            }

            return dtw[n, m] / (n + m);
        }

        private static double EuclideanDistance(double[] a, double[] b)
        {
            double sum = 0;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
                sum += (a[i] - b[i]) * (a[i] - b[i]);
            return Math.Sqrt(sum);
        }

        /// <summary>
        /// Tính RMS energy để phát hiện khoảng lặng
        /// </summary>
        public static double RMSEnergy(float[] samples)
        {
            double sum = 0;
            foreach (var s in samples) sum += s * s;
            return Math.Sqrt(sum / samples.Length);
        }

        /// <summary>
        /// Chuẩn hóa biên độ âm thanh
        /// </summary>
        public static float[] Normalize(float[] samples)
        {
            float max = 0;
            foreach (var s in samples) if (Math.Abs(s) > max) max = Math.Abs(s);
            if (max < 1e-6f) return samples;
            var norm = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) norm[i] = samples[i] / max;
            return norm;
        }

        /// <summary>
        /// Lọc bỏ khoảng im lặng ở đầu/cuối
        /// </summary>
        public static float[] TrimSilence(float[] samples, double threshold = 0.01)
        {
            int frameLen = 160;
            int start = 0, end = samples.Length;

            for (int i = 0; i < samples.Length - frameLen; i += frameLen)
            {
                var frame = new float[frameLen];
                Array.Copy(samples, i, frame, 0, frameLen);
                if (RMSEnergy(frame) > threshold) { start = i; break; }
            }

            for (int i = samples.Length - frameLen; i >= start; i -= frameLen)
            {
                var frame = new float[Math.Min(frameLen, samples.Length - i)];
                Array.Copy(samples, i, frame, 0, frame.Length);
                if (RMSEnergy(frame) > threshold) { end = Math.Min(i + frameLen, samples.Length); break; }
            }

            if (end <= start) return samples;
            var trimmed = new float[end - start];
            Array.Copy(samples, start, trimmed, 0, trimmed.Length);
            return trimmed;
        }
    }
}