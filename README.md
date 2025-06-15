# Network Traffic Sonification

Ever wake up with a crazy idea and build it? Here is "Could I listen to my network traffic?", or "A real-time network packet capture and sonification tool that transforms your network traffic into both visual and auditory experiences. Watch packets flow across your screen while hearing the sounds of your digital world."

![image](https://github.com/user-attachments/assets/fb39a203-ca76-4743-aa5e-6f9e8e3e5188)

## 🎵 What It Does

**Network Traffic Sonification** converts network packets into sound and visual effects in real-time:

- **Visual**: Watch colorful packets flow across your screen, each representing real network traffic
- **Audio**: Hear your network activity as different tones, frequencies, and waveforms
- **Real-time**: Experience your digital world as it happens

### Audio Mapping
- **Packet Size** → Volume & Pitch (larger packets = louder & higher)
- **Protocol Type** → Waveform Shape:
  - TCP/HTTP/HTTPS: Smooth sine waves (musical tones)
  - UDP: Sharp square waves (digital sounds)
  - DNS/ICMP: Triangle waves (distinctive pings)

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET Framework 4.8
- Visual Studio 2019 or later

### Installation & Running

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/network-traffic-sonification.git
   cd network-traffic-sonification
   ```

2. **Open in Visual Studio:**
   - Open `NetworkSonification.sln`
   - Build the solution (Ctrl+Shift+B)

3. **Install NuGet packages** (if not auto-restored):
   ```
   Install-Package NAudio -Version 2.2.1
   Install-Package SharpPcap -Version 6.2.5
   Install-Package PacketDotNet -Version 1.4.7
   ```

4. **Run the application:**
   - Press F5 or click "Start"
   - The app will automatically detect available capture methods

## 🔧 Capture Modes

The application uses an intelligent **hybrid approach** with automatic fallback:

### 1. 🏆 **WinPcap/Npcap Mode** (Best Experience)
- **Full packet inspection** with all protocols
- **Ethernet + IP layer analysis**
- **Most detailed packet information**
- **Requirements**: [Npcap](https://nmap.org/npcap/) installed

### 2. 🥈 **Raw Socket Mode** (Good Fallback)
- **IP-level packet capture**
- **Basic protocol detection** (TCP, UDP, ICMP, HTTP, HTTPS, DNS)
- **Works on any Windows system**
- **Requirements**: Administrator privileges

### 3. 🥉 **Demo Mode** (Always Works)
- **Simulated realistic network traffic**
- **All visualization and audio features**
- **Perfect for testing/demonstration**
- **Requirements**: None

## 🎮 Usage

1. **Select Interface**: Choose your network interface from the dropdown
2. **Start Capture**: Click "Start Capture" to begin monitoring
3. **Audio Control**: Click the mute/unmute button to control sound output
4. **Watch & Listen**: Enjoy the audiovisual representation of your network!

### Interface Elements

- **Top Panel**: Network interface selection and controls
- **Middle Panel**: Real-time packet flow visualization
- **Bottom Panel**: Oscilloscope-style waveform display
- **Status Bar**: Live statistics and mode information

## 🔊 Audio Features

- **Continuous Audio Stream**: Never stops playing once started
- **Overlapping Tones**: Multiple simultaneous sounds for busy networks
- **Natural Fade**: Smooth transitions as packets come and go
- **Anti-Clipping**: Protected against audio distortion
- **Muted by Default**: Starts silent for safe operation

## 🎨 Visual Features

- **Full-Width Animation**: Packets travel across the entire screen
- **Protocol-Specific Colors**:
  - 🔵 TCP: Blue
  - 🔴 UDP: Red  
  - 🟢 HTTP: Green
  - 🟣 HTTPS: Purple
  - 🟠 DNS: Orange
  - 🟡 ICMP: Yellow
  - 🔵 IP: Cyan
- **Size-Based Scaling**: Larger packets appear bigger and move faster
- **Glow Effects**: Particles fade naturally with subtle lighting
- **Real-time Oscilloscope**: Live waveform visualization

## 🛠️ Technical Details

### Built With
- **C# / WPF** - Modern Windows desktop application
- **NAudio** - Real-time audio synthesis and playback
- **SharpPcap** - Network packet capture (when available)
- **PacketDotNet** - Packet parsing and analysis
- **.NET Framework 4.8** - Broad Windows compatibility

### Architecture
- **Hybrid Capture System**: Automatic detection and fallback
- **Real-time Audio Engine**: Continuous buffer-based audio generation
- **Thread-safe Processing**: Concurrent packet queues and processing
- **WPF Visualization**: Hardware-accelerated graphics rendering

## 🚨 Important Notes

### For Real Packet Capture
- **Administrator Rights**: Required for raw socket and WinPcap modes
- **Npcap Installation**: Download from [nmap.org/npcap](https://nmap.org/npcap/)
  - ⚠️ **Important**: Check "Install Npcap in WinPcap API-compatible Mode" during installation
- **Firewall**: Windows may prompt for network access permissions

### Privacy & Security
- **Local Processing**: All packet analysis happens locally on your machine
- **No Data Transmission**: No network data is sent anywhere
- **No Logging**: Packets are processed in real-time and discarded

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **NAudio** - Excellent .NET audio library
- **SharpPcap** - Robust packet capture for .NET
- **Nmap Project** - For Npcap packet capture driver

## 🐛 Troubleshooting

### Common Issues

**"Npcap/WinPcap not installed"**
- Install [Npcap](https://nmap.org/npcap/) with WinPcap compatibility mode

**"Access denied" or "Run as Administrator"**
- Right-click the executable and select "Run as administrator"

**Audio cuts out**
- This version includes continuous audio buffering to prevent dropouts
- Try unmuting and muting again if issues persist

**No packets visible**
- Ensure you're connected to a network
- Try demo mode to verify the application is working
- Check Windows Firewall settings

**Performance issues**
- Close other network-intensive applications
- Try reducing visual effects by modifying the fade rates in code

---

**Transform your network into art. Listen to the internet.** 🎵🌐
