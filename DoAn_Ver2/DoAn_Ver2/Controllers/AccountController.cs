using DoAn_Ver2.Common;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using DoAn_Ver2.Models.ViewModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;

namespace DoAn_Ver2.Controllers
{
    public class AccountController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Account
        // 1. ĐĂNG KÝ
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check trùng tên đăng nhập
                var checkUser = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.TenDangNhap == model.TenDangNhap).FirstOrDefault();
                if (checkUser != null)
                {
                    ModelState.AddModelError("", "Tên đăng nhập đã tồn tại.");
                    return View(model);
                }

                // Check trùng Email
                var checkEmail = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.Email == model.Email).FirstOrDefault();
                if (checkEmail != null)
                {
                    ModelState.AddModelError("", "Email đã được sử dụng.");
                    return View(model);
                }

                var user = new NguoiDung
                {
                    TenDangNhap = model.TenDangNhap,
                    MatKhau = SecurityHelper.MD5Hash(model.MatKhau), 
                    HoTen = model.HoTen,
                    Email = model.Email,
                    SDT = model.SDT,
                    VaiTro = "Customer", // CỨNG: Khách hàng
                    NgayTao = DateTime.Now,
                    TrangThai = true,
                    Avt = "/Content/images/default-user.png"
                };

                _unitOfWork.Repository<NguoiDung>().Add(user);
                _unitOfWork.Save();

                TempData["Success"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        // 2. ĐĂNG NHẬP
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                string passHash = SecurityHelper.MD5Hash(model.MatKhau);

                // Cho phép đăng nhập bằng cả User hoặc Email
                var user = _unitOfWork.Repository<NguoiDung>().GetMany(x =>
                    (x.TenDangNhap == model.TenDangNhap || x.Email == model.TenDangNhap)
                    && x.MatKhau == passHash
                    && x.VaiTro == "Customer").FirstOrDefault();

                if (user != null)
                {
                    if (user.TrangThai == false)
                    {
                        ModelState.AddModelError("", "Tài khoản của bạn đang bị khóa.");
                        return View(model);
                    }
                    Session["KhachHang"] = user;
                    MergeCart(user.ID);
                    UpdateCartCountSession(user.ID);

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                }
            }
            return View(model);
        }

        // 3. ĐĂNG XUẤT
        public ActionResult Logout()
        {
            Session["KhachHang"] = null;
            Session["Cart"] = null; 
            Session["CartCount"] = null;
            return RedirectToAction("Login");
        }

        // 4. QUÊN MẬT KHẨU
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.Email == model.Email && x.VaiTro == "Customer").FirstOrDefault();

                // Nếu user tồn tại
                if (user != null)
                {
                    // Tạo Token
                    string token = Guid.NewGuid().ToString();
                    user.ResetToken = token;

                    // --- SET HẠN 10 PHÚT ---
                    user.ResetTokenExpiry = DateTime.Now.AddMinutes(10);
                    // ------------------------------------

                    _unitOfWork.Repository<NguoiDung>().Update(user);
                    _unitOfWork.Save();

                    // Gửi Mail
                    string resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Url.Scheme);
                    string subject = "Yêu cầu đặt lại mật khẩu";
                    string body = $"Chào {user.HoTen},<br/>Bạn có 10 phút để đặt lại mật khẩu.<br/>Bấm vào đây: <a href='{resetLink}'>Đặt lại mật khẩu</a>";

                    SendEmail(user.Email, subject, body);
                }

                // Luôn báo thành công để bảo mật (tránh dò email)
                ViewBag.Success = "Vui lòng kiểm tra Email để đặt lại mật khẩu (Link có hiệu lực 10 phút).";
            }
            return View(model);
        }

        // 5. ĐẶT LẠI MẬT KHẨU
        [HttpGet]
        public ActionResult ResetPassword(string token)
        {
            // Tìm user có token khớp VÀ thời gian hết hạn lớn hơn thời gian hiện tại
            var user = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.ResetToken == token).FirstOrDefault();

            if (user == null)
            {
                ViewBag.Error = "Đường dẫn không hợp lệ.";
                return View("Error"); 
            }

            // ---KIỂM TRA HẾT HẠN ---
            if (user.ResetTokenExpiry < DateTime.Now)
            {
                ViewBag.Error = "Đường dẫn đã hết hạn (quá 10 phút). Vui lòng yêu cầu lại.";
                return View("Error");
            }
            // --------------------------------------

            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = _unitOfWork.Repository<NguoiDung>().GetMany(x => x.ResetToken == model.Token).FirstOrDefault();

                if (user != null)
                {
                    // --- KIỂM TRA LẠI LẦN NỮA ---
                    if (user.ResetTokenExpiry < DateTime.Now)
                    {
                        ModelState.AddModelError("", "Phiên làm việc đã hết hạn. Vui lòng thực hiện lại yêu cầu quên mật khẩu.");
                        return View(model);
                    }
                    // -----------------------------------------

                    user.MatKhau = SecurityHelper.MD5Hash(model.MatKhauMoi);

                    // Xóa Token và Hạn sau khi dùng xong
                    user.ResetToken = null;
                    user.ResetTokenExpiry = null;

                    _unitOfWork.Repository<NguoiDung>().Update(user);
                    _unitOfWork.Save();

                    TempData["Success"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "Token không hợp lệ.");
                }
            }
            return View(model);
        }

        // Helper gửi mail 
        private void SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                string host = ConfigurationManager.AppSettings["EmailHost"];
                int port = int.Parse(ConfigurationManager.AppSettings["EmailPort"]);
                string fromEmail = ConfigurationManager.AppSettings["EmailUserName"];
                string password = ConfigurationManager.AppSettings["EmailPassword"];

                var message = new MailMessage();
                message.From = new MailAddress(fromEmail, "Men Store Support");
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject;
                message.IsBodyHtml = true;
                message.Body = body;

                using (var client = new SmtpClient(host, port))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(fromEmail, password);
                    client.Send(message);
                }
            }
            catch (Exception ex)
            {
                 
            }
        }


        // --- HELPER: GỘP GIỎ HÀNG (SESSION -> DB) ---
        private void MergeCart(int userId)
        {
            // Lấy giỏ hàng từ Session (nếu khách vãng lai đã thêm trước khi login)
            var sessionCart = Session["Cart"] as List<CartItemViewModel>;
            if (sessionCart != null && sessionCart.Count > 0)
            {
                // Tìm giỏ hàng của User trong DB
                var dbCart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == userId).FirstOrDefault();
                if (dbCart == null)
                {
                    dbCart = new GioHang { NguoiDungID = userId, NgayTao = DateTime.Now };
                    _unitOfWork.Repository<GioHang>().Add(dbCart);
                    _unitOfWork.Save();
                }

                foreach (var item in sessionCart)
                {
                    var existItem = _unitOfWork.Repository<GioHangChiTiet>()
                        .GetMany(x => x.GioHangID == dbCart.ID && x.BienTheSanPhamID == item.SKU_ID)
                        .FirstOrDefault();

                    if (existItem != null)
                    {
                        existItem.SoLuong += item.SoLuong;
                        _unitOfWork.Repository<GioHangChiTiet>().Update(existItem);
                    }
                    else
                    {
                        _unitOfWork.Repository<GioHangChiTiet>().Add(new GioHangChiTiet
                        {
                            GioHangID = dbCart.ID,
                            BienTheSanPhamID = item.SKU_ID,
                            SoLuong = item.SoLuong
                        });
                    }
                }
                _unitOfWork.Save();

                Session["Cart"] = null;
            }
        }

        // --- HELPER: TÍNH TỔNG SỐ LƯỢNG TỪ DB ---
        private void UpdateCartCountSession(int userId)
        {
            var dbCart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == userId).FirstOrDefault();
            if (dbCart != null)
            {
                var totalQty = _unitOfWork.Repository<GioHangChiTiet>()
                    .GetMany(x => x.GioHangID == dbCart.ID)
                    .Sum(x => x.SoLuong);

                Session["CartCount"] = totalQty;
            }
            else
            {
                Session["CartCount"] = 0;
            }
        }
    }
}