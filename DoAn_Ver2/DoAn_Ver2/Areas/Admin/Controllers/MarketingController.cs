using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class MarketingController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Admin/Marketing
        public ActionResult Index(string searchString, int? page)
        {
            var query = _unitOfWork.Repository<MaGiamGia>().GetAll();

            // Tìm kiếm theo Mã code
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(x => x.MaCode.Contains(searchString));
            }

            // Sắp xếp: Mã mới nhất lên đầu
            query = query.OrderByDescending(x => x.ID);

            int pageSize = 10;
            int pageNumber = (page ?? 1);
            ViewBag.CurrentFilter = searchString;

            return View(query.ToPagedList(pageNumber, pageSize));
        }
        // 1. GET: Hiển thị form thêm mới (BẠN ĐANG THIẾU CÁI NÀY)
        public ActionResult Create()
        {
            return View();
        }
        // 2. TẠO MỚI MÃ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(MaGiamGia model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra mã trùng
                var exist = _unitOfWork.Repository<MaGiamGia>().GetMany(x => x.MaCode == model.MaCode).FirstOrDefault();
                if (exist != null)
                {
                    ModelState.AddModelError("", "Mã giảm giá này đã tồn tại!");
                    return View(model);
                }

                // Viết hoa mã code cho chuẩn
                model.MaCode = model.MaCode.ToUpper();

                _unitOfWork.Repository<MaGiamGia>().Add(model);
                _unitOfWork.Save();
                TempData["Success"] = "Tạo mã giảm giá thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 3. CHỈNH SỬA MÃ (GET)
        public ActionResult Edit(int id)
        {
            var item = _unitOfWork.Repository<MaGiamGia>().GetById(id);
            if (item == null) return HttpNotFound();
            return View(item);
        }

        // 3. CHỈNH SỬA MÃ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(MaGiamGia model)
        {
            if (ModelState.IsValid)
            {
                var item = _unitOfWork.Repository<MaGiamGia>().GetById(model.ID);
                if (item == null) return HttpNotFound();

                // Cập nhật thông tin
                item.LoaiGiam = model.LoaiGiam;
                item.GiaTri = model.GiaTri;
                item.DonToiThieu = model.DonToiThieu;
                item.SoLuong = model.SoLuong;
                item.NgayHetHan = model.NgayHetHan;

                // Không cho sửa MaCode để tránh lỗi logic các đơn hàng cũ

                _unitOfWork.Repository<MaGiamGia>().Update(item);
                _unitOfWork.Save();
                TempData["Success"] = "Cập nhật thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 4. XÓA MÃ (POST)
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                var item = _unitOfWork.Repository<MaGiamGia>().GetById(id);
                if (item == null) return Json(new { success = false, message = "Không tìm thấy mã" });

                _unitOfWork.Repository<MaGiamGia>().Delete(item);
                _unitOfWork.Save();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}