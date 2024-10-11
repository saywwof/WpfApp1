using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private PerformanceCounter networkCounter;
        private DispatcherTimer timer;
        private Computer computer;
        private const int MaxDataPoints = 30;
        private double[] cpuUsageData = new double[MaxDataPoints];
        private int dataIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitializePerformanceCounters();
            InitializeHardwareMonitor();
            StartMonitoring();
            UpdateProcessesList();
        }

        private void InitializePerformanceCounters()
        {
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            var category = new PerformanceCounterCategory("Network Interface");
            string instanceName = category.GetInstanceNames().FirstOrDefault();
            if (instanceName != null)
            {
                networkCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec", instanceName);
            }
        }

        private void InitializeHardwareMonitor()
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true
            };
            computer.Open();
        }

        private void StartMonitoring()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += UpdatePerformanceData;
            timer.Start();
        }

        private void UpdatePerformanceData(object sender, EventArgs e)
        {
            float cpuUsage = cpuCounter.NextValue();
            float availableMemory = ramCounter.NextValue();
            float totalMemory = 16384; // Replace with actual total memory in MB

            cpuProgressBar.Value = cpuUsage;
            memoryProgressBar.Value = (totalMemory - availableMemory) / totalMemory * 100;

            cpuUsageTextBlock.Text = $"{cpuUsage:F2}%";
            memoryUsageTextBlock.Text = $"{availableMemory:F2} MB";

            if (networkCounter != null)
            {
                float networkUsage = networkCounter.NextValue() / 1024; // Convert to KB/s
                networkProgressBar.Value = networkUsage;
                networkUsageTextBlock.Text = $"{networkUsage:F2} KB/s";
            }

            UpdateChart(cpuUsage);
            UpdateTemperatureData();
        }

        private void UpdateTemperatureData()
        {
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            // Выводим отладочную информацию в консоль
                            Console.WriteLine($"Sensor: {sensor.Name}, Value: {sensor.Value}");

                            // Обновляем текстовое поле, если значение доступно
                            if (sensor.Value.HasValue)
                            {
                                cpuTemperatureTextBlock.Text = $"{sensor.Name}: {sensor.Value.GetValueOrDefault():F1} °C";
                            }
                            else
                            {
                                cpuTemperatureTextBlock.Text = $"{sensor.Name}: N/A";
                            }
                        }
                    }
                }
            }
        }

        private void UpdateChart(float cpuUsage)
        {
            cpuUsageData[dataIndex] = cpuUsage;
            dataIndex = (dataIndex + 1) % MaxDataPoints;

            cpuUsageChart.Points.Clear();
            double canvasHeight = cpuUsageCanvas.ActualHeight;
            double canvasWidth = cpuUsageCanvas.ActualWidth;
            for (int i = 0; i < MaxDataPoints; i++)
            {
                double x = i * (canvasWidth / MaxDataPoints);
                double y = canvasHeight - (cpuUsageData[(dataIndex + i) % MaxDataPoints] / 100) * canvasHeight;
                cpuUsageChart.Points.Add(new Point(x, y));
            }
        }

        private void UpdateProcessesList()
        {
            string filter = filterTextBox.Text.ToLower();
            var processes = Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains(filter)).Select(p => p.ProcessName).Distinct().OrderBy(name => name).ToList();
            processesListBox.ItemsSource = processes;
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateProcessesList();
        }

        private void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            UpdatePerformanceData(null, null);
            UpdateProcessesList();
        }
    }
}