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
    public class GeminiService
    {
        // Dán API Key Google AI Studio của em vào đây
        private readonly string _apiKey = "AIzaSyCjxscWDGcRqYe3fzoTNmPJtQMGSNYAi18";

        // HÀM ĐÃ ĐƯỢC NÂNG CẤP LÊN 3 THAM SỐ (Thêm chatHistory)
        public async Task<string> ChatAsync(string userMessage, string contextData, List<dynamic> chatHistory)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            using (var client = new HttpClient())
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

                // =================================================================================
                // BỘ PROMPT NÂNG CẤP: DẠY AI BIẾT ƯU TIÊN LỊCH SỬ CHAT (CONTEXT PRECEDENCE)
                // =================================================================================
                string systemInstruction =
                    "Bạn là chuyên gia tư vấn thời trang nam. Bạn đang trò chuyện liên tục với khách hàng.\n\n" +
                    "--- 📏 BÍ KÍP TƯ VẤN SIZE ---\n" +
                    "- ÁO: Dưới 58kg/1m65 -> Size M | 59-68kg/1m72 -> Size L | 69-78kg/1m80 -> Size XL | 79-88kg/1m88 -> Size XXL.\n" +
                    "- QUẦN: Dưới 60kg -> Size 29,30 | 60-70kg -> Size 31 | Trên 70kg -> Size 32.\n" +
                    "- GIÀY: 24.5cm -> Size 40 | 25.5cm -> Size 41 | 26.5cm -> Size 42 | 27.5cm -> Size 43.\n" +
                    "- CHÍNH SÁCH: Đổi trả 7 ngày. Freeship từ 500k.\n\n" +
                    "--- 🧠 TƯ DUY XỬ LÝ NGỮ CẢNH (RẤT QUAN TRỌNG) ---\n" +
                    "1. ƯU TIÊN LỊCH SỬ CHAT: Nếu câu hỏi của khách (VD: 'vậy mặc size gì', 'lấy cái đó đi', 'có màu khác không') là câu hỏi nối tiếp về các sản phẩm bạn ĐÃ GIỚI THIỆU ở câu trả lời ngay trước đó, bạn PHẢI TƯ VẤN TIẾP dựa trên các sản phẩm trong Lịch sử Chat.\n" +
                    "2. KẾT QUẢ MỚI: BẠN CHỈ ĐƯỢC DÙNG mục [KẾT QUẢ TÌM KIẾM MỚI TỪ DB] bên dưới khi khách yêu cầu tìm một món đồ HOÀN TOÀN MỚI.\n" +
                    "3. Tư duy về Size: Nếu cân nặng và chiều cao lệch size (VD: cân nặng size M nhưng cao size L), hãy thông minh chọn size L để áo/quần không bị cộc và giải thích cặn kẽ cho khách.\n\n" +
                    "--- 🎨 QUY TẮC TRÌNH BÀY GIAO DIỆN (BẮT BUỘC TUÂN THỦ) ---\n" +
                    "Để khách hàng dễ đọc, mỗi khi liệt kê sản phẩm, bạn PHẢI trình bày theo đúng cấu trúc HTML dưới đây:\n" +
                    "🏷️ <b>[Tên Sản Phẩm]</b>\n" +
                    "💰 Giá: <b>[Giá VNĐ]</b>\n" +
                    "📝 [Tự tóm tắt 1 câu mô tả ngắn gọn, hấp dẫn về sản phẩm này]\n" +
                    "<a href='https://localhost:44338/Product/Detail/[ID]' target='_blank' style='display:inline-block; margin-top:5px; padding:6px 12px; background-color:#d70018; color:#fff; text-decoration:none; border-radius:4px; font-weight:bold; font-size:12px;'>🛒 Xem chi tiết & Đặt hàng</a>\n" +
                    "<hr style='border: 0px; border-top: 1px dashed #ccc; margin: 15px 0;'>\n" +
                    "LƯU Ý QUAN TRỌNG: Thay thế [ID] bằng ID thực tế của sản phẩm để link hoạt động.\n\n" +
                    "[KẾT QUẢ TÌM KIẾM MỚI TỪ DB (Chỉ dùng khi khách hỏi món đồ mới)]:\n" + contextData;

                var contentsArray = new JArray();

                // Đưa lịch sử cũ vào (nếu có)
                if (chatHistory != null)
                {
                    foreach (var msg in chatHistory)
                    {
                        contentsArray.Add(new JObject(
                            new JProperty("role", msg.role.ToString()),
                            new JProperty("parts", new JArray(new JObject(new JProperty("text", msg.text.ToString()))))
                        ));
                    }
                }

                // Đưa câu hỏi mới nhất của khách vào
                string finalMessage = $"[HƯỚNG DẪN HỆ THỐNG]:\n{systemInstruction}\n\n[KHÁCH HÀNG HỎI]: {userMessage}";
                contentsArray.Add(new JObject(
                    new JProperty("role", "user"),
                    new JProperty("parts", new JArray(new JObject(new JProperty("text", finalMessage))))
                ));

                var requestBody = new JObject(new JProperty("contents", contentsArray));
                var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject jsonResponse = JObject.Parse(responseString);
                    return jsonResponse["candidates"][0]["content"]["parts"][0]["text"].ToString();
                }
                else
                {
                    throw new Exception("Lỗi API: " + await response.Content.ReadAsStringAsync());
                }
            }
        }
    }
}