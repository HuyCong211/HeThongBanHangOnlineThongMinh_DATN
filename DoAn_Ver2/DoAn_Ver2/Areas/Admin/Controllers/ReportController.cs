using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    
    public class ReportController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Admin/Report
        public ActionResult Index()
        {
            // 1. THỐNG KÊ SƠ BỘ ĐƠN HÀNG
            var allOrders = _unitOfWork.Repository<DonHang>().GetAll();

            ViewBag.TotalOrders = allOrders.Count();
            ViewBag.SuccessOrders = allOrders.Count(x => x.TrangThaiDonHang == 3); // Hoàn thành
            ViewBag.PendingOrders = allOrders.Count(x => x.TrangThaiDonHang == 0 || x.TrangThaiDonHang == 1); // Chờ/Đã xác nhận
            ViewBag.CancelOrders = allOrders.Count(x => x.TrangThaiDonHang == 4); // Hủy

            return View();
        }
        // 2. JSON: BIỂU ĐỒ DOANH THU (CẬP NHẬT)
        [HttpGet]
        // [HttpGet] GetRevenueChart
        public JsonResult GetRevenueChart(string mode, string fromDate, string toDate, int? month, int? year)
        {
            var dbData = _unitOfWork.Repository<DonHang>().GetAll().Where(x => x.TrangThaiDonHang == 3); // Đơn thành công

            // --- MODE: THEO THÁNG (Xem từng ngày trong tháng) ---
            if (mode == "MONTH" && month.HasValue && year.HasValue)
            {
                // 1. Lấy dữ liệu từ DB
                var data = dbData.Where(x => x.NgayDat.Value.Month == month && x.NgayDat.Value.Year == year)
                    .GroupBy(x => x.NgayDat.Value.Day)
                    .Select(g => new { Day = g.Key, Rev = g.Sum(x => x.TongThanhToan) ?? 0 })
                    .ToList();

                // 2. Tạo danh sách đầy đủ các ngày trong tháng (Lấp đầy ngày trống)
                int daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
                var fullList = new List<object>();

                for (int d = 1; d <= daysInMonth; d++)
                {
                    var record = data.FirstOrDefault(x => x.Day == d);
                    fullList.Add(new
                    {
                        Label = "Ngày " + d + "/" + month, // Nhãn: "Ngày 1/12"
                        Value = record != null ? record.Rev : 0
                    });
                }
                return Json(fullList, JsonRequestBehavior.AllowGet);
            }

            // --- MODE: THEO NĂM (Xem 12 tháng) ---
            else if (mode == "YEAR" && year.HasValue)
            {
                var data = dbData.Where(x => x.NgayDat.Value.Year == year)
                    .GroupBy(x => x.NgayDat.Value.Month)
                    .Select(g => new { Month = g.Key, Rev = g.Sum(x => x.TongThanhToan) ?? 0 })
                    .ToList();

                var fullList = new List<object>();
                for (int m = 1; m <= 12; m++)
                {
                    var record = data.FirstOrDefault(x => x.Month == m);
                    fullList.Add(new
                    {
                        Label = "Tháng " + m + "/" + year, // Nhãn: "Tháng 1/2025"
                        Value = record != null ? record.Rev : 0
                    });
                }
                return Json(fullList, JsonRequestBehavior.AllowGet);
            }

            // --- MODE: THEO NGÀY (Khoảng ngày tùy chọn) ---
            else
            {
                DateTime dtFrom = DateTime.Today.AddDays(-7);
                DateTime dtTo = DateTime.Now;
                if (!string.IsNullOrEmpty(fromDate)) DateTime.TryParse(fromDate, out dtFrom);
                if (!string.IsNullOrEmpty(toDate)) { DateTime.TryParse(toDate, out dtTo); dtTo = dtTo.AddDays(1).AddTicks(-1); }

                var data = dbData.Where(x => x.NgayDat >= dtFrom && x.NgayDat <= dtTo)
                     .GroupBy(x => System.Data.Entity.DbFunctions.TruncateTime(x.NgayDat))
                     .Select(g => new { Date = g.Key, Rev = g.Sum(x => x.TongThanhToan) ?? 0 })
                     .ToList();

                // Lấp đầy các ngày không có đơn
                var fullList = new List<object>();
                for (var day = dtFrom.Date; day <= dtTo.Date; day = day.AddDays(1))
                {
                    var record = data.FirstOrDefault(x => x.Date == day);
                    fullList.Add(new
                    {
                        Label = day.ToString("dd/MM/yyyy"),
                        Value = record != null ? record.Rev : 0
                    });
                }
                return Json(fullList, JsonRequestBehavior.AllowGet);
            }
        }

        // 3. JSON: BIỂU ĐỒ SẢN PHẨM
        [HttpGet]
        public JsonResult GetProductChart(string type)
        {
            object data = null;

            if (type == "TOP_SOLD")
            {
                // Top bán chạy (theo số lượng)
                // [CẬP NHẬT]: Ưu tiên sản phẩm bán gần đây nhất nếu số lượng bằng nhau
                data = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    .Where(x => x.DonHang.TrangThaiDonHang == 3)
                    .GroupBy(x => x.BienTheSanPham.SanPham.TenSanPham)
                    .Select(g => new
                    {
                        Label = g.Key,
                        Value = g.Sum(x => x.SoLuong),
                        LastSoldDate = g.Max(x => x.DonHang.NgayDat) // Lấy ngày bán gần nhất
                    })
                    // Sắp xếp: Số lượng giảm dần -> Ngày bán giảm dần (Mới nhất lên trước)
                    .OrderByDescending(x => x.Value)
                    .ThenByDescending(x => x.LastSoldDate)
                    .Take(10)
                    .Select(x => new { x.Label, x.Value }) // Chỉ lấy lại Label và Value để trả về JSON cho gọn
                    .ToList();
            }
            else if (type == "TOP_VIEW")
            {
                // Top xem nhiều
                data = _unitOfWork.Repository<SanPham>().GetAll()
                    .OrderByDescending(x => x.LuotXem).Take(10)
                    .Select(x => new { Label = x.TenSanPham, Value = x.LuotXem ?? 0 }).ToList();
            }
            else if (type == "NO_SALE")
            {
                // [SỬA LOGIC]: Sản phẩm có lượt xem cao nhưng CHƯA BÁN ĐƯỢC cái nào

                // 1. Lấy danh sách ID sản phẩm đã bán được (trong đơn hoàn thành)
                var soldProductIds = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    .Where(x => x.DonHang.TrangThaiDonHang == 3)
                    .Select(x => x.BienTheSanPham.SanPhamID)
                    .Distinct()
                    .ToList();

                // 2. Lấy sản phẩm KHÔNG nằm trong danh sách đã bán -> Sắp xếp theo Lượt Xem giảm dần
                data = _unitOfWork.Repository<SanPham>().GetAll()
                    .Where(x => !soldProductIds.Contains(x.ID))
                    .OrderByDescending(x => x.LuotXem) // Quan trọng: Xem nhiều mà ko mua
                    .Take(10)
                    .Select(x => new { Label = x.TenSanPham, Value = x.LuotXem ?? 0 })
                    .ToList();
            }
            else if (type == "TOP_CANCEL")
            {
                data = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    .Where(x => x.DonHang.TrangThaiDonHang == 4)
                    .GroupBy(x => x.BienTheSanPham.SanPham.TenSanPham)
                    .Select(g => new { Label = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value).Take(10).ToList();
            }

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        // 4. XUẤT EXCEL (DOANH THU) - ĐÃ FIX LỖI FONT
        public void ExportRevenue(string fromDate, string toDate)
        {
            DateTime dtFrom = DateTime.Today.AddDays(-30);
            DateTime dtTo = DateTime.Now;
            if (!string.IsNullOrEmpty(fromDate)) DateTime.TryParse(fromDate, out dtFrom);
            if (!string.IsNullOrEmpty(toDate)) { DateTime.TryParse(toDate, out dtTo); dtTo = dtTo.AddDays(1).AddTicks(-1); }

            var data = _unitOfWork.Repository<DonHang>().GetAll()
                .Where(x => x.TrangThaiDonHang == 3 && x.NgayDat >= dtFrom && x.NgayDat <= dtTo)
                .Select(x => new {
                    MaDonHang = x.MaDonHang,
                    NgayDat = x.NgayDat,
                    KhachHang = x.TenNguoiNhan,
                    SDT = x.SDT_NguoiNhan,
                    TongTien = x.TongThanhToan
                })
                .OrderByDescending(x => x.NgayDat)
                .ToList();

            var grid = new GridView();
            grid.DataSource = data;
            grid.DataBind();

            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=BaoCaoDoanhThu.xls");
            Response.ContentType = "application/ms-excel";

            // [QUAN TRỌNG] Fix lỗi font tiếng Việt
            Response.ContentEncoding = System.Text.Encoding.Unicode;
            Response.BinaryWrite(System.Text.Encoding.Unicode.GetPreamble());

            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);

            // Thêm thẻ meta utf-8 vào đầu file HTML để chắc chắn
            sw.Write("<meta http-equiv='content-type' content='text/html; charset=utf-8' />");

            grid.RenderControl(htw);
            Response.Write(sw.ToString());
            Response.End();
        }

        // 5. XUẤT EXCEL (SẢN PHẨM) - ĐÃ FIX LỖI FONT
        public void ExportProduct(string type)
        {
            object data = null;

            if (type == "TOP_SOLD")
            {
                data = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    .Where(x => x.DonHang.TrangThaiDonHang == 3)
                    .GroupBy(x => x.BienTheSanPham.SanPham.TenSanPham)
                    .Select(g => new
                    {
                        TenSanPham = g.Key,
                        SoLuongBan = g.Sum(x => x.SoLuong),
                        DoanhThu = g.Sum(x => x.ThanhTien),
                        LastSoldDate = g.Max(x => x.DonHang.NgayDat) // Lấy ngày bán gần nhất để sort
                    })
                    // Sắp xếp: Số lượng giảm dần -> Ngày bán giảm dần
                    .OrderByDescending(x => x.SoLuongBan)
                    .ThenByDescending(x => x.LastSoldDate)
                    .Take(50)
                    // Select lại để loại bỏ cột LastSoldDate khỏi file Excel (nếu không muốn hiện)
                    .Select(x => new { x.TenSanPham, x.SoLuongBan, x.DoanhThu })
                    .ToList();
            }
            else if (type == "TOP_VIEW")
            {
                data = _unitOfWork.Repository<SanPham>().GetAll()
                    .OrderByDescending(x => x.LuotXem).Take(50)
                    .Select(x => new { TenSanPham = x.TenSanPham, LuotXem = x.LuotXem, GiaBan = x.GiaGoc }).ToList();
            }
            else if (type == "NO_SALE")
            {
                var soldProductIds = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                   .Where(x => x.DonHang.TrangThaiDonHang == 3)
                   .Select(x => x.BienTheSanPham.SanPhamID).Distinct().ToList();

                data = _unitOfWork.Repository<SanPham>().GetAll()
                    .Where(x => !soldProductIds.Contains(x.ID))
                    .OrderByDescending(x => x.LuotXem)
                    .Select(x => new { TenSanPham = x.TenSanPham, LuotXem = x.LuotXem, TinhTrang = "Chưa bán được" })
                    .Take(50).ToList();
            }
            else // TOP_CANCEL
            {
                data = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    .Where(x => x.DonHang.TrangThaiDonHang == 4)
                    .GroupBy(x => x.BienTheSanPham.SanPham.TenSanPham)
                    .Select(g => new { TenSanPham = g.Key, SoLanHuy = g.Count() })
                    .OrderByDescending(x => x.SoLanHuy).Take(50).ToList();
            }

            var grid = new GridView();
            grid.DataSource = data;
            grid.DataBind();

            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=BaoCaoSanPham_" + type + ".xls");
            Response.ContentType = "application/ms-excel";

            // [QUAN TRỌNG] Fix lỗi font tiếng Việt
            Response.ContentEncoding = System.Text.Encoding.Unicode;
            Response.BinaryWrite(System.Text.Encoding.Unicode.GetPreamble());

            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);

            sw.Write("<meta http-equiv='content-type' content='text/html; charset=utf-8' />");

            grid.RenderControl(htw);
            Response.Write(sw.ToString());
            Response.End();
        }

    }
}