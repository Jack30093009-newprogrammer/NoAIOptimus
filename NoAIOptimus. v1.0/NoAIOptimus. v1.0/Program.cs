using System;
using System.IO;
using System.Management;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

class Program
{
    
    static List<string> nameOfProgrammsAutoLoad = new List<string>(); //создаем список программ в автозагрузке
    static DriveInfo[] allDrives = DriveInfo.GetDrives(); //для работы с диском
    
static void addToList(string nameOfProg)
    {
        nameOfProgrammsAutoLoad.Add(nameOfProg);
    }

    static void Main()
    {
        
        SelectQuery query = new SelectQuery("SELECT * FROM Win32_StartupCommand");
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
        {
            long counter = 0;
            foreach (ManagementObject item in searcher.Get())
            {
                //сохраним список программ себе
                string name = item["Name"].ToString();
                addToList(name);
                Console.WriteLine($"{counter}. {item["Name"]}");
                ++counter;
            }


        }
        foreach (DriveInfo d in allDrives)
        {
            Console.WriteLine("Диск {0}", d.Name);
            Console.WriteLine("Тип диска: {0}", d.DriveType);
            if (d.IsReady)
            {
                Console.WriteLine("Имя тома: {0}", d.VolumeLabel);
                Console.WriteLine("Файловая система: {0}", d.DriveFormat);
                Console.WriteLine("{0,-55} {1,20:F2} Гб", "Доступно пространства для текущего пользователя:", d.AvailableFreeSpace/(1024.0*1024.0*1024.0));
                Console.WriteLine("{0,-55} {1,20:F2} Гб", "Всего доступно пространства:", d.TotalFreeSpace / (1024.0 * 1024.0*1024.0));
                Console.WriteLine("{0,-55} {1,20:F2} Гб", "Размер всего диска:", d.TotalSize / (1024.0 * 1024.0*1024.0));

            }
        }
        using (var physicalDiskSearcher = new ManagementObjectSearcher(@"Root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_PhysicalDisk"))
        {
            bool isSSD = false;
            foreach (ManagementObject disk in physicalDiskSearcher.Get())
            {
                string name = disk["FriendlyName"]?.ToString() ?? "Неизвестный накопитель";
                ushort mediaType = (ushort)(disk["MediaType"] ?? 0);

                string typeStr = mediaType switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    _ => "Не определено (например, флешка/внешний диск)"
                };
                if (isSSD == true)
                {
                    //для хз пока чего
                }

                Console.WriteLine($"Накопитель: {name} | Физический тип: {typeStr}");
            }
        }
        //поработем с цешкой (он интересуеь больше)
        string driveC = "C:";
        using var readCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", driveC);
        using var writeCounter = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", driveC);
        readCounter.NextValue();
        writeCounter.NextValue();


        while (true)
        {
            Thread.Sleep(1000);
            double readSpeedMbs = readCounter.NextValue() / 1024 / 1024;
            double writeSpeedMbs = writeCounter.NextValue() / 1024 / 1024;
            Console.WriteLine($"[Диск {driveC}]");
            Console.WriteLine($"Скорость чтения: {readSpeedMbs:F2} МБ/с");
            Console.WriteLine($"Скорость записи: {writeSpeedMbs:F2} МБ/с");
        }
    }
}
