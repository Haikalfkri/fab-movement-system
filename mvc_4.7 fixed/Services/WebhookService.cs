using Newtonsoft.Json;
using ok1.Data;
using ok1.Models;
using ok1.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ok1.Services
{
    public class WebhookService
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            UseDefaultCredentials = true
        });

        private string WebhookUrl = "https://webhook.site/your-webhook-url"; // Replace with your actual webhook URL

        public async Task SendWebhookAsync(
            List<FabMove> fabMoves,
            string badgeId, 
            int month,
            int year,
            TargetTime target,
            tbl_GMDS gmds
        )
        {
            try
            {
                if (fabMoves == null || !fabMoves.Any())
                {
                    return;
                }

                int total = fabMoves.Count;
                int failed = fabMoves.Count(fm =>
                    string.Equals(fm.TimeClass, "Failed", StringComparison.OrdinalIgnoreCase));

                string name = gmds?.Name;
                string department = gmds?.Department;
                string designation = gmds?.Designation;
                string shift = gmds?.Group;
                string areaDetail = target?.AreaDI;
                string area = target?.Area;
                string groupArea = target?.Group;

                var payload = new
                {
                    badgeId = badgeId,
                    name = name,
                    organizationalUnit = department,
                    areaDetail = areaDetail,
                    area = area,
                    shift = shift,
                    designation = designation,
                    groupArea = groupArea,
                    month = month,
                    year = year,
                    totalFab = total,
                    FABMovement = failed,
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(WebhookUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Webhook] Failed badgeId={badgeId} " +
                            $"Status={(int)response.StatusCode} " +
                            $"Month={month} Year={year}");
                }
                else
                {
                    Debug.WriteLine($"[Webhook] Success badgeId={badgeId} " +
                        $"Month={month} Year={year} " +
                        $"Total={total} Failed={failed}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Webhook] Exception badgeId={badgeId}: {ex.Message}");
            }
        }
    }
}