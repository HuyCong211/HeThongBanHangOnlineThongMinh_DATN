using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DoAn_Ver2.Models.AI_Services
{
    public class CohereService
    {
        // Dán API Key Trial của Cohere vào đây
        private readonly string _apiKey = "yzWr3FFhlS9uykMCY9RNVt2hg8dEOn5e0ofGNWZg";

        public async Task<List<double>> GetEmbeddingAsync(string text)
        {
            // Ép chuẩn bảo mật TLS 1.2 (Bắt buộc cho .NET Framework)
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            using (var client = new HttpClient())
            {
                // Cấu hình Header siêu đơn giản
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                client.DefaultRequestHeaders.Add("accept", "application/json");

                // URL của Cohere không bao giờ thay đổi
                string url = "https://api.cohere.ai/v1/embed";

                // Định dạng dữ liệu gửi đi (Cohere nhận mảng các câu)
                var requestBody = new
                {
                    texts = new[] { text },
                    model = "embed-multilingual-v3.0", // Mô hình hỗ trợ Tiếng Việt cực đỉnh
                    input_type = "search_document"     // Báo cho AI biết đây là data để tìm kiếm
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                // Gọi API
                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();

                    // Phân tích JSON cực kỳ gọn gàng
                    JObject jsonResponse = JObject.Parse(responseString);

                    // Lấy thẳng mảng số Vector ra
                    var embeddingArray = jsonResponse["embeddings"][0].ToObject<List<double>>();

                    return embeddingArray;
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Lỗi gọi Cohere API: {error}");
                }
            }
        }





       
    }
}