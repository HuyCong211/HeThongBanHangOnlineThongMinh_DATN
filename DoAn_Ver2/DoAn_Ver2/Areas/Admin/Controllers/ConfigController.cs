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
    public class ConfigController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Admin/Config
        public ActionResult Index()
        {
            // Lấy tất cả cấu hình đưa ra list để hiển thị và sửa trực tiếp
            var listConfig = _unitOfWork.Repository<CauHinhChung>().GetAll().ToList();

            // Nếu chưa có các key cơ bản thì tạo mặc định
            EnsureConfigExists("SiteLogo", "/Content/images/logo.png", "Logo Website");
            EnsureConfigExists("SiteIcon", "/Content/images/favicon.ico", "Favicon");
            EnsureConfigExists("Hotline", "1900 xxxx", "Số điện thoại hotline");
            EnsureConfigExists("Email", "contact@menstore.com", "Email liên hệ");
            EnsureConfigExists("BannerHome", "/Content/images/banner-home.jpg", "Banner chính trang chủ");
            EnsureConfigExists("BannerLogin", "/Content/images/bg-login.jpg", "Ảnh nền trang đăng nhập");
            EnsureConfigExists("FooterInfo", "Địa chỉ: 123 ABC...", "Thông tin chân trang");
            EnsureConfigExists("BannerAbout", "https://theme.hstatic.net/200000690725/1001078549/14/slide_2_img.jpg?v=235", "Banner trang giới thiệu");
            EnsureConfigExists("ImageAbout", "https://images.unsplash.com/photo-1490578474895-699cd4e2cf59", "Ảnh nội dung giới thiệu");
            EnsureConfigExists("SiteEffect", "sakura", "Hiệu ứng theo mùa");

            listConfig = _unitOfWork.Repository<CauHinhChung>().GetAll().ToList();
            return View(listConfig);
        }

        private void EnsureConfigExists(string key, string value, string desc)
        {
            var conf = _unitOfWork.Repository<CauHinhChung>().GetById(key);
            if (conf == null)
            {
                conf = new CauHinhChung { KeyName = key, Value = value, MoTa = desc };
                _unitOfWork.Repository<CauHinhChung>().Add(conf);
                _unitOfWork.Save();
            }
        }

        // 2. CẬP NHẬT CẤU HÌNH (POST)
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult Update(List<CauHinhChung> configs, 
                            HttpPostedFileBase SiteLogo, HttpPostedFileBase SiteIcon, 
                            HttpPostedFileBase BannerHome, HttpPostedFileBase BannerLogin,
                            HttpPostedFileBase BannerAbout, HttpPostedFileBase ImageAbout)
        {
            if (configs != null)
            {
                foreach (var item in configs)
                {
                    var conf = _unitOfWork.Repository<CauHinhChung>().GetById(item.KeyName);
                    if (conf != null)
                    {
                        
                        if (item.KeyName == "SiteLogo")
                            conf.Value = UploadFile(SiteLogo) ?? conf.Value;
                        else if (item.KeyName == "SiteIcon")
                            conf.Value = UploadFile(SiteIcon) ?? conf.Value;
                        else if (item.KeyName == "BannerHome")
                            conf.Value = UploadFile(BannerHome) ?? conf.Value;
                        else if (item.KeyName == "BannerLogin")
                            conf.Value = UploadFile(BannerLogin) ?? conf.Value;
                        else if (item.KeyName == "BannerAbout")
                            conf.Value = UploadFile(BannerAbout) ?? conf.Value;
                        else if (item.KeyName == "ImageAbout")
                            conf.Value = UploadFile(ImageAbout) ?? conf.Value;

                        else
                            conf.Value = item.Value; 

                        _unitOfWork.Repository<CauHinhChung>().Update(conf);
                    }
                }
                _unitOfWork.Save();
                TempData["Success"] = "Cập nhật cấu hình thành công!";
            }
            return RedirectToAction("Index");
        }

        private string UploadFile(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                string _FileName = Path.GetFileName(file.FileName);
                string _path = Path.Combine(Server.MapPath("~/Content/images/config/"), _FileName);

                if (!Directory.Exists(Server.MapPath("~/Content/images/config/")))
                {
                    Directory.CreateDirectory(Server.MapPath("~/Content/images/config/"));
                }

                file.SaveAs(_path);
                return "/Content/images/config/" + _FileName;
            }
            return null;
        }
    }
}