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

                // Danh sách các Controller nhân viên ĐƯỢC PHÉP vào
                var allowedControllers = new List<string>()
                {
                    "Dashboard",
                    "DanhMuc", 
                    "SanPham",  
                    "MauSac",    
                    "KichThuoc",     
                    "Kho",
                    "DonHang",    
                    "News",     
                    "Profile",
                    "Review"
                };

                
                if (!allowedControllers.Contains(currentController))
                {
                    filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Dashboard", action = "Index", area = "Admin" }));
                    
                }
            }
            else
            {
                
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Auth", action = "Login", area = "Admin" }));
            }
        }
    }
}