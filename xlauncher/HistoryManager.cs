using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace xlauncher
{

    public class HistoryManager
    {
        private string _filePath;
        public List<HistoryItem> History { get; private set; }

        public HistoryManager()
        {
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

        public void AddOrUpdate(string fullPath, bool isFolder)
        {
            var item = History.FirstOrDefault(h => h.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                item.RunCount++;
                item.LastRun = DateTime.Now;
                item.IsFolder = isFolder;
            }
            else
            {
                History.Add(new HistoryItem
                {
                    FullPath = fullPath,
                    FileName = Path.GetFileName(fullPath),
                    Alias = null,
                    LastRun = DateTime.Now,
                    RunCount = 1,
                    IsFolder = isFolder
                });
            }
            Save();
        }

        public void UpdateAlias(string fullPath, string newAlias)
        {
            var item = History.FirstOrDefault(h => h.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.Alias = newAlias;
                Save();
            }
        }

        public void Remove(string fullPath)
        {
            var item = History.FirstOrDefault(h => h.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                History.Remove(item);
                Save();
            }
        }

        private void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(History, options);
            File.WriteAllText(_filePath, json);
        }

        public List<HistoryItem> Search(string query)
        {
            return History
                .Where(h =>
                    (!string.IsNullOrEmpty(h.Alias) && h.Alias.StartsWith(query, StringComparison.OrdinalIgnoreCase)) ||
                    (string.IsNullOrEmpty(h.Alias) && h.FileName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                )
                .OrderByDescending(h => h.RunCount)
                .ToList();
        }
    }

}