using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using DoAn_Ver2.Models.AI_Services;
using DoAn_Ver2.Models.ViewModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;


namespace DoAn_Ver2.Controllers
{
    public class HomeController : Controller
    {
        // Khởi tạo UnitOfWork (Copy logic từ Admin sang dùng chung)
        private UnitOfWork _unitOfWork = new UnitOfWork();
        public ActionResult Index()
        {
            // 1. CHỈ LẤY DANH MỤC GỐC (DanhMucChaID == null)
            // Take(3) hoặc Take(6) tùy bạn muốn hiện bao nhiêu ô
            ViewBag.Categories = _unitOfWork.Repository<DanhMuc>()
                                    .GetMany(x => x.DanhMucChaID == null)
                                    .OrderBy(x => x.ID)
                                    .ToList();

            // 2. Lấy sản phẩm MỚI NHẤT
            var newProducts = _unitOfWork.Repository<SanPham>()
                            .GetMany(x => x.TrangThai == 1)
                            .OrderByDescending(x => x.NgayTao)
                            .Take(8)
                            .ToList();

            // 3. Lấy sản phẩm BÁN CHẠY
            // 3. SẢN PHẨM BÁN CHẠY (CODE TỐI ƯU & FIX LỖI)
            try
            {
                // [TỐI ƯU HÓA] Thực hiện Group By và Sum ngay dưới Database
                // Không dùng .ToList() sớm để tránh tải dư thừa dữ liệu về RAM

                // Bước 1: Truy vấn vào bảng ChiTietDonHang
                // Lưu ý: Cần đảm bảo Repository trả về IQueryable để EF dịch sang SQL
                // Nếu UnitOfWork của bạn .GetAll() trả về IEnumerable thì buộc phải sửa Repository hoặc dùng DbContext trực tiếp.
                // Ở đây tôi giả định bạn có thể truy cập DbSet hoặc Repository hỗ trợ IQueryable.

                // Cách an toàn nhất với mô hình Repository hiện tại của bạn (chấp nhận query in-memory nhưng lọc kỹ):
                // Hoặc nếu Repository của bạn trả về IQueryable, bỏ .ToList() ở dòng dưới đi.

                var bestSellerStats = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    // Chỉ lấy đơn thành công
                    .Where(ct => ct.DonHang.TrangThaiDonHang == 3)
                    // Group theo ID Sản phẩm cha (thông qua Biến thể)
                    .GroupBy(ct => ct.BienTheSanPham.SanPhamID)
                    .Select(group => new
                    {
                        SanPhamID = group.Key,
                        TotalSold = group.Sum(ct => ct.SoLuong),
                        // [THÊM MỚI] Lấy ngày đặt hàng gần nhất của sản phẩm này
                        LastOrderDate = group.Max(ct => ct.DonHang.NgayDat)
                    })
                    // [THAY ĐỔI] Sắp xếp ưu tiên SL bán -> Sau đó đến Ngày bán gần nhất
                    .OrderByDescending(x => x.TotalSold)
                    .ThenByDescending(x => x.LastOrderDate)
                    .Take(15)
                    .ToList();

                // Bước 2: Lấy thông tin chi tiết sản phẩm từ danh sách ID trên
                if (bestSellerStats.Any())
                {
                    var productIds = bestSellerStats.Select(x => x.SanPhamID).ToList();

                    var products = _unitOfWork.Repository<SanPham>()
                                                .GetMany(p => productIds.Contains(p.ID) && p.TrangThai == 1)
                                                .ToList();

                    // Bước 3: Join lại để giữ đúng thứ tự (Quan trọng)
                    // Join giữa danh sách thống kê (đã sort chuẩn) và danh sách sản phẩm
                    ViewBag.BestSellers = bestSellerStats
                        .Join(products,
                              stat => stat.SanPhamID,
                              prod => prod.ID,
                              (stat, prod) => prod)
                        .ToList();
                }
                else
                {
                    // Fallback: Nếu chưa có đơn hàng nào thì lấy theo Lượt xem
                    ViewBag.BestSellers = _unitOfWork.Repository<SanPham>()
                                                     .GetMany(x => x.TrangThai == 1)
                                                     .OrderByDescending(x => x.LuotXem)
                                                     .Take(15).ToList();
                }
            }
            catch (Exception ex)
            {
                // Fallback an toàn
                ViewBag.BestSellers = new List<SanPham>();
            }


            // =========================================================
            // [PHẦN MỚI] 4. GỢI Ý THÔNG MINH CHO NGƯỜI DÙNG (TRANG CHỦ)
            // =========================================================
            string sessionId = GetOrSetGuestSessionId();
            int? userId = null;
            if (Session["KhachHang"] != null)
            {
                userId = ((NguoiDung)Session["KhachHang"]).ID;
            }

            var aiService = new DoAn_Ver2.Services.RecommendationService();
            var repoGoiY = _unitOfWork.Repository<GoiYSanPham>();

            // Lấy danh sách ID đã tính toán từ DB
            var recommendIds = repoGoiY.GetMany(x =>
                    (userId.HasValue ? x.NguoiDungID == userId.Value : x.SessionID == sessionId)
                    && x.LoaiGoiY == "ForYou"
                )
                .OrderByDescending(x => x.DiemGoiY)
                .Select(x => x.SanPhamDuocGoiYID)
                .Take(8).ToList();

            // Nếu DB rỗng (Khách vừa vào web lần đầu) -> Gọi AI chạy Đồng bộ ngay lập tức
            if (!recommendIds.Any())
            {
                aiService.CalculateUserRecommendations(userId, sessionId);

                // Sau khi AI tính xong, lấy lại từ DB
                recommendIds = repoGoiY.GetMany(x =>
                        (userId.HasValue ? x.NguoiDungID == userId.Value : x.SessionID == sessionId)
                        && x.LoaiGoiY == "ForYou"
                    )
                    .OrderByDescending(x => x.DiemGoiY)
                    .Select(x => x.SanPhamDuocGoiYID)
                    .Take(8).ToList();
            }
            else
            {
                // Nếu DB đã có dữ liệu -> Hiển thị luôn cho web load nhanh
                // Đồng thời gọi AI chạy ngầm (Task.Run) để cập nhật dữ liệu cho lần load sau
                Task.Run(() => aiService.CalculateUserRecommendations(userId, sessionId));
            }

            // Join ID để lấy chi tiết sản phẩm
            var recommendedProducts = new List<SanPham>();
            if (recommendIds.Any())
            {
                var raw = _unitOfWork.Repository<SanPham>().GetMany(x => recommendIds.Contains(x.ID) && x.TrangThai == 1).ToList();
                recommendedProducts = recommendIds.Select(id => raw.FirstOrDefault(p => p.ID == id))
                                                .Where(p => p != null)
                                                .ToList();
            }

            // Truyền sang View
            ViewBag.RecommendedProducts = recommendedProducts;
            // =========================================================
            return View(newProducts);
        }

        public ActionResult About()
        {
            // Lấy thông tin cấu hình để hiển thị dynamic
            string hotline = "1900 xxxx";
            string email = "contact@menstore.com";
            string address = "Hà Nội";
            // Giá trị mặc định
            string banner = "https://theme.hstatic.net/200000690725/1001078549/14/slide_2_img.jpg?v=235";
            string image = "https://images.unsplash.com/photo-1490578474895-699cd4e2cf59";

            try
            {
                var repo = _unitOfWork.Repository<CauHinhChung>();

                var confHotline = repo.GetById("Hotline");
                var confEmail = repo.GetById("Email");
                var confAddress = repo.GetById("FooterInfo");

                // [MỚI] Lấy Banner và Ảnh
                var confBanner = repo.GetById("BannerAbout");
                var confImage = repo.GetById("ImageAbout");

                if (confHotline != null) hotline = confHotline.Value;
                if (confEmail != null) email = confEmail.Value;
                if (confAddress != null) address = confAddress.Value;

                if (confBanner != null && !string.IsNullOrEmpty(confBanner.Value)) banner = confBanner.Value;
                if (confImage != null && !string.IsNullOrEmpty(confImage.Value)) image = confImage.Value;
            }
            catch { }

            ViewBag.Hotline = hotline;
            ViewBag.Email = email;
            ViewBag.Address = address;

            // Truyền sang View
            ViewBag.BannerAbout = banner;
            ViewBag.ImageAbout = image;

            return View();
        }

        public ActionResult Contact()
        {
            // Lấy thông tin từ cấu hình chung để hiển thị
            string address = "Hà Nội";
            string hotline = "1900 xxxx";
            string email = "contact@menstore.com";

            try
            {
                var confAddress = _unitOfWork.Repository<CauHinhChung>().GetById("FooterInfo");
                var confHotline = _unitOfWork.Repository<CauHinhChung>().GetById("Hotline");
                var confEmail = _unitOfWork.Repository<CauHinhChung>().GetById("Email");

                if (confAddress != null) address = confAddress.Value;
                if (confHotline != null) hotline = confHotline.Value;
                if (confEmail != null) email = confEmail.Value;
            }
            catch { }

            ViewBag.Address = address;
            ViewBag.Hotline = hotline;
            ViewBag.Email = email;

            return View();
        }

        // 2. XỬ LÝ GỬI LIÊN HỆ (POST AJAX) - ĐÃ SỬA LỖI MAIL
        // 2. XỬ LÝ GỬI LIÊN HỆ (POST AJAX) - DÙNG MAILSETTINGS TRONG WEBCONFIG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitContact(LienHeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Lấy email người nhận từ Web.config (AppSettings) hoặc fix cứng
                    string toEmail = ConfigurationManager.AppSettings["EmailUserName"];
                    // Hoặc string toEmail = "hoconline2k4@gmail.com";

                    var message = new MailMessage();
                    // Không cần set From thủ công nếu Web.config đã có attribute 'from'
                    // Nhưng để chắc chắn, ta vẫn set From = toEmail (vì toEmail là mail server gửi đi)
                    message.From = new MailAddress(toEmail, "MEN STORE");
                    message.To.Add(new MailAddress(toEmail));

                    message.Subject = $"[MenStore Liên Hệ] Từ: {model.HoTen} - {model.SDT}";
                    message.ReplyToList.Add(new MailAddress(model.Email));
                    message.IsBodyHtml = true;

                    message.Body = $@"
                    <div style='font-family:Arial, sans-serif; padding:20px; border:1px solid #ddd; max-width:600px;'>
                        <h2 style='color:#007bff; border-bottom:2px solid #007bff; padding-bottom:10px;'>📩 THÔNG BÁO LIÊN HỆ MỚI</h2>
                        <table style='width:100%; border-collapse:collapse;'>
                            <tr><td style='padding:8px; font-weight:bold;'>Họ tên:</td><td style='padding:8px;'>{model.HoTen}</td></tr>
                            <tr><td style='padding:8px; font-weight:bold;'>Email:</td><td style='padding:8px;'>{model.Email}</td></tr>
                            <tr><td style='padding:8px; font-weight:bold;'>SĐT:</td><td style='padding:8px;'>{model.SDT}</td></tr>
                        </table>
                        <div style='background:#f9f9f9; padding:15px; margin-top:15px; border-left:4px solid #28a745;'>
                            <strong>Nội dung:</strong><br/>{model.NoiDung}
                        </div>
                    </div>";

                    // [QUAN TRỌNG] Khởi tạo SmtpClient không tham số -> Nó sẽ tự đọc <mailSettings>
                    using (var client = new SmtpClient())
                    {
                        // [LƯU Ý]: Mặc dù đọc từ config, nhưng Gmail đôi khi vẫn cần set lại Credentials bằng code để chắc chắn
                        // Nếu chỉ new SmtpClient() mà vẫn lỗi, hãy bỏ comment dòng dưới để override
                        /*
                        client.Host = "smtp.gmail.com";
                        client.Port = 587;
                        client.EnableSsl = true;
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential("hoconline2k4@gmail.com", "clifscenvwbvvaip");
                        */

                        client.Send(message);
                    }

                    return Json(new { success = true, msg = "Cảm ơn bạn! Chúng tôi đã nhận được tin nhắn." });
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.Message;
                    if (ex.InnerException != null) errorMsg += " | " + ex.InnerException.Message;
                    return Json(new { success = false, msg = "Lỗi gửi mail: " + errorMsg });
                }
            }
            return Json(new { success = false, msg = "Vui lòng kiểm tra lại thông tin!" });
        }

        // Action PartialView để render Menu Danh mục động trên Layout
        [ChildActionOnly]
        public ActionResult _MenuPartial()
        {
            var danhMucs = _unitOfWork.Repository<DanhMuc>().GetAll().ToList();
            return PartialView(danhMucs);
        }

        // Action render Header User Info (để Layout gọn hơn)
        [ChildActionOnly]
        public ActionResult _HeaderUserPartial()
        {
            return PartialView();
        }
        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }


        [ChildActionOnly]
        public ActionResult _HeaderCart()
        {
            int count = 0;

            if (Session["KhachHang"] != null)
            {
                // Nếu đã đăng nhập -> Lấy count trực tiếp từ DB cho chắc chắn
                var user = (NguoiDung)Session["KhachHang"];
                var cart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == user.ID).FirstOrDefault();
                if (cart != null)
                {
                    // Dùng Sum(x => (int?)x.SoLuong) ?? 0 để tránh lỗi null
                    count = _unitOfWork.Repository<GioHangChiTiet>().GetMany(x => x.GioHangID == cart.ID).Sum(x => x.SoLuong) ?? 0;
                }
            }
            else
            {
                // Nếu chưa đăng nhập -> Lấy từ Session
                var list = Session["Cart"] as List<CartItemViewModel>;
                if (list != null) count = list.Sum(x => x.SoLuong);
            }

            // Cập nhật lại Session luôn cho đồng bộ
            Session["CartCount"] = count;

            return PartialView(count);
        }






        public ActionResult TestTrangThaiDuLieuAI()
        {
            AIDataService aiService = new AIDataService();

            // Gọi hàm lấy dữ liệu text
            var lstData = aiService.ExtractDataForAI();

            // In thẳng ra màn hình trình duyệt dưới dạng JSON để kiểm tra kết quả
            return Json(lstData, JsonRequestBehavior.AllowGet);
        }


        public async Task<ActionResult> TestTaoVector()
        {
            AIDataService aiService = new AIDataService();
            var lstData = aiService.ExtractDataForAI();

            if (lstData.Count == 0) return Content("Không có sản phẩm nào!");
            string textSanPhamDauTien = lstData[0];

            // GỌI COHERE SERVICE CHUYÊN NGHIỆP
            CohereService cohere = new CohereService();

            try
            {
                var vector = await cohere.GetEmbeddingAsync(textSanPhamDauTien);

                string htmlResult = $"<h3 style='color:blue;'>Text gốc:</h3> <p>{textSanPhamDauTien}</p>";
                htmlResult += $"<h3 style='color:green;'>Vector sinh ra từ Cohere (Độ dài: {vector.Count} chiều):</h3>";

                htmlResult += "<p>[ " + string.Join(", ", vector.GetRange(0, 10)) + ", ... ]</p>";

                return Content(htmlResult, "text/html", System.Text.Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                return Content("<h3 style='color:red;'>Lỗi:</h3> " + ex.Message);
            }
        }


        /*========================CHAT BOT AI===================================*/
        public async Task<ActionResult> DongBoDuLieuAI()
        {
            // 1. Lấy tất cả sản phẩm từ SQL ra dạng Text
            AIDataService aiDataService = new AIDataService();
            var lstProducts = aiDataService.ExtractDataForAI();

            if (lstProducts.Count == 0) return Content("Không có sản phẩm nào để đồng bộ!");

            CohereService cohere = new CohereService();
            PineconeService pinecone = new PineconeService();

            int successCount = 0;

            // 2. Lặp qua từng sản phẩm để xử lý
            foreach (var textData in lstProducts)
            {
                try
                {
                    // Trích xuất ID Sản phẩm từ chuỗi Text (Ví dụ text là "[ID: 15] Tên SP...")
                    int startIndex = textData.IndexOf("[ID: ") + 5;
                    int endIndex = textData.IndexOf("]");
                    string sanPhamID = textData.Substring(startIndex, endIndex - startIndex);

                    // 3. Gọi Cohere biến Text thành Vector
                    var vector = await cohere.GetEmbeddingAsync(textData);

                    // 4. Gọi Pinecone lưu Vector cùng với ID Sản phẩm
                    bool isSaved = await pinecone.UpsertVectorAsync(sanPhamID, vector);

                    if (isSaved) successCount++;

                    // Tạm nghỉ 1 giây để tránh bị Cohere/Pinecone chặn vì gọi quá nhanh (Rate limit)
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    // Nếu lỗi 1 sản phẩm thì bỏ qua chạy tiếp sản phẩm khác
                    System.Diagnostics.Debug.WriteLine($"Lỗi đồng bộ SP: {ex.Message}");
                }
            }

            return Content($"<h2 style='color:green;'>Đã đồng bộ thành công {successCount}/{lstProducts.Count} sản phẩm lên não AI!</h2>", "text/html", Encoding.UTF8);
        }




        public async Task<ActionResult> TestChatbotAI(string cauHoi = "Có áo sơ mi nam nào màu trắng để mặc đi làm không?")
        {
            try
            {
                CohereService cohere = new CohereService();
                PineconeService pinecone = new PineconeService();
                AIDataService aiDataService = new AIDataService();

                // 1. Biến câu hỏi của khách thành Vector
                var questionVector = await cohere.GetEmbeddingAsync(cauHoi);

                // 2. Tìm top 3 SanPhamID giống nhất trong Pinecone
                List<string> top3ProductIds = await pinecone.SearchVectorAsync(questionVector, 3);

                if (top3ProductIds.Count == 0) return Content("Không tìm thấy sản phẩm nào trong Pinecone.");

                // 3. Lấy thông tin chi tiết của 3 sản phẩm đó (Giả lập việc lấy từ list hoặc query DB)
                // Ở đây để nhanh, ta lấy từ hàm ExtractDataForAI (thực tế em có thể query Entity Framework)
                var allProductsText = aiDataService.ExtractDataForAI();
                string contextForAI = "";

                foreach (var idStr in top3ProductIds)
                {
                    var productInfo = allProductsText.FirstOrDefault(p => p.Contains($"[ID: {idStr}]"));
                    if (productInfo != null)
                    {
                        contextForAI += productInfo + "\n";
                    }
                }

                // 4. GỌI GEMINI ĐỂ CHAT (THAY VÌ COHERE)
                GeminiService gemini = new GeminiService();
                string botReply = await gemini.ChatAsync(cauHoi, contextForAI, null);

                // 5. In kết quả ra màn hình
                string htmlResult = $"<h3 style='color:blue;'>Khách hỏi:</h3> <p>{cauHoi}</p>";
                htmlResult += $"<h3 style='color:purple;'>Sản phẩm tìm được từ Pinecone:</h3> <p>{contextForAI}</p>";
                htmlResult += $"<h3 style='color:green;'>Chatbot (Gemini) trả lời:</h3> <p><b>{botReply}</b></p>";

                return Content(htmlResult, "text/html", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }





        [HttpPost]
        [ValidateInput(false)]
        public async Task<JsonResult> SendMessage(string userMessage, string historyJson)
        {
            try
            {
                CohereService cohere = new CohereService();
                PineconeService pinecone = new PineconeService();
                AIDataService aiDataService = new AIDataService();
                GeminiService gemini = new GeminiService();

                // 1. Giải mã lịch sử chat
                List<dynamic> chatHistory = new List<dynamic>();
                if (!string.IsNullOrEmpty(historyJson))
                {
                    chatHistory = JsonConvert.DeserializeObject<List<dynamic>>(historyJson);
                }

                string contextForAI = "";
                string msgLower = userMessage.ToLower();

                // 2. ĐỊNH TUYẾN TƯ DUY (ROUTING LOGIC)
                // Kiểm tra xem khách có nhắc đến các từ khóa về "bán chạy" không
                if (msgLower.Contains("bán chạy") || msgLower.Contains("hot") || msgLower.Contains("nhiều người mua") || msgLower.Contains("top"))
                {
                    contextForAI += "[TOP BÁN CHẠY TỪ DB THỰC TẾ]:\n";

                    // Khởi tạo Query lấy toàn bộ hóa đơn thành công
                    var query = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                                           .Where(ct => ct.DonHang.TrangThaiDonHang == 3);

                    // ================================================================
                    // BỘ LỌC TỪ KHÓA SIÊU CHUẨN DỰA THEO DANH MỤC THỰC TẾ CỦA SHOP
                    // ================================================================
                    string tuKhoa = "";

                    // Nhóm Áo
                    if (msgLower.Contains("sơ mi")) tuKhoa = "sơ mi";
                    else if (msgLower.Contains("phông") || msgLower.Contains("thun")) tuKhoa = "phông";
                    else if (msgLower.Contains("blazer") || msgLower.Contains("vest")) tuKhoa = "blazer";
                    else if (msgLower.Contains("khoác") || msgLower.Contains("bomber")) tuKhoa = "khoác";
                    else if (msgLower.Contains("polo")) tuKhoa = "polo";
                    else if (msgLower.Contains("sweater") || msgLower.Contains("nỉ") || msgLower.Contains("len")) tuKhoa = "sweater";
                    else if (msgLower.Contains("tanktop") || msgLower.Contains("ba lỗ")) tuKhoa = "tanktop";

                    // Nhóm Quần
                    else if (msgLower.Contains("quần âu") || msgLower.Contains("quần tây")) tuKhoa = "quần âu";
                    else if (msgLower.Contains("jean") || msgLower.Contains("bò")) tuKhoa = "jean";
                    else if (msgLower.Contains("kaki")) tuKhoa = "kaki";
                    else if (msgLower.Contains("short") || msgLower.Contains("đùi") || msgLower.Contains("ngắn")) tuKhoa = "short";

                    // Phụ kiện
                    else if (msgLower.Contains("balo") || msgLower.Contains("cặp") || msgLower.Contains("túi")) tuKhoa = "balo";
                    else if (msgLower.Contains("lót") || msgLower.Contains("sịp")) tuKhoa = "lót";
                    else if (msgLower.Contains("thắt lưng") || msgLower.Contains("dây nịt")) tuKhoa = "thắt lưng";
                    else if (msgLower.Contains("giày") || msgLower.Contains("dép")) tuKhoa = "giày";

                    // Phân loại chung (Nếu khách chỉ nói "áo bán chạy", "quần bán chạy")
                    else if (msgLower.Contains("áo")) tuKhoa = "áo";
                    else if (msgLower.Contains("quần")) tuKhoa = "quần";
                    else if (msgLower.Contains("phụ kiện")) tuKhoa = "phụ kiện";

                    // Nếu phát hiện khách muốn tìm loại cụ thể, ÉP Entity Framework PHẢI LỌC TRƯỚC!
                    if (!string.IsNullOrEmpty(tuKhoa))
                    {
                        // Lọc theo Tên Sản Phẩm (Vì tên thường chứa từ khóa như 'Áo Sơ Mi...')
                        query = query.Where(ct => ct.BienTheSanPham.SanPham.TenSanPham.ToLower().Contains(tuKhoa));

                        // Ghi chú: Nếu bảng SanPham của em có khóa ngoại đến DanhMuc, em có thể làm xịn hơn bằng cách dùng:
                        // query = query.Where(ct => ct.BienTheSanPham.SanPham.TenSanPham.ToLower().Contains(tuKhoa) || ct.BienTheSanPham.SanPham.DanhMuc.TenDanhMuc.ToLower().Contains(tuKhoa));
                    }

                    // Sau khi LỌC xong, mới bắt đầu ĐẾM ĐƠN HÀNG
                    var topSellingStats = query
                        .GroupBy(ct => ct.BienTheSanPham.SanPhamID)
                        .Select(group => new
                        {
                            SanPhamID = group.Key,
                            TotalSold = group.Sum(ct => ct.SoLuong),
                            LastOrderDate = group.Max(ct => ct.DonHang.NgayDat)
                        })
                        .OrderByDescending(x => x.TotalSold)
                        .ThenByDescending(x => x.LastOrderDate)
                        .Take(5)
                        .ToList();

                    if (topSellingStats.Any())
                    {
                        var productIds = topSellingStats.Select(x => x.SanPhamID).ToList();
                        var products = _unitOfWork.Repository<SanPham>()
                            .GetMany(p => productIds.Contains(p.ID) && p.TrangThai == 1)
                            .ToList();

                        foreach (var stat in topSellingStats)
                        {
                            var sp = products.FirstOrDefault(p => p.ID == stat.SanPhamID);
                            if (sp != null)
                            {
                                // Truyền nguyên liệu chuẩn xác cho AI
                                contextForAI += $"- [ID: {sp.ID}] Tên: {sp.TenSanPham}, Giá: {sp.GiaBan:N0} VNĐ, Đã bán: {stat.TotalSold} sản phẩm.\n";
                            }
                        }
                    }
                    else
                    {
                        // Xử lý đúng trường hợp mảng ngách (Edge case)
                        contextForAI += $"Dạ hiện tại nhóm hàng '{tuKhoa}' chưa có sản phẩm nào lọt top bán chạy hoặc đang tạm hết hàng ạ.\n";
                    }
                }
                else
                {
                    // Nếu khách hỏi bình thường (tìm đồ đi tiệc, tư vấn phối đồ...), dùng Pinecone Vector Search
                    var questionVector = await cohere.GetEmbeddingAsync(userMessage);
                    List<string> top3ProductIds = await pinecone.SearchVectorAsync(questionVector, 3);

                    if (top3ProductIds.Count > 0)
                    {
                        var allProductsText = aiDataService.ExtractDataForAI();
                        foreach (var idStr in top3ProductIds)
                        {
                            var productInfo = allProductsText.FirstOrDefault(p => p.Contains($"[ID: {idStr}]"));
                            if (productInfo != null) contextForAI += productInfo + "\n";
                        }
                    }
                }

                // 3. Truyền cho Gemini xử lý
                string botReply = await gemini.ChatAsync(userMessage, contextForAI, chatHistory);

                return Json(new { success = true, reply = botReply });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, reply = "Dạ, hệ thống đang bận. Lỗi: " + ex.Message });
            }
        }




        // --- HÀM HỖ TRỢ AI TRACKING ---
        private string GetOrSetGuestSessionId()
        {
            string cookieName = "AI_Guest_SessionID";
            if (Request.Cookies[cookieName] != null) return Request.Cookies[cookieName].Value;
            string newSessionId = Guid.NewGuid().ToString();
            HttpCookie cookie = new HttpCookie(cookieName, newSessionId) { Expires = DateTime.Now.AddDays(30) };
            Response.Cookies.Add(cookie);
            return newSessionId;
        }

    }
}