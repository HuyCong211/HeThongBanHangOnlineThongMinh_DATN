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
            int pageSize = 10; 
            int pageNumber = (page ?? 1);
            var query = _unitOfWork.Repository<MauSac>().GetAll().AsQueryable();
            if (!string.IsNullOrEmpty(searchString))
            {
                string key = searchString.Trim().ToLower();

                query = query.Where(x => x.TenMau.ToLower().Contains(key) ||
                                         x.MaHex.ToLower().Contains(key));
            }
            query = query.OrderByDescending(x => x.ID);
            ViewBag.CurrentFilter = searchString;
            return View(query.ToPagedList(pageNumber, pageSize));
        }

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
            bool isUsed = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.MauSacID == id).Any();

            if (isUsed)
            {
                TempData["Message"] = "Không thể xóa màu này vì đang có sản phẩm sử dụng.";
                TempData["MessageType"] = "danger"; 
                return RedirectToAction("Index");
            }

            _unitOfWork.Repository<MauSac>().Delete(id);
            _unitOfWork.Save();

            TempData["Message"] = "Đã xóa màu thành công.";
            TempData["MessageType"] = "success"; 
            return RedirectToAction("Index");
        }
    }
}