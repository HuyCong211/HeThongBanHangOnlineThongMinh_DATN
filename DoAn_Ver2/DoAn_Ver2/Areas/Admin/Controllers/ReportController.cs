using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.WebControls;
using ClosedXML.Excel;

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
            ViewBag.SuccessOrders = allOrders.Count(x => x.TrangThaiDonHang == 3); 
            ViewBag.PendingOrders = allOrders.Count(x => x.TrangThaiDonHang == 0 || x.TrangThaiDonHang == 1); 
            ViewBag.CancelOrders = allOrders.Count(x => x.TrangThaiDonHang == 4); 

            return View();
        }
        // 2. JSON: BIỂU ĐỒ DOANH THU 
        [HttpGet]
        public JsonResult GetRevenueChart(string mode, string fromDate, string toDate, int? month, int? year)
        {
            var dbData = _unitOfWork.Repository<DonHang>().GetAll().Where(x => x.TrangThaiDonHang == 3); 

            // --- MODE: THEO THÁNG (Xem từng ngày trong tháng) ---
            if (mode == "MONTH" && month.HasValue && year.HasValue)
            {
                var data = dbData.Where(x => x.NgayDat.Value.Month == month && x.NgayDat.Value.Year == year)
                    .GroupBy(x => x.NgayDat.Value.Day)
                    .Select(g => new { Day = g.Key, Rev = g.Sum(x => x.TongThanhToan) ?? 0 })
                    .ToList();

                int daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
                var fullList = new List<object>();

                for (int d = 1; d <= daysInMonth; d++)
                {
                    var record = data.FirstOrDefault(x => x.Day == d);
                    fullList.Add(new
                    {
                        Label = "Ngày " + d + "/" + month, 
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
                        Label = "Tháng " + m + "/" + year, 
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
                // Ưu tiên sản phẩm bán gần đây nhất nếu số lượng bằng nhau
                data = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    .Where(x => x.DonHang.TrangThaiDonHang == 3)
                    .GroupBy(x => x.BienTheSanPham.SanPham.TenSanPham)
                    .Select(g => new
                    {
                        Label = g.Key,
                        Value = g.Sum(x => x.SoLuong),
                        LastSoldDate = g.Max(x => x.DonHang.NgayDat) 
                    })
                    // Sắp xếp: Số lượng giảm dần -> Ngày bán giảm dần (Mới nhất lên trước)
                    .OrderByDescending(x => x.Value)
                    .ThenByDescending(x => x.LastSoldDate)
                    .Take(10)
                    .Select(x => new { x.Label, x.Value }) 
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

                // 1. Lấy danh sách ID sản phẩm đã bán được (trong đơn hoàn thành)
                var soldProductIds = _unitOfWork.Repository<ChiTietDonHang>().GetAll()
                    .Where(x => x.DonHang.TrangThaiDonHang == 3)
                    .Select(x => x.BienTheSanPham.SanPhamID)
                    .Distinct()
                    .ToList();

                // 2. Lấy sản phẩm KHÔNG nằm trong danh sách đã bán -> Sắp xếp theo Lượt Xem giảm dần
                data = _unitOfWork.Repository<SanPham>().GetAll()
                    .Where(x => !soldProductIds.Contains(x.ID))
                    .OrderByDescending(x => x.LuotXem) 
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

        // 4. XUẤT EXCEL DOANH THU
        public ActionResult ExportRevenue(string mode, string fromDate, string toDate, int? month, int? year)
        {
            // ── Lấy tên admin đang đăng nhập ──────────────────────────
            // BaseController lưu thông tin session vào CurrentUser (NguoiDung model).
            // Thay "CurrentUser" bằng cách bạn đang lấy user session nếu khác.
            string exporterName = "Quản trị viên";
            var currentUser = Session["UserAdmin"] as NguoiDung
                           ?? Session["CurrentUser"] as NguoiDung;
            if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.HoTen))
                exporterName = currentUser.HoTen;

            // ── Tiêu đề báo cáo & lọc dữ liệu ────────────────────────
            string reportTitle;
            IQueryable<DonHang> query = _unitOfWork.Repository<DonHang>().GetAll()
                                                    .Where(x => x.TrangThaiDonHang == 3);

            if (mode == "MONTH" && month.HasValue && year.HasValue)
            {
                query = query.Where(x => x.NgayDat.Value.Month == month.Value
                                      && x.NgayDat.Value.Year == year.Value);
                reportTitle = $"DOANH THU THÁNG {month}/{year}";
            }
            else if (mode == "YEAR" && year.HasValue)
            {
                query = query.Where(x => x.NgayDat.Value.Year == year.Value);
                reportTitle = $"DOANH THU NĂM {year}";
            }
            else
            {
                // Chế độ DAY (khoảng ngày)
                DateTime dtFrom = DateTime.Today.AddDays(-7);
                DateTime dtTo = DateTime.Today;
                if (!string.IsNullOrEmpty(fromDate)) DateTime.TryParse(fromDate, out dtFrom);
                if (!string.IsNullOrEmpty(toDate)) DateTime.TryParse(toDate, out dtTo);
                DateTime dtToEnd = dtTo.AddDays(1).AddTicks(-1);

                query = query.Where(x => x.NgayDat >= dtFrom && x.NgayDat <= dtToEnd);
                reportTitle = $"BÁO CÁO DOANH THU TỪ NGÀY {dtFrom:dd/MM/yyyy} ĐẾN NGÀY {dtTo:dd/MM/yyyy}";
            }

            // ── Dữ liệu chi tiết ──────────────────────────────────────
            var orders = query.OrderByDescending(x => x.NgayDat)
                .Select(x => new
                {
                    x.MaDonHang,
                    x.NgayDat,
                    x.TenNguoiNhan,
                    x.SDT_NguoiNhan,
                    // Sản phẩm: ghép tên từ chi tiết đơn hàng
                    SanPhams = x.ChiTietDonHangs.Select(ct => ct.BienTheSanPham.SanPham.TenSanPham).Distinct(),
                    TongTien = x.TongTien ?? 0,   // Tiền chưa trừ giảm giá
                    PhiVanChuyen = x.PhiVanChuyen ?? 0,
                    GiamGia = x.GiamGia ?? 0,   // Tiền giảm giá
                    TongThanhToan = x.TongThanhToan ?? 0,   // Thực thu (đã trừ giảm giá)
                    x.MaGiamGiaApDung
                })
                .ToList();

            // ── Tổng hợp ──────────────────────────────────────────────
            decimal sumTongTien = orders.Sum(x => x.TongTien);
            decimal sumPhiVanChuyen = orders.Sum(x => x.PhiVanChuyen);
            decimal sumGiamGia = orders.Sum(x => x.GiamGia);
            decimal sumThucThu = orders.Sum(x => x.TongThanhToan);

            // ── Tạo workbook với ClosedXML ────────────────────────────
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Báo cáo doanh thu");

                // ━━━━━ DÒNG 1: Tiêu đề chính ━━━━━
                ws.Cell(1, 1).Value = reportTitle;
                var titleRange = ws.Range(1, 1, 1, 9);
                titleRange.Merge();
                titleRange.Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(14)
                    .Font.SetFontColor(XLColor.FromArgb(0x1F, 0x38, 0x64))   // xanh đậm
                    .Fill.SetBackgroundColor(XLColor.FromArgb(0xBD, 0xD7, 0xEE))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Row(1).Height = 30;

                // ━━━━━ DÒNG 2: Trống (khoảng cách) ━━━━━
                ws.Row(2).Height = 6;

                // ━━━━━ DÒNG 3–4: Thông tin người xuất & tổng hợp ━━━━━
                // Cột trái
                ws.Cell(3, 1).Value = "Người xuất báo cáo";
                ws.Cell(4, 1).Value = exporterName;
                ws.Cell(3, 3).Value = "Ngày xuất báo cáo";
                ws.Cell(4, 3).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                // Cột phải — tổng hợp (căn giữa theo hàng 3 & 4)
                // Tổng tiền đơn hàng (chưa trừ giảm giá)
                ws.Cell(3, 6).Value = "Tổng tiền đơn hàng";
                ws.Cell(3, 8).Value = sumTongTien;
                // Phí vận chuyển
                ws.Cell(3, 7).Value = ""; // merge place-holder
                SetSummaryLabelStyle(ws.Cell(3, 6));
                SetSummaryValueStyle(ws.Cell(3, 8), XLColor.FromArgb(0xFF, 0xC0, 0x00)); // vàng

                ws.Cell(4, 6).Value = "Tổng tiền giảm giá";
                ws.Cell(4, 8).Value = sumGiamGia;
                SetSummaryLabelStyle(ws.Cell(4, 6));
                SetSummaryValueStyle(ws.Cell(4, 8), XLColor.FromArgb(0xFF, 0xC0, 0x00));

                // Thêm dòng phí vận chuyển ở hàng 5 (trước tổng thực thu)
                ws.Cell(5, 6).Value = "Tổng phí vận chuyển";
                ws.Cell(5, 8).Value = sumPhiVanChuyen;
                SetSummaryLabelStyle(ws.Cell(5, 6));
                SetSummaryValueStyle(ws.Cell(5, 8), XLColor.FromArgb(0xFF, 0xC0, 0x00));

                // Tổng thực thu — nền xanh lá nổi bật
                ws.Cell(6, 6).Value = "Tổng thực thu";
                ws.Cell(6, 8).Value = sumThucThu;
                SetSummaryLabelStyle(ws.Cell(6, 6), bold: true);
                SetSummaryValueStyle(ws.Cell(6, 8), XLColor.FromArgb(0x70, 0xAD, 0x47), fontColor: XLColor.White, bold: true);

                // Style chung cho phần thông tin trái
                foreach (int r in new[] { 3, 4 })
                {
                    ws.Cell(r, 1).Style.Font.SetBold(true);
                    ws.Cell(r, 3).Style.Font.SetBold(true);
                    ws.Cell(r, 1).Style.Font.SetFontSize(11);
                    ws.Cell(r, 3).Style.Font.SetFontSize(11);
                }
                ws.Cell(4, 1).Style.Font.SetBold(false);
                ws.Cell(4, 3).Style.Font.SetBold(false);

                // ━━━━━ DÒNG 7: Trống ━━━━━
                ws.Row(7).Height = 6;

                // ━━━━━ DÒNG 8: Header bảng ━━━━━
                int headerRow = 8;
                string[] headers = {
                    "Mã đơn hàng", "Sản phẩm", "Ngày đặt",
                    "Tên khách hàng", "Số điện thoại",
                    "Tổng tiền hàng", "Phí vận chuyển", "Giảm giá", "Thực thu"
                };
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cell(headerRow, c + 1);
                    cell.Value = headers[c];
                    cell.Style
                        .Font.SetBold(true)
                        .Font.SetFontColor(XLColor.White)
                        .Fill.SetBackgroundColor(XLColor.FromArgb(0x1F, 0x58, 0x97)) // xanh đậm
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                        .Border.SetOutsideBorderColor(XLColor.White);
                }
                ws.Row(headerRow).Height = 22;

                // ━━━━━ DÒNG 9+: Dữ liệu ━━━━━
                int dataRow = headerRow + 1;
                bool isAltRow = false;
                XLColor altColor = XLColor.FromArgb(0xDD, 0xEB, 0xF7); // xanh nhạt

                foreach (var o in orders)
                {
                    string sanPhamStr = string.Join(", ", o.SanPhams);

                    ws.Cell(dataRow, 1).Value = o.MaDonHang;
                    ws.Cell(dataRow, 2).Value = sanPhamStr;
                    ws.Cell(dataRow, 3).Value = o.NgayDat.HasValue ? o.NgayDat.Value.ToString("dd/MM/yyyy") : "";
                    ws.Cell(dataRow, 4).Value = o.TenNguoiNhan;
                    ws.Cell(dataRow, 5).Value = o.SDT_NguoiNhan;
                    ws.Cell(dataRow, 6).Value = (double)o.TongTien;
                    ws.Cell(dataRow, 7).Value = (double)o.PhiVanChuyen;
                    ws.Cell(dataRow, 8).Value = (double)o.GiamGia;
                    ws.Cell(dataRow, 9).Value = (double)o.TongThanhToan;

                    // Format số tiền
                    foreach (int c in new[] { 6, 7, 8, 9 })
                        ws.Cell(dataRow, c).Style.NumberFormat.Format = "#,##0";

                    // Căn giữa một số cột
                    ws.Cell(dataRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    ws.Cell(dataRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    ws.Cell(dataRow, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    // Màu xen kẽ & border
                    var rowRange = ws.Range(dataRow, 1, dataRow, 9);
                    if (isAltRow)
                        rowRange.Style.Fill.SetBackgroundColor(altColor);
                    rowRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                    rowRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Hair);
                    rowRange.Style.Border.SetOutsideBorderColor(XLColor.FromArgb(0xBF, 0xBF, 0xBF));

                    ws.Row(dataRow).Height = 18;
                    dataRow++;
                    isAltRow = !isAltRow;
                }

                // ━━━━━ Dòng tổng cuối bảng ━━━━━
                ws.Cell(dataRow, 1).Value = "TỔNG CỘNG";
                ws.Range(dataRow, 1, dataRow, 5).Merge()
                    .Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Font.SetBold(true);

                ws.Cell(dataRow, 6).Value = (double)sumTongTien;
                ws.Cell(dataRow, 7).Value = (double)sumPhiVanChuyen;
                ws.Cell(dataRow, 8).Value = (double)sumGiamGia;
                ws.Cell(dataRow, 9).Value = (double)sumThucThu;

                foreach (int c in new[] { 6, 7, 8, 9 })
                {
                    ws.Cell(dataRow, c).Style
                        .NumberFormat.Format = "#,##0";
                    ws.Cell(dataRow, c).Style
                        .Font.SetBold(true)
                        .Fill.SetBackgroundColor(XLColor.FromArgb(0xBD, 0xD7, 0xEE));
                }
                ws.Cell(dataRow, 1).Style
                    .Fill.SetBackgroundColor(XLColor.FromArgb(0xBD, 0xD7, 0xEE));

                ws.Range(dataRow, 1, dataRow, 9).Style
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetInsideBorder(XLBorderStyleValues.Thin);
                ws.Row(dataRow).Height = 20;

                // ━━━━━ Độ rộng cột ━━━━━
                ws.Column(1).Width = 22;  // Mã đơn hàng
                ws.Column(2).Width = 35;  // Sản phẩm
                ws.Column(3).Width = 14;  // Ngày đặt
                ws.Column(4).Width = 20;  // Tên khách hàng
                ws.Column(5).Width = 14;  // SĐT
                ws.Column(6).Width = 16;  // Tổng tiền hàng
                ws.Column(7).Width = 16;  // Phí vận chuyển
                ws.Column(8).Width = 14;  // Giảm giá
                ws.Column(9).Width = 16;  // Thực thu

                // Wrap text cột Sản phẩm
                ws.Column(2).Style.Alignment.SetWrapText(true);

                // ━━━━━ Xuất file ━━━━━
                string safeName = reportTitle.Replace("/", "-").Replace(":", "").Replace(" ", "_");
                string fileName = $"BaoCao_{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return File(ms.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        fileName);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Helper: style ô tổng hợp (góc phải)
        // ─────────────────────────────────────────────────────────────
        private static void SetSummaryLabelStyle(IXLCell cell, bool bold = false)
        {
            cell.Style
                .Font.SetBold(bold)
                .Font.SetFontSize(11)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }

        private static void SetSummaryValueStyle(IXLCell cell, XLColor bgColor,
            XLColor fontColor = null, bool bold = false)
        {
            cell.Style.NumberFormat.Format = "#,##0";
            cell.Style
                .Font.SetBold(bold)
                .Font.SetFontSize(11)
                .Fill.SetBackgroundColor(bgColor)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            if (fontColor != null)
                cell.Style.Font.SetFontColor(fontColor);
        }

        // 5. XUẤT EXCEL (SẢN PHẨM) 
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
                        LastSoldDate = g.Max(x => x.DonHang.NgayDat) 
                    })
                    .OrderByDescending(x => x.SoLuongBan)
                    .ThenByDescending(x => x.LastSoldDate)
                    .Take(50)
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
            else 
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