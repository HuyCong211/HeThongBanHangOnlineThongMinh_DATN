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
            // Lấy tất cả đánh giá, sắp xếp mới nhất lên đầu
            var reviews = _unitOfWork.Repository<DanhGia>().GetAll()
                                     .OrderByDescending(x => x.NgayDanhGia)
                                     .ToList();

            // Load thủ công thông tin Sản phẩm và Khách hàng để tránh lỗi Lazy Loading (nếu có)
            foreach (var r in reviews)
            {
                if (r.SanPham == null)
                    r.SanPham = _unitOfWork.Repository<SanPham>().GetById(r.SanPhamID);

                if (r.NguoiDung == null)
                    r.NguoiDung = _unitOfWork.Repository<NguoiDung>().GetById(r.NguoiDungID);
            }

            return View(reviews);
        }

        // 2. ẨN / HIỆN ĐÁNH GIÁ (AJAX)
        // Chỉ thay đổi trạng thái, KHÔNG XÓA
        [HttpPost]
        public ActionResult ToggleStatus(int id)
        {
            try
            {
                var review = _unitOfWork.Repository<DanhGia>().GetById(id);
                if (review == null)
                    return Json(new { success = false, msg = "Không tìm thấy đánh giá!" });

                // Đảo ngược trạng thái: Nếu đang Hiện (true) -> thành Ẩn (false) và ngược lại
                // Mặc định nếu null coi như là đang Hiện (true)
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

        // 3. TRẢ LỜI ĐÁNH GIÁ (AJAX)
        [HttpPost]
        [ValidateInput(false)] // Cho phép gửi HTML nếu cần
        public ActionResult ReplyReview(int id, string replyContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(replyContent))
                    return Json(new { success = false, msg = "Nội dung trả lời không được để trống!" });

                var review = _unitOfWork.Repository<DanhGia>().GetById(id);
                if (review == null)
                    return Json(new { success = false, msg = "Không tìm thấy đánh giá!" });

                // Cập nhật nội dung phản hồi
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