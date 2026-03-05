using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using DoAn_Ver2.Models.ViewModel;
using PagedList;

namespace DoAn_Ver2.Controllers
{
    public class WishlistController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();

        // 1. HIỂN THỊ DANH SÁCH YÊU THÍCH
        public ActionResult Index(int? page)
        {
            // --- CẤU HÌNH ---
            int pageSize = 8; // 8 sản phẩm/trang
            int pageNumber = (page ?? 1);

            List<int> wishIds = new List<int>();

            // A. LẤY LIST ID TỪ NGUỒN (DB hoặc Cookie)
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                // Lấy từ DB, sắp xếp ngày thêm mới nhất
                wishIds = _unitOfWork.Repository<SanPhamYeuThich>()
                                     .GetMany(x => x.NguoiDungID == user.ID)
                                     .OrderByDescending(x => x.NgayThem)
                                     .Select(x => x.SanPhamID)
                                     .ToList();
            }
            else
            {
                var cookie = Request.Cookies["Wishlist"];
                if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                {
                    try
                    {
                        // Cookie lưu dạng chuỗi "1,2,3". Cần đảo ngược để cái mới thêm (thường add vào cuối) lên đầu
                        var list = cookie.Value.Split(',').Select(int.Parse).ToList();
                        list.Reverse();
                        wishIds = list;
                    }
                    catch { }
                }
            }

            // B. TÍNH TOÁN PHÂN TRANG
            int totalItems = wishIds.Count;
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Kiểm tra pageNumber hợp lệ
            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            // C. LẤY DỮ LIỆU TRANG HIỆN TẠI (Skip & Take)
            var pagedIds = wishIds.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var products = new List<SanPham>();
            if (pagedIds.Any())
            {
                // Lấy thông tin sản phẩm từ DB
                var raw = _unitOfWork.Repository<SanPham>()
                                     .GetMany(x => pagedIds.Contains(x.ID) && x.TrangThai == 1)
                                     .ToList();

                // Sắp xếp lại danh sách sản phẩm theo đúng thứ tự của pagedIds
                foreach (var id in pagedIds)
                {
                    var p = raw.FirstOrDefault(x => x.ID == id);
                    if (p != null) products.Add(p);
                }

                // Lấy ảnh đại diện
                foreach (var p in products)
                {
                    var img = _unitOfWork.Repository<AnhSanPham>()
                                         .GetMany(x => x.SanPhamID == p.ID && x.MacDinh == true)
                                         .FirstOrDefault();
                    // Lưu URL ảnh vào ViewData để View dùng
                    ViewData["Img_" + p.ID] = img != null ? img.URL : "https://via.placeholder.com/300";
                }
            }

            // D. TRUYỀN DỮ LIỆU SANG VIEW
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;

            return View(products);
        }

        // 2. THÊM / XÓA SẢN PHẨM (TOGGLE)
        [HttpPost]
        public ActionResult Toggle(int productId)
        {
            try
            {
                bool isAdded = false;
                int count = 0;

                if (Session["KhachHang"] != null)
                {
                    // MEMBER: Lưu DB
                    var user = (NguoiDung)Session["KhachHang"];
                    var repo = _unitOfWork.Repository<SanPhamYeuThich>();
                    var item = repo.GetMany(x => x.NguoiDungID == user.ID && x.SanPhamID == productId).FirstOrDefault();

                    if (item == null)
                    {
                        repo.Add(new SanPhamYeuThich { NguoiDungID = user.ID, SanPhamID = productId, NgayThem = DateTime.Now });
                        isAdded = true;
                    }
                    else
                    {
                        repo.Delete(item);
                        isAdded = false;
                    }
                    _unitOfWork.Save();
                    count = repo.GetMany(x => x.NguoiDungID == user.ID).Count();
                }
                else
                {
                    // GUEST: Lưu Cookie
                    var cookie = Request.Cookies["Wishlist"];
                    var ids = new List<int>();
                    if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                    {
                        try { ids = cookie.Value.Split(',').Select(int.Parse).ToList(); } catch { }
                    }

                    if (!ids.Contains(productId))
                    {
                        ids.Add(productId);
                        isAdded = true;
                    }
                    else
                    {
                        ids.Remove(productId);
                        isAdded = false;
                    }

                    var newCookie = new HttpCookie("Wishlist", string.Join(",", ids));
                    newCookie.Expires = DateTime.Now.AddDays(30);
                    Response.Cookies.Add(newCookie);
                    count = ids.Count;
                }

                return Json(new { success = true, isAdded = isAdded, count = count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        // 3. ACTION CHILD CHO LAYOUT (ĐẾM SỐ LƯỢNG)
        [ChildActionOnly]
        public ActionResult _HeaderWishlist()
        {
            int count = 0;
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                count = _unitOfWork.Repository<SanPhamYeuThich>().GetMany(x => x.NguoiDungID == user.ID).Count();
            }
            else
            {
                var cookie = Request.Cookies["Wishlist"];
                if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                {
                    try { count = cookie.Value.Split(',').Length; } catch { }
                }
            }
            return PartialView(count); ;
        }
    }
}