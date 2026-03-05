using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using PagedList;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DoAn_Ver2.Common;
using System.Web;
using System.Web.Mvc;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class NewsController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Admin/News
        public ActionResult Index(string searchString, int? page)
        {
            var query = _unitOfWork.Repository<TinTuc>().GetAll();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(x => x.TieuDe.Contains(searchString));
            }

            query = query.OrderByDescending(x => x.NgayDang);

            int pageSize = 10;
            int pageNumber = (page ?? 1);
            ViewBag.CurrentFilter = searchString;

            return View(query.ToPagedList(pageNumber, pageSize));
        }


        // 5. XEM CHI TIẾT (GET)
        public ActionResult Details(int id)
        {
            var item = _unitOfWork.Repository<TinTuc>().GetById(id);
            if (item == null) return HttpNotFound();

            // Lấy tên người đăng để hiển thị
            var author = _unitOfWork.Repository<NguoiDung>().GetById(item.NguoiDungID ?? 0);
            ViewBag.AuthorName = author != null ? (author.HoTen ?? author.TenDangNhap) : "Không xác định";

            return View(item);
        }

        // 2. TẠO MỚI (GET)
        public ActionResult Create()
        {
            return View();
        }

        // 2. TẠO MỚI (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(TinTuc model, HttpPostedFileBase uploadBtn)
        {
            if (ModelState.IsValid)
            {
                // Xử lý upload ảnh
                if (uploadBtn != null && uploadBtn.ContentLength > 0)
                {
                    string _FileName = Path.GetFileName(uploadBtn.FileName);
                    string _path = Path.Combine(Server.MapPath("~/Content/images/news/"), _FileName);

                    if (!Directory.Exists(Server.MapPath("~/Content/images/news/")))
                    {
                        Directory.CreateDirectory(Server.MapPath("~/Content/images/news/"));
                    }

                    uploadBtn.SaveAs(_path);
                    model.HinhAnh = "/Content/images/news/" + _FileName;
                }

                // [SỬA LẠI] Dùng MyTools để tạo Slug
                model.Slug = MyTools.GenerateSlug(model.TieuDe);

                model.NgayDang = DateTime.Now;

                // Lấy ID người đang đăng nhập (Admin)
                if (Session["UserAdmin"] != null)
                {
                    var adminUser = (NguoiDung)Session["UserAdmin"];
                    model.NguoiDungID = adminUser.ID;
                }

                _unitOfWork.Repository<TinTuc>().Add(model);
                _unitOfWork.Save();
                TempData["Success"] = "Đăng bài thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 3. CẬP NHẬT (GET)
        public ActionResult Edit(int id)
        {
            var item = _unitOfWork.Repository<TinTuc>().GetById(id);
            if (item == null) return HttpNotFound();
            return View(item);
        }

        // 3. CẬP NHẬT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Edit(TinTuc model, HttpPostedFileBase uploadBtn)
        {
            if (ModelState.IsValid)
            {
                var item = _unitOfWork.Repository<TinTuc>().GetById(model.ID);
                if (item == null) return HttpNotFound();

                item.TieuDe = model.TieuDe;
                item.TomTat = model.TomTat;
                item.NoiDung = model.NoiDung;
                item.TrangThai = model.TrangThai;

                // [SỬA LẠI] Cập nhật Slug bằng MyTools
                item.Slug = MyTools.GenerateSlug(model.TieuDe);

                if (uploadBtn != null && uploadBtn.ContentLength > 0)
                {
                    string _FileName = Path.GetFileName(uploadBtn.FileName);
                    string _path = Path.Combine(Server.MapPath("~/Content/images/news/"), _FileName);

                    if (!Directory.Exists(Server.MapPath("~/Content/images/news/")))
                    {
                        Directory.CreateDirectory(Server.MapPath("~/Content/images/news/"));
                    }

                    uploadBtn.SaveAs(_path);
                    item.HinhAnh = "/Content/images/news/" + _FileName;
                }

                _unitOfWork.Repository<TinTuc>().Update(item);
                _unitOfWork.Save();
                TempData["Success"] = "Cập nhật thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 4. XÓA (POST)
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                var item = _unitOfWork.Repository<TinTuc>().GetById(id);
                if (item == null) return Json(new { success = false, message = "Không tìm thấy bài viết" });

                _unitOfWork.Repository<TinTuc>().Delete(item);
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