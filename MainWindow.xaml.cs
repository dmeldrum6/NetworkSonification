﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetworkSonification
{
    public enum CaptureMode
    {
        WinPcap,
        RawSocket,
        Demo
    }

    public class ActiveTone
    {
        public double Frequency { get; set; }
        public float Amplitude { get; set; }
        public SignalGeneratorType WaveType { get; set; }
        public double Life { get; set; }
        public double Phase { get; set; }
        public string Protocol { get; set; }
    }

    public class PacketData
    {
        public int Size { get; set; }
        public DateTime Timestamp { get; set; }
        public string Protocol { get; set; }
        public string SourceIP { get; set; }
        public string DestinationIP { get; set; }
        public int Port { get; set; }
    }

    public class PacketVisual
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; }
        public Color Color { get; set; }
        public double Life { get; set; }
        public string Protocol { get; set; }
    }

    public partial class MainWindow : Window
    {
        private LibPcapLiveDevice selectedDevice;
        private Socket rawSocket;
        private CaptureMode currentMode;
        private bool isCapturing;
        private bool isMuted = true; // Start muted by default
        private Thread captureThread;

        private WaveOutEvent waveOut;
        private BufferedWaveProvider bufferedProvider;
        private ConcurrentQueue<PacketData> packetQueue;
        private DispatcherTimer visualTimer;
        private DispatcherTimer demoTimer;
        private DispatcherTimer audioTimer;
        private List<double> audioSamples;
        private List<PacketVisual> packetVisuals;
        private List<ActiveTone> activeTones;
        private Random random;

        // Audio parameters
        private const int SAMPLE_RATE = 44100;
        private const int BUFFER_SIZE = 1024;

        public MainWindow()
        {
            InitializeComponent();
            InitializeAudio();
            InitializeVisualization();
            DetectCaptureMethods();
            UpdateMuteButton();
        }

        private void InitializeAudio()
        {
            packetQueue = new ConcurrentQueue<PacketData>();
            audioSamples = new List<double>();
            activeTones = new List<ActiveTone>();
            random = new Random();

            // Setup continuous audio buffer
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, 1);
            bufferedProvider = new BufferedWaveProvider(waveFormat);
            bufferedProvider.BufferLength = SAMPLE_RATE * 2; // 2 seconds buffer
            bufferedProvider.DiscardOnBufferOverflow = true;

            waveOut = new WaveOutEvent();
            waveOut.Init(bufferedProvider);

            // Timer for continuous audio generation
            audioTimer = new DispatcherTimer();
            audioTimer.Interval = TimeSpan.FromMilliseconds(20); // 50Hz audio updates
            audioTimer.Tick += AudioTimer_Tick;
        }

        private void AudioTimer_Tick(object sender, EventArgs e)
        {
            GenerateContinuousAudio();
        }

        private void InitializeVisualization()
        {
            packetVisuals = new List<PacketVisual>();

            // Timer for updating visuals
            visualTimer = new DispatcherTimer();
            visualTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            visualTimer.Tick += UpdateVisualization;

            // Timer for demo mode
            demoTimer = new DispatcherTimer();
            demoTimer.Interval = TimeSpan.FromMilliseconds(100);
            demoTimer.Tick += GenerateDemoPacket;
        }

        private void DetectCaptureMethods()
        {
            // Try WinPcap/Npcap first
            if (TryLoadWinPcapInterfaces())
            {
                currentMode = CaptureMode.WinPcap;
                StatusLabel.Content = "Ready - WinPcap/Npcap detected";
                return;
            }

            // Try Raw Sockets
            if (TryTestRawSocket())
            {
                currentMode = CaptureMode.RawSocket;
                LoadRawSocketInterface();
                StatusLabel.Content = "Ready - Raw Socket mode (Run as Admin for best results)";
                return;
            }

            // Fall back to Demo mode
            currentMode = CaptureMode.Demo;
            LoadDemoMode();
            StatusLabel.Content = "Ready - Demo mode (Install Npcap for real capture)";
        }

        private bool TryLoadWinPcapInterfaces()
        {
            try
            {
                InterfaceComboBox.Items.Clear();
                var devices = LibPcapLiveDeviceList.Instance;

                foreach (var device in devices)
                {
                    InterfaceComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = string.Format("[WinPcap] {0}", device.Description ?? device.Name),
                        Tag = device
                    });
                }

                if (InterfaceComboBox.Items.Count > 0)
                {
                    InterfaceComboBox.SelectedIndex = 0;
                    return true;
                }
            }
            catch (System.DllNotFoundException)
            {
                // WinPcap/Npcap not available
            }
            catch (Exception)
            {
                // Other WinPcap errors
            }

            return false;
        }

        private bool TryTestRawSocket()
        {
            try
            {
                // Test if we can create a raw socket
                using (var testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, System.Net.Sockets.ProtocolType.IP))
                {
                    // Don't bind during test, just check if we can create it
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void LoadRawSocketInterface()
        {
            InterfaceComboBox.Items.Clear();
            InterfaceComboBox.Items.Add(new ComboBoxItem
            {
                Content = "[Raw Socket] All IP Traffic",
                Tag = "raw_socket"
            });
            InterfaceComboBox.SelectedIndex = 0;
        }

        private void LoadDemoMode()
        {
            InterfaceComboBox.Items.Clear();
            InterfaceComboBox.Items.Add(new ComboBoxItem
            {
                Content = "[Demo] Simulated Network Traffic",
                Tag = "demo"
            });
            InterfaceComboBox.SelectedIndex = 0;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (isCapturing)
            {
                StopCapture();
                StartButton.Content = "Start Capture";
            }
            else
            {
                StartCapture();
                StartButton.Content = "Stop Capture";
            }
        }

        private void StartCapture()
        {
            try
            {
                isCapturing = true;

                if (currentMode == CaptureMode.WinPcap)
                {
                    StartWinPcapCapture();
                }
                else if (currentMode == CaptureMode.RawSocket)
                {
                    StartRawSocketCapture();
                }
                else if (currentMode == CaptureMode.Demo)
                {
                    StartDemoCapture();
                }

                visualTimer.Start();
                audioTimer.Start();

                if (!isMuted)
                {
                    waveOut.Play();
                }
            }
            catch (Exception ex)
            {
                isCapturing = false;
                MessageBox.Show(string.Format("Error starting capture: {0}", ex.Message),
                               "Capture Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartWinPcapCapture()
        {
            var selectedItem = InterfaceComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null && selectedItem.Tag is LibPcapLiveDevice)
            {
                var device = selectedItem.Tag as LibPcapLiveDevice;
                selectedDevice = device;
                selectedDevice.OnPacketArrival += OnWinPcapPacketArrival;
                selectedDevice.Open(DeviceModes.Promiscuous, 1000);
                selectedDevice.StartCapture();

                StatusLabel.Content = string.Format("Capturing via WinPcap: {0}", device.Description ?? device.Name);
            }
        }

        private void StartRawSocketCapture()
        {
            try
            {
                rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, System.Net.Sockets.ProtocolType.IP);

                // Get local IP address for binding
                string localIP = GetLocalIPAddress();
                if (localIP == null)
                {
                    throw new Exception("Could not determine local IP address");
                }

                rawSocket.Bind(new IPEndPoint(IPAddress.Parse(localIP), 0));
                rawSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);

                // Enable promiscuous mode (SIO_RCVALL)
                try
                {
                    byte[] byTrue = BitConverter.GetBytes(1);
                    byte[] byOut = new byte[4];
                    rawSocket.IOControl(IOControlCode.ReceiveAll, byTrue, byOut);
                }
                catch (Exception ex)
                {
                    // If promiscuous mode fails, continue anyway - we'll still get some packets
                    System.Diagnostics.Debug.WriteLine(string.Format("Could not enable promiscuous mode: {0}", ex.Message));
                }

                captureThread = new Thread(RawSocketCaptureLoop);
                captureThread.IsBackground = true;
                captureThread.Start();

                StatusLabel.Content = string.Format("Capturing via Raw Socket: {0}", localIP);
            }
            catch (UnauthorizedAccessException)
            {
                throw new Exception("Raw socket requires Administrator privileges. Please run as Administrator.");
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.AccessDenied)
                {
                    throw new Exception("Access denied. Please run as Administrator to use raw sockets.");
                }
                else
                {
                    throw new Exception(string.Format("Socket error: {0}. Try running as Administrator.", ex.Message));
                }
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint.Address.ToString();
                }
            }
            catch
            {
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ip.ToString();
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private void StartDemoCapture()
        {
            demoTimer.Start();
            StatusLabel.Content = "Demo Mode: Simulated network traffic";
        }

        private void RawSocketCaptureLoop()
        {
            byte[] buffer = new byte[65536]; // Larger buffer for raw packets

            while (isCapturing && rawSocket != null)
            {
                try
                {
                    int received = rawSocket.Receive(buffer, SocketFlags.None);
                    if (received > 20) // Minimum IP header size
                    {
                        var packetData = AnalyzeRawPacket(buffer, received);
                        if (packetData != null)
                        {
                            packetQueue.Enqueue(packetData);
                            GenerateAudioFromPacket(packetData);
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (isCapturing && ex.SocketErrorCode != SocketError.Interrupted)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Raw socket error: {0}", ex.Message));
                        // Continue trying to receive
                        Thread.Sleep(10);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Socket was disposed, exit loop
                    break;
                }
                catch (Exception ex)
                {
                    if (isCapturing)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Raw capture error: {0}", ex.Message));
                        Thread.Sleep(10);
                    }
                }
            }
        }

        private void GenerateDemoPacket(object sender, EventArgs e)
        {
            // Generate realistic demo packets
            var protocols = new string[] { "TCP", "UDP", "HTTP", "HTTPS", "DNS" };
            var sizes = new int[] { 64, 128, 256, 512, 1024, 1500 };

            var packetData = new PacketData
            {
                Size = sizes[random.Next(sizes.Length)],
                Protocol = protocols[random.Next(protocols.Length)],
                Timestamp = DateTime.Now,
                SourceIP = string.Format("192.168.1.{0}", random.Next(1, 255)),
                DestinationIP = string.Format("10.0.0.{0}", random.Next(1, 255)),
                Port = random.Next(80, 65535)
            };

            // Vary the demo packet frequency
            demoTimer.Interval = TimeSpan.FromMilliseconds(50 + random.Next(200));

            packetQueue.Enqueue(packetData);
            GenerateAudioFromPacket(packetData);
        }

        private void StopCapture()
        {
            isCapturing = false;

            // Stop WinPcap
            if (selectedDevice != null)
            {
                try
                {
                    selectedDevice.StopCapture();
                    selectedDevice.Close();
                    selectedDevice.OnPacketArrival -= OnWinPcapPacketArrival;
                }
                catch { }
                selectedDevice = null;
            }

            // Stop Raw Socket
            if (rawSocket != null)
            {
                try
                {
                    rawSocket.Close();
                    rawSocket.Dispose();
                }
                catch { }
                rawSocket = null;
            }

            // Stop Demo
            demoTimer.Stop();

            // Stop capture thread
            if (captureThread != null && captureThread.IsAlive)
            {
                captureThread.Join(1000); // Wait up to 1 second
            }

            waveOut.Stop();
            visualTimer.Stop();
            audioTimer.Stop();

            // Clear active tones
            activeTones.Clear();

            StatusLabel.Content = string.Format("Stopped - {0} mode available", currentMode);
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            isMuted = !isMuted;
            UpdateMuteButton();

            if (isCapturing)
            {
                if (isMuted)
                {
                    waveOut.Stop();
                }
                else
                {
                    waveOut.Play();
                }
            }
        }

        private void UpdateMuteButton()
        {
            if (isMuted)
            {
                MuteButton.Content = "🔇 Unmute";
                MuteButton.Background = new SolidColorBrush(Color.FromRgb(200, 50, 50));
            }
            else
            {
                MuteButton.Content = "🔊 Mute";
                MuteButton.Background = new SolidColorBrush(Color.FromRgb(50, 150, 50));
            }
        }

        private void OnWinPcapPacketArrival(object sender, PacketCapture e)
        {
            try
            {
                var packet = Packet.ParsePacket(e.Device.LinkType, e.Data.ToArray());
                var packetData = AnalyzePacket(packet, e.Data.Length);

                packetQueue.Enqueue(packetData);
                GenerateAudioFromPacket(packetData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("WinPcap packet parsing error: {0}", ex.Message));
            }
        }

        private PacketData AnalyzePacket(Packet packet, int size)
        {
            var data = new PacketData
            {
                Size = size,
                Timestamp = DateTime.Now,
                Protocol = "Unknown"
            };

            // Analyze packet layers
            if (packet.PayloadPacket is IPPacket)
            {
                var ipPacket = packet.PayloadPacket as IPPacket;
                data.SourceIP = ipPacket.SourceAddress.ToString();
                data.DestinationIP = ipPacket.DestinationAddress.ToString();

                if (ipPacket.PayloadPacket is TcpPacket)
                {
                    var tcpPacket = ipPacket.PayloadPacket as TcpPacket;
                    data.Protocol = "TCP";
                    data.Port = tcpPacket.DestinationPort;

                    // Detect HTTP/HTTPS
                    if (tcpPacket.DestinationPort == 80)
                        data.Protocol = "HTTP";
                    else if (tcpPacket.DestinationPort == 443)
                        data.Protocol = "HTTPS";
                }
                else if (ipPacket.PayloadPacket is UdpPacket)
                {
                    var udpPacket = ipPacket.PayloadPacket as UdpPacket;
                    data.Protocol = "UDP";
                    data.Port = udpPacket.DestinationPort;

                    // Detect DNS
                    if (udpPacket.DestinationPort == 53 || udpPacket.SourcePort == 53)
                        data.Protocol = "DNS";
                }
                else if (ipPacket.PayloadPacket is IcmpV4Packet)
                {
                    data.Protocol = "ICMP";
                }
            }

            return data;
        }

        private PacketData AnalyzeRawPacket(byte[] buffer, int length)
        {
            try
            {
                if (length < 20) return null; // Too short for IP header

                var data = new PacketData
                {
                    Size = length,
                    Timestamp = DateTime.Now,
                    Protocol = "IP"
                };

                // Basic IP header parsing (IPv4)
                byte version = (byte)(buffer[0] >> 4);
                if (version != 4) return null; // Only handle IPv4

                // Extract source and destination IPs
                data.SourceIP = string.Format("{0}.{1}.{2}.{3}", buffer[12], buffer[13], buffer[14], buffer[15]);
                data.DestinationIP = string.Format("{0}.{1}.{2}.{3}", buffer[16], buffer[17], buffer[18], buffer[19]);

                // Extract protocol
                byte protocol = buffer[9];
                int headerLength = (buffer[0] & 0x0F) * 4;

                if (protocol == 6) // TCP
                {
                    data.Protocol = "TCP";
                    if (length >= headerLength + 4)
                    {
                        data.Port = (buffer[headerLength + 2] << 8) | buffer[headerLength + 3];

                        // Detect common protocols by port
                        if (data.Port == 80) data.Protocol = "HTTP";
                        else if (data.Port == 443) data.Protocol = "HTTPS";
                    }
                }
                else if (protocol == 17) // UDP
                {
                    data.Protocol = "UDP";
                    if (length >= headerLength + 4)
                    {
                        data.Port = (buffer[headerLength + 2] << 8) | buffer[headerLength + 3];

                        // Detect DNS
                        if (data.Port == 53) data.Protocol = "DNS";
                    }
                }
                else if (protocol == 1) // ICMP
                {
                    data.Protocol = "ICMP";
                }
                else
                {
                    data.Protocol = "IP";
                }

                return data;
            }
            catch
            {
                // Return basic packet data if parsing fails
                return new PacketData
                {
                    Size = length,
                    Timestamp = DateTime.Now,
                    Protocol = "Unknown"
                };
            }
        }

        private void GenerateAudioFromPacket(PacketData packet)
        {
            // Add packet as an active tone instead of immediate playback
            var tone = new ActiveTone
            {
                Frequency = MapToFrequency(packet),
                Amplitude = MapToAmplitude(packet),
                WaveType = GetWaveformType(packet.Protocol),
                Life = MapToDuration(packet) / 1000.0, // Convert to seconds
                Phase = 0,
                Protocol = packet.Protocol
            };

            activeTones.Add(tone);
        }

        private void GenerateContinuousAudio()
        {
            if (!isCapturing || isMuted) return;

            // Generate audio samples
            int samplesToGenerate = SAMPLE_RATE * 20 / 1000; // 20ms worth of samples
            float[] audioBuffer = new float[samplesToGenerate];

            for (int i = 0; i < samplesToGenerate; i++)
            {
                float sample = 0;

                // Mix all active tones
                for (int t = activeTones.Count - 1; t >= 0; t--)
                {
                    var tone = activeTones[t];

                    // Generate waveform sample
                    float waveValue = GenerateWaveform(tone.WaveType, tone.Phase);
                    sample += waveValue * tone.Amplitude;

                    // Update phase
                    tone.Phase += 2.0 * Math.PI * tone.Frequency / SAMPLE_RATE;
                    if (tone.Phase > 2.0 * Math.PI) tone.Phase -= 2.0 * Math.PI;

                    // Decay the tone
                    tone.Life -= 1.0 / SAMPLE_RATE;
                    tone.Amplitude *= 0.999f; // Gradual fade

                    // Remove expired tones
                    if (tone.Life <= 0 || tone.Amplitude < 0.001f)
                    {
                        activeTones.RemoveAt(t);
                    }
                }

                // Limit amplitude to prevent clipping
                sample = Math.Max(-0.8f, Math.Min(0.8f, sample));
                audioBuffer[i] = sample;
            }

            // Convert to bytes and add to buffer (.NET 4.8 compatible)
            byte[] byteBuffer = new byte[audioBuffer.Length * 4];
            for (int i = 0; i < audioBuffer.Length; i++)
            {
                byte[] floatBytes = BitConverter.GetBytes(audioBuffer[i]);
                Array.Copy(floatBytes, 0, byteBuffer, i * 4, 4);
            }

            // Add to audio buffer if there's space
            if (bufferedProvider.BufferedBytes < bufferedProvider.BufferLength - byteBuffer.Length)
            {
                bufferedProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);
            }
        }

        private float GenerateWaveform(SignalGeneratorType waveType, double phase)
        {
            if (waveType == SignalGeneratorType.Sin)
            {
                return (float)Math.Sin(phase);
            }
            else if (waveType == SignalGeneratorType.Square)
            {
                return Math.Sin(phase) >= 0 ? 1.0f : -1.0f;
            }
            else if (waveType == SignalGeneratorType.Triangle)
            {
                double normalizedPhase = phase / (2.0 * Math.PI);
                normalizedPhase = normalizedPhase - Math.Floor(normalizedPhase);
                if (normalizedPhase < 0.5)
                    return (float)(4.0 * normalizedPhase - 1.0);
                else
                    return (float)(3.0 - 4.0 * normalizedPhase);
            }
            else
            {
                return (float)Math.Sin(phase);
            }
        }

        private float MapToFrequency(PacketData packet)
        {
            // Map packet size to frequency (200Hz - 2000Hz)
            float normalizedSize = Math.Min(packet.Size / 1500.0f, 1.0f);
            return 200 + (normalizedSize * 1800);
        }

        private float MapToAmplitude(PacketData packet)
        {
            // Map packet size to amplitude (0.1 - 0.3)
            float normalizedSize = Math.Min(packet.Size / 1500.0f, 1.0f);
            return 0.1f + (normalizedSize * 0.2f);
        }

        private float MapToDuration(PacketData packet)
        {
            // Duration based on protocol (50ms - 300ms)
            if (packet.Protocol == "TCP") return 200;
            else if (packet.Protocol == "HTTP") return 250;
            else if (packet.Protocol == "HTTPS") return 250;
            else if (packet.Protocol == "UDP") return 100;
            else if (packet.Protocol == "DNS") return 150;
            else if (packet.Protocol == "ICMP") return 150;
            else return 100;
        }

        private SignalGeneratorType GetWaveformType(string protocol)
        {
            if (protocol == "TCP") return SignalGeneratorType.Sin;
            else if (protocol == "HTTP") return SignalGeneratorType.Sin;
            else if (protocol == "HTTPS") return SignalGeneratorType.Sin;
            else if (protocol == "UDP") return SignalGeneratorType.Square;
            else if (protocol == "DNS") return SignalGeneratorType.Triangle;
            else if (protocol == "ICMP") return SignalGeneratorType.Triangle;
            else return SignalGeneratorType.Sin;
        }

        private void UpdateVisualization(object sender, EventArgs e)
        {
            UpdatePacketVisuals();
            UpdateOscilloscope();
            UpdateStats();
        }

        private void UpdatePacketVisuals()
        {
            // Process queued packets for visualization
            PacketData packet;
            while (packetQueue.TryDequeue(out packet))
            {
                // Use full canvas width by starting packets at left edge
                packetVisuals.Add(new PacketVisual
                {
                    X = 0, 
                    Y = random.Next(50, Math.Max(51, (int)PacketCanvas.ActualHeight - 50)),
                    Size = Math.Max(4, Math.Min(packet.Size / 50.0, 25)), // Better size range
                    Color = GetProtocolColor(packet.Protocol),
                    Life = 1.0,
                    Protocol = packet.Protocol
                });
            }

            // Update and draw packet visuals
            PacketCanvas.Children.Clear();

            double canvasWidth = PacketCanvas.ActualWidth;
            if (canvasWidth <= 0) canvasWidth = 1000; // Fallback width

            for (int i = packetVisuals.Count - 1; i >= 0; i--)
            {
                var visual = packetVisuals[i];

                // Move packets across full screen width
                double speed = 3 + (visual.Size / 10.0); // Larger packets move slightly faster
                visual.X += speed;
                visual.Life -= 0.0015; // Slower fade for better visibility

                // Remove packets that are off-screen or fully faded
                if (visual.Life <= 0 || visual.X > canvasWidth + 50)
                {
                    packetVisuals.RemoveAt(i);
                    continue;
                }

                // Draw packet with glow effect
                var ellipse = new Ellipse
                {
                    Width = visual.Size,
                    Height = visual.Size,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)(255 * visual.Life),
                        visual.Color.R, visual.Color.G, visual.Color.B))
                };

                // Add glow effect
                try
                {
                    ellipse.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = visual.Color,
                        BlurRadius = visual.Size / 3,
                        ShadowDepth = 0,
                        Opacity = visual.Life * 0.5
                    };
                }
                catch
                {
                    // Skip glow effect if it causes issues
                }

                Canvas.SetLeft(ellipse, visual.X);
                Canvas.SetTop(ellipse, visual.Y);
                PacketCanvas.Children.Add(ellipse);
            }
        }

        private void UpdateOscilloscope()
        {
            // Generate sample waveform data
            OscilloscopeCanvas.Children.Clear();

            if (audioSamples.Count > 0)
            {
                var polyline = new Polyline
                {
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 2
                };

                double width = OscilloscopeCanvas.ActualWidth;
                double height = OscilloscopeCanvas.ActualHeight;
                double centerY = height / 2;

                for (int i = 0; i < Math.Min(audioSamples.Count, width); i++)
                {
                    double x = (i / (double)audioSamples.Count) * width;
                    double y = centerY + (audioSamples[i] * centerY * 0.5);
                    polyline.Points.Add(new Point(x, y));
                }

                OscilloscopeCanvas.Children.Add(polyline);
            }

            // Generate new sample data based on recent activity
            GenerateOscilloscopeSamples();
        }

        private void GenerateOscilloscopeSamples()
        {
            // Simplified oscilloscope sample generation
            audioSamples.Clear();

            double time = DateTime.Now.Millisecond / 1000.0;
            for (int i = 0; i < 200; i++)
            {
                double sample = 0;

                // Add contribution from recent packets
                var recentVisuals = packetVisuals.Take(10);
                foreach (var visual in recentVisuals)
                {
                    double freq = MapToFrequency(new PacketData { Size = (int)visual.Size * 50 });
                    sample += Math.Sin(2 * Math.PI * freq * time * i / 200.0) * visual.Life * 0.3;
                }

                audioSamples.Add(sample);
            }
        }

        private void UpdateStats()
        {
            var protocolCounts = packetVisuals
                .GroupBy(p => p.Protocol)
                .ToDictionary(g => g.Key, g => g.Count());

            string protocolStats = string.Join(", ",
                protocolCounts.Select(kvp => kvp.Key + ": " + kvp.Value).ToArray());

            string modeInfo = string.Format("[{0}] ", currentMode);
            ProtocolStatsLabel.Content = modeInfo + protocolStats;
        }

        private Color GetProtocolColor(string protocol)
        {
            if (protocol == "TCP") return Colors.Blue;
            else if (protocol == "UDP") return Colors.Red;
            else if (protocol == "HTTP") return Colors.Green;
            else if (protocol == "HTTPS") return Colors.Purple;
            else if (protocol == "DNS") return Colors.Orange;
            else if (protocol == "ICMP") return Colors.Yellow;
            else if (protocol == "IP") return Colors.Cyan;
            else return Colors.White;
        }

        protected override void OnClosed(EventArgs e)
        {
            StopCapture();
            if (waveOut != null)
            {
                waveOut.Dispose();
            }
            base.OnClosed(e);
        }
    }
}