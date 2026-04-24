using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using PagedList;

namespace DoAn_Ver2.Controllers
{
    public class NewsController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        public ActionResult Index(int? page)
        {
            int pageSize = 6; 
            int pageNumber = (page ?? 1);
            var listNews = _unitOfWork.Repository<TinTuc>().GetAll()
                                      .Where(x => x.TrangThai == true)
                                      .OrderByDescending(x => x.NgayDang)
                                      .ToList(); 

            return View(listNews.ToPagedList(pageNumber, pageSize));
        }

        // 2. CHI TIẾT TIN TỨC
        public ActionResult Details(int id)
        {
            var news = _unitOfWork.Repository<TinTuc>().GetById(id);
            if (news == null || news.TrangThai != true)
            {
                return HttpNotFound(); 
            }
            news.LuotXem = (news.LuotXem ?? 0) + 1; 
            _unitOfWork.Repository<TinTuc>().Update(news);
            _unitOfWork.Save();


            // --- [TĂNG TRẢI NGHIỆM] LẤY BÀI VIẾT KHÁC ---
            // Lấy 3 bài viết mới nhất (trừ bài hiện tại)
            var otherNews = _unitOfWork.Repository<TinTuc>().GetAll()
                                       .Where(x => x.TrangThai == true && x.ID != id)
                                       .OrderByDescending(x => x.NgayDang)
                                       .Take(3)
                                       .ToList();

            ViewBag.OtherNews = otherNews;

            return View(news);
        }

        // --- PARTIAL VIEW CHO TRANG CHỦ ---
        public ActionResult _HomeNews()
        {
            var top3News = _unitOfWork.Repository<TinTuc>().GetAll()
                                      .Where(x => x.TrangThai == true)
                                      .OrderByDescending(x => x.NgayDang)
                                      .Take(3)
                                      .ToList();
            return PartialView(top3News);
        }
    }
}