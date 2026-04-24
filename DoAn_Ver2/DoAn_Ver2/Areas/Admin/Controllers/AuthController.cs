using DoAn_Ver2.Common; 
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;


namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class AuthController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();

        // 1. ĐĂNG NHẬP (GET)
        public ActionResult Login()
        {
            
            if (Session["UserAdmin"] != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        // 1. ĐĂNG NHẬP (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin";
                return View();
            }

            // Mã hóa pass nhập vào để so sánh với DB
            string md5Pass = SecurityHelper.MD5Hash(password);

            var user = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.TenDangNhap == username && x.MatKhau == md5Pass).FirstOrDefault();

            if (user != null)
            {
                // Kiểm tra trạng thái hoạt động
                if (user.TrangThai == false)
                {
                    ViewBag.Error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin.";
                    return View();
                }

                // Kiểm tra vai trò (Chỉ Admin và Staff được vào)
                if (user.VaiTro != "Admin" && user.VaiTro != "Staff")
                {
                    ViewBag.Error = "Tài khoản này không có quyền truy cập trang quản trị.";
                    return View();
                }

                // Đăng nhập thành công -> Lưu Session
                Session["UserAdmin"] = user;

                // Hiển thị thông báo chào mừng (Optional)
                TempData["Success"] = "Xin chào " + (user.HoTen ?? user.TenDangNhap);

                return RedirectToAction("Index", "Dashboard");
            }
            else
            {
                ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không đúng";
                return View();
            }
        }

        // 2. ĐĂNG XUẤT
        public ActionResult Logout()
        {
            Session["UserAdmin"] = null;
            return RedirectToAction("Login");
        }

        // 3. QUÊN MẬT KHẨU (GET) - Hiển thị form nhập email
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // 3. QUÊN MẬT KHẨU (POST) - Xử lý gửi mail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            var user = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.Email == email).FirstOrDefault();
            if (user != null)
            {
                // Tạo Token ngẫu nhiên
                string token = Guid.NewGuid().ToString();

                // Lưu Token và hạn dùng vào DB
                user.ResetToken = token;
                user.ResetTokenExpiry = DateTime.Now.AddMinutes(15);
                _unitOfWork.Repository<NguoiDung>().Update(user);
                _unitOfWork.Save();

                // Tạo link reset
                string resetLink = Url.Action("ResetPassword", "Auth", new { token = token }, Request.Url.Scheme);

                // Gửi email
                string subject = "Yêu cầu đặt lại mật khẩu - Men Store";
                string body = $"<p>Xin chào {user.HoTen},</p>" +
                              $"<p>Bạn vừa yêu cầu đặt lại mật khẩu. Vui lòng nhấn vào link dưới đây để thiết lập mật khẩu mới:</p>" +
                              $"<p><a href='{resetLink}'>{resetLink}</a></p>" +
                              $"<p>Link này sẽ hết hạn sau 15 phút.</p>";

                try
                {
                    MailHelper.SendMail(user.Email, subject, body);
                    ViewBag.Success = "Link đặt lại mật khẩu đã được gửi vào email của bạn.";
                }
                catch (Exception)
                {
                    ViewBag.Error = "Không thể gửi email. Vui lòng thử lại sau.";
                }
            }
            else
            {
                ViewBag.Error = "Email không tồn tại trong hệ thống.";
            }

            return View();
        }

        // 4. ĐẶT LẠI MẬT KHẨU (GET) - Kiểm tra Token từ Email
        public ActionResult ResetPassword(string token)
        {
            var user = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.ResetToken == token && x.ResetTokenExpiry > DateTime.Now).FirstOrDefault();

            if (user == null)
            {
                ViewBag.Error = "Link không hợp lệ hoặc đã hết hạn.";
                return View("ErrorReset"); 
            }

            ViewBag.Token = token;
            return View();
        }

        // 4. ĐẶT LẠI MẬT KHẨU (POST) - Cập nhật mật khẩu mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                ViewBag.Token = token;
                return View();
            }

            var user = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.ResetToken == token).FirstOrDefault();
            if (user != null)
            {
                // Cập nhật pass mới
                user.MatKhau = SecurityHelper.MD5Hash(newPassword);

                // Xóa token để không dùng lại được nữa
                user.ResetToken = null;
                user.ResetTokenExpiry = null;

                _unitOfWork.Repository<NguoiDung>().Update(user);
                _unitOfWork.Save();

                TempData["Success"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }

            ViewBag.Error = "Lỗi xác thực. Vui lòng thử lại.";
            return View();
        }
    }
}