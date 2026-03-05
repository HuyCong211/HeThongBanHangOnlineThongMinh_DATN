using DoAn_Ver2.Common;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using DoAn_Ver2.Models.ViewModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Payment;

namespace DoAn_Ver2.Controllers
{
    public class OrderController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();

        // --- HELPER: LẤY CHI TIẾT KÈM ẢNH & TÊN ---
        private List<OrderDetailDisplayVM> GetOrderDetailsWithInfo(int orderId)
        {
            // 1. Lấy thông tin đơn hàng để check trạng thái
            var order = _unitOfWork.Repository<DonHang>().GetById(orderId);
            bool isCompleted = (order != null && order.TrangThaiDonHang == 3); // 3 = Hoàn thành

            // 2. Lấy danh sách chi tiết đơn hàng
            var rawDetails = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == orderId).ToList();
            var resultList = new List<OrderDetailDisplayVM>();

            foreach (var item in rawDetails)
            {
                var vm = new OrderDetailDisplayVM
                {
                    SoLuong = item.SoLuong ?? 0,
                    DonGia = item.DonGia ?? 0,
                    ThanhTien = item.ThanhTien ?? 0,
                    CanReview = isCompleted // [MỚI] Truyền trạng thái cho View biết
                };

                // 3. Lấy thông tin biến thể (SKU)
                var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(item.BienTheSanPhamID);
                if (sku != null)
                {
                    var sp = _unitOfWork.Repository<SanPham>().GetById(sku.SanPhamID);
                    if (sp != null)
                    {
                        vm.TenSanPham = sp.TenSanPham;
                        vm.SanPhamID = sp.ID; // [QUAN TRỌNG] Cần ID để link sang trang đánh giá
                    }

                    var mau = _unitOfWork.Repository<MauSac>().GetById(sku.MauSacID);
                    var size = _unitOfWork.Repository<KichThuoc>().GetById(sku.KichThuocID);
                    vm.TenPhanLoai = $"{mau?.TenMau ?? "N/A"} / {size?.TenSize ?? "N/A"}";

                    var anh = _unitOfWork.Repository<AnhSanPham>()
                        .GetMany(x => x.SanPhamID == sku.SanPhamID && x.MauSacID == sku.MauSacID)
                        .FirstOrDefault();

                    if (anh == null)
                    {
                        anh = _unitOfWork.Repository<AnhSanPham>()
                            .GetMany(x => x.SanPhamID == sku.SanPhamID && x.MacDinh == true)
                            .FirstOrDefault();
                    }

                    vm.HinhAnh = anh != null ? anh.URL : "/Content/images/no-image.png";
                }
                else
                {
                    vm.TenSanPham = "Sản phẩm không còn tồn tại";
                    vm.HinhAnh = "/Content/images/no-image.png";
                    vm.CanReview = false; // Không tồn tại thì không đánh giá được
                }

                resultList.Add(vm);
            }
            return resultList;
        }

        // 1. TRANG TRA CỨU / LỊCH SỬ ĐƠN HÀNG (Dùng chung cho cả Guest và Member)
        [HttpGet]
        public ActionResult Index(string orderCode)
        {
            var user = Session["KhachHang"] as NguoiDung;

            // TRƯỜNG HỢP 1: ĐÃ ĐĂNG NHẬP -> Hiện danh sách đơn hàng của họ
            if (user != null)
            {
                var listOrders = _unitOfWork.Repository<DonHang>()
                                    .GetMany(x => x.NguoiDungID == user.ID)
                                    .OrderByDescending(x => x.NgayDat)
                                    .ToList();
                ViewBag.IsLoggedIn = true;
                return View(listOrders);
            }

            // TRƯỜNG HỢP 2: CHƯA ĐĂNG NHẬP (GUEST)
            ViewBag.IsLoggedIn = false;

            // Nếu có mã đơn hàng (Do người dùng nhập tìm kiếm)
            if (!string.IsNullOrEmpty(orderCode))
            {
                var order = _unitOfWork.Repository<DonHang>().GetMany(x => x.MaDonHang == orderCode).FirstOrDefault();
                if (order != null)
                {
                    // Trả về danh sách chứa 1 đơn hàng tìm được để tái sử dụng View
                    return View(new List<DonHang> { order });
                }
                else
                {
                    ViewBag.Error = "Không tìm thấy đơn hàng nào với mã này!";
                    return View(new List<DonHang>()); // Trả list rỗng
                }
            }

            // Mặc định chưa tìm gì -> Trả list rỗng
            return View(new List<DonHang>());
        }

        // 2. XEM CHI TIẾT ĐƠN HÀNG (Dùng cho Popup hoặc trang riêng)
        public ActionResult Detail(int id)
        {
            var order = _unitOfWork.Repository<DonHang>().GetById(id);
            if (order == null) return HttpNotFound();

            // [SỬA ĐỔI]: Dùng hàm Helper ở trên để lấy dữ liệu đầy đủ
            var detailsVM = GetOrderDetailsWithInfo(id);

            // Truyền ViewModel mới sang View qua ViewBag
            ViewBag.OrderDetails = detailsVM;

            return PartialView("_OrderDetailPartial", order);
        }

        // 3. TRANG TRA CỨU CHO GUEST (Action Tracking cũ của bạn)
        [HttpGet]
        public ActionResult Tracking(string id)
        {
            if (string.IsNullOrEmpty(id)) return View();

            var order = _unitOfWork.Repository<DonHang>().GetMany(x => x.MaDonHang == id).FirstOrDefault();

            if (order == null)
            {
                ViewBag.Error = "Không tìm thấy đơn hàng.";
                return View();
            }

            // [SỬA ĐỔI]: Dùng hàm Helper để lấy List<OrderDetailDisplayVM>
            ViewBag.ChiTiet = GetOrderDetailsWithInfo(order.ID);

            return View(order);
        }


        // =========================================================
        // CÁC CHỨC NĂNG DÀNH CHO MEMBER (GỌI TỪ PROFILE)
        // =========================================================

        // 2. HỦY ĐƠN HÀNG
        [HttpPost]
        public ActionResult CancelOrder(int id, string lyDo)
        {
            // Check đăng nhập
            var userSession = Session["KhachHang"] as NguoiDung;
            if (userSession == null) return Json(new { success = false, msg = "Vui lòng đăng nhập." });

            var order = _unitOfWork.Repository<DonHang>().GetById(id);

            // Validate quyền sở hữu
            if (order == null || order.NguoiDungID != userSession.ID)
                return Json(new { success = false, msg = "Đơn hàng không hợp lệ." });

            // Validate trạng thái (Chỉ cho hủy 0: Chờ xác nhận, 1: Đã xác nhận)
            // Giả sử: 0=Mới, 1=Đã xác nhận, 2=Đang giao, 3=Hoàn thành, 4=Hủy
            if (order.TrangThaiDonHang != 0 && order.TrangThaiDonHang != 1)
                return Json(new { success = false, msg = "Đơn hàng đang giao hoặc đã hoàn thành, không thể hủy." });

            // Cập nhật trạng thái
            order.TrangThaiDonHang = 4; // 4 là Hủy
            order.LyDoHuy = lyDo; // Cần đảm bảo DB có cột này, nếu không thì bỏ qua

            // Hoàn lại kho (QUAN TRỌNG)
            var chiTiet = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == id).ToList();
            foreach (var item in chiTiet)
            {
                var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(item.BienTheSanPhamID);
                if (sku != null)
                {
                    sku.SoLuong += item.SoLuong; // Cộng lại số lượng thực
                    sku.SoLuongTamGiu -= item.SoLuong; // Trừ số lượng tạm giữ
                    _unitOfWork.Repository<BienTheSanPham>().Update(sku);
                    // 3. GHI LỊCH SỬ KHO (Thêm đoạn này)
                    var ls = new LichSuKho()
                    {
                        BienTheSanPhamID = sku.ID,
                        SoLuongBienDong = item.SoLuong, // Số dương = Nhập lại
                        TonThucTeSauBienDong = sku.SoLuong,
                        LoaiGiaoDich = "Khách hủy đơn",
                        MaThamChieu = "HUY-DH-" + order.MaDonHang,
                        NgayGhi = DateTime.Now
                    };
                    _unitOfWork.Repository<LichSuKho>().Add(ls);
                }
            }

            _unitOfWork.Repository<DonHang>().Update(order);
            _unitOfWork.Save();

            // --- [CODE GỬI MAIL ADMIN ĐÃ ĐƯỢC THÊM VÀO] ---
            try
            {
                // 1. Lấy cấu hình email
                string host = ConfigurationManager.AppSettings["EmailHost"];
                int port = int.Parse(ConfigurationManager.AppSettings["EmailPort"]);
                string fromEmail = ConfigurationManager.AppSettings["EmailUserName"];
                string password = ConfigurationManager.AppSettings["EmailPassword"];
                string fromName = ConfigurationManager.AppSettings["EmailFromName"];

                // Email người nhận là chính email cấu hình (Gửi về cho Admin)
                // Hoặc bạn có thể tạo 1 key "AdminEmail" riêng trong Web.config
                string adminEmail = fromEmail;

                // 2. Tạo nội dung email
                var message = new MailMessage();
                message.From = new MailAddress(fromEmail, fromName);
                message.To.Add(new MailAddress(adminEmail));
                message.Subject = $"[Cảnh báo HỦY ĐƠN] Khách hàng hủy đơn #{order.MaDonHang}";
                message.IsBodyHtml = true;
                message.Body = $@"
                    <div style='border: 1px solid #ddd; padding: 15px; font-family: Arial;'>
                        <h3 style='color: red;'>Thông báo hủy đơn hàng</h3>
                        <p>Khách hàng <strong>{userSession.HoTen}</strong> vừa thực hiện hủy đơn hàng.</p>
                        <hr />
                        <ul>
                            <li><strong>Mã đơn hàng:</strong> {order.MaDonHang}</li>
                            <li><strong>Ngày đặt:</strong> {order.NgayDat:dd/MM/yyyy HH:mm}</li>
                            <li><strong>Tổng tiền:</strong> {order.TongThanhToan:N0} đ</li>
                            <li><strong>Lý do hủy:</strong> <span style='color:red; font-weight:bold'>{lyDo}</span></li>
                        </ul>
                        <p>Vui lòng truy cập trang quản trị để kiểm tra kho hàng.</p>
                    </div>";

                // 3. Gửi mail
                using (var client = new SmtpClient(host, port))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(fromEmail, password);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Send(message);
                }
            }
            catch (Exception ex)
            {
                // Nếu gửi mail lỗi thì ghi log, KHÔNG ĐƯỢC return false 
                // vì đơn hàng đã hủy thành công trong DB rồi.
                System.Diagnostics.Debug.WriteLine("Lỗi gửi mail admin: " + ex.Message);
            }
            // ------------------------------------------------
            return Json(new { success = true, msg = "Đã hủy đơn hàng thành công." });
        }

        // 3. MUA LẠI (RE-ORDER)
        [HttpPost]
        public ActionResult ReOrder(int id)
        {
            var userSession = Session["KhachHang"] as NguoiDung;
            if (userSession == null) return Json(new { success = false, msg = "Vui lòng đăng nhập." });

            var oldDetails = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == id).ToList();
            if (oldDetails.Count == 0) return Json(new { success = false, msg = "Đơn hàng lỗi." });

            // Lấy giỏ hàng hiện tại của user (Từ DB)
            var currentCart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == userSession.ID).FirstOrDefault();
            if (currentCart == null)
            {
                currentCart = new GioHang { NguoiDungID = userSession.ID, NgayTao = DateTime.Now };
                _unitOfWork.Repository<GioHang>().Add(currentCart);
                _unitOfWork.Save();
            }

            int countAdded = 0;
            foreach (var item in oldDetails)
            {
                // Kiểm tra sản phẩm còn tồn tại và còn hàng không
                var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(item.BienTheSanPhamID);
                if (sku != null && sku.SoLuong > 0)
                {
                    // Kiểm tra xem trong giỏ đã có chưa
                    var cartItem = _unitOfWork.Repository<GioHangChiTiet>()
                        .GetMany(x => x.GioHangID == currentCart.ID && x.BienTheSanPhamID == sku.ID)
                        .FirstOrDefault();

                    if (cartItem != null)
                    {
                        cartItem.SoLuong += item.SoLuong;
                        _unitOfWork.Repository<GioHangChiTiet>().Update(cartItem);
                    }
                    else
                    {
                        _unitOfWork.Repository<GioHangChiTiet>().Add(new GioHangChiTiet
                        {
                            GioHangID = currentCart.ID,
                            BienTheSanPhamID = sku.ID,
                            SoLuong = item.SoLuong
                        });
                    }
                    countAdded++;
                }
            }

            _unitOfWork.Save();

            // Cập nhật lại Session CartCount để hiển thị trên Header
            var totalQty = _unitOfWork.Repository<GioHangChiTiet>().GetMany(x => x.GioHangID == currentCart.ID).Sum(x => x.SoLuong);
            Session["CartCount"] = totalQty;

            if (countAdded > 0)
                return Json(new { success = true, msg = "Đã thêm sản phẩm vào giỏ hàng." });
            else
                return Json(new { success = false, msg = "Sản phẩm trong đơn hàng này đã hết hàng hoặc ngừng kinh doanh." });
        }


        // =========================================================
        // TÍNH NĂNG: THANH TOÁN LẠI (RE-PAYMENT) CHO ĐƠN VNPAY
        // =========================================================

        [HttpPost] // Hoặc [HttpGet] tùy bạn, nhưng Post an toàn hơn
        public ActionResult RetryPayment(int id)
        {
            // 1. Kiểm tra đăng nhập
            var userSession = Session["KhachHang"] as NguoiDung;
            if (userSession == null) return Json(new { success = false, msg = "Vui lòng đăng nhập." });

            // 2. Lấy đơn hàng
            var order = _unitOfWork.Repository<DonHang>().GetById(id);

            // 3. Validate
            if (order == null || order.NguoiDungID != userSession.ID)
            {
                return Json(new { success = false, msg = "Đơn hàng không tồn tại." });
            }

            // Chỉ cho thanh toán lại nếu:
            // - Phương thức là VNPay (ID = 2)
            // - Chưa thanh toán (TrangThaiThanhToan = false)
            // - Đơn hàng chưa bị hủy (TrangThaiDonHang != 4) và chưa hoàn thành
            if (order.PhuongThucThanhToanID != 2) // Giả sử 2 là VNPay
            {
                return Json(new { success = false, msg = "Đơn hàng này không phải thanh toán qua VNPay." });
            }

            if (order.TrangThaiThanhToan == true)
            {
                return Json(new { success = false, msg = "Đơn hàng này đã được thanh toán rồi." });
            }

            if (order.TrangThaiDonHang == 4) // 4 là đã hủy
            {
                return Json(new { success = false, msg = "Đơn hàng đã bị hủy, vui lòng đặt lại đơn mới." });
            }

            // 4. Tạo URL thanh toán VNPay mới
            // Lưu ý: Số tiền phải lấy từ TongThanhToan (đã trừ khuyến mãi/cộng ship)
            // [QUAN TRỌNG] Tạo mã tham chiếu mới (Mã đơn + "_" + Số ngẫu nhiên)
            // Để VNPay hiểu đây là một giao dịch mới tinh
            string vnp_TxnRef = order.MaDonHang + "_" + DateTime.Now.Ticks.ToString();

            // Gọi hàm tạo URL với mã tham chiếu mới này
            string vnpUrl = GetVnPayUrl(vnp_TxnRef, (long)order.TongThanhToan);

            return Json(new { success = true, url = vnpUrl });
        }

        // --- HÀM HELPER TẠO URL VNPAY (Copy từ CartController sang) ---
        // Tốt nhất bạn nên để hàm này trong class Utils/Common để gọi chung 2 bên
        private string GetVnPayUrl(string vnp_TxnRef, long amount)
        {
            string vnp_Returnurl = ConfigurationManager.AppSettings["vnp_Returnurl"];
            string vnp_Url = ConfigurationManager.AppSettings["vnp_Url"];
            string vnp_TmnCode = ConfigurationManager.AppSettings["vnp_TmnCode"];
            string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];

            VnPayLibrary vnpay = new VnPayLibrary();
            vnpay.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", (amount * 100).ToString());

            // [QUAN TRỌNG] Ngày tạo phải là hiện tại
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));

            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress());
            vnpay.AddRequestData("vnp_Locale", "vn");

            // Info có thể để mã gốc cho dễ đọc
            var originalCode = vnp_TxnRef.Split('_')[0];
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang: " + originalCode);

            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);

            // [QUAN TRỌNG] Gửi mã đã nối đuôi sang VNPay
            vnpay.AddRequestData("vnp_TxnRef", vnp_TxnRef);

            return vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
        }

    }
}