using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DoAn_Ver2.Models.AI_Services
{
    public class GroqService
    {
        // Thay bằng API Key thực tế của bạn
        private readonly string _apiKey = "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";

        public async Task<string> ChatAsync(string userMessage, string contextData, List<dynamic> chatHistory)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30); // Tối ưu timeout cho Fallback
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                string url = "https://api.groq.com/openai/v1/chat/completions";

                string systemPrompt =
                    "Bạn là chuyên gia tư vấn thời trang nam. Bạn đang trò chuyện liên tục với khách hàng.\n\n" +
                    "--- 📏 BÍ KÍP TƯ VẤN SIZE ---\n" +
                    "- ÁO: Dưới 58kg/1m65 -> Size M | 59-68kg/1m72 -> Size L | 69-78kg/1m80 -> Size XL | 79-88kg/1m88 -> Size XXL.\n" +
                    "- QUẦN: Dưới 60kg -> Size 29,30 | 60-70kg -> Size 31 | Trên 70kg -> Size 32.\n" +
                    "- GIÀY: 24.5cm -> Size 40 | 25.5cm -> Size 41 | 26.5cm -> Size 42 | 27.5cm -> Size 43.\n" +
                    "- CHÍNH SÁCH: Đổi trả 7 ngày. Freeship từ 500k.\n\n" +
                    "--- 🛑 QUY TẮC SỐNG CÒN (TUYỆT ĐỐI KHÔNG ĐƯỢC BỊA THÔNG TIN SẢN PHẨM CHỈ ĐƯỢC LẤY ĐÚNG SẢN PHẨM TRONG DATABASSE (PINECONE) ĐỂ GỢI Ý) - BẮT BUỘC TUÂN THỦ ---\n" +
                    "1. DỮ LIỆU ĐỘC QUYỀN: BẠN CHỈ ĐƯỢC PHÉP giới thiệu, báo giá và mô tả các sản phẩm NẰM TRONG phần [DỮ LIỆU SẢN PHẨM TỪ DB] bên dưới hoặc các sản phẩm bạn đã tư vấn trong lịch sử trò chuyện.\n" +
                    "2. NGHIÊM CẤM BỊA ĐẶT (ZERO HALLUCINATION): TUYỆT ĐỐI KHÔNG tự sáng tạo ra tên sản phẩm, màu sắc, kiểu dáng, hoặc giá tiền không có trong [DỮ LIỆU SẢN PHẨM TỪ DB]. TUYỆT ĐỐI KHÔNG lấy ID của sản phẩm này gán cho tên một sản phẩm không tồn tại.\n" +
                    "3. XỬ LÝ TỪ CHỐI: Nếu khách hàng tìm kiếm một sản phẩm KHÔNG KHỚP với bất kỳ thông tin nào trong [DỮ LIỆU SẢN PHẨM TỪ DB], hãy từ chối khéo léo: 'Dạ hiện tại bên em đang tạm hết hoặc không có mẫu [tên món đồ khách hỏi] ạ. Anh/chị có muốn tham khảo các mẫu tương tự em đang có sẵn không ạ?'. KHÔNG cố gắng gượng ép giới thiệu nếu dữ liệu không liên quan.\n\n" +
                    "--- 🧠 TƯ DUY XỬ LÝ NGỮ CẢNH (RẤT QUAN TRỌNG) ---\n" +
                    "1. ƯU TIÊN LỊCH SỬ CHAT: Nếu câu hỏi của khách là câu hỏi nối tiếp về các sản phẩm bạn ĐÃ GIỚI THIỆU ở câu trả lời ngay trước đó, bạn PHẢI TƯ VẤN TIẾP dựa trên các sản phẩm trong Lịch sử Chat.\n" +
                    "2. KẾT QUẢ MỚI: BẠN CHỈ ĐƯỢC DÙNG mục [KẾT QUẢ TÌM KIẾM MỚI TỪ DB] bên dưới khi khách yêu cầu tìm một món đồ HOÀN TOÀN MỚI.\n" +
                    "3. Tư duy về Size: Nếu cân nặng và chiều cao lệch size, hãy thông minh chọn size lớn hơn và giải thích cặn kẽ cho khách.\n\n" +
                    "--- 🎨 QUY TẮC TRÌNH BÀY GIAO DIỆN (BẮT BUỘC TUÂN THỦ) ---\n" +
                    "Mỗi khi liệt kê sản phẩm, PHẢI trình bày theo đúng cấu trúc HTML dưới đây:\n" +
                    "<b>Mã sản phẩm: [ID]</b>\n" +
                    "🏷️ <b>[Tên Sản Phẩm chính xác từ DB]</b>\n" +
                    "💰 Giá: <b>[Giá VNĐ chính xác từ DB]</b>\n" +
                    "📝 [Tóm tắt 1 câu mô tả ngắn gọn, hấp dẫn dựa trên thông tin DB cung cấp]\n" +
                    "<a href='https://localhost:44338/Product/Detail/[ID]' target='_blank' style='display:inline-block; margin-top:5px; padding:6px 12px; background-color:#d70018; color:#fff; text-decoration:none; border-radius:4px; font-weight:bold; font-size:12px;'>🛒 Xem chi tiết & Đặt hàng</a>\n" +
                    "<hr style='border: 0px; border-top: 1px dashed #ccc; margin: 15px 0;'>\n" +
                    "LƯU Ý: Thay [ID] bằng ID thực tế của sản phẩm, phải đúng ID là số nguyên theo đúng sản phẩm, không được tự bịa ra ID khác.\n\n" +
                    "[DỮ LIỆU SẢN PHẨM TỪ DB (Chỉ sử dụng thông tin chính xác từ đây (pinecone db), ko được bịa ra sản phẩm, thông tin sản phẩm để gợi ý)]:\n" + contextData;

                var messages = new JArray();
                messages.Add(new JObject(new JProperty("role", "system"), new JProperty("content", systemPrompt)));

                // Map History
                if (chatHistory != null && chatHistory.Count > 0)
                {
                    foreach (var msg in chatHistory)
                    {
                        string role = msg.role.ToString().ToLower() == "model" ? "assistant" : "user";
                        string text = msg.text.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            messages.Add(new JObject(new JProperty("role", role), new JProperty("content", text)));
                        }
                    }
                }

                messages.Add(new JObject(new JProperty("role", "user"), new JProperty("content", userMessage)));

                var requestBody = new JObject(
                    new JProperty("model", "llama-3.1-8b-instant"), 
                    new JProperty("messages", messages),
                    new JProperty("temperature", 0.5)
                );

                var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JObject.Parse(responseString);
                    return jsonResponse["choices"][0]["message"]["content"].ToString();
                }
                else
                {
                    throw new Exception("Lỗi Groq API: " + await response.Content.ReadAsStringAsync());
                }
            }
        }
    }
}