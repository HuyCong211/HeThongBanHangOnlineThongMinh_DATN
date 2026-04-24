using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Common; 
using DoAn_Ver2.Models;
using PagedList;
using DoAn_Ver2.Infrastructure;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class UserController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Admin/User
        public ActionResult Index(string searchString, string vaiTro, int? page)
        {
            var query = _unitOfWork.Repository<NguoiDung>().GetAll();
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(x => x.TenDangNhap.Contains(searchString) ||
                                         x.Email.Contains(searchString) ||
                                         x.SDT.Contains(searchString));
            }
            if (!string.IsNullOrEmpty(vaiTro))
            {
                query = query.Where(x => x.VaiTro == vaiTro);
            }

            query = query.OrderByDescending(x => x.NgayTao); 

            int pageSize = 10;
            int pageNumber = (page ?? 1);

            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentRole = vaiTro;

            return View(query.ToPagedList(pageNumber, pageSize));
        }

        // 2. TẠO MỚI (GET)
        public ActionResult Create()
        {
            return View();
        }

        // 2. TẠO MỚI (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(NguoiDung model, HttpPostedFileBase uploadBtn)
        {
            if (ModelState.IsValid)
            {
                if (_unitOfWork.Repository<NguoiDung>().GetMany(x => x.TenDangNhap == model.TenDangNhap).Any())
                {
                    ModelState.AddModelError("TenDangNhap", "Tên đăng nhập này đã tồn tại!");
                    return View(model);
                }
                if (_unitOfWork.Repository<NguoiDung>().GetMany(x => x.Email == model.Email).Any())
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng!");
                    return View(model);
                }
                if (uploadBtn != null && uploadBtn.ContentLength > 0)
                {
                    string _FileName = Path.GetFileName(uploadBtn.FileName);
                    string _path = Path.Combine(Server.MapPath("~/Content/images/users/"), _FileName);
                    if (!Directory.Exists(Server.MapPath("~/Content/images/users/")))
                    {
                        Directory.CreateDirectory(Server.MapPath("~/Content/images/users/"));
                    }
                    uploadBtn.SaveAs(_path);
                    model.Avt = "/Content/images/users/" + _FileName;
                }
                else
                {
                    model.Avt = "/wwwroot/images/user.png";
                }
                model.MatKhau = SecurityHelper.MD5Hash(model.MatKhau);

                model.NgayTao = DateTime.Now;
                model.NgayCapNhat = DateTime.Now;
                model.TrangThai = true; 

                _unitOfWork.Repository<NguoiDung>().Add(model);
                _unitOfWork.Save();
                TempData["Success"] = "Thêm người dùng thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 3. CẬP NHẬT (GET)
        public ActionResult Edit(int id)
        {
            var item = _unitOfWork.Repository<NguoiDung>().GetById(id);
            if (item == null) return HttpNotFound();

            item.MatKhau = "";
            return View(item);
        }

        // 3. CẬP NHẬT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(NguoiDung model, HttpPostedFileBase uploadBtn)
        {
            ModelState.Remove("MatKhau");

            if (ModelState.IsValid)
            {
                var userInDb = _unitOfWork.Repository<NguoiDung>().GetById(model.ID);
                if (userInDb == null) return HttpNotFound();

                userInDb.HoTen = model.HoTen;
                userInDb.SDT = model.SDT;
                userInDb.Email = model.Email;
                userInDb.VaiTro = model.VaiTro;
                userInDb.TrangThai = model.TrangThai;
                userInDb.NgayCapNhat = DateTime.Now;

                if (!string.IsNullOrEmpty(model.MatKhau))
                {
                    userInDb.MatKhau = SecurityHelper.MD5Hash(model.MatKhau);
                }
                if (uploadBtn != null && uploadBtn.ContentLength > 0)
                {
                    string _FileName = Path.GetFileName(uploadBtn.FileName);
                    string _path = Path.Combine(Server.MapPath("~/Content/images/users/"), _FileName);

                    if (!Directory.Exists(Server.MapPath("~/Content/images/users/")))
                    {
                        Directory.CreateDirectory(Server.MapPath("~/Content/images/users/"));
                    }

                    uploadBtn.SaveAs(_path);
                    userInDb.Avt = "/Content/images/users/" + _FileName;
                }

                _unitOfWork.Repository<NguoiDung>().Update(userInDb);
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
                var user = _unitOfWork.Repository<NguoiDung>().GetById(id);
                if (user == null) return Json(new { success = false, message = "Không tìm thấy người dùng" });
                var hasOrder = _unitOfWork.Repository<DonHang>().GetMany(x => x.NguoiDungID == id).Any();
                if (hasOrder)
                {
                    return Json(new { success = false, message = "User này đã có đơn hàng, không thể xóa! Hãy chuyển trạng thái sang Khóa." });
                }

                _unitOfWork.Repository<NguoiDung>().Delete(user);
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