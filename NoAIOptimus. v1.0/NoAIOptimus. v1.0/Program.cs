using System;
using System.IO;
using System.Text;
using Microsoft.Win32;
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

    static List<string> displayAutoLoadList = new List<string>();
    static List<string> cleanNames = new List<string>();
    static List<string> rawLocations = new List<string>();
    static List<int> locationTypes = new List<int>();

    static DriveInfo[] allDrives = DriveInfo.GetDrives();
    static List<string> physicalDisksInfo = new List<string>();

    static void EnableAnsiColors()
    {
        var handle = GetStdHandle(-11);
        if (GetConsoleMode(handle, out uint mode))
        {
            SetConsoleMode(handle, mode | 0x0004);
        }
    }

    static void LoadStartupPrograms()
    {
        displayAutoLoadList.Clear();
        cleanNames.Clear();
        rawLocations.Clear();
        locationTypes.Clear();

        try
        {
            SelectQuery query = new SelectQuery("SELECT * FROM Win32_StartupCommand");
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject item in searcher.Get())
                {
                    string name = item["Name"]?.ToString() ?? "Unknown";
                    string location = item["Location"]?.ToString() ?? "Unknown";

                    displayAutoLoadList.Add(name);
                    cleanNames.Add(name);
                    rawLocations.Add(location);

                    if (location.Contains("HKU"))
                    {
                        locationTypes.Add(0);
                    }
                    else if (location.Contains("HKLM"))
                    {
                        locationTypes.Add(1);
                    }
                    else if (location.Contains("Startup", StringComparison.OrdinalIgnoreCase))
                    {
                        locationTypes.Add(2);
                    }
                    else
                    {
                        locationTypes.Add(3);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при чтении автозагрузки: {ex.Message}".Rgb(255, 0, 0));
        }
    }

    static void LoadPhysicalDisks()
    {
        try
        {
            using (var physicalDiskSearcher = new ManagementObjectSearcher(@"Root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_PhysicalDisk"))
            {
                foreach (ManagementObject disk in physicalDiskSearcher.Get())
                {
                    string name = disk["FriendlyName"]?.ToString() ?? "Неизвестный накопитель";
                    ushort mediaType = Convert.ToUInt16(disk["MediaType"] ?? 0);

                    string typeStr = mediaType switch
                    {
                        3 => "HDD".Rgb(255, 165, 0),
                        4 => "SSD".Rgb(50, 205, 50),
                        _ => "Неизвестный тип".Rgb(180, 180, 180)
                    };

                    physicalDisksInfo.Add($"  {name} — [{typeStr}]");
                }
            }
        }
        catch
        {
            physicalDisksInfo.Add("  Не удалось определить физические накопители.".Rgb(180, 180, 180));
        }
    }

    static void DeleteStartupProgram(int index)
    {
        string progName = cleanNames[index];
        string location = rawLocations[index];
        int type = locationTypes[index];

        if (progName.Equals("SecurityHealth", StringComparison.OrdinalIgnoreCase) ||
            location.Contains("S-1-5-19") ||
            location.Contains("S-1-5-20"))
        {
            Console.WriteLine("\n[ОТКАЗ] Это системный компонент Windows, его нельзя удалять!".Rgb(255, 50, 50));
            return;
        }

        try
        {
            if (type == 0)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) throw new Exception("Не удалось открыть ветку реестра HKCU.");
                    key.DeleteValue(progName, false);
                }
                Console.WriteLine($"\n[OK] Программа '{progName}' удалена из автозагрузки пользователя (HKCU).".Rgb(50, 205, 50));
            }
            else if (type == 1)
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) throw new Exception("Не удалось открыть ветку реестра HKLM.");
                    key.DeleteValue(progName, false);
                }
                Console.WriteLine($"\n[OK] Программа '{progName}' удалена из системной автозагрузки (HKLM).".Rgb(50, 205, 50));
            }
            else if (type == 2)
            {
                string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

                string[] possibleFiles = new[]
                {
                    Path.Combine(userStartup, progName + ".lnk"),
                    Path.Combine(userStartup, progName),
                    Path.Combine(commonStartup, progName + ".lnk"),
                    Path.Combine(commonStartup, progName)
                };

                bool deleted = false;
                foreach (string filePath in possibleFiles)
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        deleted = true;
                    }
                }

                if (!deleted)
                {
                    throw new FileNotFoundException($"Ярлык '{progName}' не найден в папках Startup.");
                }

                Console.WriteLine($"\n[OK] Ярлык '{progName}' удален из папки Startup.".Rgb(50, 205, 50));
            }
            else if (type == 3)
            {
                throw new NotSupportedException($"БИБА! Неизвестный источник автозагрузки: {location}");
            }

            displayAutoLoadList.RemoveAt(index);
            cleanNames.RemoveAt(index);
            rawLocations.RemoveAt(index);
            locationTypes.RemoveAt(index);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("\n[ОШИБКА ДОСТУПА] Запустите утилиту от имени Администратора!".Rgb(255, 0, 0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ОШИБКА] {ex.Message}".Rgb(255, 0, 0));
        }
    }

    static void PrintHeader()
    {
        Console.SetCursorPosition(0, 0);
        string spaceHint = "ПРОБЕЛ".Rgb(255, 255, 255);
        string exitHint = "Q / ESC".Rgb(255, 255, 255);
        Console.WriteLine($"[{spaceHint} — смена экрана | {exitHint} — выход]".Rgb(255, 60, 60) + "          ");
    }

    static void Main()
    {
        EnableAnsiColors();
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        LoadStartupPrograms();
        LoadPhysicalDisks();

        Computer computer = new Computer { IsStorageEnabled = true };
        computer.Open();

        string driveC = "C:";
        using var readCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", driveC);
        using var writeCounter = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", driveC);
        readCounter.NextValue();
        writeCounter.NextValue();

        int currentScreen = 0;
        int lastScreen = -1;
        bool screen0Drawn = false;

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Spacebar)
                {
                    currentScreen = (currentScreen + 1) % 3;
                }
                else if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Q)
                {
                    break;
                }
                else if (key.Key == ConsoleKey.D && currentScreen == 0)
                {
                    Console.CursorVisible = true;
                    Console.Write("\nВведите номер программы для удаления (или Enter для отмены): ".Rgb(255, 215, 0));
                    string input = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        if (int.TryParse(input, out int numberOfProgForDel)
                            && numberOfProgForDel >= 0
                            && numberOfProgForDel < cleanNames.Count)
                        {
                            DeleteStartupProgram(numberOfProgForDel);
                        }
                        else
                        {
                            Console.WriteLine("\n[ОШИБКА] Неверный номер программы!".Rgb(255, 0, 0));
                        }
                        Thread.Sleep(1800);
                    }

                    Console.CursorVisible = false;
                    Console.Clear();
                    screen0Drawn = false;
                }
            }

            if (currentScreen != lastScreen)
            {
                Console.Clear();
                lastScreen = currentScreen;
                screen0Drawn = false;
            }

            if (currentScreen == 0)
            {
                if (!screen0Drawn)
                {
                    PrintHeader();
                    Console.WriteLine();
                    Console.WriteLine("--- РЕЖИМ АВТОЗАГРУЗКИ И ДИСКОВ ---".Rgb(0, 200, 255));

                    Console.WriteLine();
                    Console.WriteLine("Программы в автозагрузке:".Rgb(255, 215, 0));
                    if (displayAutoLoadList.Count == 0)
                    {
                        Console.WriteLine("  Список автозагрузки пуст.".Rgb(180, 180, 180));
                    }
                    else
                    {
                        for (int i = 0; i < displayAutoLoadList.Count; i++)
                        {
                            string idx = i.ToString().Rgb(100, 255, 100);
                            Console.WriteLine($"  {idx}. {displayAutoLoadList[i]}");
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("Физические накопители:".Rgb(255, 215, 0));
                    foreach (string diskInfo in physicalDisksInfo)
                    {
                        Console.WriteLine(diskInfo);
                    }

                    Console.WriteLine();
                    Console.WriteLine("Информация о логических дисках:".Rgb(255, 215, 0));
                    foreach (DriveInfo d in allDrives)
                    {
                        string dName = d.Name.Rgb(0, 255, 180);
                        string dType = d.DriveType.ToString().Rgb(180, 180, 180);
                        Console.WriteLine($"Диск {dName}");
                        Console.WriteLine($"  Тип диска: {dType}");
                        if (d.IsReady)
                        {
                            Console.WriteLine($"  Имя тома: {d.VolumeLabel}");
                            Console.WriteLine($"  Файловая система: {d.DriveFormat}");
                            Console.WriteLine("  {0,-55} {1,20:F2} Гб", "Доступно пространства для текущего пользователя:", d.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0));
                            Console.WriteLine("  {0,-55} {1,20:F2} Гб", "Всего доступно пространства:", d.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0));
                            Console.WriteLine("  {0,-55} {1,20:F2} Гб", "Размер всего диска:", d.TotalSize / (1024.0 * 1024.0 * 1024.0));
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("[Нажмите клавишу D (англ.), чтобы удалить программу из автозагрузки]".Rgb(255, 100, 100));
                    screen0Drawn = true;
                }
            }
            else if (currentScreen == 1)
            {
                PrintHeader();
                double readSpeedMbs = readCounter.NextValue() / 1024.0 / 1024.0;
                double writeSpeedMbs = writeCounter.NextValue() / 1024.0 / 1024.0;
                string readStr = $"{readSpeedMbs:F2} МБ/с".Rgb(50, 205, 50);
                string writeStr = $"{writeSpeedMbs:F2} МБ/с".Rgb(255, 140, 0);
                string driveTitle = driveC.Rgb(255, 215, 0);

                Console.WriteLine();
                Console.WriteLine($"--- РЕЖИМ СКОРОСТЕЙ ДИСКА [{driveTitle}] ---".Rgb(0, 200, 255));
                Console.WriteLine($"Скорость чтения: {readStr}          ");
                Console.WriteLine($"Скорость записи: {writeStr}          ");
            }
            else if (currentScreen == 2)
            {
                PrintHeader();
                Console.WriteLine();
                Console.WriteLine("--- РЕЖИМ ТЕМПЕРАТУРЫ И ЗДОРОВЬЯ НАКОПИТЕЛЕЙ ---".Rgb(0, 200, 255));

                bool detected = false;
                bool healthFoundInLhm = false;

                foreach (IHardware hardware in computer.Hardware)
                {
                    hardware.Update();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (!sensor.Value.HasValue) continue;

                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            detected = true;
                            float temp = sensor.Value.Value;
                            string tempStr = $"{temp:F1} °C";
                            string coloredTemp = temp >= 55 ? tempStr.Rgb(255, 50, 50) : tempStr.Rgb(50, 205, 50);

                            Console.WriteLine($"  {hardware.Name} | Температура ({sensor.Name}): {coloredTemp}          ");
                        }
                        else if (sensor.SensorType == SensorType.Level)
                        {
                            detected = true;
                            healthFoundInLhm = true;
                            float health = sensor.Value.Value;
                            string healthStr = $"{health:F0}%";
                            string coloredHealth = health <= 20 ? healthStr.Rgb(255, 50, 50)
                                                 : health <= 50 ? healthStr.Rgb(255, 215, 0)
                                                 : healthStr.Rgb(50, 205, 50);

                            Console.WriteLine($"  {hardware.Name} | Здоровье ({sensor.Name}): {coloredHealth}          ");
                        }
                    }
                }

                if (!healthFoundInLhm)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(@"Root\Microsoft\Windows\Storage", "SELECT FriendlyName, HealthStatus FROM MSFT_PhysicalDisk"))
                        {
                            foreach (ManagementObject disk in searcher.Get())
                            {
                                detected = true;
                                string name = disk["FriendlyName"]?.ToString() ?? "Накопитель";
                                ushort status = Convert.ToUInt16(disk["HealthStatus"] ?? 0);

                                string statusStr = status switch
                                {
                                    0 => "Здоров (Healthy)".Rgb(50, 205, 50),
                                    1 => "Предупреждение (Warning)".Rgb(255, 215, 0),
                                    2 => "Критично (Unhealthy)".Rgb(255, 50, 50),
                                    _ => "Неизвестно".Rgb(180, 180, 180)
                                };

                                Console.WriteLine($"  {name} | Статус SMART (WMI): {statusStr}          ");
                            }
                        }
                    }
                    catch { }
                }

                if (!detected)
                {
                    Console.WriteLine("  Датчики накопителей не найдены (нужны права Администратора).".Rgb(180, 180, 180));
                }
            }

            Thread.Sleep(250);
        }

        computer.Close();
        Console.CursorVisible = true;
    }
}
