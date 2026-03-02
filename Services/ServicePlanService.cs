using System.IO;
using System.Text.Json;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.Services
{
    /// <summary>
    /// Service for saving and loading the "service plan" (pre-defined media slots).
    /// </summary>
    public class ServicePlanService
    {
        private readonly string _savePath;

        public ServicePlanService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, AppConstants.Storage.AppDataFolderName);
            Directory.CreateDirectory(folder);
            _savePath = Path.Combine(folder, "service_plan.json");
        }

        public void SavePlan(Dictionary<string, string> plan)
        {
            try
            {
                var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_savePath, json);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to save service plan");
                throw;
            }
        }

        public Dictionary<string, string> LoadPlan()
        {
            try
            {
                if (!File.Exists(_savePath))
                    return new Dictionary<string, string>();

                var json = File.ReadAllText(_savePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to load service plan");
                return new Dictionary<string, string>();
            }
        }
    }
}
