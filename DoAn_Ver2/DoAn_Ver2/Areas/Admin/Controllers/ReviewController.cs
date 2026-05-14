using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using System.Data.Entity;
using PagedList;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class ReviewController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();

        // 1. HIỂN THỊ DANH SÁCH ĐÁNH GIÁ
        public ActionResult Index(int? page, int? pageSize, string searchName = "", int? filterStar = null, string filterStatus = "")
        {
            int currentPage = page ?? 1;
            int size = pageSize ?? 10;

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

            // Bộ lọc tìm kiếm
            if (!string.IsNullOrWhiteSpace(searchName))
                reviews = reviews.Where(x =>
                    (x.SanPham != null && x.SanPham.TenSanPham.ToLower().Contains(searchName.ToLower())) ||
                    (x.NguoiDung != null && x.NguoiDung.HoTen.ToLower().Contains(searchName.ToLower()))
                ).ToList();

            if (filterStar.HasValue)
                reviews = reviews.Where(x => x.SoSao == filterStar.Value).ToList();

            if (filterStatus == "show")
                reviews = reviews.Where(x => x.TrangThai == true || x.TrangThai == null).ToList();
            else if (filterStatus == "hide")
                reviews = reviews.Where(x => x.TrangThai == false).ToList();

            // Truyền ViewBag để giữ lại bộ lọc
            ViewBag.SearchName = searchName;
            ViewBag.FilterStar = filterStar;
            ViewBag.FilterStatus = filterStatus;
            ViewBag.PageSize = size;
            ViewBag.TotalCount = reviews.Count;

            return View(reviews.ToPagedList(currentPage, size));
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