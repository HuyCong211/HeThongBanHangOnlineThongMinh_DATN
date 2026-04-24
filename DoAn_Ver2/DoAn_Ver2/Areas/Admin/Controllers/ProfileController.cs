using DoAn_Ver2.Common;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class ProfileController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();

        // GET: Thông tin cá nhân
        public ActionResult Index()
        {
            var userSession = (NguoiDung)Session["UserAdmin"];
            if (userSession == null) return RedirectToAction("Login", "Auth");

            var user = _unitOfWork.Repository<NguoiDung>().GetById(userSession.ID);
            user.MatKhau = "";
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(NguoiDung model, string NewPassword, HttpPostedFileBase uploadBtn)
        {
            var user = _unitOfWork.Repository<NguoiDung>().GetById(model.ID);
            if (user == null) return HttpNotFound();
            user.HoTen = model.HoTen;
            user.Email = model.Email;
            user.SDT = model.SDT;
            user.NgayCapNhat = DateTime.Now;
            if (!string.IsNullOrEmpty(NewPassword))
            {
                user.MatKhau = SecurityHelper.MD5Hash(NewPassword);
            }
            if (uploadBtn != null && uploadBtn.ContentLength > 0)
            {
                string _FileName = Path.GetFileName(uploadBtn.FileName);
                string _path = Path.Combine(Server.MapPath("~/Content/images/users/"), _FileName);
                if (!Directory.Exists(Server.MapPath("~/Content/images/users/")))
                    Directory.CreateDirectory(Server.MapPath("~/Content/images/users/"));

                uploadBtn.SaveAs(_path);
                user.Avt = "/Content/images/users/" + _FileName;
            }

            _unitOfWork.Repository<NguoiDung>().Update(user);
            _unitOfWork.Save();

            Session["UserAdmin"] = user;
            TempData["Success"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction("Index");
        }
    }
}