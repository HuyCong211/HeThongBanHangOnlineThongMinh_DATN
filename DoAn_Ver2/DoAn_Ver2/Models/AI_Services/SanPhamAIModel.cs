using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;

namespace DoAn_Ver2.Models.AI_Services
{
    public class SanPhamAIModel
    {
        public int SanPhamID { get; set; }
        public string TenSanPham { get; set; }
        public string TenDanhMuc { get; set; }
        public string MoTa { get; set; }
        public string ChatLieu { get; set; }
        public string KieuDang { get; set; }
        public decimal GiaBan { get; set; }
        public string MauSacHienCo { get; set; }
        public string KichThuocHienCo { get; set; }
        public string Tags { get; set; }

        // Hàm biến đổi 1 object sản phẩm thành 1 đoạn văn bản (Text) cho AI đọc
        public string ToAITextDocument()
        {
            string moTaSach = string.Empty;
            if (!string.IsNullOrEmpty(MoTa))
            {
                // 1. Thay thế các thẻ HTML bằng 1 khoảng trắng (để các chữ không bị dính vào nhau khi mất thẻ)
                moTaSach = Regex.Replace(MoTa, "<.*?>", " ");

                // 2. Giải mã các ký tự HTML đặc biệt (Biến &nbsp; thành khoảng trắng, &quot; thành ngoặc kép...)
                moTaSach = HttpUtility.HtmlDecode(moTaSach);

                // 3. Dọn dẹp các khoảng trắng thừa (nhiều dấu cách liền nhau biến thành 1 dấu cách)
                moTaSach = Regex.Replace(moTaSach, @"\s+", " ").Trim();
            }

            // Xử lý giá trị NULL
            string chatLieuStr = string.IsNullOrEmpty(ChatLieu) ? "Chưa cập nhật" : ChatLieu;
            string kieuDangStr = string.IsNullOrEmpty(KieuDang) ? "Chưa cập nhật" : KieuDang;
            string mauSacStr = string.IsNullOrEmpty(MauSacHienCo) ? "Chưa cập nhật" : MauSacHienCo;
            string kichThuocStr = string.IsNullOrEmpty(KichThuocHienCo) ? "Chưa cập nhật" : KichThuocHienCo;
            string tagsStr = string.IsNullOrEmpty(Tags) ? "Không có" : Tags;

            return $"[ID: {SanPhamID}] Tên sản phẩm: {TenSanPham}. " +
                   $"Danh mục: {TenDanhMuc}. " +
                   $"Sử dụng/Phong cách (Tags): {tagsStr}. " +
                   $"Mô tả: {moTaSach}. " +
                   $"Chất liệu: {chatLieuStr}. " +
                   $"Kiểu dáng: {kieuDangStr}. " +
                   $"Giá bán: {GiaBan:N0} VNĐ. " +
                   $"Màu sắc hiện có: {mauSacStr}. " +
                   $"Kích thước hiện có: {kichThuocStr}.";
        }
    }
}