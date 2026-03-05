namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DanhGia")]
    public partial class DanhGia
    {
        public int ID { get; set; }

        public int SanPhamID { get; set; }

        public int NguoiDungID { get; set; }

        public int? SoSao { get; set; }

        public string BinhLuan { get; set; }

        public DateTime? NgayDanhGia { get; set; }
        // Map với cột [TrangThai] BIT
        public bool? TrangThai { get; set; }

        // Map với cột [PhanHoi] NTEXT
        public string PhanHoi { get; set; }

        // Map với cột [NgayPhanHoi] DATETIME
        public DateTime? NgayPhanHoi { get; set; }

        public virtual NguoiDung NguoiDung { get; set; }

        public virtual SanPham SanPham { get; set; }
    }
}
