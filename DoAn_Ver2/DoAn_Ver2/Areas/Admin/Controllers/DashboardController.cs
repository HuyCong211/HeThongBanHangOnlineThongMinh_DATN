using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class DashboardController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Admin/Dashboard
        public ActionResult Index()
        {
            ViewBag.Header = "Bảng điều khiển";
            ViewBag.Title = "Dashboard";

            // 1. THỐNG KÊ SỐ LIỆU (CARD)
            ViewBag.CountProduct = _unitOfWork.Repository<SanPham>().GetAll().Count();
            ViewBag.CountOrder = _unitOfWork.Repository<DonHang>().GetAll().Count();
            ViewBag.CountUser = _unitOfWork.Repository<NguoiDung>().GetAll().Count();
            ViewBag.CountStock = _unitOfWork.Repository<BienTheSanPham>().GetAll().Count();

            // 2. LẤY TÊN ADMIN 
            if (Session["UserAdmin"] != null)
            {
                var admin = (NguoiDung)Session["UserAdmin"];
                ViewBag.AdminName = admin.HoTen ?? admin.TenDangNhap;
            }
            else
            {
                ViewBag.AdminName = "Quản trị viên";
            }

            // --- 3. ĐƠN HÀNG MỚI  ---
            var newOrders = _unitOfWork.Repository<DonHang>().GetAll()
                .Where(x => x.TrangThaiDonHang == 0) 
                .OrderByDescending(x => x.NgayDat)
                .ToList();

            // 4. CẢNH BÁO SẮP HẾT HÀNG
            var lowStockProducts = _unitOfWork.Repository<BienTheSanPham>().GetAll()
                .Include(x => x.SanPham)   
                .Include(x => x.MauSac)    
                .Include(x => x.KichThuoc) 
                .Where(x => x.SoLuong < 5)
                .OrderBy(x => x.SoLuong)
                .Take(5)
                .ToList();

            ViewBag.LowStockList = lowStockProducts;



            // ==========================================
            // GỌI AI LẤY INSIGHT CHO BẢNG ĐIỀU KHIỂN
            // ==========================================
            try
            {
                using (var client = new HttpClient())
                {
                    string url = "http://127.0.0.1:5000/admin_insights";
                    var response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = response.Content.ReadAsStringAsync().Result;
                        dynamic aiData = JsonConvert.DeserializeObject(jsonResponse);

                        if (aiData.success == true)
                        {
                           
                            ViewBag.WarningProducts = aiData.warning_products;
                            ViewBag.TrendingKeywords = aiData.trending_keywords;
                            ViewBag.BestSellers = aiData.best_sellers;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Nếu Python tắt, Dashboard vẫn load bình thường không bị sập
                System.Diagnostics.Debug.WriteLine("LỖI GỌI AI DASHBOARD: " + ex.Message);
            }


            return View(newOrders);
        }
    }
}