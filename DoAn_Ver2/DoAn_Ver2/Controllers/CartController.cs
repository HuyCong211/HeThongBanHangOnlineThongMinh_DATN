using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using DoAn_Ver2.Models.ViewModel;
using DoAn_Ver2.Payment;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;

namespace DoAn_Ver2.Controllers
{
    public class CartController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Cart
        public ActionResult Index()
        {
            var model = new ShoppingCartViewModel();
            model.Items = GetCartItems(); // Lấy danh sách sản phẩm (từ DB hoặc Session)
            Session["CartCount"] = model.Items.Sum(x => x.SoLuong);
            return View(model);
        }
        // 2. THÊM VÀO GIỎ (AJAX)
        [HttpPost]
        public ActionResult AddToCart(int skuId, int quantity)
        {
            try
            {
                var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(skuId);
                if (sku == null) return Json(new { success = false, msg = "Sản phẩm không tồn tại!" });
                if ((sku.SoLuong ?? 0) < quantity) return Json(new { success = false, msg = "Không đủ hàng trong kho!" });

                if (Session["KhachHang"] != null)
                {
                    // --- MEMBER: LƯU DB ---
                    var user = (NguoiDung)Session["KhachHang"];
                    var cart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == user.ID).FirstOrDefault();

                    if (cart == null)
                    {
                        cart = new GioHang { NguoiDungID = user.ID, NgayTao = DateTime.Now };
                        _unitOfWork.Repository<GioHang>().Add(cart);
                        _unitOfWork.Save();
                    }

                    var cartItem = _unitOfWork.Repository<GioHangChiTiet>()
                                        .GetMany(x => x.GioHangID == cart.ID && x.BienTheSanPhamID == skuId)
                                        .FirstOrDefault();

                    if (cartItem != null)
                    {
                        cartItem.SoLuong = (cartItem.SoLuong ?? 0) + quantity;
                        _unitOfWork.Repository<GioHangChiTiet>().Update(cartItem);
                    }
                    else
                    {
                        var newItem = new GioHangChiTiet
                        {
                            GioHangID = cart.ID,
                            BienTheSanPhamID = skuId,
                            SoLuong = quantity
                        };
                        _unitOfWork.Repository<GioHangChiTiet>().Add(newItem);
                    }
                    _unitOfWork.Save();
                }
                else
                {
                    // --- GUEST: LƯU SESSION ---
                    // [QUAN TRỌNG] Khởi tạo list nếu Session null
                    List<CartItemViewModel> cart = Session["Cart"] as List<CartItemViewModel>;
                    if (cart == null) cart = new List<CartItemViewModel>();

                    var item = cart.FirstOrDefault(x => x.SKU_ID == skuId);
                    if (item != null)
                    {
                        item.SoLuong += quantity;
                    }
                    else
                    {
                        var sp = _unitOfWork.Repository<SanPham>().GetById(sku.SanPhamID);
                        var mau = _unitOfWork.Repository<MauSac>().GetById(sku.MauSacID);
                        var size = _unitOfWork.Repository<KichThuoc>().GetById(sku.KichThuocID);

                        // Lấy ảnh: Ưu tiên ảnh biến thể -> ảnh chính -> placeholder
                        var anh = _unitOfWork.Repository<AnhSanPham>()
                                    .GetMany(x => x.SanPhamID == sp.ID && x.MacDinh == true).FirstOrDefault();
                        string imgUrl = anh != null ? anh.URL : "https://via.placeholder.com/100";

                        cart.Add(new CartItemViewModel
                        {
                            SanPhamID = sp.ID,
                            SKU_ID = skuId,
                            TenSanPham = sp.TenSanPham,
                            TenPhanLoai = $"{mau.TenMau} / {size.TenSize}",
                            HinhAnh = imgUrl,
                            DonGia = sp.GiaBan ?? sp.GiaGoc ?? 0,
                            SoLuong = quantity,
                            TonKho = sku.SoLuong ?? 0
                        });
                    }
                    // [QUAN TRỌNG] Gán ngược lại vào Session
                    Session["Cart"] = cart;
                }

                // Cập nhật số lượng hiển thị
                Session["CartCount"] = GetCartCount();
                return Json(new { success = true, msg = "Đã thêm vào giỏ hàng!", count = Session["CartCount"] });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Lỗi server: " + ex.Message });
            }
        }

        // 3. CẬP NHẬT SỐ LƯỢNG (AJAX)
        [HttpPost]
        public ActionResult UpdateQuantity(int skuId, int quantity)
        {
            if (quantity < 1) return Json(new { success = false });

            var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(skuId);
            // [FIX LỖI]
            if ((sku.SoLuong ?? 0) < quantity) return Json(new { success = false, msg = "Số lượng vượt quá tồn kho!" });

            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                var cart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == user.ID).FirstOrDefault();
                if (cart != null)
                {
                    var item = _unitOfWork.Repository<GioHangChiTiet>()
                                .GetMany(x => x.GioHangID == cart.ID && x.BienTheSanPhamID == skuId).FirstOrDefault();
                    if (item != null)
                    {
                        item.SoLuong = quantity;
                        _unitOfWork.Repository<GioHangChiTiet>().Update(item);
                        _unitOfWork.Save();
                    }
                }
            }
            else
            {
                var cart = Session["Cart"] as List<CartItemViewModel>;
                var item = cart.FirstOrDefault(x => x.SKU_ID == skuId);
                if (item != null) item.SoLuong = quantity;
                Session["CartCount"] = GetCartCount();
            }

            var currentCart = GetCartItems();
            var updatedItem = currentCart.FirstOrDefault(x => x.SKU_ID == skuId);
            var totalCart = currentCart.Sum(x => x.ThanhTien);

            return Json(new
            {
                success = true,
                itemTotal = updatedItem?.ThanhTien.ToString("N0") + "đ",
                cartTotal = totalCart.ToString("N0") + "đ"
            });
        }

        // 4. XÓA SẢN PHẨM
        [HttpPost]
        public ActionResult Remove(int skuId)
        {
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                var cart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == user.ID).FirstOrDefault();
                if (cart != null)
                {
                    var item = _unitOfWork.Repository<GioHangChiTiet>()
                                .GetMany(x => x.GioHangID == cart.ID && x.BienTheSanPhamID == skuId).FirstOrDefault();
                    if (item != null)
                    {
                        _unitOfWork.Repository<GioHangChiTiet>().Delete(item);
                        _unitOfWork.Save();
                    }
                }
            }
            else
            {
                var cart = Session["Cart"] as List<CartItemViewModel>;
                var item = cart.FirstOrDefault(x => x.SKU_ID == skuId);
                if (item != null) cart.Remove(item);
                Session["CartCount"] = GetCartCount();
            }

            var currentCart = GetCartItems();
            return Json(new
            {
                success = true,
                cartTotal = currentCart.Sum(x => x.ThanhTien).ToString("N0") + "đ",
                count = currentCart.Count
            });
        }

        // --- HELPER FUNCTIONS (SỬA LẠI ĐỂ CHẮC CHẮN TRẢ VỀ LIST) ---
        private List<CartItemViewModel> GetCartItems()
        {
            var list = new List<CartItemViewModel>();

            if (Session["KhachHang"] != null)
            {
                // Lấy từ DB
                var user = (NguoiDung)Session["KhachHang"];
                var cart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == user.ID).FirstOrDefault();

                if (cart != null)
                {
                    var details = _unitOfWork.Repository<GioHangChiTiet>().GetMany(x => x.GioHangID == cart.ID).ToList();
                    foreach (var d in details)
                    {
                        var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(d.BienTheSanPhamID);
                        if (sku == null) continue;

                        var sp = _unitOfWork.Repository<SanPham>().GetById(sku.SanPhamID);
                        var mau = _unitOfWork.Repository<MauSac>().GetById(sku.MauSacID);
                        var size = _unitOfWork.Repository<KichThuoc>().GetById(sku.KichThuocID);
                        var anh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == sp.ID && x.MacDinh == true).FirstOrDefault();

                        list.Add(new CartItemViewModel
                        {
                            SanPhamID = sp.ID,
                            SKU_ID = d.BienTheSanPhamID,
                            TenSanPham = sp.TenSanPham,
                            TenPhanLoai = $"{mau.TenMau} / {size.TenSize}",
                            HinhAnh = anh != null ? anh.URL : "https://via.placeholder.com/100",
                            DonGia = sp.GiaBan ?? sp.GiaGoc ?? 0,
                            SoLuong = d.SoLuong ?? 1,
                            TonKho = sku.SoLuong ?? 0
                        });
                    }
                }
            }
            else
            {
                // Lấy từ Session
                var sessionCart = Session["Cart"] as List<CartItemViewModel>;
                if (sessionCart != null)
                {
                    list = sessionCart;
                }
            }
            return list;
        }

        private int GetCartCount()
        {
            var list = GetCartItems();
            return list.Sum(x => x.SoLuong);
        }



        // =========================================================================
        // PHẦN THANH TOÁN (CHECKOUT)
        // =========================================================================

        // 1. TRANG THANH TOÁN (GET)
        [HttpGet]
        public ActionResult Checkout(string selectedIds)
        {
            // [FIX LỖI] Giải mã URL nếu bị mã hóa (ví dụ %2C thành dấu phẩy)
            if (!string.IsNullOrEmpty(selectedIds))
            {
                selectedIds = HttpUtility.UrlDecode(selectedIds);
            }

            if (string.IsNullOrEmpty(selectedIds)) return RedirectToAction("Index");

            try
            {
                // [FIX LỖI] Dùng TryParse hoặc lọc bỏ chuỗi rỗng để tránh lỗi Format
                var ids = selectedIds.Split(',')
                                     .Where(x => !string.IsNullOrWhiteSpace(x) && int.TryParse(x, out _))
                                     .Select(int.Parse)
                                     .ToList();

                if (!ids.Any()) return RedirectToAction("Index");

                var fullCart = GetCartItems();
                var checkoutItems = fullCart.Where(x => ids.Contains(x.SKU_ID)).ToList();

                if (checkoutItems.Count == 0) return RedirectToAction("Index");

                // Check tồn kho
                foreach (var item in checkoutItems)
                {
                    var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(item.SKU_ID);
                    if (sku == null || (sku.SoLuong ?? 0) < item.SoLuong)
                    {
                        TempData["Error"] = $"Sản phẩm {item.TenSanPham} không đủ hàng!";
                        return RedirectToAction("Index");
                    }
                }

                decimal tongTienHang = checkoutItems.Sum(x => x.ThanhTien);

                var model = new CheckoutViewModel
                {
                    CartItems = checkoutItems,
                    TongTienHang = tongTienHang,
                    PhiShip = 30000
                };


                // --- [LOGIC VOUCHER MỚI] ---
                // Lấy các mã giảm giá thỏa mãn điều kiện:
                // 1. Chưa hết hạn (NgayHetHan >= Now)
                // 2. Còn số lượng (SoLuong > 0)
                // 3. Đơn tối thiểu <= Tổng tiền hàng hiện tại
                // --- [LOGIC VOUCHER ĐÃ SỬA] ---
                var today = DateTime.Now;
                var validVouchers = _unitOfWork.Repository<MaGiamGia>()
                    .GetMany(x => x.NgayHetHan >= today && x.SoLuong > 0 && x.DonToiThieu <= tongTienHang)
                    .ToList();

                // Sắp xếp: Ưu tiên giảm nhiều tiền nhất
                // Lưu ý: Cần tính ra số tiền giảm thực tế để so sánh
                model.DsVoucherKhaDung = validVouchers.OrderByDescending(v =>
                {
                    if (v.LoaiGiam == "PERCENT") // Khớp với DB: "PERCENT"
                        return (tongTienHang * (v.GiaTri ?? 0)) / 100;
                    else
                        return v.GiaTri ?? 0;
                }).ToList();
                // ---------------------------


                if (Session["KhachHang"] != null)
                {
                    var user = (NguoiDung)Session["KhachHang"];
                    model.SoDiaChi = _unitOfWork.Repository<DiaChi>().GetMany(x => x.NguoiDungID == user.ID).ToList();
                    model.TenNguoiNhan = user.HoTen;
                    model.SDT = user.SDT;
                    model.Email = user.Email;
                }

                ViewBag.SelectedIds = selectedIds; // Truyền lại chuỗi ID sạch sang View
                return View(model);
            }
            catch
            {
                return RedirectToAction("Index");
            }
        }

        // 2. XỬ LÝ ĐẶT HÀNG (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Checkout(CheckoutViewModel model, string selectedIds)
        {
            // [FIX LỖI] Tương tự như hàm GET
            if (!string.IsNullOrEmpty(selectedIds))
            {
                selectedIds = HttpUtility.UrlDecode(selectedIds);
            }

            var ids = new List<int>();
            if (!string.IsNullOrEmpty(selectedIds))
            {
                ids = selectedIds.Split(',')
                                 .Where(x => !string.IsNullOrWhiteSpace(x) && int.TryParse(x, out _))
                                 .Select(int.Parse)
                                 .ToList();
            }

            // Nếu không có ID nào hợp lệ -> lỗi
            if (!ids.Any())
            {
                // Xử lý lỗi, ví dụ quay về giỏ hàng
                return RedirectToAction("Index");
            }

            var fullCart = GetCartItems();
            model.CartItems = fullCart.Where(x => ids.Contains(x.SKU_ID)).ToList();
            model.TongTienHang = model.CartItems.Sum(x => x.ThanhTien);

            // --- [XỬ LÝ VOUCHER SERVER-SIDE] ---
            // --- [XỬ LÝ VOUCHER SERVER-SIDE] ---
            decimal tienGiam = 0;
            MaGiamGia appliedVoucher = null;

            if (!string.IsNullOrEmpty(model.MaVoucher))
            {
                var today = DateTime.Now;
                appliedVoucher = _unitOfWork.Repository<MaGiamGia>()
                    .GetMany(x => x.MaCode == model.MaVoucher && x.NgayHetHan >= today && x.SoLuong > 0)
                    .FirstOrDefault();

                if (appliedVoucher != null && appliedVoucher.DonToiThieu <= model.TongTienHang)
                {
                    // [SỬA QUAN TRỌNG]: Check đúng từ khóa "PERCENT" trong DB
                    if (appliedVoucher.LoaiGiam == "PERCENT")
                    {
                        // Công thức: Tổng tiền * Giá trị % / 100
                        tienGiam = (model.TongTienHang * (appliedVoucher.GiaTri ?? 0)) / 100;
                    }
                    else // Trường hợp "AMOUNT"
                    {
                        tienGiam = appliedVoucher.GiaTri ?? 0;
                    }

                    // Không giảm quá tổng tiền hàng
                    if (tienGiam > model.TongTienHang) tienGiam = model.TongTienHang;
                }
            }
            model.SoTienGiam = tienGiam;
            // ----------------------------------


            if (!ModelState.IsValid)
            {
                if (Session["KhachHang"] != null)
                {
                    var user = (NguoiDung)Session["KhachHang"];
                    model.SoDiaChi = _unitOfWork.Repository<DiaChi>().GetMany(x => x.NguoiDungID == user.ID).ToList();

                    // Load lại danh sách voucher để hiển thị lại
                    // Load lại Voucher
                    var today = DateTime.Now;
                    model.DsVoucherKhaDung = _unitOfWork.Repository<MaGiamGia>()
                        .GetMany(x => x.NgayHetHan >= today && x.SoLuong > 0 && x.DonToiThieu <= model.TongTienHang)
                        .OrderByDescending(v => v.LoaiGiam == "PERCENT" ? (model.TongTienHang * v.GiaTri / 100) : v.GiaTri)
                        .ToList();
                }
                ViewBag.SelectedIds = string.Join(",", ids);
                return View(model);
            }

            // --- [CẬP NHẬT: ĐỊA CHỈ 2 CẤP] ---
            // Tạo chuỗi địa chỉ: Chi tiết, Xã, Tỉnh (Bỏ Quận/Huyện)
            string diaChiFull = $"{model.DiaChiChiTiet}, {model.PhuongXa}, {model.TinhThanh}";

            if (!string.IsNullOrEmpty(model.GhiChu))
            {
                diaChiFull += $" . (Ghi chú: {model.GhiChu})";
            }
            // ----------------------

            // A. TẠO ĐƠN HÀNG
            var donHang = new DonHang
            {
                MaDonHang = "ORD" + DateTime.Now.ToString("yyMMddHHmmss"),
                NgayDat = DateTime.Now,
                TenNguoiNhan = model.TenNguoiNhan,
                SDT_NguoiNhan = model.SDT,
                EmailNguoiNhan = model.Email,
                DiaChiGiaoHang = diaChiFull,
                TongTien = model.TongTienHang,
                PhiVanChuyen = model.PhiShip,
                // [QUAN TRỌNG] Lưu giá trị giảm giá và tổng thanh toán thực tế
                GiamGia = tienGiam,
                TongThanhToan = (model.TongTienHang + model.PhiShip) - tienGiam,
                TrangThaiDonHang = 0,
                TrangThaiThanhToan = false,
                PhuongThucThanhToanID = model.PhuongThucThanhToan
            };

            // --- CẬP NHẬT: TỰ ĐỘNG LƯU ĐỊA CHỈ MỚI ---
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                donHang.NguoiDungID = user.ID;

                // LOGIC: Kiểm tra xem địa chỉ người dùng vừa nhập đã có trong sổ chưa
                try
                {
                    // Lấy tất cả địa chỉ của user này ra để so sánh
                    var listDiaChi = _unitOfWork.Repository<DiaChi>()
                                                .GetMany(d => d.NguoiDungID == user.ID)
                                                .ToList();

                    // So sánh từng trường (Tên, SĐT, Tỉnh, Xã, Chi tiết) - Không phân biệt hoa thường
                    bool isExist = listDiaChi.Any(d =>
                        (d.TenNguoiNhan ?? "").Trim().Equals(model.TenNguoiNhan.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        (d.SDT_NguoiNhan ?? "").Trim().Equals(model.SDT.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        (d.Tinh_ThanhPho ?? "").Trim().Equals(model.TinhThanh.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        (d.PhuongXa ?? "").Trim().Equals(model.PhuongXa.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        (d.DiaChiChiTiet ?? "").Trim().Equals(model.DiaChiChiTiet.Trim(), StringComparison.OrdinalIgnoreCase)
                    );

                    // Nếu chưa có -> Thêm mới vào DB
                    if (!isExist)
                    {
                        var newAddress = new DiaChi
                        {
                            NguoiDungID = user.ID,
                            TenNguoiNhan = model.TenNguoiNhan,
                            SDT_NguoiNhan = model.SDT,
                            Tinh_ThanhPho = model.TinhThanh,
                            PhuongXa = model.PhuongXa,
                            DiaChiChiTiet = model.DiaChiChiTiet,
                            MacDinh = false // Địa chỉ tự lưu thì không set mặc định
                        };
                        _unitOfWork.Repository<DiaChi>().Add(newAddress);

                        // Lưu ý: Không cần gọi Save() ở đây, vì bên dưới có lệnh Save() chung cho cả đơn hàng
                    }
                }
                catch
                {
                    // Nếu lỗi phần lưu địa chỉ thì bỏ qua, ưu tiên lưu đơn hàng thành công
                }
            }
            // ------------------------------------------

            _unitOfWork.Repository<DonHang>().Add(donHang);
            _unitOfWork.Save();

            // B. CẬP NHẬT SỐ LƯỢNG VOUCHER (NẾU CÓ DÙNG)
            if (appliedVoucher != null)
            {
                appliedVoucher.SoLuong = (appliedVoucher.SoLuong ?? 0) - 1;
                _unitOfWork.Repository<MaGiamGia>().Update(appliedVoucher);
                // Save ở cuối cùng chung 1 lần
            }


            // C. LƯU CHI TIẾT & TRỪ KHO
            foreach (var item in model.CartItems)
            {
                var ct = new ChiTietDonHang
                {
                    DonHangID = donHang.ID,
                    BienTheSanPhamID = item.SKU_ID,
                    SoLuong = item.SoLuong,
                    DonGia = item.DonGia,
                    ThanhTien = item.ThanhTien
                };
                _unitOfWork.Repository<ChiTietDonHang>().Add(ct);

                // Trừ kho
                var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(item.SKU_ID);
                if (sku != null)
                {
                    sku.SoLuong = (sku.SoLuong ?? 0) - item.SoLuong;
                    sku.SoLuongTamGiu = (sku.SoLuongTamGiu ?? 0) + item.SoLuong;
                    _unitOfWork.Repository<BienTheSanPham>().Update(sku);

                    // b. GHI LỊCH SỬ KHO (ĐÂY LÀ ĐOẠN BẠN THIẾU)
                    var history = new LichSuKho
                    {
                        BienTheSanPhamID = sku.ID,
                        SoLuongBienDong = -item.SoLuong, // Số âm thể hiện xuất kho
                        TonThucTeSauBienDong = sku.SoLuong,
                        LoaiGiaoDich = "Khách đặt hàng", // Ghi chú rõ ràng
                        MaThamChieu = "DH-" + donHang.MaDonHang, // Gắn mã đơn hàng để tra cứu
                        NgayGhi = DateTime.Now
                    };
                    _unitOfWork.Repository<LichSuKho>().Add(history);
                }
            }
            _unitOfWork.Save();

            // C. XÓA KHỎI GIỎ
            RemoveItemsFromCart(ids);

            // =========================================================
            // [PHẦN MỚI] KÍCH HOẠT AI: CẬP NHẬT GỢI Ý SAU KHI MUA HÀNG
            // =========================================================
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                // Chạy ngầm tính toán lại gợi ý "UserRec" và "CoBuy"
                Task.Run(() =>
                {
                    var aiService = new DoAn_Ver2.Services.RecommendationService();
                    // Tính lại gợi ý cho người dùng này
                    aiService.CalculateUserRecommendations(user.ID, null);

                    // (Tùy chọn) Tính lại "Mua cùng" cho các sản phẩm vừa mua
                    // foreach(var item in model.CartItems) aiService.UpdateBoughtTogether(item.SKU_ID);
                });
            }
            



            // D. THANH TOÁN
            if (model.PhuongThucThanhToan == 2) // VNPay
            {
                string url = GetVnPayUrl(donHang.MaDonHang, (long)donHang.TongThanhToan);
                return Redirect(url);
            }

            // [SỬA LẠI CHỖ NÀY]: Gọi Task.Run để gửi mail ngầm (Async)
            // Thay vì gọi SendOrderEmail(donHang) như cũ
            Task.Run(() => SendOrderEmailAsync(donHang.ID));
            return RedirectToAction("OrderSuccess");
        }

        // --- HÀM GỬI MAIL MỚI (CHẠY NGẦM) ---
        // Nội dung HTML bên trong vẫn giữ nguyên 100% như bạn yêu cầu
        private void SendOrderEmailAsync(int donHangId)
        {
            try
            {
                // Phải tạo UnitOfWork mới vì đang chạy ở Thread khác
                using (var uow = new UnitOfWork())
                {
                    var dh = uow.Repository<DonHang>().GetById(donHangId);
                    if (dh == null) return;

                    var chiTietDH = uow.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == donHangId).ToList();

                    // --- BẮT ĐẦU PHẦN COPY Y NGUYÊN TỪ CODE CŨ ---
                    string listProductHtml = "";
                    foreach (var item in chiTietDH)
                    {
                        var sku = uow.Repository<BienTheSanPham>().GetById(item.BienTheSanPhamID);
                        if (sku != null)
                        {
                            var sp = uow.Repository<SanPham>().GetById(sku.SanPhamID);
                            var mau = uow.Repository<MauSac>().GetById(sku.MauSacID);
                            var size = uow.Repository<KichThuoc>().GetById(sku.KichThuocID);

                            listProductHtml += $@"
                            <tr>
                                <td style='padding:8px; border-bottom:1px solid #ddd; vertical-align: top;'>
                                    <strong style='color:#333;'>{sp.TenSanPham}</strong>
                                    <br/>
                                    <span style='color:#666; font-size:12px;'>Phân loại: {mau.TenMau} / {size.TenSize}</span>
                                </td>
                                <td style='padding:8px; border-bottom:1px solid #ddd; text-align:center; vertical-align: top;'>x{item.SoLuong}</td>
                                <td style='padding:8px; border-bottom:1px solid #ddd; text-align:right; vertical-align: top; font-weight:bold;'>{item.ThanhTien:N0}đ</td>
                            </tr>";
                        }
                    }

                    string host = ConfigurationManager.AppSettings["EmailHost"];
                    int port = int.Parse(ConfigurationManager.AppSettings["EmailPort"]);
                    string fromEmail = ConfigurationManager.AppSettings["EmailUserName"];
                    string password = ConfigurationManager.AppSettings["EmailPassword"];
                    string fromName = ConfigurationManager.AppSettings["EmailFromName"];
                    string adminEmail = fromEmail;
                    string baseUrl = "https://localhost:44338";

                    // -----------------------------------------------------------
                    // A. GỬI CHO KHÁCH HÀNG (ĐÃ BỎ CỘT ẢNH)
                    // -----------------------------------------------------------

                    if (!string.IsNullOrEmpty(dh.EmailNguoiNhan))
                    {
                        var mailKhach = new MailMessage();
                        mailKhach.From = new MailAddress(fromEmail, fromName);
                        mailKhach.To.Add(new MailAddress(dh.EmailNguoiNhan));
                        mailKhach.Subject = $"[Men Store] Xác nhận đơn hàng #{dh.MaDonHang}";
                        mailKhach.IsBodyHtml = true;

                        string rowVoucher = "";
                        if (dh.GiamGia > 0)
                        {
                            rowVoucher = $@"<tr>
                                            <td colspan='2' style='padding:8px; text-align:right; color:#28a745;'><strong>Voucher giảm giá:</strong></td>
                                            <td style='padding:8px; text-align:right; color:#28a745;'>-{dh.GiamGia:N0}đ</td>
                                        </tr>";
                        }

                        mailKhach.Body = $@"
                        <div style='font-family: Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;'>
                            <div style='background-color: #007bff; padding: 25px; text-align: center;'>
                                <h2 style='color: #ffffff; margin: 0; font-size: 24px;'>ĐẶT HÀNG THÀNH CÔNG</h2>
                                <p style='color: #e6f2ff; margin: 10px 0 0;'>Cảm ơn bạn đã tin tưởng Men Store!</p>
                            </div>
                            <div style='padding: 25px; background-color: #ffffff;'>
                                <p>Xin chào <strong>{dh.TenNguoiNhan}</strong>,</p>
                                <p>Đơn hàng của bạn đã được hệ thống ghi nhận. Chúng tôi sẽ sớm liên hệ để xác nhận và giao hàng.</p>
                                
                                <div style='background-color: #f8f9fa; padding: 15px; border-radius: 6px; margin: 20px 0; border-left: 4px solid #007bff;'>
                                    <p style='margin: 5px 0;'><strong>Mã đơn hàng:</strong> {dh.MaDonHang}</p>
                                    <p style='margin: 5px 0;'><strong>Ngày đặt:</strong> {dh.NgayDat:dd/MM/yyyy HH:mm}</p>
                                    <p style='margin: 5px 0;'><strong>Địa chỉ nhận:</strong> {dh.DiaChiGiaoHang}</p>
                                    <p style='margin: 5px 0;'><strong>Thanh toán:</strong> {(dh.TrangThaiThanhToan == true ? "Đã thanh toán VNPAY" : "Thanh toán khi nhận hàng (COD)")}</p>
                                </div>

                                <table style='width: 100%; border-collapse: collapse; font-size: 14px;'>
                                    <thead>
                                        <tr style='background-color: #eee;'>
                                            <th style='padding:10px; text-align:left;'>Sản phẩm</th> <th style='padding:10px; text-align:center;'>SL</th>
                                            <th style='padding:10px; text-align:right;'>Thành tiền</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {listProductHtml}
                                    </tbody>
                                    <tfoot>
                                        <tr>
                                            <td colspan='2' style='padding:10px; text-align:right; border-top:2px solid #eee;'><strong>Tạm tính:</strong></td>
                                            <td style='padding:10px; text-align:right; border-top:2px solid #eee;'>{dh.TongTien:N0}đ</td>
                                        </tr>
                                        <tr>
                                            <td colspan='2' style='padding:8px; text-align:right;'><strong>Phí vận chuyển:</strong></td>
                                            <td style='padding:8px; text-align:right;'>{dh.PhiVanChuyen:N0}đ</td>
                                        </tr>
                                        {rowVoucher}
                                        <tr>
                                            <td colspan='2' style='padding:12px; text-align:right; font-size:16px;'><strong>TỔNG THANH TOÁN:</strong></td>
                                            <td style='padding:12px; text-align:right; color:#d9534f; font-size:18px; font-weight:bold;'>{dh.TongThanhToan:N0}đ</td>
                                        </tr>
                                    </tfoot>
                                </table>

                                <div style='text-align: center; margin-top: 30px; font-size: 13px; color: #888;'>
                                    <p>Mọi thắc mắc vui lòng liên hệ hotline: <strong>1900 xxxx</strong></p>
                                    <p>&copy; 2026 Men Store. All rights reserved.</p>
                                </div>
                            </div>
                        </div>";

                        SendMailSMTPS(mailKhach, host, port, fromEmail, password);
                    }
                    // -----------------------------------------------------------
                    // B. GỬI CHO ADMIN (ĐÃ BỎ CỘT ẢNH & FIX CÚ PHÁP)
                    // -----------------------------------------------------------
                    var mailAdmin = new MailMessage();
                    mailAdmin.From = new MailAddress(fromEmail, fromName);
                    mailAdmin.To.Add(new MailAddress(adminEmail));
                    mailAdmin.Subject = $"[New Order] Đơn hàng mới #{dh.MaDonHang} - {dh.TongThanhToan:N0}đ";
                    mailAdmin.IsBodyHtml = true;

                    mailAdmin.Body = $@"
                    <div style='font-family: Arial, sans-serif; border: 1px solid #007bff; padding: 20px; border-radius: 5px; max-width: 600px;'>
                        <h3 style='color: #007bff; margin-top: 0; border-bottom: 2px solid #007bff; padding-bottom: 10px;'>
                            🔔 THÔNG BÁO ĐƠN HÀNG MỚI
                        </h3>
                        
                        <div style='margin-bottom: 20px;'>
                            <p><strong>Khách hàng:</strong> {dh.TenNguoiNhan}</p>
                            <p><strong>Mã đơn:</strong> <span style='background:#eee; padding:2px 5px; font-weight:bold;'>{dh.MaDonHang}</span></p>
                            <p><strong>Tổng tiền:</strong> <span style='color:red; font-weight:bold; font-size:16px;'>{dh.TongThanhToan:N0}đ</span></p>
                            <p><strong>Thời gian:</strong> {dh.NgayDat:dd/MM/yyyy HH:mm}</p>
                            <p><strong>Hình thức:</strong> {(dh.TrangThaiThanhToan == true ? "Online" : "COD")}</p>
                        </div>

                        <table style='width: 100%; border-collapse: collapse; border: 1px solid #ddd; margin-bottom: 20px;'>
                            <tr style='background:#f8f9fa;'>
                                <th style='padding:8px; border:1px solid #ddd;'>Sản phẩm</th> <th style='padding:8px; border:1px solid #ddd; text-align:center;'>SL</th>
                                <th style='padding:8px; border:1px solid #ddd; text-align:right;'>Thành tiền</th>
                            </tr>
                            {listProductHtml}
                        </table>
                        
                        <div style='text-align:right; font-weight:bold; padding-bottom: 20px;'>
                             TỔNG CỘNG: <span style='color:red;'>{dh.TongThanhToan:N0}đ</span>
                        </div>

                        <div style='text-align:center;'>
                            <a href='{baseUrl}/Admin/DonHang/Details/{dh.ID}' 
                               style='background-color:#28a745; color:#ffffff; padding:12px 25px; text-decoration:none; border-radius:4px; font-weight:bold; display:inline-block;'>
                               XỬ LÝ ĐƠN HÀNG NGAY
                            </a>
                        </div>
                    </div>";

                    SendMailSMTPS(mailAdmin, host, port, fromEmail, password);
                    // --- KẾT THÚC PHẦN COPY ---
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi gửi mail: " + ex.Message);
            }
        }

        // 3. XỬ LÝ URL VNPAY
        private string GetVnPayUrl(string orderCode, long amount)
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
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress());
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang: " + orderCode);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", orderCode);

            return vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
        }

        // 4. CALLBACK TỪ VNPAY
        public ActionResult PaymentCallback()
        {
            if (Request.QueryString.Count > 0)
            {
                string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
                var vnpayData = Request.QueryString;
                VnPayLibrary vnpay = new VnPayLibrary();

                // 1. Duyệt dữ liệu để kiểm tra chữ ký (Giữ nguyên)
                foreach (string s in vnpayData)
                {
                    if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(s, vnpayData[s]);
                    }
                }

                // 2. Lấy các thông số từ VNPay trả về
                // [QUAN TRỌNG] Đây là mã tham chiếu (VD: "ORD123" hoặc "ORD123_456789")
                string vnp_TxnRef = vnpay.GetResponseData("vnp_TxnRef");

                long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100;
                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
                string vnp_SecureHash = Request.QueryString["vnp_SecureHash"];

                // 3. Kiểm tra chữ ký bảo mật
                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

                if (checkSignature)
                {
                    // =================================================================
                    // [SỬA ĐỔI BẮT ĐẦU]: Xử lý tách chuỗi để lấy mã đơn hàng gốc
                    // =================================================================

                    string orderCode = vnp_TxnRef; // Mặc định gán bằng mã trả về

                    // Nếu mã chứa dấu gạch dưới "_" (tức là đơn thanh toán lại có kèm timestamp)
                    if (!string.IsNullOrEmpty(orderCode) && orderCode.Contains("_"))
                    {
                        // Cắt chuỗi, chỉ lấy phần đầu tiên (Mã đơn hàng gốc)
                        // Ví dụ: "ORD250212001_638433..." -> Lấy "ORD250212001"
                        orderCode = orderCode.Split('_')[0];
                    }

                    // =================================================================
                    // [SỬA ĐỔI KẾT THÚC]
                    // =================================================================

                    // 4. Tìm đơn hàng trong Database bằng mã gốc (orderCode)
                    var donHang = _unitOfWork.Repository<DonHang>()
                                             .GetMany(x => x.MaDonHang == orderCode)
                                             .FirstOrDefault();

                    if (donHang != null)
                    {
                        // Kiểm tra số tiền (Optional: nên check xem tiền trả về có khớp đơn hàng không)
                        // if (donHang.TongThanhToan != vnp_Amount) { return View("OrderFail"); }

                        if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                        {
                            // Chỉ cập nhật nếu đơn hàng CHƯA thanh toán
                            // (Tránh trường hợp khách F5 lại trang callback)
                            if (donHang.TrangThaiThanhToan == false)
                            {
                                donHang.TrangThaiThanhToan = true;
                                _unitOfWork.Repository<DonHang>().Update(donHang);
                                _unitOfWork.Save();

                                // Gửi mail xác nhận
                                Task.Run(() => SendOrderEmailAsync(donHang.ID));
                            }
                            ViewBag.TitleStatus = "THANH TOÁN THÀNH CÔNG!"; // Tiêu đề H2
                            ViewBag.Msg = "Giao dịch VNPay của bạn đã hoàn tất. Cảm ơn bạn đã mua hàng tại Men Store. Hệ thống đã ghi nhận đơn hàng và sẽ liên hệ sớm nhất để giao hàng cho bạn.";
                            return View("OrderSuccess");
                        }
                        else
                        {
                            // Thanh toán lỗi -> Không làm gì cả, đơn hàng vẫn ở trạng thái chờ
                            ViewBag.Msg = "Thanh toán lỗi hoặc bị hủy. Bạn có thể thử lại trong phần lịch sử đơn hàng.";
                            return View("OrderFail");
                        }
                    }
                    else
                    {
                        ViewBag.Msg = "Không tìm thấy đơn hàng: " + orderCode;
                        return View("OrderFail");
                    }
                }
            }

            ViewBag.Msg = "Chữ ký không hợp lệ!";
            return View("OrderFail");
        }

        public ActionResult OrderSuccess() { return View(); }
        public ActionResult OrderFail() { return View(); }

      

        // Helper thực hiện gửi SMTP (Tách ra để tái sử dụng)
        private void SendMailSMTPS(MailMessage message, string host, int port, string email, string pass)
        {
            using (var client = new SmtpClient(host, port))
            {
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(email, pass);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Send(message);
            }
        }
        private void RemoveItemsFromCart(List<int> skuIds)
        {
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                var cart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == user.ID).FirstOrDefault();
                if (cart != null)
                {
                    var items = _unitOfWork.Repository<GioHangChiTiet>().GetMany(x => x.GioHangID == cart.ID && skuIds.Contains(x.BienTheSanPhamID)).ToList();
                    foreach (var i in items) _unitOfWork.Repository<GioHangChiTiet>().Delete(i);
                    _unitOfWork.Save();
                }
            }
            else
            {
                var cart = Session["Cart"] as List<CartItemViewModel>;
                if (cart != null)
                {
                    cart.RemoveAll(x => skuIds.Contains(x.SKU_ID));
                    Session["Cart"] = cart;
                }
            }
            Session["CartCount"] = GetCartCount();
        }

    }
}