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
            // Tổng sản phẩm (Đếm số bản ghi trong bảng SanPham)
            ViewBag.CountProduct = _unitOfWork.Repository<SanPham>().GetAll().Count();

            // Tổng đơn hàng (Đếm số bản ghi trong bảng DonHang)
            ViewBag.CountOrder = _unitOfWork.Repository<DonHang>().GetAll().Count();

            // Tổng người dùng (Đếm số bản ghi trong bảng NguoiDung)
            ViewBag.CountUser = _unitOfWork.Repository<NguoiDung>().GetAll().Count();

            // Tổng tồn kho (Đếm số bản ghi SKU/Biến thể trong kho - theo yêu cầu của bạn)
            ViewBag.CountStock = _unitOfWork.Repository<BienTheSanPham>().GetAll().Count();

            // 2. LẤY TÊN ADMIN (Hiển thị lời chào)
            if (Session["UserAdmin"] != null)
            {
                var admin = (NguoiDung)Session["UserAdmin"];
                ViewBag.AdminName = admin.HoTen ?? admin.TenDangNhap;
            }
            else
            {
                ViewBag.AdminName = "Quản trị viên";
            }

            // --- 3. ĐƠN HÀNG MỚI (CHỈ LẤY ĐƠN CHỜ XÁC NHẬN) ---
            var newOrders = _unitOfWork.Repository<DonHang>().GetAll()
                .Where(x => x.TrangThaiDonHang == 0) // [SỬA] Chỉ lấy đơn Chờ xác nhận (0)
                .OrderByDescending(x => x.NgayDat)
                .ToList();

            // 4. CẢNH BÁO SẮP HẾT HÀNG (Lấy SP có số lượng < 10)
            var lowStockProducts = _unitOfWork.Repository<BienTheSanPham>().GetAll()
                .Include(x => x.SanPham)   // Nạp tên sản phẩm
                .Include(x => x.MauSac)    // Nạp tên màu
                .Include(x => x.KichThuoc) // Nạp tên size
                .Where(x => x.SoLuong < 5)
                .OrderBy(x => x.SoLuong)
                .Take(5)
                .ToList();

            ViewBag.LowStockList = lowStockProducts;



            // ==========================================
            // [PHẦN MỚI] GỌI AI LẤY INSIGHT CHO BẢNG ĐIỀU KHIỂN
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
                            // Truyền dữ liệu phân tích ra View
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