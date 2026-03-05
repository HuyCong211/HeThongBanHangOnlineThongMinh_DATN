using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace DoAn_Ver2.Common
{
    public class MyTools
    {
        // Hàm chuyển đổi: "Điện Thoại" -> "dien-thoai"
        public static string GenerateSlug(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return "";

            string str = RemoveAccent(phrase).ToLower();

            // Thay thế khoảng trắng bằng dấu gạch ngang
            str = Regex.Replace(str, @"\s+", "-");

            // Loại bỏ các ký tự không hợp lệ (chỉ giữ lại a-z, 0-9 và dấu gạch ngang)
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");

            // Loại bỏ nhiều dấu gạch ngang liên tiếp (ví dụ: "a---b" -> "a-b")
            str = Regex.Replace(str, @"-+", "-").Trim();

            return str;
        }

        // Hàm bỏ dấu tiếng Việt chuẩn (Sử dụng Normalization FormD)
        public static string RemoveAccent(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Bước 1: Chuẩn hóa chuỗi sang dạng FormD (tách ký tự gốc và dấu thành 2 phần riêng biệt)
            string normalizedString = text.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                // Bước 2: Lọc bỏ các ký tự thuộc nhóm NonSpacingMark (chính là các dấu)
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            // Bước 3: Chuẩn hóa lại về FormC và xử lý riêng chữ đ/Đ
            string result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

            // Xử lý thủ công chữ Đ/đ vì FormD đôi khi không tách được
            return result.Replace("đ", "d").Replace("Đ", "D");
        }
    }
}