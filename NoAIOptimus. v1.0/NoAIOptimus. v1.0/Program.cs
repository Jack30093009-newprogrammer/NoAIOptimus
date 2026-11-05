using System;
using System.Management; 

class Program
{
    
    static List<string> nameOfProgrammsAutoLoad = new List<string>(); //создаем список программ в автозагрузке
    
    static void addToList(string nameOfProg)
    {
        nameOfProgrammsAutoLoad.Add(nameOfProg);
    }

    static void Main()
    {
        
        SelectQuery query = new SelectQuery("SELECT * FROM Win32_StartupCommand");
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
        {
            
            foreach (ManagementObject item in searcher.Get())
            {
                //сохраним список программ себе
                string name = item["Name"].ToString();
                addToList(name);
                Console.WriteLine($"Программа: {item["Name"]}");
            }
        }
    }
}
