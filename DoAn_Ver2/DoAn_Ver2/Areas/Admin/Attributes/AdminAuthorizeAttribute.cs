using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using DoAn_Ver2.Models;

namespace DoAn_Ver2.Areas.Admin.Attributes
{
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // 1. Kiểm tra đăng nhập
            var session = HttpContext.Current.Session["UserAdmin"];
            if (session == null)
            {
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Auth", action = "Login", area = "Admin" }));
                return;
            }

            // 2. Kiểm tra quyền hạn
            var user = (NguoiDung)session;

            // Nếu là Admin thì cho qua hết
            if (user.VaiTro == "Admin") return;

            // 3. Nếu là Staff (Nhân viên), kiểm tra Controller được phép truy cập
            if (user.VaiTro == "Staff")
            {
                string currentController = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;

                // Danh sách các Controller nhân viên ĐƯỢC PHÉP vào (Dựa trên yêu cầu của bạn)
                // Lưu ý: Tên Controller phải khớp với tên file class (bỏ chữ Controller)
                var allowedControllers = new List<string>()
                {
                    "Dashboard",
                    "DanhMuc", // Danh mục
                    "SanPham",  // Sản phẩm
                    "MauSac",    // Màu sắc (Nếu bạn đặt tên Controller là ColorController)
                    "KichThuoc",     // Kích thước
                    "Kho",// Kho
                    "DonHang",    // Đơn hàng
                    "News",     // Tin tức
                    "Profile"   // Trang cá nhân
                };

                // Nếu controller hiện tại KHÔNG nằm trong danh sách cho phép -> Đá về Dashboard hoặc trang báo lỗi
                if (!allowedControllers.Contains(currentController))
                {
                    filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Dashboard", action = "Index", area = "Admin" }));
                    // Hoặc bạn có thể tạo trang "AccessDenied" riêng
                }
            }
            else
            {
                // Nếu là Customer (Khách hàng) mà cố tình vào Admin -> Đá ra Login
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Auth", action = "Login", area = "Admin" }));
            }
        }
    }
}