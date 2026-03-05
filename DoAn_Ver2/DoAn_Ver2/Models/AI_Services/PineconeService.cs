using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;

namespace DoAn_Ver2.Models.AI_Services
{
    public class PineconeService
    {
        // 1. Dán API Key của Pinecone vào đây
        private readonly string _apiKey = "pcsk_qBXZH_4tCGTxLB6hG5V3rhrTDmPrVY12H9HBV3svV7XZESmHVAgxGzS21vXVeDvdNkByF";

        // 2. Dán Host URL của Index vào đây (Nhớ thêm https:// ở đầu)
        private readonly string _hostUrl = "https://chatbot-ai-webbanhang-iwtlyqf.svc.aped-4627-b74a.pinecone.io";

        public async Task<bool> UpsertVectorAsync(string sanPhamID, List<double> vectorValues)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Api-Key", _apiKey);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                string url = $"{_hostUrl}/vectors/upsert";

                var requestBody = new
                {
                    vectors = new[]
                    {
                        new
                        {
                            id = sanPhamID,
                            values = vectorValues
                        }
                    },
                    @namespace = "sanpham_thoitrang"
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Lỗi lưu Pinecone: {error}");
                }
            }
        }



        // Thêm hàm này vào dưới hàm UpsertVectorAsync trong PineconeService.cs
        public async Task<List<string>> SearchVectorAsync(List<double> queryVector, int topK = 3)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Api-Key", _apiKey);
                client.DefaultRequestHeaders.Add("accept", "application/json");

                string url = $"{_hostUrl}/query";

                var requestBody = new
                {
                    vector = queryVector,
                    topK = topK,
                    includeValues = false,
                    includeMetadata = false,
                    @namespace = "sanpham_thoitrang" // Nhớ dùng đúng namespace lúc em lưu dữ liệu
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject jsonResponse = JObject.Parse(responseString);

                    List<string> matchedIds = new List<string>();
                    foreach (var match in jsonResponse["matches"])
                    {
                        matchedIds.Add(match["id"].ToString()); // Lấy ra danh sách SanPhamID
                    }
                    return matchedIds;
                }
                else
                {
                    throw new Exception("Lỗi tìm kiếm Pinecone: " + await response.Content.ReadAsStringAsync());
                }
            }
        }

    }
}