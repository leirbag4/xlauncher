using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace xlauncher
{
    public class HistoryItem
    {
        public string FullPath { get; set; }
        public string FileName { get; set; }
        public DateTime LastRun { get; set; }
        public int RunCount { get; set; }
    }

    public class HistoryManager
    {
        private string _filePath;
        public List<HistoryItem> History { get; private set; }

        public HistoryManager()
        {
            // Guardamos en %AppData%\xlauncher\history.json
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "xlauncher");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            _filePath = Path.Combine(folder, "history.json");
            History = new List<HistoryItem>();
            Load();
        }

        public void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    History = JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
                }
                catch { History = new List<HistoryItem>(); }
            }
        }

        public void AddOrUpdate(string fullPath)
        {
            var item = History.FirstOrDefault(h => h.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                item.RunCount++;
                item.LastRun = DateTime.Now;
            }
            else
            {
                History.Add(new HistoryItem
                {
                    FullPath = fullPath,
                    FileName = Path.GetFileName(fullPath),
                    LastRun = DateTime.Now,
                    RunCount = 1
                });
            }
            Save();
        }

        private void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(History, options);
            File.WriteAllText(_filePath, json);
        }

        // Busca coincidencias en el historial (por nombre de archivo)
        public List<HistoryItem> Search(string query)
        {
            return History
                .Where(h => h.FileName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.RunCount) // Los más usados primero
                .ToList();
        }
    }
}