using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace VoiceCommandApp
{
    /// <summary>
    /// Mẫu giọng nói cho một khẩu lệnh
    /// </summary>
    public class VoiceTemplate
    {
        public string CommandName { get; set; }
        public string CommandLabel { get; set; }
        public List<double[][]> MFCCRecordings { get; set; } = new List<double[][]>();
        public DateTime LastUpdated { get; set; }
        public int RecordingCount => MFCCRecordings.Count;

        public VoiceTemplate(string commandName, string commandLabel)
        {
            CommandName = commandName;
            CommandLabel = commandLabel;
            LastUpdated = DateTime.Now;
        }

        public void AddRecording(double[][] mfcc)
        {
            MFCCRecordings.Add(mfcc);
            LastUpdated = DateTime.Now;
        }

        public void ClearRecordings()
        {
            MFCCRecordings.Clear();
        }

        /// <summary>
        /// Tính khoảng cách trung bình DTW từ template đến chuỗi query
        /// </summary>
        public double MatchScore(double[][] queryMFCC)
        {
            if (MFCCRecordings.Count == 0) return double.MaxValue;

            double minDist = double.MaxValue;
            foreach (var template in MFCCRecordings)
            {
                double d = MFCCExtractor.DTWDistance(template, queryMFCC);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }
    }

    /// <summary>
    /// Quản lý và mã hóa lưu/tải dữ liệu mẫu giọng nói
    /// </summary>
    public class VoiceCommandDatabase
    {
        private static readonly string DataFile = "voice_commands.dat";
        private static readonly byte[] EncryptionKey = DeriveKey("VoiceCmd_Secret_2024");
        private static readonly byte[] IV = new byte[16] { 0x1A, 0x2B, 0x3C, 0x4D, 0x5E, 0x6F, 0x70, 0x81, 0x92, 0xA3, 0xB4, 0xC5, 0xD6, 0xE7, 0xF8, 0x09 };

        public Dictionary<string, VoiceTemplate> Templates { get; private set; }

        public static readonly string[] CommandNames = { "tat_may", "dung_day", "di_thoi" };
        public static readonly string[] CommandLabels = { "Tắt máy", "Đứng dậy", "Đi thôi" };
        public static readonly string[] CommandEmojis = { "⛔", "🧍", "🚶" };
        public static readonly string[] CommandColors = { "#FF4757", "#2ED573", "#1E90FF" };

        public VoiceCommandDatabase()
        {
            Templates = new Dictionary<string, VoiceTemplate>();
            for (int i = 0; i < CommandNames.Length; i++)
                Templates[CommandNames[i]] = new VoiceTemplate(CommandNames[i], CommandLabels[i]);
        }

        public void Save()
        {
            try
            {
                var data = new SerializableData();
                foreach (var kvp in Templates)
                {
                    var entry = new TemplateEntry
                    {
                        CommandName = kvp.Value.CommandName,
                        CommandLabel = kvp.Value.CommandLabel,
                        LastUpdated = kvp.Value.LastUpdated,
                        Recordings = new List<List<List<double>>>()
                    };

                    foreach (var rec in kvp.Value.MFCCRecordings)
                    {
                        var frames = new List<List<double>>();
                        foreach (var frame in rec)
                            frames.Add(new List<double>(frame));
                        entry.Recordings.Add(frames);
                    }
                    data.Templates.Add(entry);
                }

                string json = JsonConvert.SerializeObject(data, Formatting.None);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encrypted = Encrypt(plainBytes);
                File.WriteAllBytes(DataFile, encrypted);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi lưu dữ liệu: {ex.Message}", ex);
            }
        }

        public bool Load()
        {
            try
            {
                if (!File.Exists(DataFile)) return false;

                byte[] encrypted = File.ReadAllBytes(DataFile);
                byte[] plainBytes = Decrypt(encrypted);
                string json = Encoding.UTF8.GetString(plainBytes);
                var data = JsonConvert.DeserializeObject<SerializableData>(json);

                if (data?.Templates == null) return false;

                foreach (var entry in data.Templates)
                {
                    if (!Templates.ContainsKey(entry.CommandName))
                        continue;

                    var template = Templates[entry.CommandName];
                    template.ClearRecordings();
                    template.LastUpdated = entry.LastUpdated;

                    foreach (var recList in entry.Recordings)
                    {
                        var frames = new double[recList.Count][];
                        for (int i = 0; i < recList.Count; i++)
                            frames[i] = recList[i].ToArray();
                        template.AddRecording(frames);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Encrypt(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(data, 0, data.Length);
        }

        private static byte[] Decrypt(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, 0, data.Length);
        }

        private static byte[] DeriveKey(string password)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        // Serialization helpers
        private class SerializableData
        {
            public List<TemplateEntry> Templates { get; set; } = new List<TemplateEntry>();
        }

        private class TemplateEntry
        {
            public string CommandName { get; set; }
            public string CommandLabel { get; set; }
            public DateTime LastUpdated { get; set; }
            public List<List<List<double>>> Recordings { get; set; }
        }
    }
}