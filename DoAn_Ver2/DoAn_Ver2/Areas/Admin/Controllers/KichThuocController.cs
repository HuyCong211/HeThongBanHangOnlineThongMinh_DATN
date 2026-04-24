using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Models;
using PagedList;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class KichThuocController : BaseController
    {
        // GET: Admin/KichThuoc
        public ActionResult Index(string searchString, int? page)
        {
            int pageSize = 10;
            int pageNumber = (page ?? 1);
            var query = _unitOfWork.Repository<KichThuoc>().GetAll().AsQueryable();
            if (!string.IsNullOrEmpty(searchString))
            {
                string key = searchString.Trim().ToLower();
                query = query.Where(x => x.TenSize.ToLower().Contains(key));
            }
            query = query.OrderByDescending(x => x.ID);
            ViewBag.CurrentFilter = searchString;
            return View(query.ToPagedList(pageNumber, pageSize));
        }

        // 2. TẠO MỚI (GET)
        public ActionResult Create()
        {
            return View();
        }

        // 3. TẠO MỚI (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(KichThuoc model)
        {
            if (ModelState.IsValid)
            {
                _unitOfWork.Repository<KichThuoc>().Add(model);
                _unitOfWork.Save();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 4. CẬP NHẬT (GET)
        public ActionResult Edit(int id)
        {
            var model = _unitOfWork.Repository<KichThuoc>().GetById(id);
            if (model == null) return HttpNotFound();
            return View(model);
        }

        // 5. CẬP NHẬT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(KichThuoc model)
        {
            if (ModelState.IsValid)
            {
                var existItem = _unitOfWork.Repository<KichThuoc>().GetById(model.ID);
                if (existItem == null) return HttpNotFound();

                existItem.TenSize = model.TenSize;

                _unitOfWork.Repository<KichThuoc>().Update(existItem);
                _unitOfWork.Save();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 6. XÓA
        public ActionResult Delete(int id)
        {
            // Kiểm tra nghiệp vụ: Size đang dùng trong Biến thể sản phẩm thì KHÔNG ĐƯỢC XÓA
            bool isUsed = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.KichThuocID == id).Any();

            if (isUsed)
            {
                TempData["Message"] = "Không thể xóa size này vì đang có sản phẩm sử dụng.";
                TempData["MessageType"] = "danger";
                return RedirectToAction("Index");
            }

            _unitOfWork.Repository<KichThuoc>().Delete(id);
            _unitOfWork.Save();

            TempData["Message"] = "Đã xóa size thành công.";
            TempData["MessageType"] = "success";
            return RedirectToAction("Index");
        }
    }
}