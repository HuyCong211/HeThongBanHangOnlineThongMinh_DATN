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

            var query = _unitOfWork.Repository<BienTheSanPham>().GetAll().AsQueryable();

            var listSP = _unitOfWork.Repository<SanPham>().GetAll();
            var listMau = _unitOfWork.Repository<MauSac>().GetAll();
            var listSize = _unitOfWork.Repository<KichThuoc>().GetAll();
            var listDM = _unitOfWork.Repository<DanhMuc>().GetAll(); 

            ViewBag.MapTenSP = listSP.ToDictionary(x => x.ID, x => x.TenSanPham);
            ViewBag.MapMau = listMau.ToDictionary(x => x.ID, x => x); 
            ViewBag.MapSize = listSize.ToDictionary(x => x.ID, x => x.TenSize);

            if (danhMucId.HasValue)
            {
                var spThuocDanhMuc = listSP.Where(x => x.DanhMucID == danhMucId).Select(x => x.ID).ToList();
                query = query.Where(x => spThuocDanhMuc.Contains(x.SanPhamID));
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                string key = searchString.ToLower().Trim();
                var spIds = listSP.Where(x => x.TenSanPham.ToLower().Contains(key)).Select(x => x.ID).ToList();
                query = query.Where(x => x.SKU.ToLower().Contains(key) || spIds.Contains(x.SanPhamID));
            } 
            query = query.OrderBy(x => x.SoLuong);

            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentDanhMuc = danhMucId;
            ViewBag.ListDanhMuc = new SelectList(listDM, "ID", "TenDanhMuc", danhMucId);
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

                // 2. GHI LỊCH SỬ KHO
                var ls = new LichSuKho()
                {
                    BienTheSanPhamID = sku.ID,
                    SoLuongBienDong = SoLuongNhap, 
                    TonThucTeSauBienDong = sku.SoLuong,
                    LoaiGiaoDich = "Nhập hàng",
                    MaThamChieu = "PN-" + DateTime.Now.ToString("ddMMyy-HHmm"), 
                    NgayGhi = DateTime.Now
                };

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