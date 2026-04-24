using DoAn_Ver2.Areas.Admin.Attributes;
using DoAn_Ver2.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class BaseController : Controller
    {
        // Khởi tạo UnitOfWork dùng chung cho toàn bộ Admin
        protected UnitOfWork _unitOfWork = new UnitOfWork();

        // Giải phóng tài nguyên khi Controller đóng
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _unitOfWork.Dispose();
            }
            base.Dispose(disposing);
        }

        
    }
}