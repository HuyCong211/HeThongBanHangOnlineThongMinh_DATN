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
    }
}