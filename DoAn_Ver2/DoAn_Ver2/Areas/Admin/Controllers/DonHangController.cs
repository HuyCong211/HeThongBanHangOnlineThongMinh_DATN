using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Models;
using PagedList;
using System.Net;        // [MỚI] Để gửi mail
using System.Net.Mail;   // [MỚI] Để gửi mail
using System.Configuration;


namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class DonHangController : BaseController
    {
        // GET: Admin/DonHang
        public ActionResult Index(string searchString, int? trangThai, string fromDate, string toDate, int? page)
        {
            var query = _unitOfWork.Repository<DonHang>().GetAll();

            // 1. Tìm kiếm theo Mã đơn hoặc Tên khách
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(x => x.MaDonHang.Contains(searchString) || x.TenNguoiNhan.Contains(searchString));
            }

            // 2. Lọc theo trạng thái
            if (trangThai.HasValue)
            {
                query = query.Where(x => x.TrangThaiDonHang == trangThai);
            }

            // 3. Lọc theo khoảng thời gian (Ngày bắt đầu - Ngày kết thúc)
            if (!string.IsNullOrEmpty(fromDate))
            {
                DateTime dtFrom;
                if (DateTime.TryParse(fromDate, out dtFrom))
                {
                    query = query.Where(x => x.NgayDat >= dtFrom);
                }
            }

            if (!string.IsNullOrEmpty(toDate))
            {
                DateTime dtTo;
                if (DateTime.TryParse(toDate, out dtTo))
                {
                    // Lấy đến cuối ngày (23:59:59) của ngày kết thúc
                    dtTo = dtTo.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.NgayDat <= dtTo);
                }
            }

            // 4. Sắp xếp: Mới nhất lên đầu
            query = query.OrderByDescending(x => x.NgayDat);

            // 5. Phân trang
            int pageSize = 10;
            int pageNumber = (page ?? 1);

            // Lưu giữ trạng thái bộ lọc để hiển thị lại trên View & Pagination
            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentStatus = trangThai;
            ViewBag.CurrentFromDate = fromDate;
            ViewBag.CurrentToDate = toDate;

            return View(query.ToPagedList(pageNumber, pageSize));
        }

        // 2. CHI TIẾT ĐƠN HÀNG (HÓA ĐƠN)
        public ActionResult Details(int id)
        {
            var donHang = _unitOfWork.Repository<DonHang>().GetById(id);
            if (donHang == null) return HttpNotFound();

            // Lấy chi tiết đơn hàng
            var chiTiet = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == id).ToList();
            ViewBag.ChiTiet = chiTiet;

            // Lấy thông tin SKU và Sản phẩm để hiển thị tên
            // (Thực tế nên dùng ViewModel hoặc Include, ở đây query thủ công cho đơn giản với repo pattern hiện tại)
            var listSKU = _unitOfWork.Repository<BienTheSanPham>().GetAll().ToList();
            var listSP = _unitOfWork.Repository<SanPham>().GetAll().ToList();
            var listMau = _unitOfWork.Repository<MauSac>().GetAll().ToList();
            var listSize = _unitOfWork.Repository<KichThuoc>().GetAll().ToList();
            var listAnh = _unitOfWork.Repository<AnhSanPham>().GetAll().ToList();

            ViewBag.ListSKU = listSKU;
            ViewBag.ListSP = listSP;
            ViewBag.ListMau = listMau;
            ViewBag.ListSize = listSize;
            ViewBag.ListAnh = listAnh;

            return View(donHang);
        }

        // 3. CẬP NHẬT TRẠNG THÁI (POST)
        [HttpPost]
        public JsonResult UpdateStatus(int id, int trangThai, string lyDo = "")
        {
            try
            {
                var donHang = _unitOfWork.Repository<DonHang>().GetById(id);
                if (donHang == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

                int trangThaiCu = donHang.TrangThaiDonHang ?? 0;
                if (trangThaiCu == trangThai) return Json(new { success = true });

                var chiTietDH = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == id).ToList();

                // --- LOGIC 1: DUYỆT ĐƠN (Trừ kho tạm giữ) ---
                if ((trangThaiCu == 0) && (trangThai == 1 || trangThai == 2))
                {
                    foreach (var item in chiTietDH)
                    {
                        var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(item.BienTheSanPhamID);
                        if (sku != null)
                        {
                            sku.SoLuongTamGiu = (sku.SoLuongTamGiu ?? 0) - item.SoLuong;
                            if (sku.SoLuongTamGiu < 0) sku.SoLuongTamGiu = 0;
                            _unitOfWork.Repository<BienTheSanPham>().Update(sku);
                        }
                    }
                }

                // --- LOGIC 2: HỦY ĐƠN (Hoàn kho + Gửi Mail) ---
                if (trangThai == 4 && trangThaiCu != 3)
                {
                    // A. Hoàn kho
                    foreach (var item in chiTietDH)
                    {
                        var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(item.BienTheSanPhamID);
                        if (sku != null)
                        {
                            sku.SoLuong = (sku.SoLuong ?? 0) + item.SoLuong; // Cộng lại kho thực

                            if (trangThaiCu == 0) // Nếu chưa duyệt thì trừ luôn kho tạm giữ
                            {
                                sku.SoLuongTamGiu = (sku.SoLuongTamGiu ?? 0) - item.SoLuong;
                                if (sku.SoLuongTamGiu < 0) sku.SoLuongTamGiu = 0;
                            }
                            _unitOfWork.Repository<BienTheSanPham>().Update(sku);

                            // 3. GHI LỊCH SỬ KHO (GHI NHẬN HÀNG HOÀN VỀ)
                            var ls = new LichSuKho()
                            {
                                BienTheSanPhamID = sku.ID,
                                SoLuongBienDong = item.SoLuong, // Số dương thể hiện nhập lại
                                TonThucTeSauBienDong = sku.SoLuong,
                                LoaiGiaoDich = "Admin hủy đơn",
                                MaThamChieu = "HUY-DH-" + donHang.MaDonHang,
                                NgayGhi = DateTime.Now
                            };
                            _unitOfWork.Repository<LichSuKho>().Add(ls);
                        }
                    }

                    // B. Gửi Email thông báo hủy
                    SendCancellationEmail(donHang, lyDo);

                    // C. (Tùy chọn) Lưu lý do hủy vào ghi chú đơn hàng để Admin sau này nhớ
                    if (!string.IsNullOrEmpty(lyDo))
                    {
                        donHang.DiaChiGiaoHang += $" . (Lý do hủy: {lyDo})";
                    }
                }

                // Cập nhật trạng thái
                donHang.TrangThaiDonHang = trangThai;
                _unitOfWork.Repository<DonHang>().Update(donHang);
                _unitOfWork.Save();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // --- HELPER: GỬI MAIL HỦY ---
        private void SendCancellationEmail(DonHang dh, string lyDo)
        {
            try
            {
                // Chỉ gửi nếu có email
                if (string.IsNullOrEmpty(dh.EmailNguoiNhan)) return;

                string host = ConfigurationManager.AppSettings["EmailHost"];
                int port = int.Parse(ConfigurationManager.AppSettings["EmailPort"]);
                string fromEmail = ConfigurationManager.AppSettings["EmailUserName"];
                string password = ConfigurationManager.AppSettings["EmailPassword"];
                string fromName = ConfigurationManager.AppSettings["EmailFromName"];

                var message = new MailMessage();
                message.From = new MailAddress(fromEmail, fromName);
                message.To.Add(new MailAddress(dh.EmailNguoiNhan));
                message.Subject = $"[Men Store] Thông báo HỦY đơn hàng #{dh.MaDonHang}";
                message.IsBodyHtml = true;

                message.Body = $@"
                    <div style='font-family:Arial, sans-serif; padding:20px; border:1px solid #ddd;'>
                        <h2 style='color:red'>Đơn hàng của bạn đã bị hủy</h2>
                        <p>Xin chào <strong>{dh.TenNguoiNhan}</strong>,</p>
                        <p>Rất tiếc, đơn hàng <strong>#{dh.MaDonHang}</strong> của bạn đã bị hủy với lý do:</p>
                        <div style='background:#f8d7da; color:#721c24; padding:15px; margin: 10px 0; border-radius:5px;'>
                            <strong>{lyDo}</strong>
                        </div>
                        <p>Nếu bạn đã thanh toán online (VNPay), hệ thống sẽ tiến hành hoàn tiền trong vòng 3-5 ngày làm việc.</p>
                        <p>Chúng tôi thành thật xin lỗi vì sự bất tiện này.</p>
                        <hr/>
                        <p>Mọi thắc mắc xin liên hệ hotline: 1900 xxxx.</p>
                    </div>";

                using (var client = new SmtpClient(host, port))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(fromEmail, password);
                    client.Send(message);
                }
            }
            catch (Exception ex)
            {
                // Chỉ ghi log, không throw lỗi để tránh crash luồng UpdateStatus chính
                System.Diagnostics.Debug.WriteLine("Lỗi gửi mail hủy: " + ex.Message);
            }
        }

        // ==============================================================
        // XUẤT HÓA ĐƠN PDF (IN HÓA ĐƠN)
        // ==============================================================
        public ActionResult PrintInvoice(int id)
        {
            var donHang = _unitOfWork.Repository<DonHang>().GetById(id);
            if (donHang == null) return HttpNotFound();

            // 1. Lấy chi tiết đơn hàng
            ViewBag.ChiTiet = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == id).ToList();

            // 2. LẤY THÔNG TIN NGƯỜI BÁN (Lấy đúng Session["UserAdmin"] theo code AuthController của bạn)
            if (Session["UserAdmin"] != null)
            {
                var currentUser = Session["UserAdmin"] as NguoiDung;
                ViewBag.SellerName = currentUser.HoTen ?? currentUser.TenDangNhap; // Nếu chưa có Họ tên thì lấy Tên đăng nhập
                ViewBag.SellerPhone = string.IsNullOrEmpty(currentUser.SDT) ? "Không có" : currentUser.SDT;
                ViewBag.SellerEmail = string.IsNullOrEmpty(currentUser.Email) ? "Không có" : currentUser.Email;
            }
            else
            {
                // Phòng trường hợp session hết hạn nhưng vẫn mở tab hóa đơn
                ViewBag.SellerName = "Nhân viên Men Store";
                ViewBag.SellerPhone = "---";
                ViewBag.SellerEmail = "---";
            }

            // 3. Lấy Data cho Sản Phẩm (SKU)
            ViewBag.ListSKU = _unitOfWork.Repository<BienTheSanPham>().GetAll().ToList();
            ViewBag.ListSP = _unitOfWork.Repository<SanPham>().GetAll().ToList();
            ViewBag.ListMau = _unitOfWork.Repository<MauSac>().GetAll().ToList();
            ViewBag.ListSize = _unitOfWork.Repository<KichThuoc>().GetAll().ToList();

            // 4. THÔNG TIN CỬA HÀNG (Lấy từ Cấu hình chung)
            var configs = _unitOfWork.Repository<CauHinhChung>().GetAll().ToList();
            ViewBag.Logo = configs.FirstOrDefault(x => x.KeyName == "SiteLogo")?.Value ?? "/Content/images/logo.png";
            ViewBag.Hotline = configs.FirstOrDefault(x => x.KeyName == "Hotline")?.Value ?? "1900 xxxx";
            ViewBag.Email = configs.FirstOrDefault(x => x.KeyName == "Email")?.Value ?? "contact@menstore.com";
            ViewBag.Address = configs.FirstOrDefault(x => x.KeyName == "FooterInfo")?.Value ?? "Hà Nội";

            // 5. Đổi tổng tiền ra chữ
            ViewBag.TienBangChu = NumberToWords(donHang.TongThanhToan ?? 0) + " đồng.";

            return View(donHang);
        }

        // Hàm hỗ trợ đổi số thành chữ
        private string NumberToWords(decimal number)
        {
            if (number == 0) return "Không";
            string[] units = { "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ" };
            string[] digits = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            string sNumber = number.ToString("0");
            string words = "";
            int unitIndex = 0;
            while (sNumber.Length > 0)
            {
                string chunk = sNumber.Length > 3 ? sNumber.Substring(sNumber.Length - 3) : sNumber;
                sNumber = sNumber.Length > 3 ? sNumber.Substring(0, sNumber.Length - 3) : "";
                int num = int.Parse(chunk);
                if (num > 0)
                {
                    string chunkWords = "";
                    int tram = num / 100, chuc = (num % 100) / 10, donVi = num % 10;
                    if (tram > 0 || (sNumber.Length > 0 && num > 0)) chunkWords += digits[tram] + " trăm ";
                    if (chuc > 1) chunkWords += digits[chuc] + " mươi ";
                    else if (chuc == 1) chunkWords += "mười ";
                    else if (chuc == 0 && donVi > 0 && tram > 0) chunkWords += "lẻ ";
                    if (donVi > 0)
                    {
                        if (chuc > 1 && donVi == 1) chunkWords += "mốt ";
                        else if (chuc > 0 && donVi == 5) chunkWords += "lăm ";
                        else chunkWords += digits[donVi] + " ";
                    }
                    words = chunkWords + units[unitIndex] + " " + words;
                }
                unitIndex++;
            }
            words = words.Trim();
            return char.ToUpper(words[0]) + words.Substring(1);
        }

    }
}