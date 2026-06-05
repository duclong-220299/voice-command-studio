# Voice Detector - Ứng dụng Nhận Diện Giọng Nói

Ứng dụng Windows Forms để ghi âm, xử lý và nhận diện các lệnh giọng nói bằng tiếng Việt.

## ✨ Tính năng

- **Ghi âm** - Ghi lại âm thanh từ microphone
- **Xử lý tín hiệu âm thanh** - Trích xuất đặc trưng MFCC (Mel-frequency cepstral coefficients)
- **Hiển thị sóng âm** - Xem trực quan biểu đồ sóng âm trong thời gian thực
- **Lưu trữ lệnh** - Quản lý cơ sở dữ liệu các lệnh giọng nói
- **Phát lại âm thanh** - Phát lại các file ghi âm đã lưu

## 🛠️ Yêu cầu

- .NET 10.0 trở lên
- Windows 7 hoặc cao hơn
- Microphone để ghi âm

## 📥 Cài đặt

1. Clone hoặc tải về repository
2. Cài đặt .NET SDK từ [dotnet.microsoft.com](https://dotnet.microsoft.com)
3. Mở terminal tại thư mục project

## 🚀 Chạy ứng dụng

```bash
# Xây dựng project
dotnet build

# Chạy ứng dụng
dotnet run

# Hoặc chạy file .exe trực tiếp
.\bin\Debug\net10.0-windows\VoiceDetector.exe
```

## 📦 Dependencies

- **NAudio** - Thư viện xử lý âm thanh
- **Newtonsoft.Json** - Serialization dữ liệu

## 📁 Cấu trúc Project

```
voice-detector/
├── Program.cs              # Entry point ứng dụng
├── MainForm.cs             # Giao diện chính
├── AudioRecorder.cs        # Quản lý ghi âm
├── MFCCExtractor.cs        # Trích xuất đặc trưng âm thanh
├── WaveformControl.cs      # Control hiển thị sóng âm
├── VoiceCommandDatabase.cs # Quản lý cơ sở dữ liệu
├── ResultPopupForm.cs      # Giao diện kết quả
└── VoiceDetector.csproj    # Cấu hình project
```

## 💡 Cách sử dụng

1. Mở ứng dụng
2. Nhấn "Bắt đầu ghi" để bắt đầu ghi âm
3. Nói vào microphone
4. Nhấn "Dừng" để kết thúc ghi âm
5. Kết quả sẽ hiển thị và lưu vào cơ sở dữ liệu

## 🤝 Đóng góp

Chào mừng các pull request và issue!

## 📄 Giấy phép

MIT License

## 👤 Tác giả

Voice Command Application
