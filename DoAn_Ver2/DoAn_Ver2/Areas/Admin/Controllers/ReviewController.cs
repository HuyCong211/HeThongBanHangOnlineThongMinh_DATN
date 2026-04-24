using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using System.Data.Entity;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class ReviewController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();

        // 1. HIỂN THỊ DANH SÁCH ĐÁNH GIÁ
        public ActionResult Index()
        {
            var reviews = _unitOfWork.Repository<DanhGia>().GetAll()
                                     .OrderByDescending(x => x.NgayDanhGia)
                                     .ToList();
            foreach (var r in reviews)
            {
                if (r.SanPham == null)
                    r.SanPham = _unitOfWork.Repository<SanPham>().GetById(r.SanPhamID);

                if (r.NguoiDung == null)
                    r.NguoiDung = _unitOfWork.Repository<NguoiDung>().GetById(r.NguoiDungID);
            }

            return View(reviews);
        }

        // 2. ẨN / HIỆN ĐÁNH GIÁ 
        [HttpPost]
        public ActionResult ToggleStatus(int id)
        {
            try
            {
                var review = _unitOfWork.Repository<DanhGia>().GetById(id);
                if (review == null)
                    return Json(new { success = false, msg = "Không tìm thấy đánh giá!" });
                bool currentStatus = review.TrangThai ?? true;
                review.TrangThai = !currentStatus;

                _unitOfWork.Repository<DanhGia>().Update(review);
                _unitOfWork.Save();

                return Json(new
                {
                    success = true,
                    msg = review.TrangThai == true ? "Đã HIỆN đánh giá này." : "Đã ẨN đánh giá này.",
                    status = review.TrangThai
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // 3. TRẢ LỜI ĐÁNH GIÁ 
        [HttpPost]
        [ValidateInput(false)] 
        public ActionResult ReplyReview(int id, string replyContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(replyContent))
                    return Json(new { success = false, msg = "Nội dung trả lời không được để trống!" });

                var review = _unitOfWork.Repository<DanhGia>().GetById(id);
                if (review == null)
                    return Json(new { success = false, msg = "Không tìm thấy đánh giá!" });

                review.PhanHoi = replyContent;
                review.NgayPhanHoi = DateTime.Now;

                _unitOfWork.Repository<DanhGia>().Update(review);
                _unitOfWork.Save();

                return Json(new { success = true, msg = "Đã gửi phản hồi thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Lỗi: " + ex.Message });
            }
        }
    }
}