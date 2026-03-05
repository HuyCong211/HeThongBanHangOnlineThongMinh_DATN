using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Models;
using PagedList;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class MauSacController : BaseController
    {
        // GET: Admin/MauSac
        public ActionResult Index(string searchString, int? page)
        {
            // 1. Cấu hình phân trang
            int pageSize = 10; // Số dòng trên 1 trang
            int pageNumber = (page ?? 1);

            // 2. Lấy dữ liệu (Chưa thực thi SQL ngay)
            var query = _unitOfWork.Repository<MauSac>().GetAll().AsQueryable();

            // 3. Xử lý Tìm kiếm (Theo Tên màu hoặc Mã Hex)
            if (!string.IsNullOrEmpty(searchString))
            {
                // Xóa khoảng trắng thừa và chuyển về chữ thường
                string key = searchString.Trim().ToLower();

                query = query.Where(x => x.TenMau.ToLower().Contains(key) ||
                                         x.MaHex.ToLower().Contains(key));
            }

            // 4. Sắp xếp (Mới nhất lên đầu)
            query = query.OrderByDescending(x => x.ID);

            // 5. Lưu lại từ khóa tìm kiếm vào ViewBag để giữ trạng thái ở View
            ViewBag.CurrentFilter = searchString;

            // 6. Trả về model dạng PagedList
            return View(query.ToPagedList(pageNumber, pageSize));
        }

        // Tạo mới (POST luôn cho nhanh, dùng Modal hoặc trang riêng tùy ý, ở đây làm trang riêng cho chuẩn)
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(MauSac model)
        {
            if (ModelState.IsValid)
            {
                _unitOfWork.Repository<MauSac>().Add(model);
                _unitOfWork.Save();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public ActionResult Edit(int id)
        {
            var model = _unitOfWork.Repository<MauSac>().GetById(id);
            if (model == null) return HttpNotFound();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(MauSac model)
        {
            if (ModelState.IsValid)
            {
                var existItem = _unitOfWork.Repository<MauSac>().GetById(model.ID);
                if (existItem == null) return HttpNotFound();

                existItem.TenMau = model.TenMau;
                existItem.MaHex = model.MaHex;

                _unitOfWork.Repository<MauSac>().Update(existItem);
                _unitOfWork.Save();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public ActionResult Delete(int id)
        {
            // Kiểm tra nghiệp vụ: Màu đang dùng trong Biến thể sản phẩm thì KHÔNG ĐƯỢC XÓA
            bool isUsed = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.MauSacID == id).Any();

            if (isUsed)
            {
                // Dùng TempData để truyền thông báo lỗi sang trang Index
                TempData["Message"] = "Không thể xóa màu này vì đang có sản phẩm sử dụng.";
                TempData["MessageType"] = "danger"; // Màu đỏ
                return RedirectToAction("Index");
            }

            _unitOfWork.Repository<MauSac>().Delete(id);
            _unitOfWork.Save();

            TempData["Message"] = "Đã xóa màu thành công.";
            TempData["MessageType"] = "success"; // Màu xanh
            return RedirectToAction("Index");
        }
    }
}