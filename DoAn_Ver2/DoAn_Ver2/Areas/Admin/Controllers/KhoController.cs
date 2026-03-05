using DoAn_Ver2.Models;
using PagedList;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class KhoController : BaseController
    {
        // GET: Admin/Kho
        public ActionResult Index(string searchString, int? danhMucId, int? page, int? pageSize)
        {
            int pageNumber = (page ?? 1);
            int size = (pageSize ?? 10);
            ViewBag.PageSize = size;

            // 1. Truy vấn (Có Include hoặc Join nếu Repository hỗ trợ, ở đây ta query và filter)
            var query = _unitOfWork.Repository<BienTheSanPham>().GetAll().AsQueryable();

            // Lấy danh sách phụ để map tên (Cách này hơi chậm nếu dữ liệu lớn, nên dùng Include trong Repository sẽ tốt hơn)
            // Tuy nhiên với cách hiện tại của bạn:
            var listSP = _unitOfWork.Repository<SanPham>().GetAll();
            var listMau = _unitOfWork.Repository<MauSac>().GetAll();
            var listSize = _unitOfWork.Repository<KichThuoc>().GetAll();
            var listDM = _unitOfWork.Repository<DanhMuc>().GetAll(); // Lấy danh mục

            // Tạo Dictionary để tra cứu nhanh trong View (Tối ưu hiệu năng hiển thị)
            ViewBag.MapTenSP = listSP.ToDictionary(x => x.ID, x => x.TenSanPham);
            ViewBag.MapMau = listMau.ToDictionary(x => x.ID, x => x); // Lưu cả object màu để lấy mã Hex
            ViewBag.MapSize = listSize.ToDictionary(x => x.ID, x => x.TenSize);

            // 3. LỌC THEO DANH MỤC (Logic mới)
            if (danhMucId.HasValue)
            {
                // Bước 1: Tìm tất cả ID sản phẩm thuộc danh mục này (bao gồm cả danh mục con nếu muốn)
                // Ở đây làm đơn giản: Chỉ lọc theo danh mục trực tiếp của sản phẩm
                var spThuocDanhMuc = listSP.Where(x => x.DanhMucID == danhMucId).Select(x => x.ID).ToList();

                // Bước 2: Lọc các SKU có SanPhamID nằm trong danh sách trên
                query = query.Where(x => spThuocDanhMuc.Contains(x.SanPhamID));
            }

            // 2. Tìm kiếm (Theo Mã SKU hoặc Tên Sản phẩm)
            if (!string.IsNullOrEmpty(searchString))
            {
                string key = searchString.ToLower().Trim();

                // Tìm ID các sản phẩm có tên chứa từ khóa
                var spIds = listSP.Where(x => x.TenSanPham.ToLower().Contains(key)).Select(x => x.ID).ToList();

                // Filter: Hoặc SKU chứa từ khóa, Hoặc thuộc sản phẩm có tên chứa từ khóa
                query = query.Where(x => x.SKU.ToLower().Contains(key) || spIds.Contains(x.SanPhamID));
            }

            // 3. Sắp xếp (Ưu tiên sản phẩm sắp hết hàng lên đầu)
            query = query.OrderBy(x => x.SoLuong);

            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentDanhMuc = danhMucId;

            // Tạo Dropdown Danh mục
            ViewBag.ListDanhMuc = new SelectList(listDM, "ID", "TenDanhMuc", danhMucId);

            // 4. Trả về PagedList
            return View(query.ToPagedList(pageNumber, size));
        }

        // 2. NHẬP HÀNG (IMPORT) - GET
        public ActionResult NhapHang(int id)
        {
            var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(id);
            if (sku == null) return HttpNotFound();

            var sanPham = _unitOfWork.Repository<SanPham>().GetById(sku.SanPhamID);
            var mau = _unitOfWork.Repository<MauSac>().GetById(sku.MauSacID);
            var size = _unitOfWork.Repository<KichThuoc>().GetById(sku.KichThuocID);

            // Truyền thông tin sang View để người dùng biết đang nhập cho cái gì
            ViewBag.TenSP = $"{sanPham.TenSanPham} ({mau.TenMau} - {size.TenSize})";
            ViewBag.SKU = sku.SKU;
            ViewBag.TonHienTai = sku.SoLuong;

            return View(sku);
        }

        // 3. NHẬP HÀNG (IMPORT) - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NhapHang(int id, int SoLuongNhap, string GhiChu)
        {
            var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(id);
            if (sku == null) return HttpNotFound();

            if (SoLuongNhap <= 0)
            {
                ModelState.AddModelError("", "Số lượng nhập phải lớn hơn 0");
                // Load lại thông tin cũ để hiện lỗi
                var sp = _unitOfWork.Repository<SanPham>().GetById(sku.SanPhamID);
                var m = _unitOfWork.Repository<MauSac>().GetById(sku.MauSacID);
                var s = _unitOfWork.Repository<KichThuoc>().GetById(sku.KichThuocID);
                ViewBag.TenSP = $"{sp.TenSanPham} ({m.TenMau} - {s.TenSize})";
                ViewBag.SKU = sku.SKU;
                ViewBag.TonHienTai = sku.SoLuong;
                return View(sku);
            }

            try
            {
                // 1. CẬP NHẬT KHO
                sku.SoLuong = sku.SoLuong + SoLuongNhap;
                _unitOfWork.Repository<BienTheSanPham>().Update(sku);

                // 2. GHI LỊCH SỬ KHO (Stock Card)
                var ls = new LichSuKho()
                {
                    BienTheSanPhamID = sku.ID,
                    SoLuongBienDong = SoLuongNhap, // Số dương là nhập
                    TonThucTeSauBienDong = sku.SoLuong,
                    LoaiGiaoDich = "Nhập hàng",
                    MaThamChieu = "PN-" + DateTime.Now.ToString("ddMMyy-HHmm"), // Tạo mã phiếu nhập tự động
                    NgayGhi = DateTime.Now
                    // Nếu muốn lưu người nhập, cần thêm cột NguoiDungID vào bảng LichSuKho hoặc ghi vào MaThamChieu
                };

                // Lưu ý: Nếu bảng LichSuKho trong DB chưa map vào Model, bạn cần chắc chắn đã update EDMX hoặc Model Code First
                _unitOfWork.Repository<LichSuKho>().Add(ls);

                _unitOfWork.Save();

                TempData["Message"] = $"Đã nhập thêm {SoLuongNhap} sản phẩm thành công!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi nhập hàng: " + ex.Message);
                return View(sku);
            }
        }

        // 4. XEM LỊCH SỬ KHO CỦA 1 SKU
        public ActionResult LichSu(int id)
        {
            var history = _unitOfWork.Repository<LichSuKho>()
                            .GetMany(x => x.BienTheSanPhamID == id)
                            .OrderByDescending(x => x.NgayGhi)
                            .ToList();

            var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(id);
            ViewBag.SKU_Code = sku.SKU;

            return View(history);
        }
    }
}