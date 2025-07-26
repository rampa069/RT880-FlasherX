using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Threading;

namespace RT880_FlasherX // <- Change this if needed
{
    public partial class MainWindow : Window
    {
        private string? selectedFirmwareFile;
        //private SerialPort? port;
        private SerialPort? portInUse;

        public MainWindow()
        {
            InitializeComponent();

            // Populate serial port dropdown
            PortComboBox.ItemsSource = SerialPort.GetPortNames();

            // Select the first port by default
            try { PortComboBox.SelectedIndex = 0; } catch { }
            
            // Hook up event handlers
            SelectFileButton.Click += OnSelectFirmwareClick;
            FlashButton.Click += OnFlashFirmwareClick;
            AbortButton.Click += AbortButton_Click;
        }

        private async void OnSelectFirmwareClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var options = new FilePickerOpenOptions
                {
                    Title = "Select Firmware File",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Binary Files")
                        {
                            Patterns = ["*.bin"]
                        },
                        new FilePickerFileType("All Files")
                        {
                            Patterns = ["*"]
                        }
                    ]
                };

                var files = await this.StorageProvider.OpenFilePickerAsync(options);
                if (files?.Any() == true)
                {
                    var file = files[0];
                    selectedFirmwareFile = file.Path.LocalPath;
                    SelectedFileLabel.Text = file.Name;
                    SetStatus("Firmware file selected.");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"File pick error: {ex.Message}");
            }
        }

        private static SerialPort? OpenPort(string com, int baud, int timeout)
        {
            SerialPort? port = null;
            try
            {
                port = new SerialPort(com, baud, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = timeout
                };
                port.Open();
                return port;
            }
            catch { }
            ClosePort(port);
            return null;
        }

        private static void ClosePort(SerialPort? port)
        {
            using (port)
            {
                port?.Close();
            }
        }

        private static int ReadByte(SerialPort port)
        {
            try
            {
                return port.ReadByte();
            }
            catch (TimeoutException) { return -2; }
            catch { return -1; }
        }

        private bool WriteData(SerialPort port, byte[] data)
        {
            try
            {
                port.Write(data, 0, data.Length);
                return true;
            }
            catch { SetStatus("COM Port Write Error/Abort"); }
            return false;
        }

        private void SetStatus(string message)
        {
            Dispatcher.UIThread.Invoke(() => {
                StatusTextBlock.Text = message;
            });
        }

        private bool GetAck(SerialPort port)
        {
            switch (ReadByte(port))
            {
                case 6: return true;
                case -1: SetStatus("COM Port Read Error/Abort"); break;
                case -2: SetStatus("COM Port Timeout"); break;
                default: SetStatus("Bad Acknowledgement"); break;
            }
            return false;
        }

        private void SetProgress(int now, int len)
        {
            Dispatcher.UIThread.Invoke(() => 
            {
                double d = (double)now / len;
                d *= 100;
                if (d < 0) d = 0; else if (d > 100) d = 100;
                FlashProgressBar.Value = d;
                string progString = $"{(int)d}%";
                SetStatus($"Progress: {progString}");
            });
        }


        private void Flash(string com, byte[] unpadded)
        {
            int len = (int)Math.Ceiling(unpadded.Length / 1024.0) * 1024;
            byte[] firmware = new byte[len];
            Array.Copy(unpadded, 0, firmware, 0, unpadded.Length);
            if (OpenPort(com, 115200, 5000) is SerialPort port)
            {
                portInUse = port;
                try
                {
                    SetStatus("Erasing Flash");
                    byte[] packet = ConstructPacket(0x39, 0x3305, [0x10], 0, 1);
                    if (!WriteData(port, packet)) return;
                    if (!GetAck(port)) return;
                    if (!WriteData(port, packet)) return;
                    if (!GetAck(port)) return;
                    packet = ConstructPacket(0x39, 0x3305, [0x55], 0, 1);
                    if (!WriteData(port, packet)) return;
                    if (!GetAck(port)) return;
                    for (int i = 0; i < len; i += 1024)
                    {
                        packet = ConstructPacket(0x57, i, firmware, i, 1024);
                        if (!WriteData(port, packet)) return;
                        if (!GetAck(port)) return;
                        SetProgress(i, len - 1024);
                    }
                    if (!GetAck(port)) return;
                    SetStatus("Firmware Flash Finished Okay");
                }
                finally { ClosePort(port); }
            }
            else
                SetStatus($"Cannot open port: {com}");

        }

        private void AbortButton_Click(object? sender, RoutedEventArgs e)
        {
            ClosePort(portInUse);
        }

        private async void OnFlashFirmwareClick(object? sender, RoutedEventArgs e)
        {
            var portName = PortComboBox.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(portName))
            {
                SetStatus("Please select a serial port.");
                return;
            }

            if (string.IsNullOrEmpty(selectedFirmwareFile))
            {
                SetStatus("Please select a firmware file.");
                return;
            }

            byte[] unpadded;
            try
            {
                unpadded = File.ReadAllBytes(selectedFirmwareFile);
            }
            catch
            {
                SetStatus("Error reading firmware file.");
                return;
            }

            PortComboBox.IsEnabled = false;
            FlashButton.IsEnabled = false;
            AbortButton.IsEnabled = true;
            ModelComboBox.IsEnabled = false;
            SelectFileButton.IsEnabled = false;
            using var flashTask = Task.Run(() => Flash(portName, unpadded));
            await flashTask;
            FlashButton.IsEnabled = true;
            AbortButton.IsEnabled = false;
            PortComboBox.IsEnabled = true;
            ModelComboBox.IsEnabled = true;
            SelectFileButton.IsEnabled = true;
        }

        private byte[] ConstructPacket(int type, int address, byte[] data, int offset, int count)
        {
            byte[] packet = new byte[count + 4];
            int cs = packet.Length - 1;
            packet[0] = (byte)type;
            packet[1] = (byte)((address >> 8) & 0xff);
            packet[2] = (byte)(address & 0xff);
            packet[cs] = (byte)(ModelComboBox.SelectedIndex == 1 ? 0x00 : 0x52);
            Array.Copy(data, offset, packet, 3, count);
            for (int i = 0; i < cs; i++)
            {
                packet[cs] += packet[i];
            }
            return packet;
        }

    }
}
