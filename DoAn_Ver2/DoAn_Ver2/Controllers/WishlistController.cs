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
        public ActionResult Index(int? page)
        {
            int pageSize = 8; 
            int pageNumber = (page ?? 1);

            List<int> wishIds = new List<int>();
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
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
                        var list = cookie.Value.Split(',').Select(int.Parse).ToList();
                        list.Reverse();
                        wishIds = list;
                    }
                    catch { }
                }
            }
            int totalItems = wishIds.Count;
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;
            var pagedIds = wishIds.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var products = new List<SanPham>();
            if (pagedIds.Any())
            {
                var raw = _unitOfWork.Repository<SanPham>()
                                     .GetMany(x => pagedIds.Contains(x.ID) && x.TrangThai == 1)
                                     .ToList();

                foreach (var id in pagedIds)
                {
                    var p = raw.FirstOrDefault(x => x.ID == id);
                    if (p != null) products.Add(p);
                }
                foreach (var p in products)
                {
                    var img = _unitOfWork.Repository<AnhSanPham>()
                                         .GetMany(x => x.SanPhamID == p.ID && x.MacDinh == true)
                                         .FirstOrDefault();
                    ViewData["Img_" + p.ID] = img != null ? img.URL : "https://via.placeholder.com/300";
                }
            }

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;

            return View(products);
        }

        // 2. THÊM / XÓA SẢN PHẨM
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