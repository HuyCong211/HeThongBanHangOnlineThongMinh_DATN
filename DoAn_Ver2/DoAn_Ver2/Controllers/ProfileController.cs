using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Web.Mvc;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using DoAn_Ver2.Models.ViewModel;
using DoAn_Ver2.Common;


namespace DoAn_Ver2.Controllers
{
    public class ProfileController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();

        // Middleware kiểm tra đăng nhập
        private NguoiDung GetCurrentUser()
        {
            return Session["KhachHang"] as NguoiDung;
        }

        // 1. TRANG TỔNG QUAN & THÔNG TIN (Dashboard)
        public ActionResult Index()
        {
            var userSession = GetCurrentUser();
            if (userSession == null) return RedirectToAction("Login", "Account");

            var user = _unitOfWork.Repository<NguoiDung>().GetById(userSession.ID);

            var model = new CustomerInfoViewModel
            {
                ID = user.ID,
                HoTen = user.HoTen,
                Email = user.Email,
                SDT = user.SDT,
                Avt = user.Avt
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateInfo(CustomerInfoViewModel model, HttpPostedFileBase uploadAvt)
        {
            var userSession = GetCurrentUser();
            if (userSession == null) return RedirectToAction("Login", "Account");

            var user = _unitOfWork.Repository<NguoiDung>().GetById(userSession.ID);

            if (ModelState.IsValid)
            {
                user.HoTen = model.HoTen;
                user.SDT = model.SDT;
                user.Email = model.Email;

                // Xử lý Avatar
                if (uploadAvt != null && uploadAvt.ContentLength > 0)
                {
                    string _FileName = Path.GetFileName(uploadAvt.FileName);
                    string _path = Path.Combine(Server.MapPath("~/Content/images/users/"), _FileName);
                    if (!Directory.Exists(Server.MapPath("~/Content/images/users/")))
                        Directory.CreateDirectory(Server.MapPath("~/Content/images/users/"));

                    uploadAvt.SaveAs(_path);
                    user.Avt = "/Content/images/users/" + _FileName;
                }

                _unitOfWork.Repository<NguoiDung>().Update(user);
                _unitOfWork.Save();

                // Cập nhật lại Session
                Session["KhachHang"] = user;
                TempData["Success"] = "Cập nhật thông tin thành công!";
            }

            return RedirectToAction("Index");
        }

        // 2. ĐỔI MẬT KHẨU
        public ActionResult ChangePassword()
        {
            if (GetCurrentUser() == null) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var userSession = GetCurrentUser();
            if (userSession == null) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var user = _unitOfWork.Repository<NguoiDung>().GetById(userSession.ID);
                string oldPassHash = SecurityHelper.MD5Hash(model.MatKhauCu);

                if (user.MatKhau != oldPassHash)
                {
                    ModelState.AddModelError("MatKhauCu", "Mật khẩu hiện tại không đúng");
                    return View(model);
                }

                user.MatKhau = SecurityHelper.MD5Hash(model.MatKhauMoi);
                _unitOfWork.Repository<NguoiDung>().Update(user);
                _unitOfWork.Save();

                TempData["Success"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 3. QUẢN LÝ SỔ ĐỊA CHỈ
        public ActionResult Addresses()
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            var list = _unitOfWork.Repository<DiaChi>().GetMany(x => x.NguoiDungID == user.ID).ToList();
            return View(list);
        }

        [HttpPost]
        public ActionResult AddAddress(UserAddressViewModel model)
        {
            var user = GetCurrentUser();
            if (user == null) return Json(new { success = false, msg = "Vui lòng đăng nhập" });

            if (ModelState.IsValid)
            {
                var dc = new DiaChi
                {
                    NguoiDungID = user.ID,
                    TenNguoiNhan = model.TenNguoiNhan,
                    SDT_NguoiNhan = model.SDT,
                    Tinh_ThanhPho = model.TinhThanh,
                    PhuongXa = model.PhuongXa,
                    DiaChiChiTiet = model.DiaChiChiTiet,
                    MacDinh = model.MacDinh
                };

                // Nếu đặt mặc định, bỏ mặc định các cái cũ
                if (model.MacDinh)
                {
                    var oldDefaults = _unitOfWork.Repository<DiaChi>().GetMany(x => x.NguoiDungID == user.ID && x.MacDinh == true);
                    foreach (var item in oldDefaults)
                    {
                        item.MacDinh = false;
                        _unitOfWork.Repository<DiaChi>().Update(item);
                    }
                }

                _unitOfWork.Repository<DiaChi>().Add(dc);
                _unitOfWork.Save();
                return Json(new { success = true });
            }
            return Json(new { success = false, msg = "Dữ liệu không hợp lệ" });
        }

        [HttpPost]
        public ActionResult DeleteAddress(int id)
        {
            var user = GetCurrentUser();
            var dc = _unitOfWork.Repository<DiaChi>().GetById(id);
            if (dc != null && dc.NguoiDungID == user.ID)
            {
                _unitOfWork.Repository<DiaChi>().Delete(dc);
                _unitOfWork.Save();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // 4. LỊCH SỬ ĐƠN HÀNG
        public ActionResult Orders()
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            var list = _unitOfWork.Repository<DonHang>()
                                  .GetMany(x => x.NguoiDungID == user.ID)
                                  .OrderByDescending(x => x.NgayDat)
                                  .ToList();
            return View(list);
        }

        // Chi tiết đơn hàng
        public ActionResult OrderDetail(int id)
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            var donHang = _unitOfWork.Repository<DonHang>().GetById(id);
            if (donHang == null || donHang.NguoiDungID != user.ID) return HttpNotFound();

            // Lấy chi tiết kèm thông tin sản phẩm (Join tay hoặc Include nếu cấu hình)
            // Ở đây mình lấy thủ công để đảm bảo chạy
            var chiTiet = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.DonHangID == id).ToList();

            // ViewBag để truyền danh sách chi tiết sang View
            ViewBag.ChiTiet = chiTiet;

            return View(donHang);
        }
    }
}