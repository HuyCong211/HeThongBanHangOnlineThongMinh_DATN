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
        private readonly string _apiKey = "AIzaSyCutGufD0msvMjcSTK4T2gxzZKkJvIKlWM";

        // HÀM ĐÃ ĐƯỢC NÂNG CẤP LÊN 3 THAM SỐ (Thêm chatHistory)
        public async Task<string> ChatAsync(string userMessage, string contextData, List<dynamic> chatHistory)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(8);

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3-flash-preview:generateContent?key={_apiKey}";
                //string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-preview:generateContent?key={_apiKey}";
                //string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-tts-preview:generateContent?key={_apiKey}";
                //string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
                string systemInstruction =
                    "Bạn là chuyên gia tư vấn thời trang nam. Bạn đang trò chuyện liên tục với khách hàng.\n\n" +
                    "--- 📏 BÍ KÍP TƯ VẤN SIZE ---\n" +
                    "- ÁO: Dưới 58kg/1m65 -> Size M | 59-68kg/1m72 -> Size L | 69-78kg/1m80 -> Size XL | 79-88kg/1m88 -> Size XXL.\n" +
                    "- QUẦN: Dưới 60kg -> Size 29,30 | 60-70kg -> Size 31 | Trên 70kg -> Size 32.\n" +
                    "- GIÀY: 24.5cm -> Size 40 | 25.5cm -> Size 41 | 26.5cm -> Size 42 | 27.5cm -> Size 43.\n" +
                    "- CHÍNH SÁCH: Đổi trả 7 ngày. Freeship từ 500k.\n\n" +
                    "--- 🛑 QUY TẮC SỐNG CÒN (ANTI-HALLUCINATION) - BẮT BUỘC TUÂN THỦ ---\n" +
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
                    "[DỮ LIỆU SẢN PHẨM TỪ DB (Chỉ sử dụng thông tin chính xác từ đây)]:\n" + contextData;

                var systemInstructionObj = new JObject(
                    new JProperty("parts", new JArray(new JObject(new JProperty("text", systemInstruction))))
                );

                var contentsArray = new JArray();

                if (chatHistory != null && chatHistory.Count > 0)
                {
                    var cleanedHistory = new List<JObject>();

                    // Bước 1: Chuẩn hóa role và strip HTML
                    foreach (var msg in chatHistory)
                    {
                        string role = msg.role.ToString().ToLower();
                        if (role == "bot" || role == "assistant") role = "model";
                        else if (role != "user" && role != "model") role = "user";

                        string cleanText = StripHtml(msg.text.ToString());
                        if (string.IsNullOrWhiteSpace(cleanText)) continue;

                        cleanedHistory.Add(new JObject(
                            new JProperty("role", role),
                            new JProperty("parts", new JArray(new JObject(new JProperty("text", cleanText))))
                        ));
                    }

                    // Bước 2: Bỏ các turn "model" ở đầu
                    while (cleanedHistory.Count > 0 && cleanedHistory[0]["role"]?.ToString() == "model")
                        cleanedHistory.RemoveAt(0);

                    // Bước 3: Gộp các turn liên tiếp cùng role
                    for (int i = cleanedHistory.Count - 1; i > 0; i--)
                    {
                        if (cleanedHistory[i]["role"]?.ToString() == cleanedHistory[i - 1]["role"]?.ToString())
                        {
                            string merged = cleanedHistory[i - 1]["parts"][0]["text"].ToString()
                                          + "\n" + cleanedHistory[i]["parts"][0]["text"].ToString();
                            cleanedHistory[i - 1]["parts"][0]["text"] = merged;
                            cleanedHistory.RemoveAt(i);
                        }
                    }

                    // Bước 4: Nếu turn cuối history là "user" thì gộp với userMessage
                    // để tránh 2 "user" turns liên tiếp → Gemini báo lỗi 503
                    string finalUserMessage = userMessage;
                    if (cleanedHistory.Count > 0 &&
                        cleanedHistory[cleanedHistory.Count - 1]["role"]?.ToString() == "user")
                    {
                        string lastText = cleanedHistory[cleanedHistory.Count - 1]["parts"][0]["text"].ToString();
                        finalUserMessage = lastText + "\n" + userMessage;
                        cleanedHistory.RemoveAt(cleanedHistory.Count - 1);
                    }

                    // Bước 5: Đưa history vào contents
                    foreach (var item in cleanedHistory)
                        contentsArray.Add(item);

                    // Bước 6: Thêm tin nhắn user cuối
                    contentsArray.Add(new JObject(
                        new JProperty("role", "user"),
                        new JProperty("parts", new JArray(new JObject(new JProperty("text", finalUserMessage))))
                    ));
                }
                else
                {
                    // Không có history (chưa đăng nhập / câu đầu tiên)
                    contentsArray.Add(new JObject(
                        new JProperty("role", "user"),
                        new JProperty("parts", new JArray(new JObject(new JProperty("text", userMessage))))
                    ));
                }

                var requestBody = new JObject(
                    new JProperty("system_instruction", systemInstructionObj),
                    new JProperty("contents", contentsArray)
                );

                var httpContent = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, httpContent);

                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject jsonResponse = JObject.Parse(responseString);
                    return jsonResponse["candidates"][0]["content"]["parts"][0]["text"].ToString();
                }
                else
                {
                    string errorDetail = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Lỗi API ({response.StatusCode}): {errorDetail}");
                }
            }
        }





        // Thêm hàm helper này vào cuối class GeminiService
        private string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            // Xóa toàn bộ thẻ HTML, giữ lại text thuần
            return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")
                .Replace("&nbsp;", " ")
                .Replace("  ", " ")
                .Trim();
        }
    }
}