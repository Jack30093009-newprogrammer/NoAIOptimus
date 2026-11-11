using System;
using System.IO;
using System.Text;
using System.Management;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;


public static class StringColorExtensions
{
    public static string Rgb(this string text, byte r, byte g, byte b)
    {
        return $"\x1b[38;2;{r};{g};{b}m{text}\x1b[0m";
    }
}
class Program
{

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    static void EnableAnsiColors()
    {
        var handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
        if (GetConsoleMode(handle, out uint mode))
        {
            SetConsoleMode(handle, mode | 0x0004); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
        }
    }
    static List<string> nameOfProgrammsAutoLoad = new List<string>(); //создаем список программ в автозагрузке
    static DriveInfo[] allDrives = DriveInfo.GetDrives(); //для работы с диском

    static void addToList(string nameOfProg)
    {
        nameOfProgrammsAutoLoad.Add(nameOfProg);
    }
    
//try to read heheh lol
    static void Main()
    {
        EnableAnsiColors(); //метод для починки цветов в режиме админа
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.CursorVisible = false; 

        SelectQuery query = new SelectQuery("SELECT * FROM Win32_StartupCommand");
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
        {
            // Проигрывание первых нот "Имперского марша"
            //Console.Beep(440, 500);
            //Console.Beep(440, 500);
            //Console.Beep(440, 500);
            //Console.Beep(349, 350);
            //Console.Beep(523, 150);
            //Console.Beep(440, 500);
            //Console.Beep(349, 350);
            //Console.Beep(523, 150);
            //Console.Beep(440, 1000);

            foreach (ManagementObject item in searcher.Get())
            {
                string name = item["Name"].ToString();
                string location = item["Location"]?.ToString() ?? "Unknown";
                addToList($"{name} | Где же гад: {location}");
            }
        }

        Computer computer = new Computer { IsStorageEnabled = true };
        computer.Open();

        using (var physicalDiskSearcher = new ManagementObjectSearcher(@"Root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_PhysicalDisk"))
        {
            bool isSSD = false;
            bool isHDD = false;
            foreach (ManagementObject disk in physicalDiskSearcher.Get())
            {
                string name = disk["FriendlyName"]?.ToString() ?? "Неизвестный накопитель";
                ushort mediaType = (ushort)(disk["MediaType"] ?? 0);

                string typeStr = mediaType switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    _ => "Страшно, очень страшно! Если бы мы знали, что это такое, мы не знаем, что это такое!"
                };
                if (typeStr == "HDD") isHDD = true;
                else if (typeStr == "SSD") isSSD = true;
            }
        }

        string driveC = "C:";
        using var readCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", driveC);
        using var writeCounter = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", driveC);
        readCounter.NextValue();
        writeCounter.NextValue();

        // 0 - автозагрузка и диски, 1 - скорости работы, 2 - температура и здоровье
        int currentScreen = 0;

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Spacebar)
                {
                    currentScreen = (currentScreen + 1) % 3;
                }
            }

            Console.Clear();
            Console.WriteLine($"[{"Нажмите ПРОБЕЛ для переключения экранов утилиты".Rgb(255, 0, 0)}]");

            if (currentScreen == 0)
            {
                Console.WriteLine("\n--- РЕЖИМ АВТОЗАГРУЗКИ И ЛОГИЧЕСКИХ ДИСКОВ ---".Rgb(0, 200, 255));

                Console.WriteLine("\nПрограммы в автозагрузке:".Rgb(255, 215, 0));
                for (int i = 0; i < nameOfProgrammsAutoLoad.Count; i++)
                {
                    Console.WriteLine($"{i.ToString().Rgb(100, 255, 100)}. {nameOfProgrammsAutoLoad[i]}");
                }

                Console.WriteLine("\nИнформация о логических дисках:".Rgb(255, 215, 0));
                foreach (DriveInfo d in allDrives)
                {
                    Console.WriteLine($"Диск {d.Name.Rgb(0, 255, 180)}");
                    Console.WriteLine($"  Тип диска: {d.DriveType.ToString().Rgb(180, 180, 180)}");
                    if (d.IsReady)
                    {
                        Console.WriteLine($"  Имя тома: {d.VolumeLabel}");
                        Console.WriteLine($"  Файловая система: {d.DriveFormat}");
                        Console.WriteLine("  {0,-55} {1,20:F2} Гб", "Доступно пространства для текущего пользователя:", d.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0));
                        Console.WriteLine("  {0,-55} {1,20:F2} Гб", "Всего доступно пространства:", d.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0));
                        Console.WriteLine("  {0,-55} {1,20:F2} Гб", "Размер всего диска:", d.TotalSize / (1024.0 * 1024.0 * 1024.0));
                    }
                }
            }
            else if (currentScreen == 1)
            {
                double readSpeedMbs = readCounter.NextValue() / 1024 / 1024;
                double writeSpeedMbs = writeCounter.NextValue() / 1024 / 1024;
                Console.WriteLine($"\n--- РЕЖИМ СКОРОСТЕЙ ДИСКА [{driveC.Rgb(255, 215, 0)}] ---".Rgb(0, 200, 255));
                Console.WriteLine($"Скорость чтения: {$"{readSpeedMbs:F2} МБ/с".Rgb(50, 205, 50)}");
                Console.WriteLine($"Скорость записи: {$"{writeSpeedMbs:F2} МБ/с".Rgb(255, 140, 0)}");
            }
            else if (currentScreen == 2)
            {
                Console.WriteLine("\n--- РЕЖИМ ТЕМПЕРАТУРЫ И ЗДОРОВЬЯ НАКОПИТЕЛЕЙ ---".Rgb(0, 200, 255));

                bool detected = false;
                long tempCounter = 0;
                foreach (IHardware hardware in computer.Hardware)
                {
                    hardware.Update();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            float temp = sensor.Value ?? 0;
                            string tempText = $"{temp:F1} °C";

                            // Красим температуру в зависимости от нагрева
                            string coloredTemp = temp > 55 ? tempText.Rgb(255, 60, 60) :
                                                 temp > 42 ? tempText.Rgb(255, 200, 0) :
                                                             tempText.Rgb(50, 205, 50);

                            if (tempCounter == 3 || tempCounter == 4)
                            {
                                Console.WriteLine($"{tempCounter}. {"Не беспокойтесь - это лимит".Rgb(140, 140, 140)} {hardware.Name}: {coloredTemp}");
                                detected = true;
                                ++tempCounter;
                                continue;
                            }
                            Console.WriteLine($"{tempCounter}. Температура накопителя {hardware.Name.Rgb(0, 255, 255)}: {coloredTemp}");
                            detected = true;
                            ++tempCounter;
                        }

                        if (sensor.SensorType == SensorType.Level && (sensor.Name.Contains("Life") || sensor.Name.Contains("Wear") || sensor.Name.Contains("Health")))
                        {
                            float health = sensor.Value ?? 0;
                            string healthText = $"{health:F0}%";
                            string coloredHealth = health < 30 ? healthText.Rgb(255, 60, 60) :
                                                   health < 70 ? healthText.Rgb(255, 200, 0) :
                                                                 healthText.Rgb(50, 205, 50);

                            Console.WriteLine($"\n{"Здоровье:".Rgb(255, 215, 0)}\nОставшийся ресурс накопителя {hardware.Name}: {coloredHealth}");
                        }

                        if (sensor.SensorType == SensorType.Factor && (sensor.Name.Contains("Error") || sensor.Name.Contains("Spare")))
                        {
                            Console.WriteLine($"{"[NVMe Статус]".Rgb(180, 100, 255)} {sensor.Name} на {hardware.Name}: {sensor.Value ?? 0:F0}");
                        }
                    }
                }

                if (!detected)
                {
                    Console.WriteLine("Датчики не найдены. Перезапустите приложение " + "ОТ ИМЕНИ АДМИНИСТРАТОРА!".Rgb(255, 50, 50));
                }
            }

            Thread.Sleep(1000);
        }
    }
}
